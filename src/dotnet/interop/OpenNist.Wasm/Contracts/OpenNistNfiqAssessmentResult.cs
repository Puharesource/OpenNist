namespace OpenNist.Wasm.Contracts;

using OpenNist.Nfiq.Model;

internal sealed record OpenNistNfiqAssessmentResult(
    int FingerCode,
    int QualityScore,
    string? OptionalError,
    bool Quantized,
    bool Resampled,
    Dictionary<string, double?> ActionableFeedback,
    Dictionary<string, double?> NativeQualityMeasures,
    Dictionary<string, double?> MappedQualityMeasures)
{
    public static OpenNistNfiqAssessmentResult FromAssessment(Nfiq2AssessmentResult assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        return new(
            assessment.FingerCode,
            assessment.QualityScore,
            assessment.OptionalError,
            assessment.Quantized,
            assessment.Resampled,
            new(assessment.ActionableFeedback, StringComparer.Ordinal),
            new(assessment.NativeQualityMeasures, StringComparer.Ordinal),
            new(assessment.MappedQualityMeasures, StringComparer.Ordinal));
    }
}
