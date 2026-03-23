namespace OpenNist.Viewer.Maui.Models;

using System.Diagnostics.CodeAnalysis;

[SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "The public viewer service uses this model in its API.")]
public sealed record LoadedImageDocument(
    string SourcePath,
    string FileName,
    string SourceKind,
    int Width,
    int Height,
    int PixelsPerInch,
    ReadOnlyMemory<byte> GrayscalePixels,
    ReadOnlyMemory<byte> PreviewPngBytes);
