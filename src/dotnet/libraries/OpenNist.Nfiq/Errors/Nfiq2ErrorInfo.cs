namespace OpenNist.Nfiq.Errors;

using JetBrains.Annotations;
using OpenNist.Primitives.Errors;

/// <summary>
/// Represents a structured OpenNist NFIQ 2 failure.
/// </summary>
/// <param name="Code">The stable machine-readable error code.</param>
/// <param name="Message">The human-readable error message.</param>
/// <param name="Kind">The error classification.</param>
/// <param name="IsRetryable">A value indicating whether the failure may succeed on retry.</param>
/// <param name="Documentation">The documentation URI for the error code.</param>
/// <param name="Metadata">Additional machine-readable metadata for the error.</param>
/// <param name="ValidationErrors">The nested validation errors, when the failure is validation-related.</param>
[PublicAPI]
public sealed record Nfiq2ErrorInfo(
    string Code,
    string Message,
    Nfiq2ErrorKind Kind,
    bool IsRetryable,
    Uri? Documentation = null,
    IReadOnlyDictionary<string, object?>? Metadata = null,
    IReadOnlyList<Nfiq2ValidationError>? ValidationErrors = null)
    : OpenNistErrorInfo<Nfiq2ErrorKind, Nfiq2ValidationError>(Code, Message, Kind, IsRetryable, Documentation, Metadata, ValidationErrors);
