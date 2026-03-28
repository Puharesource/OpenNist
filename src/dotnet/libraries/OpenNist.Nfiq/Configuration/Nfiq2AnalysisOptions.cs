namespace OpenNist.Nfiq.Configuration;

using JetBrains.Annotations;

/// <summary>
/// Controls managed NFIQ 2 analysis behavior.
/// </summary>
/// <param name="IncludeMappedQualityMeasures">
/// Indicates whether mapped ISO/IEC 39794 quality-block values should be included in the output.
/// </param>
/// <param name="Force">
/// Reserved compatibility flag for callers migrating from the official tool.
/// </param>
/// <param name="ThreadCount">
/// Indicates an optional worker-thread hint for multi-image analysis.
/// </param>
[PublicAPI]
public readonly record struct Nfiq2AnalysisOptions(
    bool IncludeMappedQualityMeasures = true,
    bool Force = true,
    int? ThreadCount = null);
