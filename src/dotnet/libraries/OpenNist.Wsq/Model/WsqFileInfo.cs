namespace OpenNist.Wsq.Model;

/// <summary>
/// Represents metadata that can be read directly from a WSQ container.
/// </summary>
/// <param name="Width">The encoded image width in pixels.</param>
/// <param name="Height">The encoded image height in pixels.</param>
/// <param name="BitsPerPixel">The decoded raster bit depth.</param>
/// <param name="PixelsPerInch">The image resolution in pixels per inch.</param>
/// <param name="Black">The frame-header black pixel value.</param>
/// <param name="White">The frame-header white pixel value.</param>
/// <param name="Shift">The frame-header normalization shift.</param>
/// <param name="Scale">The frame-header normalization scale.</param>
/// <param name="WsqEncoder">The WSQ encoder identifier in the frame header.</param>
/// <param name="SoftwareImplementationNumber">The WSQ software implementation number from the frame header.</param>
/// <param name="HighPassFilterLength">The transform-table high-pass filter length.</param>
/// <param name="LowPassFilterLength">The transform-table low-pass filter length.</param>
/// <param name="QuantizationBinCenter">The quantization table bin center.</param>
/// <param name="HuffmanTableIds">The defined Huffman table identifiers.</param>
/// <param name="BlockCount">The number of encoded WSQ blocks.</param>
/// <param name="EncodedBlockByteCount">The total byte count of encoded block payloads.</param>
/// <param name="CommentCount">The number of comment segments.</param>
/// <param name="NistCommentCount">The number of parsed NIST comment segments.</param>
/// <param name="Comments">The decoded comment segments.</param>
public sealed record WsqFileInfo(
    int Width,
    int Height,
    int BitsPerPixel,
    int PixelsPerInch,
    byte Black,
    byte White,
    double Shift,
    double Scale,
    byte WsqEncoder,
    ushort SoftwareImplementationNumber,
    byte HighPassFilterLength,
    byte LowPassFilterLength,
    double QuantizationBinCenter,
    IReadOnlyList<byte> HuffmanTableIds,
    int BlockCount,
    int EncodedBlockByteCount,
    int CommentCount,
    int NistCommentCount,
    IReadOnlyList<WsqCommentInfo> Comments);
