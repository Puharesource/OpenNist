namespace OpenNist.Wsq.Internal.Encoding;

internal sealed record WsqQuantizationArtifacts(
    float[] Variances,
    float[] QuantizationBins,
    float[] ZeroBins);

internal sealed record WsqQuantizationTrace(
    float[] Variances,
    float[] Sigma,
    float[] InitialQuantizationBins,
    float[] QuantizationBins,
    float[] ZeroBins,
    int[] FinalActiveSubbands,
    float ReciprocalAreaSum,
    float Product,
    float QuantizationScale,
    int IterationCount);
