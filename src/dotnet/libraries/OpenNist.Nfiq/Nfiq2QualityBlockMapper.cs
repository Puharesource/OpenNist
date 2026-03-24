namespace OpenNist.Nfiq;

using JetBrains.Annotations;

/// <summary>
/// Maps native NFIQ 2 quality measures into ISO/IEC 39794 quality-block values.
/// </summary>
[PublicAPI]
public static class Nfiq2QualityBlockMapper
{
    private const double OrientationFlowAngleMinDegrees = 4.0;
    private const string ImageMean = "Mu";
    private const string MeanOfBlockMeans = "MMB";
    private const string RegionOfInterestMean = "ImgProcROIArea_Mean";
    private const string MinutiaeCount = "FingerJetFX_MinutiaeCount";
    private const string MinutiaeCountCom = "FingerJetFX_MinCount_COMMinRect200x200";
    private const string MinutiaePercentImageMean50 = "FJFXPos_Mu_MinutiaeQuality_2";
    private const string MinutiaePercentOrientationCertainty80 = "FJFXPos_OCL_MinutiaeQuality_80";
    private const string RegionOfInterestCoherenceMean = "OrientationMap_ROIFilter_CoherenceRel";
    private const string RegionOfInterestCoherenceSum = "OrientationMap_ROIFilter_CoherenceSum";
    private const string FrequencyDomainAnalysisMean = "FDA_Bin10_Mean";
    private const string FrequencyDomainAnalysisStdDev = "FDA_Bin10_StdDev";
    private const string LocalClarityMean = "LCS_Bin10_Mean";
    private const string LocalClarityStdDev = "LCS_Bin10_StdDev";
    private const string OrientationCertaintyMean = "OCL_Bin10_Mean";
    private const string OrientationCertaintyStdDev = "OCL_Bin10_StdDev";
    private const string OrientationFlowMean = "OF_Bin10_Mean";
    private const string OrientationFlowStdDev = "OF_Bin10_StdDev";
    private const string RidgeValleyUniformityMean = "RVUP_Bin10_Mean";
    private const string RidgeValleyUniformityStdDev = "RVUP_Bin10_StdDev";

    /// <summary>
    /// Maps all supplied native quality measures to quality-block values where supported.
    /// </summary>
    /// <param name="nativeQualityMeasures">The native quality measures.</param>
    /// <returns>A map of native identifiers to quality-block values or <see langword="null"/> when unsupported.</returns>
    public static IReadOnlyDictionary<string, byte?> GetQualityBlockValues(
        IReadOnlyDictionary<string, double> nativeQualityMeasures)
    {
        ArgumentNullException.ThrowIfNull(nativeQualityMeasures);

        return nativeQualityMeasures.ToDictionary(
            static pair => pair.Key,
            static pair => GetQualityBlockValue(pair.Key, pair.Value),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Maps one native quality measure to its quality-block value when the native identifier is supported.
    /// </summary>
    /// <param name="featureIdentifier">The native quality measure identifier.</param>
    /// <param name="nativeQualityMeasureValue">The native quality measure value.</param>
    /// <returns>The quality-block value, or <see langword="null"/> when the native value is not mapped by NFIQ 2.</returns>
    public static byte? GetQualityBlockValue(string featureIdentifier, double nativeQualityMeasureValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureIdentifier);

        if (featureIdentifier is ImageMean or MeanOfBlockMeans or RegionOfInterestMean)
        {
            return KnownRange(nativeQualityMeasureValue, 0.0, 255.0);
        }

        if (featureIdentifier is MinutiaeCount or MinutiaeCountCom)
        {
            return checked((byte)Math.Min(nativeQualityMeasureValue, 100.0));
        }

        if (featureIdentifier is MinutiaePercentOrientationCertainty80 or RegionOfInterestCoherenceMean
            or MinutiaePercentImageMean50 or FrequencyDomainAnalysisMean or LocalClarityMean
            or OrientationCertaintyMean or FrequencyDomainAnalysisStdDev or LocalClarityStdDev
            or OrientationCertaintyStdDev or OrientationFlowStdDev)
        {
            return KnownRange(nativeQualityMeasureValue, 0.0, 1.0);
        }

        if (featureIdentifier == RegionOfInterestCoherenceSum)
        {
            return KnownRange(nativeQualityMeasureValue, 0.0, 3150.0);
        }

        if (featureIdentifier == OrientationFlowMean)
        {
            const double degreesToRadians = Math.PI / 180.0;
            var thetaMinRadians = OrientationFlowAngleMinDegrees * degreesToRadians;
            var denominator = (90.0 * degreesToRadians) - thetaMinRadians;
            var minLocalValue = (0.0 - thetaMinRadians) / denominator;
            var maxLocalValue = ((180.0 * degreesToRadians) - thetaMinRadians) / denominator;
            return KnownRange(nativeQualityMeasureValue, minLocalValue, maxLocalValue);
        }

        if (featureIdentifier is RidgeValleyUniformityMean or RidgeValleyUniformityStdDev)
        {
            return checked((byte)Math.Floor((100.0 * Sigmoid(nativeQualityMeasureValue, 1.0, 0.5)) + 0.5));
        }

        return null;
    }

    internal static double Sigmoid(double nativeQuality, double inflectionPoint, double scaling)
    {
        return Math.Pow(1.0 + Math.Exp((inflectionPoint - nativeQuality) / scaling), -1.0);
    }

    internal static byte KnownRange(double nativeQualityMeasure, double min, double max)
    {
        return checked((byte)Math.Floor(
            101.0 * ((nativeQualityMeasure - min) / (max - min + double.Epsilon))));
    }
}
