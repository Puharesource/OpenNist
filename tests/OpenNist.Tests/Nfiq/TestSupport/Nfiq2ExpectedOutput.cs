namespace OpenNist.Tests.Nfiq.TestSupport;

internal sealed record Nfiq2ExpectedOutput(
    int QualityScore,
    IReadOnlyDictionary<string, double> ActionableFeedback,
    IReadOnlyDictionary<string, double> NativeQualityMeasures);
