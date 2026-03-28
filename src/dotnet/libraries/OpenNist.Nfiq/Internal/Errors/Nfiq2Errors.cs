namespace OpenNist.Nfiq.Internal.Errors;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using OpenNist.Nfiq.Errors;
using OpenNist.Nfiq.Model;
using OpenNist.Primitives.Documentation;
using OpenNist.Primitives.Errors;

internal static class Nfiq2Errors
{
    public static Nfiq2ErrorInfo UnexpectedFailure(string message) =>
        Error(
            Nfiq2ErrorCodes.UnexpectedFailure,
            message,
            Nfiq2ErrorKind.Internal,
            isRetryable: false);

    public static Nfiq2ErrorInfo ValidationFailed(IReadOnlyList<Nfiq2ValidationError> validationErrors)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);

        return new(
            Code: Nfiq2ErrorCodes.ValidationFailed,
            Message: OpenNistValidationMessages.BuildFailureMessage("NFIQ 2", validationErrors),
            Kind: Nfiq2ErrorKind.Validation,
            IsRetryable: false,
            Documentation: DocumentationUri(Nfiq2ErrorCodes.ValidationFailed),
            Metadata: new Dictionary<string, object?>
            {
                ["errorCount"] = validationErrors.Count
            },
            ValidationErrors: validationErrors);
    }

    public static Nfiq2ValidationError RawImageWidthMustBePositive(int width) =>
        Validation(
            Nfiq2ErrorCodes.RawImageWidthMustBePositive,
            $"Image width must be positive. Provide a width greater than zero, but received {width.ToString(CultureInfo.InvariantCulture)}.",
            field: nameof(Nfiq2RawImageDescription.Width),
            metadata: new Dictionary<string, object?> { ["width"] = width });

    public static Nfiq2ValidationError RawImageHeightMustBePositive(int height) =>
        Validation(
            Nfiq2ErrorCodes.RawImageHeightMustBePositive,
            $"Image height must be positive. Provide a height greater than zero, but received {height.ToString(CultureInfo.InvariantCulture)}.",
            field: nameof(Nfiq2RawImageDescription.Height),
            metadata: new Dictionary<string, object?> { ["height"] = height });

    public static Nfiq2ValidationError RawImageBitsPerPixelUnsupported(int bitsPerPixel) =>
        Validation(
            Nfiq2ErrorCodes.RawImageBitsPerPixelUnsupported,
            $"Managed NFIQ 2 only supports 8-bit grayscale input. Provide 8 bits per pixel, but received {bitsPerPixel.ToString(CultureInfo.InvariantCulture)}.",
            field: nameof(Nfiq2RawImageDescription.BitsPerPixel),
            metadata: new Dictionary<string, object?> { ["bitsPerPixel"] = bitsPerPixel, ["supportedBitsPerPixel"] = 8 });

    public static Nfiq2ValidationError RawImagePixelsPerInchUnsupported(int pixelsPerInch) =>
        Validation(
            Nfiq2ErrorCodes.RawImagePixelsPerInchUnsupported,
            $"Managed NFIQ 2 currently supports 500 PPI input only. Provide a 500 PPI image, but received {pixelsPerInch.ToString(CultureInfo.InvariantCulture)} PPI.",
            field: nameof(Nfiq2RawImageDescription.PixelsPerInch),
            metadata: new Dictionary<string, object?> { ["pixelsPerInch"] = pixelsPerInch, ["supportedPixelsPerInch"] = 500 });

    public static Nfiq2ValidationError RawImagePixelBufferLengthMismatch(int actualLength, int expectedLength) =>
        Validation(
            Nfiq2ErrorCodes.RawImagePixelBufferLengthMismatch,
            $"The supplied raw pixel buffer length ({actualLength.ToString(CultureInfo.InvariantCulture)}) does not match the declared image area ({expectedLength.ToString(CultureInfo.InvariantCulture)}). Provide exactly width × height bytes.",
            field: "rawPixels",
            metadata: new Dictionary<string, object?> { ["actualLength"] = actualLength, ["expectedLength"] = expectedLength });

    public static Nfiq2ValidationError ImageRowsAppearBlank() =>
        Validation(
            Nfiq2ErrorCodes.ImageRowsAppearBlank,
            "All image rows appear blank after near-white trimming. Provide an image with visible fingerprint content.",
            field: "rawPixels");

    public static Nfiq2ValidationError ImageColumnsAppearBlank() =>
        Validation(
            Nfiq2ErrorCodes.ImageColumnsAppearBlank,
            "All image columns appear blank after near-white trimming. Provide an image with visible fingerprint content.",
            field: "rawPixels");

    public static Nfiq2ValidationError TrimmedImageWidthTooLarge(int width, int height, int maximumWidth) =>
        Validation(
            Nfiq2ErrorCodes.TrimmedImageWidthTooLarge,
            $"The fingerprint area is still too wide after near-white trimming. The trimmed image is {width.ToString(CultureInfo.InvariantCulture)}x{height.ToString(CultureInfo.InvariantCulture)}, but the maximum supported width is {maximumWidth.ToString(CultureInfo.InvariantCulture)}.",
            field: nameof(Nfiq2RawImageDescription.Width),
            metadata: new Dictionary<string, object?> { ["width"] = width, ["height"] = height, ["maximumWidth"] = maximumWidth });

    public static Nfiq2ValidationError TrimmedImageHeightTooLarge(int width, int height, int maximumHeight) =>
        Validation(
            Nfiq2ErrorCodes.TrimmedImageHeightTooLarge,
            $"The fingerprint area is still too tall after near-white trimming. The trimmed image is {width.ToString(CultureInfo.InvariantCulture)}x{height.ToString(CultureInfo.InvariantCulture)}, but the maximum supported height is {maximumHeight.ToString(CultureInfo.InvariantCulture)}.",
            field: nameof(Nfiq2RawImageDescription.Height),
            metadata: new Dictionary<string, object?> { ["width"] = width, ["height"] = height, ["maximumHeight"] = maximumHeight });

    public static Nfiq2ValidationError FingerJetCreateFeatureSetDimensionsExceeded(int width, int height, int maximumDimension) =>
        Validation(
            Nfiq2ErrorCodes.FingerJetCreateFeatureSetDimensionsExceeded,
            $"The raw image dimensions exceed the native FingerJet CreateFeatureSet limit. Width and height must both be {maximumDimension.ToString(CultureInfo.InvariantCulture)} pixels or smaller, but received {width.ToString(CultureInfo.InvariantCulture)}x{height.ToString(CultureInfo.InvariantCulture)}.",
            metadata: new Dictionary<string, object?> { ["width"] = width, ["height"] = height, ["maximumDimension"] = maximumDimension });

    public static Nfiq2ValidationError FingerJetCreateFeatureSetResolutionOutOfRange(int pixelsPerInch, int minimumDpi, int maximumDpi) =>
        Validation(
            Nfiq2ErrorCodes.FingerJetCreateFeatureSetResolutionOutOfRange,
            $"The image resolution falls outside the native FingerJet CreateFeatureSet range. Provide a resolution between {minimumDpi.ToString(CultureInfo.InvariantCulture)} and {maximumDpi.ToString(CultureInfo.InvariantCulture)} DPI, but received {pixelsPerInch.ToString(CultureInfo.InvariantCulture)}.",
            field: nameof(Nfiq2RawImageDescription.PixelsPerInch),
            metadata: new Dictionary<string, object?> { ["pixelsPerInch"] = pixelsPerInch, ["minimumDpi"] = minimumDpi, ["maximumDpi"] = maximumDpi });

    public static Nfiq2ValidationError FingerJetCreateFeatureSetWidthOutOfRange(int width, int pixelsPerInch, int minimumAt500Scale, int maximumAt500Scale) =>
        Validation(
            Nfiq2ErrorCodes.FingerJetCreateFeatureSetWidthOutOfRange,
            $"The image width falls outside the native FingerJet CreateFeatureSet limits at 500 PPI scale. Provide a width equivalent to {minimumAt500Scale.ToString(CultureInfo.InvariantCulture)}-{maximumAt500Scale.ToString(CultureInfo.InvariantCulture)} pixels at 500 PPI, but received {width.ToString(CultureInfo.InvariantCulture)} pixels at {pixelsPerInch.ToString(CultureInfo.InvariantCulture)} PPI.",
            field: nameof(Nfiq2RawImageDescription.Width),
            metadata: new Dictionary<string, object?> { ["width"] = width, ["pixelsPerInch"] = pixelsPerInch, ["minimumAt500Scale"] = minimumAt500Scale, ["maximumAt500Scale"] = maximumAt500Scale });

    public static Nfiq2ValidationError FingerJetCreateFeatureSetHeightOutOfRange(int height, int pixelsPerInch, int minimumAt500Scale, int maximumAt500Scale) =>
        Validation(
            Nfiq2ErrorCodes.FingerJetCreateFeatureSetHeightOutOfRange,
            $"The image height falls outside the native FingerJet CreateFeatureSet limits at 500 PPI scale. Provide a height equivalent to {minimumAt500Scale.ToString(CultureInfo.InvariantCulture)}-{maximumAt500Scale.ToString(CultureInfo.InvariantCulture)} pixels at 500 PPI, but received {height.ToString(CultureInfo.InvariantCulture)} pixels at {pixelsPerInch.ToString(CultureInfo.InvariantCulture)} PPI.",
            field: nameof(Nfiq2RawImageDescription.Height),
            metadata: new Dictionary<string, object?> { ["height"] = height, ["pixelsPerInch"] = pixelsPerInch, ["minimumAt500Scale"] = minimumAt500Scale, ["maximumAt500Scale"] = maximumAt500Scale });

    public static Nfiq2ValidationError FingerJetExtractionResolutionOutOfRange(int pixelsPerInch, int minimumDpi, int maximumDpi) =>
        Validation(
            Nfiq2ErrorCodes.FingerJetExtractionResolutionOutOfRange,
            $"The image resolution falls outside the native FingerJet extraction range. Provide a resolution between {minimumDpi.ToString(CultureInfo.InvariantCulture)} and {maximumDpi.ToString(CultureInfo.InvariantCulture)} DPI, but received {pixelsPerInch.ToString(CultureInfo.InvariantCulture)}.",
            field: nameof(Nfiq2RawImageDescription.PixelsPerInch),
            metadata: new Dictionary<string, object?> { ["pixelsPerInch"] = pixelsPerInch, ["minimumDpi"] = minimumDpi, ["maximumDpi"] = maximumDpi });

    public static Nfiq2ValidationError FingerJetExtractionWidthTooSmall(int widthAt500, int minimumWidthAt500) =>
        Validation(
            Nfiq2ErrorCodes.FingerJetExtractionWidthTooSmall,
            $"The image is too narrow for native FingerJet extraction at 500 PPI scale. Provide a width of at least {minimumWidthAt500.ToString(CultureInfo.InvariantCulture)} pixels at 500 PPI, but received {widthAt500.ToString(CultureInfo.InvariantCulture)}.",
            field: nameof(Nfiq2RawImageDescription.Width),
            metadata: new Dictionary<string, object?> { ["widthAt500Ppi"] = widthAt500, ["minimumWidthAt500Ppi"] = minimumWidthAt500 });

    public static Nfiq2ValidationError FingerJetExtractionWidthTooLarge(int widthAt500, int maximumWidthAt500) =>
        Validation(
            Nfiq2ErrorCodes.FingerJetExtractionWidthTooLarge,
            $"The image is too wide for native FingerJet extraction at 500 PPI scale. Provide a width of at most {maximumWidthAt500.ToString(CultureInfo.InvariantCulture)} pixels at 500 PPI, but received {widthAt500.ToString(CultureInfo.InvariantCulture)}.",
            field: nameof(Nfiq2RawImageDescription.Width),
            metadata: new Dictionary<string, object?> { ["widthAt500Ppi"] = widthAt500, ["maximumWidthAt500Ppi"] = maximumWidthAt500 });

    public static Nfiq2ValidationError FingerJetExtractionHeightTooSmall(int heightAt500, int minimumHeightAt500) =>
        Validation(
            Nfiq2ErrorCodes.FingerJetExtractionHeightTooSmall,
            $"The image is too short for native FingerJet extraction at 500 PPI scale. Provide a height of at least {minimumHeightAt500.ToString(CultureInfo.InvariantCulture)} pixels at 500 PPI, but received {heightAt500.ToString(CultureInfo.InvariantCulture)}.",
            field: nameof(Nfiq2RawImageDescription.Height),
            metadata: new Dictionary<string, object?> { ["heightAt500Ppi"] = heightAt500, ["minimumHeightAt500Ppi"] = minimumHeightAt500 });

    public static Nfiq2ValidationError FingerJetExtractionHeightTooLarge(int heightAt500, int maximumHeightAt500) =>
        Validation(
            Nfiq2ErrorCodes.FingerJetExtractionHeightTooLarge,
            $"The image is too tall for native FingerJet extraction at 500 PPI scale. Provide a height of at most {maximumHeightAt500.ToString(CultureInfo.InvariantCulture)} pixels at 500 PPI, but received {heightAt500.ToString(CultureInfo.InvariantCulture)}.",
            field: nameof(Nfiq2RawImageDescription.Height),
            metadata: new Dictionary<string, object?> { ["heightAt500Ppi"] = heightAt500, ["maximumHeightAt500Ppi"] = maximumHeightAt500 });

    public static Nfiq2ValidationError FingerJetInputPixelBufferTooSmall(int actualLength, int minimumLength) =>
        Validation(
            Nfiq2ErrorCodes.FingerJetInputPixelBufferTooSmall,
            $"The prepared image pixel buffer is smaller than the declared image size. Provide at least {minimumLength.ToString(CultureInfo.InvariantCulture)} bytes, but received {actualLength.ToString(CultureInfo.InvariantCulture)}.",
            field: "rawPixels",
            metadata: new Dictionary<string, object?> { ["actualLength"] = actualLength, ["minimumLength"] = minimumLength });

    public static Nfiq2ErrorInfo FingerJetCropPlanningDiverged(string axis) =>
        Error(
            Nfiq2ErrorCodes.FingerJetCropPlanningDiverged,
            $"FingerJet {axis} crop planning diverged from the expected native extractor behavior.",
            Nfiq2ErrorKind.Internal,
            isRetryable: false,
            metadata: new Dictionary<string, object?> { ["axis"] = axis });

    public static Nfiq2ErrorInfo FingerJetWorkingBufferExceeded(int requiredSize, int maximumSize) =>
        Error(
            Nfiq2ErrorCodes.FingerJetWorkingBufferExceeded,
            $"FingerJet working buffer requirements exceeded the supported native extractor limit. The operation required {requiredSize.ToString(CultureInfo.InvariantCulture)} bytes, but the limit is {maximumSize.ToString(CultureInfo.InvariantCulture)}.",
            Nfiq2ErrorKind.Internal,
            isRetryable: false,
            metadata: new Dictionary<string, object?> { ["requiredSize"] = requiredSize, ["maximumSize"] = maximumSize });

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Docs heading ids are lowercase slugs.")]
    public static Uri DocumentationUri(string code) => OpenNistDocumentation.ErrorCode(code);

    public static Nfiq2Exception ExceptionFrom(Nfiq2ErrorInfo error, Exception? innerException = null) =>
        Nfiq2Exception.From(error, innerException);

    public static Nfiq2ErrorInfo ErrorFromException(Nfiq2Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.ToErrorInfo();
    }

    private static Nfiq2ValidationError Validation(
        string code,
        string message,
        string? field = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        return new(code, message, field, DocumentationUri(code), metadata);
    }

    private static Nfiq2ErrorInfo Error(
        string code,
        string message,
        Nfiq2ErrorKind kind,
        bool isRetryable,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        return new(code, message, kind, isRetryable, DocumentationUri(code), metadata);
    }
}
