namespace OpenNist.Wsq.Internal.Encoding;

using OpenNist.Wsq.Internal;

internal sealed record WsqEncoderAnalysisResult(
    WsqFrameHeader FrameHeader,
    WsqTransformTable TransformTable,
    WsqQuantizationTable QuantizationTable,
    short[] QuantizedCoefficients,
    int[] BlockSizes);
