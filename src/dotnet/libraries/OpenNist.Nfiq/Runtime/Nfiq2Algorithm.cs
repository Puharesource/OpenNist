namespace OpenNist.Nfiq.Runtime;

using JetBrains.Annotations;
using OpenNist.Nfiq.Abstractions;
using OpenNist.Nfiq.Configuration;
using OpenNist.Nfiq.Errors;
using OpenNist.Nfiq.Internal.Csv;
using OpenNist.Nfiq.Internal.Errors;
using OpenNist.Nfiq.Internal.FingerJet;
using OpenNist.Nfiq.Internal.Model;
using OpenNist.Nfiq.Internal.Runtime;
using OpenNist.Nfiq.Internal.Support;
using OpenNist.Nfiq.Model;

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
    public async ValueTask<Nfiq2AssessmentResult> AnalyzeFileAsync(
        string fingerprintPath,
        Nfiq2AnalysisOptions options = default,
        CancellationToken cancellationToken = default)
    {
        var result = await TryAnalyzeFileAsync(fingerprintPath, options, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? result.Value!
            : throw Nfiq2Errors.ExceptionFrom(result.Error!);
    }

    /// <inheritdoc />
    public ValueTask<Nfiq2Result<Nfiq2AssessmentResult>> TryAnalyzeFileAsync(
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
        return TryAnalyzeAsync(
            pixels,
            new(width, height, BitsPerPixel: 8, PixelsPerInch: Nfiq2FingerprintImage.Resolution500Ppi),
            options,
            normalizedPath,
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<Nfiq2AssessmentResult> AnalyzeAsync(
        ReadOnlyMemory<byte> rawPixels,
        Nfiq2RawImageDescription rawImage,
        Nfiq2AnalysisOptions options = default,
        CancellationToken cancellationToken = default)
    {
        var result = await TryAnalyzeAsync(rawPixels, rawImage, options, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? result.Value!
            : throw Nfiq2Errors.ExceptionFrom(result.Error!);
    }

    /// <inheritdoc />
    public ValueTask<Nfiq2Result<Nfiq2AssessmentResult>> TryAnalyzeAsync(
        ReadOnlyMemory<byte> rawPixels,
        Nfiq2RawImageDescription rawImage,
        Nfiq2AnalysisOptions options = default,
        CancellationToken cancellationToken = default)
    {
        return TryAnalyzeAsync(rawPixels, rawImage, options, s_inMemoryFileName, cancellationToken);
    }

    private ValueTask<Nfiq2Result<Nfiq2AssessmentResult>> TryAnalyzeAsync(
        ReadOnlyMemory<byte> rawPixels,
        Nfiq2RawImageDescription rawImage,
        Nfiq2AnalysisOptions options,
        string filename,
        CancellationToken cancellationToken)
    {
        options = NormalizeOptions(options);
        ValidateOptions(options);
        cancellationToken.ThrowIfCancellationRequested();

        var validation = ValidateRawImage(rawPixels, rawImage);
        if (!validation.IsValid)
        {
            return ValueTask.FromResult(Nfiq2Results.Failure<Nfiq2AssessmentResult>(Nfiq2Errors.ValidationFailed(validation.Errors)));
        }

        try
        {
            var result = AnalyzeCore(rawPixels, rawImage, filename, options, cancellationToken);
            return ValueTask.FromResult(Nfiq2Results.Success(result));
        }
        catch (Nfiq2Exception exception)
        {
            return ValueTask.FromResult(Nfiq2Results.Failure<Nfiq2AssessmentResult>(Nfiq2Errors.ErrorFromException(exception)));
        }
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

    private static Nfiq2ValidationResult ValidateRawImage(ReadOnlyMemory<byte> rawPixels, Nfiq2RawImageDescription rawImage)
    {
        var errors = new List<Nfiq2ValidationError>(capacity: 5);

        if (rawImage.Width <= 0)
        {
            errors.Add(Nfiq2Errors.RawImageWidthMustBePositive(rawImage.Width));
        }

        if (rawImage.Height <= 0)
        {
            errors.Add(Nfiq2Errors.RawImageHeightMustBePositive(rawImage.Height));
        }

        if (rawImage.BitsPerPixel != 8)
        {
            errors.Add(Nfiq2Errors.RawImageBitsPerPixelUnsupported(rawImage.BitsPerPixel));
        }

        if (rawImage.PixelsPerInch != 500)
        {
            errors.Add(Nfiq2Errors.RawImagePixelsPerInchUnsupported(rawImage.PixelsPerInch));
        }

        var expectedLength = rawImage.Width > 0 && rawImage.Height > 0
            ? checked(rawImage.Width * rawImage.Height)
            : -1;

        if (rawPixels.Length != expectedLength)
        {
            errors.Add(Nfiq2Errors.RawImagePixelBufferLengthMismatch(rawPixels.Length, expectedLength));
        }

        return errors.Count == 0
            ? Nfiq2ValidationResult.Success()
            : Nfiq2ValidationResult.Failure(errors);
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
