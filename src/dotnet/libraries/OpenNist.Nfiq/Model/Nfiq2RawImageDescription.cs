namespace OpenNist.Nfiq.Model;

using JetBrains.Annotations;

/// <summary>
/// Describes an in-memory 8-bit grayscale fingerprint image for NFIQ 2 analysis.
/// </summary>
/// <param name="Width">The image width in pixels.</param>
/// <param name="Height">The image height in pixels.</param>
/// <param name="BitsPerPixel">The bits per pixel.</param>
/// <param name="PixelsPerInch">The image resolution in pixels per inch.</param>
[PublicAPI]
public readonly record struct Nfiq2RawImageDescription(
    int Width,
    int Height,
    int BitsPerPixel = 8,
    int PixelsPerInch = 500);
