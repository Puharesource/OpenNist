namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2ManagedFeatureVectorBuilder
{
    public static Nfiq2ManagedFeatureVector Build(
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
        var ridgeValleyContext = Nfiq2RidgeValleySupport.CreateFeatureContext(
            fingerprintImage,
            blockSize: 32,
            segmentationThreshold: 0.1,
            slantedBlockWidth: 32,
            slantedBlockHeight: 16);
        var localClarity = Nfiq2LocalClarityModule.Compute(fingerprintImage, ridgeValleyContext);
        var ridgeValleyUniformity = Nfiq2RidgeValleyUniformityModule.Compute(fingerprintImage, ridgeValleyContext);
        var frequencyDomainAnalysis = Nfiq2FrequencyDomainAnalysisModule.Compute(fingerprintImage, ridgeValleyContext);
        var minutiaeCount = Nfiq2MinutiaeCountModule.Compute(minutiae, fingerprintImage.Width, fingerprintImage.Height);
        var minutiaeQuality = Nfiq2MinutiaeQualityModule.Compute(fingerprintImage, minutiae);

        var featureCapacity = mu.Features.Count
            + roi.Features.Count
            + qualityMap.Features.Count
            + ocl.Features.Count
            + orientationFlow.Features.Count
            + localClarity.Features.Count
            + ridgeValleyUniformity.Features.Count
            + frequencyDomainAnalysis.Features.Count
            + minutiaeCount.Features.Count
            + minutiaeQuality.Count;
        var features = new Dictionary<string, double>(featureCapacity, StringComparer.Ordinal);
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

        return new(features, mu, roi);
    }

    public static IReadOnlyDictionary<string, double> BuildNativeQualityMeasures(
        Nfiq2FingerprintImage fingerprintImage,
        IReadOnlyList<Nfiq2Minutia> minutiae)
    {
        return Build(fingerprintImage, minutiae).Features;
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

internal sealed record Nfiq2ManagedFeatureVector(
    IReadOnlyDictionary<string, double> Features,
    Nfiq2MuModuleResult Mu,
    Nfiq2ImgProcRoiResult Roi);
