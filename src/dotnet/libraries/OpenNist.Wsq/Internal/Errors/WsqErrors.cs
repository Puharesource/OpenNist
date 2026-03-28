namespace OpenNist.Wsq.Internal.Errors;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using OpenNist.Primitives.Documentation;
using OpenNist.Primitives.Errors;
using OpenNist.Wsq.Errors;
using OpenNist.Wsq.Model;

internal static class WsqErrors
{
    public static WsqErrorInfo UnexpectedFailure(string message) =>
        Error(WsqErrorCodes.UnexpectedFailure, message, WsqErrorKind.Internal, false);

    public static WsqErrorInfo ValidationFailed(IReadOnlyList<WsqValidationError> validationErrors)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);

        return new(
            Code: WsqErrorCodes.ValidationFailed,
            Message: OpenNistValidationMessages.BuildFailureMessage(
                "WSQ",
                validationErrors,
                includeIssueCount: true,
                emptyFallback: "WSQ validation failed."),
            Kind: WsqErrorKind.Validation,
            IsRetryable: false,
            Documentation: DocumentationUri(WsqErrorCodes.ValidationFailed),
            Metadata: new Dictionary<string, object?> { ["errorCount"] = validationErrors.Count },
            ValidationErrors: validationErrors);
    }

    public static WsqValidationError RawImageWidthMustBePositive(int width) =>
        Validation(
            WsqErrorCodes.RawImageWidthMustBePositive,
            $"Image width must be positive. Provide a width greater than zero, but received {width.ToString(CultureInfo.InvariantCulture)}.",
            field: nameof(WsqRawImageDescription.Width),
            metadata: new Dictionary<string, object?> { ["width"] = width });

    public static WsqValidationError RawImageHeightMustBePositive(int height) =>
        Validation(
            WsqErrorCodes.RawImageHeightMustBePositive,
            $"Image height must be positive. Provide a height greater than zero, but received {height.ToString(CultureInfo.InvariantCulture)}.",
            field: nameof(WsqRawImageDescription.Height),
            metadata: new Dictionary<string, object?> { ["height"] = height });

    public static WsqValidationError RawImageBitsPerPixelUnsupported(int bitsPerPixel) =>
        Validation(
            WsqErrorCodes.RawImageBitsPerPixelUnsupported,
            $"WSQ encoding currently supports 8-bit grayscale input only. Provide 8 bits per pixel, but received {bitsPerPixel.ToString(CultureInfo.InvariantCulture)}.",
            field: nameof(WsqRawImageDescription.BitsPerPixel),
            metadata: new Dictionary<string, object?> { ["bitsPerPixel"] = bitsPerPixel, ["supportedBitsPerPixel"] = 8 });

    public static WsqValidationError BitRateMustBePositive(double bitRate) =>
        Validation(
            WsqErrorCodes.BitRateMustBePositive,
            $"WSQ bit rate must be greater than zero. Provide a positive bit rate, but received {bitRate.ToString(CultureInfo.InvariantCulture)}.",
            field: nameof(WsqEncodeOptions.BitRate),
            metadata: new Dictionary<string, object?> { ["bitRate"] = bitRate });

    public static WsqValidationError EncoderNumberOutOfRange(int encoderNumber) =>
        Validation(
            WsqErrorCodes.EncoderNumberOutOfRange,
            $"WSQ encoder number must fit in a byte. Provide a value between {byte.MinValue.ToString(CultureInfo.InvariantCulture)} and {byte.MaxValue.ToString(CultureInfo.InvariantCulture)}, but received {encoderNumber.ToString(CultureInfo.InvariantCulture)}.",
            field: nameof(WsqEncodeOptions.EncoderNumber),
            metadata: new Dictionary<string, object?> { ["encoderNumber"] = encoderNumber, ["minimum"] = byte.MinValue, ["maximum"] = byte.MaxValue });

    public static WsqValidationError SoftwareImplementationNumberOutOfRange(int softwareImplementationNumber) =>
        Validation(
            WsqErrorCodes.SoftwareImplementationNumberOutOfRange,
            $"WSQ software implementation number must fit in an unsigned 16-bit value. Provide a value between {ushort.MinValue.ToString(CultureInfo.InvariantCulture)} and {ushort.MaxValue.ToString(CultureInfo.InvariantCulture)}, but received {softwareImplementationNumber.ToString(CultureInfo.InvariantCulture)}.",
            field: nameof(WsqEncodeOptions.SoftwareImplementationNumber),
            metadata: new Dictionary<string, object?> { ["softwareImplementationNumber"] = softwareImplementationNumber, ["minimum"] = ushort.MinValue, ["maximum"] = ushort.MaxValue });

    public static WsqValidationError RawImageByteCountMismatch(int actualLength, int expectedLength) =>
        Validation(
            WsqErrorCodes.RawImageByteCountMismatch,
            $"The supplied raw pixel stream length ({actualLength.ToString(CultureInfo.InvariantCulture)}) does not match the declared image area ({expectedLength.ToString(CultureInfo.InvariantCulture)} bytes). Provide exactly width × height raw bytes.",
            field: "rawImageStream",
            metadata: new Dictionary<string, object?> { ["actualLength"] = actualLength, ["expectedLength"] = expectedLength });

    public static WsqErrorInfo MalformedBitstream(string message) =>
        Error(WsqErrorCodes.MalformedBitstream, message, WsqErrorKind.Format, false);

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Docs heading ids are lowercase slugs.")]
    public static Uri DocumentationUri(string code) => OpenNistDocumentation.ErrorCode(code);

    public static WsqException ExceptionFrom(WsqErrorInfo error, Exception? innerException = null) => WsqException.From(error, innerException);

    public static WsqErrorInfo ErrorFromException(WsqException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.ToErrorInfo();
    }

    private static WsqValidationError Validation(
        string code,
        string message,
        string? field = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        return new(code, message, field, DocumentationUri(code), metadata);
    }

    private static WsqErrorInfo Error(
        string code,
        string message,
        WsqErrorKind kind,
        bool isRetryable,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        return new(code, message, kind, isRetryable, DocumentationUri(code), metadata);
    }
}
