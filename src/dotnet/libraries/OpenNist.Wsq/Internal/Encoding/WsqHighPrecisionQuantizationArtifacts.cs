namespace OpenNist.Wsq.Internal.Encoding;

internal sealed record WsqHighPrecisionQuantizationArtifacts(
    double[] Variances,
    double[] QuantizationBins,
    double[] ZeroBins);

internal readonly record struct WsqHighPrecisionQuantizationTraceOptions(
    bool UseSinglePrecisionSigma = false,
    bool UseSinglePrecisionInitialQuantizationBins = false,
    bool UseLiteralSinglePrecisionInitialQuantizationBins = false,
    bool UseSinglePrecisionReciprocalAreaSum = false,
    bool UseSinglePrecisionProduct = false,
    bool UseSinglePrecisionScaleFactor = false);

internal sealed record WsqHighPrecisionQuantizationTrace(
    double[] Variances,
    double[] Sigma,
    double[] InitialQuantizationBins,
    double[] QuantizationBins,
    double[] ZeroBins,
    int[] FinalActiveSubbands,
    double ReciprocalAreaSum,
    double Product,
    double QuantizationScale,
    int IterationCount);
