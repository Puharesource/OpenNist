namespace OpenNist.Nist.Internal.Errors;

using System.Diagnostics.CodeAnalysis;
using OpenNist.Nist.Errors;
using OpenNist.Primitives.Documentation;

internal static class NistErrors
{
    public static NistErrorInfo UnexpectedFailure(string message) =>
        Error(NistErrorCodes.UnexpectedFailure, message, NistErrorKind.Internal, false);

    public static NistErrorInfo MalformedFile(string message) =>
        Error(NistErrorCodes.MalformedFile, message, NistErrorKind.Format, false);

    public static NistErrorInfo RecordTypeMismatch(int actualType, int expectedType) =>
        Error(
            NistErrorCodes.RecordTypeMismatch,
            $"Logical record type {actualType} did not match the transaction content entry {expectedType}.",
            NistErrorKind.Format,
            false,
            new Dictionary<string, object?> { ["actualType"] = actualType, ["expectedType"] = expectedType });

    public static NistErrorInfo RecordLengthExceedsRemainingBytes() =>
        Error(NistErrorCodes.RecordLengthExceedsRemainingBytes, "Logical record length exceeds the remaining file size.", NistErrorKind.Format, false);

    public static NistErrorInfo MissingFileSeparator() =>
        Error(NistErrorCodes.MissingFileSeparator, "Logical record did not end with the expected file separator.", NistErrorKind.Format, false);

    public static NistErrorInfo MissingLenField() =>
        Error(NistErrorCodes.MissingLenField, "Logical record does not start with a LEN field.", NistErrorKind.Format, false);

    public static NistErrorInfo LenFieldTerminatorMissing() =>
        Error(NistErrorCodes.LenFieldTerminatorMissing, "LEN field does not contain a terminating separator.", NistErrorKind.Format, false);

    public static NistErrorInfo LenFieldIntegerInvalid() =>
        Error(NistErrorCodes.LenFieldIntegerInvalid, "LEN field does not contain a valid integer.", NistErrorKind.Format, false);

    public static NistErrorInfo BinaryLengthHeaderIncomplete() =>
        Error(NistErrorCodes.BinaryLengthHeaderIncomplete, "Binary logical record length header was incomplete.", NistErrorKind.Format, false);

    public static NistErrorInfo FieldTagSeparatorMissing() =>
        Error(NistErrorCodes.FieldTagSeparatorMissing, "Field tag separator was not found.", NistErrorKind.Format, false);

    public static NistErrorInfo EmptyLogicalRecord() =>
        Error(NistErrorCodes.EmptyLogicalRecord, "Logical record does not contain any fields.", NistErrorKind.Format, false);

    public static NistErrorInfo InvalidCntDescriptor() =>
        Error(NistErrorCodes.InvalidCntDescriptor, "Transaction content field contained an empty logical record descriptor.", NistErrorKind.Format, false);

    public static NistErrorInfo InvalidCntRecordType() =>
        Error(NistErrorCodes.InvalidCntRecordType, "Transaction content field contained an invalid logical record type.", NistErrorKind.Format, false);

    public static NistErrorInfo BinaryRecordTypeInferenceFailed() =>
        Error(
            NistErrorCodes.BinaryRecordTypeInferenceFailed,
            "Binary logical record type could not be inferred from the transaction content field.",
            NistErrorKind.Format,
            false);

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Docs heading ids are lowercase slugs.")]
    public static Uri DocumentationUri(string code) => OpenNistDocumentation.ErrorCode(code);

    public static NistException ExceptionFrom(NistErrorInfo error, Exception? innerException = null) => NistException.From(error, innerException);

    public static NistErrorInfo ErrorFromException(NistException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.ToErrorInfo();
    }

    private static NistErrorInfo Error(
        string code,
        string message,
        NistErrorKind kind,
        bool isRetryable,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        return new(code, message, kind, isRetryable, DocumentationUri(code), metadata);
    }
}
