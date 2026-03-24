namespace OpenNist.Nfiq.Internal;

using System.Collections.Frozen;

internal sealed class Nfiq2ManagedAssessmentEngine
{
    private const double UniformImageThreshold = 1.0;
    private const double EmptyImageThreshold = 250.0;
    private const string UniformImage = "UniformImage";
    private const string EmptyImageOrContrastTooLow = "EmptyImageOrContrastTooLow";
    private const string FingerprintImageWithMinutiae = "FingerprintImageWithMinutiae";
    private const string SufficientFingerprintForeground = "SufficientFingerprintForeground";

    private readonly Nfiq2ManagedModel model;

    public Nfiq2ManagedAssessmentEngine(Nfiq2ManagedModel model)
    {
        this.model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public static Nfiq2ManagedAssessmentEngine LoadDefault()
    {
        return new(Nfiq2ManagedModel.LoadDefault());
    }

    public Nfiq2AssessmentResult Analyze(
        Nfiq2FingerprintImage fingerprintImage,
        IReadOnlyList<Nfiq2Minutia> minutiae,
        string filename,
        int fingerCode = 0)
    {
        ArgumentNullException.ThrowIfNull(fingerprintImage);
        ArgumentNullException.ThrowIfNull(minutiae);
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);

        var nativeQualityMeasures = Nfiq2ManagedFeatureVectorBuilder.BuildNativeQualityMeasures(fingerprintImage, minutiae);
        var qualityScore = model.ComputeUnifiedQualityScore(
            nativeQualityMeasures.ToDictionary(
                static entry => entry.Key,
                static entry => (double?)entry.Value,
                StringComparer.Ordinal));

        var actionableFeedback = BuildActionableFeedback(
            minutiae,
            Nfiq2ImgProcRoiModule.Compute(fingerprintImage),
            Nfiq2MuModule.Compute(fingerprintImage));

        var mappedQualityMeasures = BuildMappedQualityMeasures(nativeQualityMeasures);

        return new(
            Filename: filename,
            FingerCode: fingerCode,
            QualityScore: qualityScore,
            OptionalError: null,
            Quantized: false,
            Resampled: false,
            ActionableFeedback: actionableFeedback,
            NativeQualityMeasures: nativeQualityMeasures.ToFrozenDictionary(
                static entry => entry.Key,
                static entry => (double?)entry.Value,
                StringComparer.Ordinal),
            MappedQualityMeasures: mappedQualityMeasures);
    }

    private static FrozenDictionary<string, double?> BuildActionableFeedback(
        IReadOnlyList<Nfiq2Minutia> minutiae,
        Nfiq2ImgProcRoiResult roi,
        Nfiq2MuModuleResult mu)
    {
        var actionableFeedback = new Dictionary<string, double?>(StringComparer.Ordinal)
        {
            [UniformImage] = mu.Sigma,
            [EmptyImageOrContrastTooLow] = mu.ImageMean,
            [FingerprintImageWithMinutiae] = minutiae.Count,
            [SufficientFingerprintForeground] = roi.RoiPixels,
        };

        var isUniformImage = mu.Sigma < UniformImageThreshold;
        var isEmptyImage = mu.ImageMean > EmptyImageThreshold;
        if (isUniformImage || isEmptyImage)
        {
            return actionableFeedback.ToFrozenDictionary(StringComparer.Ordinal);
        }

        return actionableFeedback.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static FrozenDictionary<string, double?> BuildMappedQualityMeasures(
        IReadOnlyDictionary<string, double> nativeQualityMeasures)
    {
        var mapped = new Dictionary<string, double?>(StringComparer.Ordinal);
        foreach (var entry in Nfiq2QualityBlockMapper.GetQualityBlockValues(nativeQualityMeasures))
        {
            if (entry.Value is byte value)
            {
                mapped[$"{Nfiq2ColumnDefinitions.MappedPrefix}{entry.Key}"] = value;
            }
        }

        return mapped.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
