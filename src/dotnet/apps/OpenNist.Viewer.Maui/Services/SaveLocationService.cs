namespace OpenNist.Viewer.Maui.Services;

using System.Diagnostics;
using Foundation;
using Microsoft.Maui.ApplicationModel;
using Models;
using UIKit;

internal static class SaveLocationService
{
    private const string ExportDirectoryName = "exports";

    public static async Task<string?> SaveAsync(ViewerExportDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        var tempDirectoryPath = Path.Combine(Path.GetTempPath(), "OpenNist.Viewer.Maui", ExportDirectoryName);
        Directory.CreateDirectory(tempDirectoryPath);

        var tempFilePath = Path.Combine(tempDirectoryPath, $"{Guid.NewGuid():N}-{document.SuggestedFileName}");
        await File.WriteAllBytesAsync(tempFilePath, document.FileBytes.ToArray(), cancellationToken).ConfigureAwait(false);

        try
        {
            return await MainThread.InvokeOnMainThreadAsync(() => PresentSavePickerAsync(tempFilePath, cancellationToken)).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch (IOException)
            {
                Debug.WriteLine($"Failed to delete temporary export file '{tempFilePath}'.");
            }
            catch (UnauthorizedAccessException)
            {
                Debug.WriteLine($"Access denied while deleting temporary export file '{tempFilePath}'.");
            }
        }
    }

    private static async Task<string?> PresentSavePickerAsync(string tempFilePath, CancellationToken cancellationToken)
    {
        var sourceUrl = NSUrl.FromFilename(tempFilePath);
        var picker = new UIDocumentPickerViewController(new[] { sourceUrl }, true);
        var completionSource = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Complete(string? path)
        {
            picker.DidPickDocument -= OnDidPickDocument;
            picker.DidPickDocumentAtUrls -= OnDidPickDocumentAtUrls;
            picker.WasCancelled -= OnWasCancelled;
            completionSource.TrySetResult(path);
        }

        void OnDidPickDocument(object? sender, UIDocumentPickedEventArgs eventArgs) => Complete(eventArgs.Url?.Path);

        void OnDidPickDocumentAtUrls(object? sender, UIDocumentPickedAtUrlsEventArgs eventArgs) => Complete(eventArgs.Urls?.FirstOrDefault()?.Path);

        void OnWasCancelled(object? sender, EventArgs eventArgs) => Complete(null);

        picker.DidPickDocument += OnDidPickDocument;
        picker.DidPickDocumentAtUrls += OnDidPickDocumentAtUrls;
        picker.WasCancelled += OnWasCancelled;

        var presentingViewController = GetPresentingViewController();
        if (presentingViewController is null)
        {
            throw new InvalidOperationException("A save dialog could not be shown because no active window controller was found.");
        }

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            picker.DidPickDocument -= OnDidPickDocument;
            picker.DidPickDocumentAtUrls -= OnDidPickDocumentAtUrls;
            picker.WasCancelled -= OnWasCancelled;
            completionSource.TrySetCanceled(cancellationToken);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (picker.PresentingViewController is not null)
                {
                    picker.DismissViewController(true, null);
                }
            });
        });

        try
        {
            await presentingViewController.PresentViewControllerAsync(picker, true).ConfigureAwait(true);
            return await completionSource.Task.ConfigureAwait(false);
        }
        finally
        {
            picker.Dispose();
        }
    }

    private static UIViewController? GetPresentingViewController()
    {
        var windowScene = UIApplication.SharedApplication
            .ConnectedScenes
            .OfType<UIWindowScene>()
            .FirstOrDefault();

        var rootViewController = windowScene?
            .Windows
            .FirstOrDefault(window => window.IsKeyWindow)?
            .RootViewController;

        while (rootViewController?.PresentedViewController is not null)
        {
            rootViewController = rootViewController.PresentedViewController;
        }

        return rootViewController;
    }
}
