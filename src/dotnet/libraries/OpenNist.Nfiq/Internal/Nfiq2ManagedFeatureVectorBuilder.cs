namespace OpenNist.Nfiq.Internal;

using System.Collections.Frozen;

internal static class Nfiq2ManagedFeatureVectorBuilder
{
    public static IReadOnlyDictionary<string, double> BuildNativeQualityMeasures(
        Nfiq2FingerprintImage fingerprintImage,
        IReadOnlyList<Nfiq2Minutia> minutiae)
    {
        ArgumentNullException.ThrowIfNull(fingerprintImage);
        ArgumentNullException.ThrowIfNull(minutiae);

        var mu = Nfiq2MuModule.Compute(fingerprintImage);
        var roi = Nfiq2ImgProcRoiModule.Compute(fingerprintImage);
        var qualityMap = Nfiq2QualityMapModule.Compute(fingerprintImage, roi);
        var ocl = Nfiq2OclHistogramModule.Compute(fingerprintImage);
        var orientationFlow = Nfiq2OrientationFlowModule.Compute(fingerprintImage);
        var localClarity = Nfiq2LocalClarityModule.Compute(fingerprintImage);
        var ridgeValleyUniformity = Nfiq2RidgeValleyUniformityModule.Compute(fingerprintImage);
        var frequencyDomainAnalysis = Nfiq2FrequencyDomainAnalysisModule.Compute(fingerprintImage);
        var minutiaeCount = Nfiq2MinutiaeCountModule.Compute(minutiae, fingerprintImage.Width, fingerprintImage.Height);
        var minutiaeQuality = Nfiq2MinutiaeQualityModule.Compute(fingerprintImage, minutiae);

        var features = new Dictionary<string, double>(StringComparer.Ordinal);
        AddAll(features, mu.Features);
        AddAll(features, roi.Features);
        AddAll(features, qualityMap.Features);
        AddAll(features, ocl.Features);
        AddAll(features, orientationFlow.Features);
        AddAll(features, localClarity.Features);
        AddAll(features, ridgeValleyUniformity.Features);
        AddAll(features, frequencyDomainAnalysis.Features);
        AddAll(features, minutiaeCount.Features);
        AddAll(features, minutiaeQuality);

        return features.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static void AddAll(
        Dictionary<string, double> destination,
        IReadOnlyDictionary<string, double> source)
    {
        foreach (var entry in source)
        {
            destination[entry.Key] = entry.Value;
        }
    }
}
