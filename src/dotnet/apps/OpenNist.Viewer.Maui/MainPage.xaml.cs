namespace OpenNist.Viewer.Maui;

using System.Diagnostics.CodeAnalysis;
using Foundation;
using Models;
using Resources.Styles;
using Services;
#if MACCATALYST
using UIKit;
#endif

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "UI event handlers must resume on the main thread.")]
[SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "MAUI XAML-generated partial types require public accessibility.")]
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The native drop interaction is owned by the MAUI handler lifecycle, not by explicit page disposal.")]
public partial class MainPage : ContentPage
{
    private const double LowBitRate = 0.75d;
    private const double HighBitRate = 2.25d;
    private const string IdleStatus = "";
    private const string OpenCancelledStatus = "Open cancelled.";
    private const string SaveCancelledStatus = "Save cancelled.";
    private const string DropFileStatus = "Drop a file to open it.";
    private const string EmptyStateText = "Click or drop an image here.";

    private readonly ViewerImageService _viewerImageService;
    private IReadOnlyList<string> _directoryImagePaths = Array.Empty<string>();
    private LoadedImageDocument? _loadedImage;
    private int _loadedImageIndex = -1;
    private bool _isBusy;
    private byte[]? _previewPngBytes;
#if MACCATALYST
    private NativePreviewDropDelegate? _nativePreviewDropDelegate;
    private UIDropInteraction? _nativePreviewDropInteraction;
#endif

    public MainPage(ViewerImageService viewerImageService)
    {
        Resources.MergedDictionaries.Add(new OpenNistDesignResources());
        InitializeComponent();

        _viewerImageService = viewerImageService;
        AppDelegate.OpenRequested += OnOpenRequested;
        AppDelegate.SaveAsLowRateWsqRequested += OnSaveAsLowRateWsqRequested;
        AppDelegate.SaveAsHighRateWsqRequested += OnSaveAsHighRateWsqRequested;
        AppDelegate.ExportAsPngRequested += OnExportAsPngRequested;
        AppDelegate.ExportAsJpegRequested += OnExportAsJpegRequested;
        AppDelegate.ExportAsTiffRequested += OnExportAsTiffRequested;
        AppDelegate.ExportAsBmpRequested += OnExportAsBmpRequested;
        AppDelegate.PreviousImageRequested += OnPreviousImageRequested;
        AppDelegate.NextImageRequested += OnNextImageRequested;
#if MACCATALYST
        PreviewSurface.HandlerChanged += OnPreviewSurfaceHandlerChanged;
#endif
        UpdateUiState();
    }

    private void OnOpenRequested(object? sender, EventArgs e) => OnOpenFileClicked(sender, e);

    private void OnSaveAsLowRateWsqRequested(object? sender, EventArgs e) => OnSaveAsWsq075Clicked(sender, e);

    private void OnSaveAsHighRateWsqRequested(object? sender, EventArgs e) => OnSaveAsWsq225Clicked(sender, e);

    private void OnExportAsPngRequested(object? sender, EventArgs e) => OnSaveAsPngClicked(sender, e);

    private void OnExportAsJpegRequested(object? sender, EventArgs e) => OnSaveAsJpegClicked(sender, e);

    private void OnExportAsTiffRequested(object? sender, EventArgs e) => OnSaveAsTiffClicked(sender, e);

    private void OnExportAsBmpRequested(object? sender, EventArgs e) => OnSaveAsBmpClicked(sender, e);

    private async void OnOpenFileClicked(object? sender, EventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        try
        {
            await RunBusyAsync(async cancellationToken =>
            {
                var fileResult = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Open an image or WSQ file",
                });

                if (fileResult is null)
                {
                    StatusLabel.Text = OpenCancelledStatus;
                    return;
                }

                await LoadFromFileResultAsync(fileResult, cancellationToken);
            });
        }
        catch (Exception exception) when (IsHandledException(exception))
        {
            await DisplayAlertAsync("Open failed", exception.Message, "OK");
        }
    }

    private async void OnSaveAsWsq075Clicked(object? sender, EventArgs e)
    {
        await SaveAsWsqAsync(LowBitRate);
    }

    private async void OnSaveAsWsq225Clicked(object? sender, EventArgs e)
    {
        await SaveAsWsqAsync(HighBitRate);
    }

    private async void OnSaveAsPngClicked(object? sender, EventArgs e) => await SaveAsImageAsync(ImageExportFormat.Png);

    private async void OnSaveAsJpegClicked(object? sender, EventArgs e) => await SaveAsImageAsync(ImageExportFormat.Jpeg);

    private async void OnSaveAsTiffClicked(object? sender, EventArgs e) => await SaveAsImageAsync(ImageExportFormat.Tiff);

    private async void OnSaveAsBmpClicked(object? sender, EventArgs e) => await SaveAsImageAsync(ImageExportFormat.Bmp);

    private async Task RunBusyAsync(Func<CancellationToken, Task> operation)
    {
        _isBusy = true;
        UpdateUiState();

        try
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            await operation(cancellationTokenSource.Token);
        }
        finally
        {
            _isBusy = false;
            UpdateUiState();
        }
    }

    private async Task SaveAsWsqAsync(double bitRate)
    {
        if (_loadedImage is null || _isBusy)
        {
            return;
        }

        try
        {
            await RunBusyAsync(async cancellationToken =>
            {
                var exportDocument = await _viewerImageService.CreateWsqExportAsync(_loadedImage, bitRate, cancellationToken);
                var outputPath = await SaveLocationService.SaveAsync(exportDocument, cancellationToken);
                StatusLabel.Text = outputPath ?? SaveCancelledStatus;
            });
        }
        catch (Exception exception) when (IsHandledException(exception))
        {
            await DisplayAlertAsync("WSQ save failed", exception.Message, "OK");
        }
    }

    private async Task SaveAsImageAsync(ImageExportFormat exportFormat)
    {
        if (_loadedImage is null || _isBusy)
        {
            return;
        }

        try
        {
            await RunBusyAsync(async cancellationToken =>
            {
                var exportDocument = await ViewerImageService.CreateImageExportAsync(_loadedImage, exportFormat, cancellationToken);
                var outputPath = await SaveLocationService.SaveAsync(exportDocument, cancellationToken);
                StatusLabel.Text = outputPath ?? SaveCancelledStatus;
            });
        }
        catch (Exception exception) when (IsHandledException(exception))
        {
            await DisplayAlertAsync("Image save failed", exception.Message, "OK");
        }
    }

    private async void OnPreviousImageRequested(object? sender, EventArgs e)
    {
        await NavigateDirectoryAsync(-1);
    }

    private async void OnNextImageRequested(object? sender, EventArgs e)
    {
        await NavigateDirectoryAsync(1);
    }

    private async Task NavigateDirectoryAsync(int step)
    {
        if (_isBusy || _loadedImage is null || _directoryImagePaths.Count < 2 || _loadedImageIndex < 0)
        {
            return;
        }

        var targetIndex = _loadedImageIndex + step;
        if ((targetIndex < 0) || (targetIndex >= _directoryImagePaths.Count))
        {
            return;
        }

        try
        {
            await RunBusyAsync(cancellationToken => LoadFromPathAsync(_directoryImagePaths[targetIndex], cancellationToken));
        }
        catch (Exception exception) when (IsHandledException(exception))
        {
            await DisplayAlertAsync("Navigation failed", exception.Message, "OK");
        }
    }

    private async Task LoadFromFileResultAsync(FileResult fileResult, CancellationToken cancellationToken)
    {
        _loadedImage = await _viewerImageService.LoadAsync(fileResult, cancellationToken);
        RefreshDirectoryState(_loadedImage.SourcePath);
        ApplyLoadedImage();
    }

    private async Task LoadFromPathAsync(string filePath, CancellationToken cancellationToken)
    {
        _loadedImage = await _viewerImageService.LoadAsync(filePath, cancellationToken);
        RefreshDirectoryState(filePath);
        ApplyLoadedImage();
    }

#if MACCATALYST
    private void OnPreviewSurfaceHandlerChanged(object? sender, EventArgs e)
    {
        if (PreviewSurface.Handler?.PlatformView is not UIView previewView)
        {
            return;
        }

        if (_nativePreviewDropInteraction is not null)
        {
            previewView.RemoveInteraction(_nativePreviewDropInteraction);
            _nativePreviewDropInteraction.Dispose();
            _nativePreviewDropInteraction = null;
        }

        _nativePreviewDropDelegate ??= new NativePreviewDropDelegate(this);
        _nativePreviewDropInteraction = new UIDropInteraction(_nativePreviewDropDelegate);
        previewView.AddInteraction(_nativePreviewDropInteraction);
    }

    private async Task LoadDroppedFilePathAsync(string filePath)
    {
        if (_isBusy || string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            await RunBusyAsync(cancellationToken => LoadFromPathAsync(filePath, cancellationToken));
        }
        catch (Exception exception) when (IsHandledException(exception))
        {
            await DisplayAlertAsync("Drop failed", exception.Message, "OK");
        }
    }
#endif

    private void RefreshDirectoryState(string filePath)
    {
        _directoryImagePaths = ViewerImageService.GetSupportedFilesInDirectory(filePath);
        _loadedImageIndex = ViewerImageService.FindFileIndex(_directoryImagePaths, filePath);
    }

    private void ApplyLoadedImage()
    {
        if (_loadedImage is null)
        {
            return;
        }

        _previewPngBytes = _loadedImage.PreviewPngBytes.ToArray();
        PreviewImage.Source = null;
        PreviewImage.Source = ImageSource.FromStream(CreatePreviewStream);
        Title = _loadedImage.FileName;
        StatusLabel.Text = FormatLoadedImageStatus(_loadedImage.FileName);
        UpdateUiState();
    }

    private string FormatLoadedImageStatus(string fileName)
    {
        if (_loadedImageIndex < 0 || _directoryImagePaths.Count <= 1)
        {
            return fileName;
        }

        return $"{fileName} ({_loadedImageIndex + 1}/{_directoryImagePaths.Count})";
    }

    private static bool IsHandledException(Exception exception)
    {
        return exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or NotSupportedException
            or UnauthorizedAccessException;
    }

    private void UpdateUiState()
    {
        var hasLoadedImage = _loadedImage is not null;
        OpenMenuItem.IsEnabled = !_isBusy;
        SaveAsWsqMenuItem.IsEnabled = !_isBusy && hasLoadedImage;
        ExportImageMenuItem.IsEnabled = !_isBusy && hasLoadedImage;
        OpenToolbarItem.IsEnabled = !_isBusy;
        SaveWsqToolbarItem.IsEnabled = !_isBusy && hasLoadedImage;
        SaveWsqHighToolbarItem.IsEnabled = !_isBusy && hasLoadedImage;
        ExportPngToolbarItem.IsEnabled = !_isBusy && hasLoadedImage;
        ExportJpegToolbarItem.IsEnabled = !_isBusy && hasLoadedImage;
        ExportTiffToolbarItem.IsEnabled = !_isBusy && hasLoadedImage;
        ExportBmpToolbarItem.IsEnabled = !_isBusy && hasLoadedImage;
        PreviewSurface.InputTransparent = _isBusy;
        EmptyStateLabel.IsVisible = !hasLoadedImage;

        if (!hasLoadedImage)
        {
            _directoryImagePaths = Array.Empty<string>();
            _loadedImageIndex = -1;
            _previewPngBytes = null;
            PreviewImage.Source = null;
            Title = "OpenNist Viewer";
            StatusLabel.Text = _isBusy ? "Working…" : IdleStatus;
            EmptyStateLabel.Text = EmptyStateText;
            return;
        }
    }

    private Stream CreatePreviewStream()
    {
        return new MemoryStream(_previewPngBytes ?? Array.Empty<byte>(), writable: false);
    }

#if MACCATALYST
    private sealed class NativePreviewDropDelegate : UIDropInteractionDelegate
    {
        private readonly MainPage _page;

        public NativePreviewDropDelegate(MainPage page)
        {
            _page = page;
        }

        public override bool CanHandleSession(UIDropInteraction interaction, IUIDropSession session)
        {
            return session.Items.Length > 0;
        }

        public override UIDropProposal SessionDidUpdate(UIDropInteraction interaction, IUIDropSession session)
        {
            return new UIDropProposal(UIDropOperation.Copy);
        }

        public override void PerformDrop(UIDropInteraction interaction, IUIDropSession session)
        {
            _ = HandleDropAsync(session);
        }

        private async Task HandleDropAsync(IUIDropSession session)
        {
            foreach (var item in session.Items)
            {
                var filePath = await ResolveDroppedFilePathAsync(item.ItemProvider);
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    await MainThread.InvokeOnMainThreadAsync(() => _page.LoadDroppedFilePathAsync(filePath));
                    return;
                }
            }

            await MainThread.InvokeOnMainThreadAsync(() => _page.StatusLabel.Text = DropFileStatus);
        }

        private static async Task<string?> ResolveDroppedFilePathAsync(NSItemProvider itemProvider)
        {
            var typeIdentifiers = itemProvider.RegisteredTypeIdentifiers?.ToList();
            if (typeIdentifiers is null || typeIdentifiers.Count == 0)
            {
                return null;
            }

            foreach (var typeIdentifier in typeIdentifiers)
            {
                if (!itemProvider.HasItemConformingTo(typeIdentifier))
                {
                    continue;
                }

                var result = await itemProvider.LoadInPlaceFileRepresentationAsync(typeIdentifier);
                if (!string.IsNullOrWhiteSpace(result?.FileUrl?.Path))
                {
                    return result.FileUrl.Path;
                }
            }

            return null;
        }
    }
#endif
}
