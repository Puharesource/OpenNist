namespace OpenNist.Viewer.Maui.Services;

using System.Diagnostics.CodeAnalysis;
using CoreGraphics;
using Foundation;
using ImageIO;
using System.Collections.Frozen;
using Microsoft.Maui.Storage;
using Models;
using OpenNist.Wsq;
using UIKit;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "The service already uses explicit ConfigureAwait where it matters; stream disposal patterns trigger false positives here.")]
[SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "The viewer page constructor is public for MAUI DI.")]
public sealed class ViewerImageService
{
    private const int DefaultPixelsPerInch = 500;
    private const double DefaultPreviewBitRate = 2.25d;
    private const int WsqBitsPerPixel = 8;
    private const string WsqExtension = ".wsq";
    private const string UnknownFileName = "image";
    private static readonly StringComparer FileSystemComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly FrozenSet<string> SupportedExtensions = new[]
    {
        ".bmp",
        ".gif",
        ".heic",
        ".heif",
        ".jpeg",
        ".jpg",
        ".png",
        ".tif",
        ".tiff",
        ".webp",
        WsqExtension,
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private readonly WsqCodec _wsqCodec = new();

    public static IReadOnlyList<string> GetSupportedFilesInDirectory(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var directoryPath = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return Array.Empty<string>();
        }

        var filePaths = Directory
            .EnumerateFiles(directoryPath)
            .Where(IsSupportedFile)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Array.AsReadOnly(filePaths);
    }

    public static bool IsSupportedFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return SupportedExtensions.Contains(Path.GetExtension(filePath));
    }

    public static int FindFileIndex(IReadOnlyList<string> filePaths, string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        for (var index = 0; index < filePaths.Count; index++)
        {
            if (FileSystemComparer.Equals(filePaths[index], filePath))
            {
                return index;
            }
        }

        return -1;
    }

    public async Task<LoadedImageDocument> LoadAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fileName = Path.GetFileName(filePath);
        var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);

        return IsWsq(fileName)
            ? await LoadWsqAsync(filePath, fileName, fileBytes, cancellationToken).ConfigureAwait(false)
            : await LoadNativeImageAsync(filePath, fileName, fileBytes, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LoadedImageDocument> LoadAsync(FileResult fileResult, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileResult);

        var fileName = string.IsNullOrWhiteSpace(fileResult.FileName) ? UnknownFileName : fileResult.FileName;
        var sourcePath = GetSourcePath(fileResult);

        await using var inputStream = await fileResult.OpenReadAsync().ConfigureAwait(false);
        using var memoryStream = new MemoryStream();
        await inputStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        var fileBytes = memoryStream.ToArray();

        return IsWsq(fileName)
            ? await LoadWsqAsync(sourcePath, fileName, fileBytes, cancellationToken).ConfigureAwait(false)
            : await LoadNativeImageAsync(sourcePath, fileName, fileBytes, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ViewerExportDocument> CreateWsqExportAsync(LoadedImageDocument document, double bitRate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var rawStream = new MemoryStream(document.GrayscalePixels.ToArray(), writable: false);
        using var wsqStream = new MemoryStream();

        await _wsqCodec.EncodeAsync(
            rawStream,
            wsqStream,
            new(document.Width, document.Height, WsqBitsPerPixel, document.PixelsPerInch),
            new(bitRate),
            cancellationToken).ConfigureAwait(false);

        return new ViewerExportDocument(
            BuildSuggestedFileName(document.FileName, WsqExtension),
            wsqStream.ToArray());
    }

    public static Task<ViewerExportDocument> CreateImageExportAsync(LoadedImageDocument document, ImageExportFormat format, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(format);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new ViewerExportDocument(
            BuildSuggestedFileName(document.FileName, format.Extension),
            EncodeNativeImage(document.Width, document.Height, document.GrayscalePixels.Span, format.TypeIdentifier)));
    }

    private async Task<LoadedImageDocument> LoadWsqAsync(
        string sourcePath,
        string fileName,
        ReadOnlyMemory<byte> fileBytes,
        CancellationToken cancellationToken)
    {
        await using var inputStream = new MemoryStream(fileBytes.ToArray(), writable: false);
        await using var outputStream = new MemoryStream();
        var description = await _wsqCodec.DecodeAsync(inputStream, outputStream, cancellationToken).ConfigureAwait(false);
        var rawPixels = outputStream.ToArray();
        var previewBytes = CreatePreviewPng(description.Width, description.Height, rawPixels);

        return new(
            sourcePath,
            fileName,
            "WSQ",
            description.Width,
            description.Height,
            description.PixelsPerInch,
            rawPixels,
            previewBytes);
    }

    private async Task<LoadedImageDocument> LoadNativeImageAsync(
        string sourcePath,
        string fileName,
        ReadOnlyMemory<byte> fileBytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var imageData = NSData.FromArray(fileBytes.ToArray());
        using var image = UIImage.LoadFromData(imageData)
                          ?? throw new InvalidDataException("The selected image format could not be loaded.");
        using var orientedImage = AutoOrient(image);
        var portableGrayMap = ConvertToGrayscale(orientedImage.CGImage ?? throw new InvalidDataException("The selected image has no CGImage backing."));
        var previewBytes = await CreateWsqPreviewAsync(
            portableGrayMap.Width,
            portableGrayMap.Height,
            portableGrayMap.Pixels,
            cancellationToken).ConfigureAwait(false);

        return new LoadedImageDocument(
            sourcePath,
            fileName,
            Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant(),
            portableGrayMap.Width,
            portableGrayMap.Height,
            DefaultPixelsPerInch,
            portableGrayMap.Pixels,
            previewBytes);
    }

    private async Task<byte[]> CreateWsqPreviewAsync(
        int width,
        int height,
        ReadOnlyMemory<byte> rawPixels,
        CancellationToken cancellationToken)
    {
        await using var rawStream = new MemoryStream(rawPixels.ToArray(), writable: false);
        await using var wsqStream = new MemoryStream();

        await _wsqCodec.EncodeAsync(
            rawStream,
            wsqStream,
            new(width, height, WsqBitsPerPixel, DefaultPixelsPerInch),
            new(DefaultPreviewBitRate),
            cancellationToken).ConfigureAwait(false);

        wsqStream.Position = 0;
        await using var decodedStream = new MemoryStream();
        var description = await _wsqCodec.DecodeAsync(wsqStream, decodedStream, cancellationToken).ConfigureAwait(false);
        return CreatePreviewPng(description.Width, description.Height, decodedStream.ToArray());
    }

    private static byte[] CreatePreviewPng(int width, int height, ReadOnlySpan<byte> rawPixels)
    {
        return EncodePreviewPng(width, height, rawPixels);
    }

    private static string BuildSuggestedFileName(string sourceFileName, string extension)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFileName);
        return string.Concat(fileNameWithoutExtension, extension);
    }

    private static bool IsWsq(string fileName)
    {
        return string.Equals(Path.GetExtension(fileName), WsqExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static PortableGrayMapImage ConvertToGrayscale(CGImage image)
    {
        var width = (int)image.Width;
        var height = (int)image.Height;
        var bytesPerPixel = 4;
        var bytesPerRow = width * bytesPerPixel;
        var rgbaBytes = new byte[height * bytesPerRow];

        using var colorSpace = CGColorSpace.CreateDeviceRGB();
        using var context = new CGBitmapContext(
            rgbaBytes,
            width,
            height,
            WsqBitsPerPixel,
            bytesPerRow,
            colorSpace,
            CGBitmapFlags.PremultipliedLast);

        context.DrawImage(new CGRect(0, 0, width, height), image);

        var grayscalePixels = new byte[width * height];
        for (int sourceIndex = 0, destinationIndex = 0;
             sourceIndex < rgbaBytes.Length;
             sourceIndex += bytesPerPixel, destinationIndex++)
        {
            var red = rgbaBytes[sourceIndex];
            var green = rgbaBytes[sourceIndex + 1];
            var blue = rgbaBytes[sourceIndex + 2];
            grayscalePixels[destinationIndex] = (byte)Math.Clamp((int)Math.Round((0.299d * red) + (0.587d * green) + (0.114d * blue)), 0, MaxByteValue);
        }

        return new PortableGrayMapImage(width, height, grayscalePixels);
    }

    private static byte[] EncodePreviewPng(int width, int height, ReadOnlySpan<byte> rawPixels)
    {
        using var image = CreatePreviewImage(width, height, rawPixels);
        using var outputData = new NSMutableData();
        using var destination = CGImageDestination.Create(outputData, ImageExportFormat.Png.TypeIdentifier, 1)
                                ?? throw new InvalidOperationException("The PNG preview destination could not be created.");

        destination.AddImage(image);
        if (!destination.Close())
        {
            throw new InvalidOperationException("The PNG preview could not be written.");
        }

        return outputData.ToArray();
    }

    private static byte[] EncodeNativeImage(int width, int height, ReadOnlySpan<byte> rawPixels, string typeIdentifier)
    {
        using var image = CreateGrayscaleImage(width, height, rawPixels);
        using var outputData = new NSMutableData();
        using var destination = CGImageDestination.Create(outputData, typeIdentifier, 1)
                                ?? throw new InvalidOperationException($"The export destination could not be created for type '{typeIdentifier}'.");

        destination.AddImage(image);
        if (!destination.Close())
        {
            throw new InvalidOperationException($"The image export failed for type '{typeIdentifier}'.");
        }

        return outputData.ToArray();
    }

    private static CGImage CreateGrayscaleImage(int width, int height, ReadOnlySpan<byte> rawPixels)
    {
        var grayscaleBuffer = rawPixels.ToArray();
        using var colorSpace = CGColorSpace.CreateDeviceGray();
        using var context = new CGBitmapContext(
            grayscaleBuffer,
            width,
            height,
            WsqBitsPerPixel,
            width,
            colorSpace,
            CGImageAlphaInfo.None);

        return context.ToImage() ?? throw new InvalidOperationException("The grayscale image could not be created.");
    }

    private static CGImage CreatePreviewImage(int width, int height, ReadOnlySpan<byte> rawPixels)
    {
        var bytesPerPixel = 4;
        var bytesPerRow = width * bytesPerPixel;
        var rgbaBytes = new byte[height * bytesPerRow];

        for (var sourceIndex = 0; sourceIndex < rawPixels.Length; sourceIndex++)
        {
            var pixelValue = rawPixels[sourceIndex];
            var destinationIndex = sourceIndex * bytesPerPixel;
            rgbaBytes[destinationIndex] = pixelValue;
            rgbaBytes[destinationIndex + 1] = pixelValue;
            rgbaBytes[destinationIndex + 2] = pixelValue;
            rgbaBytes[destinationIndex + 3] = MaxByteValue;
        }

        using var colorSpace = CGColorSpace.CreateDeviceRGB();
        using var context = new CGBitmapContext(
            rgbaBytes,
            width,
            height,
            WsqBitsPerPixel,
            bytesPerRow,
            colorSpace,
            CGBitmapFlags.PremultipliedLast);

        return context.ToImage() ?? throw new InvalidOperationException("The preview image could not be created.");
    }

    private static UIImage AutoOrient(UIImage image)
    {
        if (image.Orientation == UIImageOrientation.Up)
        {
            return image;
        }

        UIGraphics.BeginImageContextWithOptions(image.Size, false, image.CurrentScale);
        try
        {
            image.Draw(CGPoint.Empty);
            return UIGraphics.GetImageFromCurrentImageContext()
                   ?? throw new InvalidOperationException("The image could not be auto-oriented.");
        }
        finally
        {
            UIGraphics.EndImageContext();
        }
    }

    private const int MaxByteValue = 255;

    private static string GetSourcePath(FileResult fileResult)
    {
        if (!string.IsNullOrWhiteSpace(fileResult.FullPath))
        {
            return fileResult.FullPath;
        }

        var fileName = string.IsNullOrWhiteSpace(fileResult.FileName) ? UnknownFileName : fileResult.FileName;
        return Path.Combine(FileSystem.Current.AppDataDirectory, fileName);
    }
}
