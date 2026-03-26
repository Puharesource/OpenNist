namespace OpenNist.Nfiq;

using JetBrains.Annotations;

/// <summary>
/// Maps native NFIQ 2 quality measures into ISO/IEC 39794 quality-block values.
/// </summary>
[PublicAPI]
public static class Nfiq2QualityBlockMapper
{
    private const double s_orientationFlowAngleMinDegrees = 4.0;
    private const string s_imageMean = "Mu";
    private const string s_meanOfBlockMeans = "MMB";
    private const string s_regionOfInterestMean = "ImgProcROIArea_Mean";
    private const string s_minutiaeCount = "FingerJetFX_MinutiaeCount";
    private const string s_minutiaeCountCom = "FingerJetFX_MinCount_COMMinRect200x200";
    private const string s_minutiaePercentImageMean50 = "FJFXPos_Mu_MinutiaeQuality_2";
    private const string s_minutiaePercentOrientationCertainty80 = "FJFXPos_OCL_MinutiaeQuality_80";
    private const string s_regionOfInterestCoherenceMean = "OrientationMap_ROIFilter_CoherenceRel";
    private const string s_regionOfInterestCoherenceSum = "OrientationMap_ROIFilter_CoherenceSum";
    private const string s_frequencyDomainAnalysisMean = "FDA_Bin10_Mean";
    private const string s_frequencyDomainAnalysisStdDev = "FDA_Bin10_StdDev";
    private const string s_localClarityMean = "LCS_Bin10_Mean";
    private const string s_localClarityStdDev = "LCS_Bin10_StdDev";
    private const string s_orientationCertaintyMean = "OCL_Bin10_Mean";
    private const string s_orientationCertaintyStdDev = "OCL_Bin10_StdDev";
    private const string s_orientationFlowMean = "OF_Bin10_Mean";
    private const string s_orientationFlowStdDev = "OF_Bin10_StdDev";
    private const string s_ridgeValleyUniformityMean = "RVUP_Bin10_Mean";
    private const string s_ridgeValleyUniformityStdDev = "RVUP_Bin10_StdDev";

    /// <summary>
    /// Maps all supplied native quality measures to quality-block values where supported.
    /// </summary>
    /// <param name="nativeQualityMeasures">The native quality measures.</param>
    /// <returns>A map of native identifiers to quality-block values or <see langword="null"/> when unsupported.</returns>
    public static IReadOnlyDictionary<string, byte?> GetQualityBlockValues(
        IReadOnlyDictionary<string, double> nativeQualityMeasures)
    {
        ArgumentNullException.ThrowIfNull(nativeQualityMeasures);

        var mappedValues = new Dictionary<string, byte?>(nativeQualityMeasures.Count, StringComparer.Ordinal);
        foreach (var pair in nativeQualityMeasures)
        {
            mappedValues[pair.Key] = GetQualityBlockValue(pair.Key, pair.Value);
        }

        return mappedValues;
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

        if (featureIdentifier is s_imageMean or s_meanOfBlockMeans or s_regionOfInterestMean)
        {
            return KnownRange(nativeQualityMeasureValue, 0.0, 255.0);
        }

        if (featureIdentifier is s_minutiaeCount or s_minutiaeCountCom)
        {
            return checked((byte)Math.Min(nativeQualityMeasureValue, 100.0));
        }

        if (featureIdentifier is s_minutiaePercentOrientationCertainty80 or s_regionOfInterestCoherenceMean
            or s_minutiaePercentImageMean50 or s_frequencyDomainAnalysisMean or s_localClarityMean
            or s_orientationCertaintyMean or s_frequencyDomainAnalysisStdDev or s_localClarityStdDev
            or s_orientationCertaintyStdDev or s_orientationFlowStdDev)
        {
            return KnownRange(nativeQualityMeasureValue, 0.0, 1.0);
        }

        if (featureIdentifier == s_regionOfInterestCoherenceSum)
        {
            return KnownRange(nativeQualityMeasureValue, 0.0, 3150.0);
        }

        if (featureIdentifier == s_orientationFlowMean)
        {
            const double degreesToRadians = Math.PI / 180.0;
            var thetaMinRadians = s_orientationFlowAngleMinDegrees * degreesToRadians;
            var denominator = 90.0 * degreesToRadians - thetaMinRadians;
            var minLocalValue = (0.0 - thetaMinRadians) / denominator;
            var maxLocalValue = (180.0 * degreesToRadians - thetaMinRadians) / denominator;
            return KnownRange(nativeQualityMeasureValue, minLocalValue, maxLocalValue);
        }

        if (featureIdentifier is s_ridgeValleyUniformityMean or s_ridgeValleyUniformityStdDev)
        {
            return checked((byte)Math.Floor(100.0 * Sigmoid(nativeQualityMeasureValue, 1.0, 0.5) + 0.5));
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
