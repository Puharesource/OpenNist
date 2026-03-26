namespace OpenNist.Nfiq;

using JetBrains.Annotations;
using OpenNist.Nfiq.Internal;

/// <summary>
/// Default NFIQ 2 implementation backed by the managed OpenNist port and official model files.
/// </summary>
[PublicAPI]
public sealed class Nfiq2Algorithm : INfiq2Algorithm
{
    private const string s_inMemoryFileName = "fingerprint.pgm";
    private static readonly Nfiq2AnalysisOptions s_defaultOptions = new(
        IncludeMappedQualityMeasures: true,
        Force: true,
        ThreadCount: null);

    private readonly Nfiq2ManagedAssessmentEngine _assessmentEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="Nfiq2Algorithm"/> class using the default installation.
    /// </summary>
    public Nfiq2Algorithm()
        : this(Nfiq2ManagedModel.LoadDefault())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Nfiq2Algorithm"/> class.
    /// </summary>
    /// <param name="installation">The NFIQ 2 model installation to use.</param>
    public Nfiq2Algorithm(Nfiq2Installation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        _assessmentEngine = new(new(Nfiq2ModelInfo.FromFile(installation.ModelInfoPath)));
    }

    private Nfiq2Algorithm(Nfiq2ManagedModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _assessmentEngine = new(model);
    }

    /// <inheritdoc />
    public ValueTask<Nfiq2AssessmentResult> AnalyzeFileAsync(
        string fingerprintPath,
        Nfiq2AnalysisOptions options = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprintPath);
        options = NormalizeOptions(options);
        ValidateOptions(options);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedPath = Path.GetFullPath(fingerprintPath);
        var (pixels, width, height) = Nfiq2PortableGrayMapCodec.Read(normalizedPath);
        var result = AnalyzeCore(
            pixels,
            new(width, height, BitsPerPixel: 8, PixelsPerInch: Nfiq2FingerprintImage.Resolution500Ppi),
            normalizedPath,
            options,
            cancellationToken);

        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public ValueTask<Nfiq2AssessmentResult> AnalyzeAsync(
        ReadOnlyMemory<byte> rawPixels,
        Nfiq2RawImageDescription rawImage,
        Nfiq2AnalysisOptions options = default,
        CancellationToken cancellationToken = default)
    {
        options = NormalizeOptions(options);
        ValidateOptions(options);
        ValidateRawImage(rawPixels, rawImage);
        cancellationToken.ThrowIfCancellationRequested();

        var result = AnalyzeCore(rawPixels, rawImage, s_inMemoryFileName, options, cancellationToken);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public ValueTask<Nfiq2CsvReport> AnalyzeFilesAsync(
        IEnumerable<string> fingerprintPaths,
        Nfiq2AnalysisOptions options = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fingerprintPaths);
        options = NormalizeOptions(options);
        ValidateOptions(options);

        var normalizedPaths = fingerprintPaths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToArray();

        if (normalizedPaths.Length == 0)
        {
            throw new ArgumentException("At least one fingerprint path must be provided.", nameof(fingerprintPaths));
        }

        var results = new Nfiq2AssessmentResult[normalizedPaths.Length];
        for (var index = 0; index < normalizedPaths.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = normalizedPaths[index];
            var (pixels, width, height) = Nfiq2PortableGrayMapCodec.Read(path);
            results[index] = AnalyzeCore(
                pixels,
                new(width, height, BitsPerPixel: 8, PixelsPerInch: Nfiq2FingerprintImage.Resolution500Ppi),
                path,
                options,
                cancellationToken);
        }

        return ValueTask.FromResult(Nfiq2CsvReportBuilder.Build(results, options.IncludeMappedQualityMeasures));
    }

    private Nfiq2AssessmentResult AnalyzeCore(
        ReadOnlyMemory<byte> rawPixels,
        Nfiq2RawImageDescription rawImage,
        string filename,
        Nfiq2AnalysisOptions options,
        CancellationToken cancellationToken)
    {
        ValidateRawImage(rawPixels, rawImage);
        cancellationToken.ThrowIfCancellationRequested();

        var fingerprintImage = new Nfiq2FingerprintImage(
            rawPixels,
            rawImage.Width,
            rawImage.Height,
            fingerCode: 0,
            ppi: checked((ushort)rawImage.PixelsPerInch));

        var croppedImage = fingerprintImage.CopyRemovingNearWhiteFrame();
        var minutiae = Nfiq2FingerJetManagedExtractor.ExtractFromCroppedImage(croppedImage);
        cancellationToken.ThrowIfCancellationRequested();

        return _assessmentEngine.Analyze(
            croppedImage,
            minutiae,
            filename,
            options.IncludeMappedQualityMeasures,
            fingerprintImage.FingerCode);
    }

    private static void ValidateRawImage(ReadOnlyMemory<byte> rawPixels, Nfiq2RawImageDescription rawImage)
    {
        if (rawImage.Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rawImage), rawImage.Width, "Image width must be positive.");
        }

        if (rawImage.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rawImage), rawImage.Height, "Image height must be positive.");
        }

        if (rawImage.BitsPerPixel != 8)
        {
            throw new NotSupportedException(
                $"NFIQ 2 only supports 8-bit grayscale input, but received {rawImage.BitsPerPixel} bits per pixel.");
        }

        if (rawImage.PixelsPerInch != 500)
        {
            throw new NotSupportedException(
                $"Managed NFIQ 2 analysis currently requires 500 PPI input, but received {rawImage.PixelsPerInch} PPI.");
        }

        var expectedLength = checked(rawImage.Width * rawImage.Height);
        if (rawPixels.Length != expectedLength)
        {
            throw new ArgumentException(
                $"The supplied raw pixel buffer length ({rawPixels.Length}) does not match the declared image area ({expectedLength}).",
                nameof(rawPixels));
        }
    }

    private static void ValidateOptions(Nfiq2AnalysisOptions options)
    {
        if (options.ThreadCount is { } threadCount and <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), threadCount, "Thread count must be positive.");
        }
    }

    private static Nfiq2AnalysisOptions NormalizeOptions(Nfiq2AnalysisOptions options)
    {
        return options == default
            ? s_defaultOptions
            : options;
    }
}
