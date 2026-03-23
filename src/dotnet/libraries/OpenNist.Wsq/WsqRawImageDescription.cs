namespace OpenNist.Wsq;

/// <summary>
/// Describes a headerless raw grayscale image used as WSQ input or output.
/// </summary>
/// <param name="Width">The image width in pixels.</param>
/// <param name="Height">The image height in pixels.</param>
/// <param name="BitsPerPixel">The bits per pixel in the raster.</param>
/// <param name="PixelsPerInch">The image resolution in pixels per inch.</param>
public readonly record struct WsqRawImageDescription(
    int Width,
    int Height,
    int BitsPerPixel = 8,
    int PixelsPerInch = 500);
