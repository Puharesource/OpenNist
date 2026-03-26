namespace OpenNist.Benchmarks;

using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using OpenNist.Nfiq;
using OpenNist.Nfiq.Internal;

[MemoryDiagnoser]
public class NfiqBenchmarks
{
    private static readonly Nfiq2AnalysisOptions s_mappedOptions = new(
        IncludeMappedQualityMeasures: true,
        Force: true,
        ThreadCount: null);

    private static readonly Nfiq2AnalysisOptions s_nativeOnlyOptions = new(
        IncludeMappedQualityMeasures: false,
        Force: true,
        ThreadCount: null);

    private Nfiq2Algorithm _algorithm = null!;
    private Nfiq2ManagedAssessmentEngine _assessmentEngine = null!;
    private Nfiq2ManagedModel _model = null!;
    private Nfiq2FingerprintImage _fingerprintImage = null!;
    private Nfiq2FingerprintImage _croppedImage = null!;
    private IReadOnlyList<Nfiq2Minutia> _minutiae = null!;
    private Nfiq2ManagedFeatureVector _featureVector = null!;
    private Dictionary<string, double> _nativeMeasures = null!;
    private byte[] _rawPixels = null!;
    private Nfiq2RawImageDescription _rawImage;
    private string _imagePath = string.Empty;

    [Params("SFinGe_Test01.pgm", "SFinGe_Test05.pgm")]
    public string ImageFileName { get; set; } = "SFinGe_Test01.pgm";

    [GlobalSetup]
    public void Setup()
    {
        _imagePath = BenchmarkPaths.NfiqExampleImage(ImageFileName);
        (_rawPixels, _rawImage) = PortableGrayMapFixture.Read(_imagePath);
        _algorithm = new();
        _assessmentEngine = Nfiq2ManagedAssessmentEngine.LoadDefault();
        _model = Nfiq2ManagedModel.LoadDefault();
        _fingerprintImage = new(
            _rawPixels,
            _rawImage.Width,
            _rawImage.Height,
            fingerCode: 0,
            ppi: checked((ushort)_rawImage.PixelsPerInch));
        _croppedImage = _fingerprintImage.CopyRemovingNearWhiteFrame();
        _minutiae = Nfiq2FingerJetManagedExtractor.ExtractFromCroppedImage(_croppedImage);
        _featureVector = Nfiq2ManagedFeatureVectorBuilder.Build(_croppedImage, _minutiae);
        var result = _algorithm.AnalyzeAsync(_rawPixels, _rawImage, s_mappedOptions).AsTask().GetAwaiter().GetResult();
        _nativeMeasures = result.NativeQualityMeasures.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value ?? throw new InvalidOperationException($"Missing native quality measure '{pair.Key}'."),
            StringComparer.Ordinal);
    }

    [Benchmark]
    public Nfiq2Algorithm CreateAlgorithm()
    {
        return new();
    }

    [Benchmark]
    public Nfiq2AssessmentResult AnalyzeRawWithMappedMeasures()
    {
        return _algorithm.AnalyzeAsync(_rawPixels, _rawImage, s_mappedOptions).AsTask().GetAwaiter().GetResult();
    }

    [Benchmark]
    public Nfiq2AssessmentResult AnalyzeRawWithoutMappedMeasures()
    {
        return _algorithm.AnalyzeAsync(_rawPixels, _rawImage, s_nativeOnlyOptions).AsTask().GetAwaiter().GetResult();
    }

    [Benchmark]
    public Nfiq2AssessmentResult AnalyzeFileWithMappedMeasures()
    {
        return _algorithm.AnalyzeFileAsync(_imagePath, s_mappedOptions).AsTask().GetAwaiter().GetResult();
    }

    [Benchmark]
    public int CropNearWhiteFrame()
    {
        var image = _fingerprintImage.CopyRemovingNearWhiteFrame();
        return image.Width * image.Height;
    }

    [Benchmark]
    public int ExtractMinutiaeFromCroppedImage()
    {
        return Nfiq2FingerJetManagedExtractor.ExtractFromCroppedImage(_croppedImage).Count;
    }

    [Benchmark]
    public int BuildManagedFeatureVector()
    {
        return Nfiq2ManagedFeatureVectorBuilder.Build(_croppedImage, _minutiae).Features.Count;
    }

    [Benchmark]
    public Nfiq2AssessmentResult AssessFromPrecomputedMinutiaeWithMappedMeasures()
    {
        return _assessmentEngine.Analyze(_croppedImage, _minutiae, _imagePath, includeMappedQualityMeasures: true, fingerCode: 0);
    }

    [Benchmark]
    public int ScoreModelFromFeatureVector()
    {
        return _model.ComputeUnifiedQualityScore(_featureVector.Features);
    }

    [Benchmark]
    public int ScoreModelFromNativeMeasures()
    {
        return _model.ComputeUnifiedQualityScore(_nativeMeasures);
    }
}
