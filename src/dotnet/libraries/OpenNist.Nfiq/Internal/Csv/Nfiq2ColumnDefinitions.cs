namespace OpenNist.Nfiq.Internal.Csv;

using System.Collections.Frozen;

internal static class Nfiq2ColumnDefinitions
{
    public const string Filename = "Filename";
    public const string FingerCode = "FingerCode";
    public const string QualityScore = "QualityScore";
    public const string OptionalError = "OptionalError";
    public const string Quantized = "Quantized";
    public const string Resampled = "Resampled";
    public const string MappedPrefix = "QB_";

    public static readonly FrozenSet<string> FixedColumns = new[]
    {
        Filename,
        FingerCode,
        QualityScore,
        OptionalError,
        Quantized,
        Resampled,
    }.ToFrozenSet(StringComparer.Ordinal);

    public static readonly FrozenSet<string> ActionableColumns = new[]
    {
        "UniformImage",
        "EmptyImageOrContrastTooLow",
        "FingerprintImageWithMinutiae",
        "SufficientFingerprintForeground",
    }.ToFrozenSet(StringComparer.Ordinal);

    public static readonly FrozenSet<string> StandardComplianceColumns = new[]
    {
        Filename,
        FingerCode,
        QualityScore,
        OptionalError,
        Quantized,
        Resampled,
        "EmptyImageOrContrastTooLow",
        "UniformImage",
        "FingerprintImageWithMinutiae",
        "SufficientFingerprintForeground",
        "FDA_Bin10_0",
        "FDA_Bin10_1",
        "FDA_Bin10_2",
        "FDA_Bin10_3",
        "FDA_Bin10_4",
        "FDA_Bin10_5",
        "FDA_Bin10_6",
        "FDA_Bin10_7",
        "FDA_Bin10_8",
        "FDA_Bin10_9",
        "FDA_Bin10_Mean",
        "FDA_Bin10_StdDev",
        "FingerJetFX_MinCount_COMMinRect200x200",
        "FingerJetFX_MinutiaeCount",
        "FJFXPos_Mu_MinutiaeQuality_2",
        "FJFXPos_OCL_MinutiaeQuality_80",
        "ImgProcROIArea_Mean",
        "LCS_Bin10_0",
        "LCS_Bin10_1",
        "LCS_Bin10_2",
        "LCS_Bin10_3",
        "LCS_Bin10_4",
        "LCS_Bin10_5",
        "LCS_Bin10_6",
        "LCS_Bin10_7",
        "LCS_Bin10_8",
        "LCS_Bin10_9",
        "LCS_Bin10_Mean",
        "LCS_Bin10_StdDev",
        "MMB",
        "Mu",
        "OCL_Bin10_0",
        "OCL_Bin10_1",
        "OCL_Bin10_2",
        "OCL_Bin10_3",
        "OCL_Bin10_4",
        "OCL_Bin10_5",
        "OCL_Bin10_6",
        "OCL_Bin10_7",
        "OCL_Bin10_8",
        "OCL_Bin10_9",
        "OCL_Bin10_Mean",
        "OCL_Bin10_StdDev",
        "OF_Bin10_0",
        "OF_Bin10_1",
        "OF_Bin10_2",
        "OF_Bin10_3",
        "OF_Bin10_4",
        "OF_Bin10_5",
        "OF_Bin10_6",
        "OF_Bin10_7",
        "OF_Bin10_8",
        "OF_Bin10_9",
        "OF_Bin10_Mean",
        "OF_Bin10_StdDev",
        "OrientationMap_ROIFilter_CoherenceRel",
        "OrientationMap_ROIFilter_CoherenceSum",
        "RVUP_Bin10_0",
        "RVUP_Bin10_1",
        "RVUP_Bin10_2",
        "RVUP_Bin10_3",
        "RVUP_Bin10_4",
        "RVUP_Bin10_5",
        "RVUP_Bin10_6",
        "RVUP_Bin10_7",
        "RVUP_Bin10_8",
        "RVUP_Bin10_9",
        "RVUP_Bin10_Mean",
        "RVUP_Bin10_StdDev",
    }.ToFrozenSet(StringComparer.Ordinal);
}
