namespace OpenNist.Nist.Errors;

using JetBrains.Annotations;

/// <summary>
/// Stable error codes exposed by the public OpenNist NIST surface.
/// </summary>
[PublicAPI]
public static class NistErrorCodes
{
    /// <summary>Unexpected library failure without a more specific public code.</summary>
    public const string UnexpectedFailure = "ONNIST9000";

    /// <summary>Malformed NIST transaction content.</summary>
    public const string MalformedFile = "ONNIST1000";

    /// <summary>Decoded record type did not match the type declared in CNT.</summary>
    public const string RecordTypeMismatch = "ONNIST1001";

    /// <summary>Logical record length exceeds the available bytes.</summary>
    public const string RecordLengthExceedsRemainingBytes = "ONNIST1002";

    /// <summary>Logical record did not end with the expected file separator.</summary>
    public const string MissingFileSeparator = "ONNIST1003";

    /// <summary>Logical record did not start with a LEN field.</summary>
    public const string MissingLenField = "ONNIST1004";

    /// <summary>LEN field did not contain a terminating separator.</summary>
    public const string LenFieldTerminatorMissing = "ONNIST1005";

    /// <summary>LEN field did not contain a valid integer.</summary>
    public const string LenFieldIntegerInvalid = "ONNIST1006";

    /// <summary>Binary logical record length header was incomplete.</summary>
    public const string BinaryLengthHeaderIncomplete = "ONNIST1007";

    /// <summary>Field tag separator was not found.</summary>
    public const string FieldTagSeparatorMissing = "ONNIST1008";

    /// <summary>Logical record did not contain any fields.</summary>
    public const string EmptyLogicalRecord = "ONNIST1009";

    /// <summary>Transaction content field contained an empty logical record descriptor.</summary>
    public const string InvalidCntDescriptor = "ONNIST1010";

    /// <summary>Transaction content field contained an invalid logical record type.</summary>
    public const string InvalidCntRecordType = "ONNIST1011";

    /// <summary>Binary logical record type could not be inferred from CNT.</summary>
    public const string BinaryRecordTypeInferenceFailed = "ONNIST1012";
}
