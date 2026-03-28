namespace OpenNist.Primitives.Errors;

using JetBrains.Annotations;

/// <summary>
/// Represents a structured OpenNist failure.
/// </summary>
/// <typeparam name="TKind">The error-kind enum type.</typeparam>
/// <typeparam name="TValidationError">The validation error type.</typeparam>
/// <param name="Code">The stable machine-readable error code.</param>
/// <param name="Message">The human-readable error message.</param>
/// <param name="Kind">The error classification.</param>
/// <param name="IsRetryable">A value indicating whether the failure may succeed on retry.</param>
/// <param name="Documentation">The documentation URI for the error code.</param>
/// <param name="Metadata">Additional machine-readable metadata for the error.</param>
/// <param name="ValidationErrors">Nested validation errors, when the failure is validation-related.</param>
[PublicAPI]
public record OpenNistErrorInfo<TKind, TValidationError>(
    string Code,
    string Message,
    TKind Kind,
    bool IsRetryable,
    Uri? Documentation = null,
    IReadOnlyDictionary<string, object?>? Metadata = null,
    IReadOnlyList<TValidationError>? ValidationErrors = null)
    where TKind : struct
    where TValidationError : OpenNistValidationError;
