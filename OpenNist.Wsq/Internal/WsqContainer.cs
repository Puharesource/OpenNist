namespace OpenNist.Wsq.Internal;

internal sealed record WsqContainer(
    WsqFrameHeader FrameHeader,
    WsqTransformTable TransformTable,
    WsqQuantizationTable QuantizationTable,
    IReadOnlyList<WsqHuffmanTable> HuffmanTables,
    IReadOnlyList<WsqCommentSegment> Comments,
    IReadOnlyList<WsqBlock> Blocks,
    int? PixelsPerInch);

internal sealed record WsqFrameHeader(
    byte Black,
    byte White,
    ushort Height,
    ushort Width,
    double Shift,
    double Scale,
    byte WsqEncoder,
    ushort SoftwareImplementationNumber);

internal sealed record WsqTransformTable(
    byte HighPassFilterLength,
    byte LowPassFilterLength,
    IReadOnlyList<float> LowPassFilterCoefficients,
    IReadOnlyList<float> HighPassFilterCoefficients);

internal sealed record WsqQuantizationTable(
    double BinCenter,
    IReadOnlyList<double> QuantizationBins,
    IReadOnlyList<double> ZeroBins);

internal sealed record WsqHuffmanTable(
    byte TableId,
    IReadOnlyList<byte> CodeLengthCounts,
    IReadOnlyList<byte> Values);

internal sealed record WsqCommentSegment(
    string Text,
    IReadOnlyDictionary<string, string> Fields)
{
    public bool IsNistComment => Fields.Count > 0;
}

internal sealed record WsqBlock(
    byte HuffmanTableId,
    WsqHuffmanTable HuffmanTable,
    byte[] EncodedData)
{
    public int EncodedByteCount => EncodedData.Length;
}
