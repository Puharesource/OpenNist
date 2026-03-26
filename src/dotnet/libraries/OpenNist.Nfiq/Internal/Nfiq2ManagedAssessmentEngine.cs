namespace OpenNist.Nfiq.Internal;

using System.Collections.Frozen;

internal sealed class Nfiq2ManagedAssessmentEngine
{
    private const string s_uniformImage = "UniformImage";
    private const string s_emptyImageOrContrastTooLow = "EmptyImageOrContrastTooLow";
    private const string s_fingerprintImageWithMinutiae = "FingerprintImageWithMinutiae";
    private const string s_sufficientFingerprintForeground = "SufficientFingerprintForeground";
    private static readonly FrozenDictionary<string, double?> s_emptyMeasures =
        new Dictionary<string, double?>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal);

    private readonly Nfiq2ManagedModel _model;

    public Nfiq2ManagedAssessmentEngine(Nfiq2ManagedModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public static Nfiq2ManagedAssessmentEngine LoadDefault()
    {
        return new(Nfiq2ManagedModel.LoadDefault());
    }

    public Nfiq2AssessmentResult Analyze(
        Nfiq2FingerprintImage fingerprintImage,
        IReadOnlyList<Nfiq2Minutia> minutiae,
        string filename,
        bool includeMappedQualityMeasures,
        int fingerCode = 0)
    {
        ArgumentNullException.ThrowIfNull(fingerprintImage);
        ArgumentNullException.ThrowIfNull(minutiae);
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);

        var featureVector = Nfiq2ManagedFeatureVectorBuilder.Build(fingerprintImage, minutiae);
        var qualityScore = _model.ComputeUnifiedQualityScore(featureVector.Features);
        var actionableFeedback = BuildActionableFeedback(minutiae, featureVector.Roi, featureVector.Mu);
        var mappedQualityMeasures = includeMappedQualityMeasures
            ? BuildMappedQualityMeasures(featureVector.Features)
            : s_emptyMeasures;
        var nativeQualityMeasureResults = BuildNativeQualityMeasureResults(featureVector.Features);

        return new(
            Filename: filename,
            FingerCode: fingerCode,
            QualityScore: qualityScore,
            OptionalError: null,
            Quantized: false,
            Resampled: false,
            ActionableFeedback: actionableFeedback,
            NativeQualityMeasures: nativeQualityMeasureResults,
            MappedQualityMeasures: mappedQualityMeasures);
    }

    private static FrozenDictionary<string, double?> BuildActionableFeedback(
        IReadOnlyList<Nfiq2Minutia> minutiae,
        Nfiq2ImgProcRoiResult roi,
        Nfiq2MuModuleResult mu)
    {
        var actionableFeedback = new Dictionary<string, double?>(capacity: 4, StringComparer.Ordinal)
        {
            [s_uniformImage] = mu.Sigma,
            [s_emptyImageOrContrastTooLow] = mu.ImageMean,
            [s_fingerprintImageWithMinutiae] = minutiae.Count,
            [s_sufficientFingerprintForeground] = roi.RoiPixels,
        };

        return actionableFeedback.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static FrozenDictionary<string, double?> BuildMappedQualityMeasures(
        IReadOnlyDictionary<string, double> nativeQualityMeasures)
    {
        var mapped = new Dictionary<string, double?>(nativeQualityMeasures.Count, StringComparer.Ordinal);
        foreach (var entry in nativeQualityMeasures)
        {
            var mappedValue = Nfiq2QualityBlockMapper.GetQualityBlockValue(entry.Key, entry.Value);
            if (mappedValue is { } value)
            {
                mapped[$"{Nfiq2ColumnDefinitions.MappedPrefix}{entry.Key}"] = value;
            }
        }

        return mapped.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static FrozenDictionary<string, double?> BuildNativeQualityMeasureResults(
        IReadOnlyDictionary<string, double> nativeQualityMeasures)
    {
        var nativeResults = new Dictionary<string, double?>(nativeQualityMeasures.Count, StringComparer.Ordinal);
        foreach (var entry in nativeQualityMeasures)
        {
            nativeResults[entry.Key] = entry.Value;
        }

        return nativeResults.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
