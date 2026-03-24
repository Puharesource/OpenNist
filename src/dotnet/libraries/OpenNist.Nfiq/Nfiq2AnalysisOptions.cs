namespace OpenNist.Nfiq;

using JetBrains.Annotations;

/// <summary>
/// Controls how the official NFIQ 2 CLI is invoked.
/// </summary>
/// <param name="IncludeMappedQualityMeasures">
/// Indicates whether mapped ISO/IEC 39794 quality-block values should be included in the output.
/// </param>
/// <param name="Force">
/// Indicates whether the official tool should force quantization and/or resampling when needed.
/// </param>
/// <param name="ThreadCount">
/// Indicates the worker-thread count to request from the official CLI for multi-image analysis.
/// </param>
[PublicAPI]
public readonly record struct Nfiq2AnalysisOptions(
    bool IncludeMappedQualityMeasures = true,
    bool Force = true,
    int? ThreadCount = null);
