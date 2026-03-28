namespace OpenNist.Wsq.Errors;

using JetBrains.Annotations;
using OpenNist.Primitives.Errors;

/// <summary>
/// Represents a structured OpenNist WSQ failure.
/// </summary>
/// <param name="Code">The stable machine-readable error code.</param>
/// <param name="Message">The human-readable error message.</param>
/// <param name="Kind">The error classification.</param>
/// <param name="IsRetryable">A value indicating whether the failure may succeed on retry.</param>
/// <param name="Documentation">The documentation URI for the error code.</param>
/// <param name="Metadata">Additional machine-readable metadata for the error.</param>
/// <param name="ValidationErrors">The nested validation errors, when the failure is validation-related.</param>
[PublicAPI]
public sealed record WsqErrorInfo(
    string Code,
    string Message,
    WsqErrorKind Kind,
    bool IsRetryable,
    Uri? Documentation = null,
    IReadOnlyDictionary<string, object?>? Metadata = null,
    IReadOnlyList<WsqValidationError>? ValidationErrors = null)
    : OpenNistErrorInfo<WsqErrorKind, WsqValidationError>(Code, Message, Kind, IsRetryable, Documentation, Metadata, ValidationErrors);
