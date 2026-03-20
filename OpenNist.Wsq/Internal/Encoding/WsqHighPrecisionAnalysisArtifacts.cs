namespace OpenNist.Wsq.Internal.Encoding;

internal sealed record WsqHighPrecisionAnalysisArtifacts(
    WsqDoubleNormalizedImage DoubleNormalizedImage,
    WsqNormalizedImage FloatNormalizedImage,
    double[] DoubleDecomposedPixels,
    float[] FloatDecomposedPixels);
