namespace OpenNist.Wsq.Internal.Encoding;

internal sealed record WsqHighPrecisionQuantizationArtifacts(
    double[] Variances,
    double[] QuantizationBins,
    double[] ZeroBins);
