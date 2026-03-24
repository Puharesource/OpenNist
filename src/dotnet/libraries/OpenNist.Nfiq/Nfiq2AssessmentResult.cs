namespace OpenNist.Nfiq;

using JetBrains.Annotations;

/// <summary>
/// Represents the parsed result of an NFIQ 2 quality analysis.
/// </summary>
/// <param name="Filename">The source filename reported by the official CLI.</param>
/// <param name="FingerCode">The finger-position code reported by the official CLI.</param>
/// <param name="QualityScore">The unified NFIQ 2 quality score.</param>
/// <param name="OptionalError">The optional error value reported by the official CLI, if any.</param>
/// <param name="Quantized">Indicates whether the input was quantized by the official CLI.</param>
/// <param name="Resampled">Indicates whether the input was resampled by the official CLI.</param>
/// <param name="ActionableFeedback">The actionable NFIQ 2 quality values.</param>
/// <param name="NativeQualityMeasures">The native quality measures produced by the official CLI.</param>
/// <param name="MappedQualityMeasures">The mapped quality-block values produced by the official CLI.</param>
[PublicAPI]
public sealed record Nfiq2AssessmentResult(
    string Filename,
    int FingerCode,
    int QualityScore,
    string? OptionalError,
    bool Quantized,
    bool Resampled,
    IReadOnlyDictionary<string, double?> ActionableFeedback,
    IReadOnlyDictionary<string, double?> NativeQualityMeasures,
    IReadOnlyDictionary<string, double?> MappedQualityMeasures);
