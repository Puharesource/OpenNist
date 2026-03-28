namespace OpenNist.Wsq.Errors;

using JetBrains.Annotations;
using OpenNist.Primitives.Errors;

/// <summary>
/// Represents a single WSQ validation issue.
/// </summary>
/// <param name="Code">The stable validation code.</param>
/// <param name="Message">The validation message.</param>
/// <param name="Field">The logical field associated with the error, when available.</param>
/// <param name="Documentation">The documentation URI for the validation code.</param>
/// <param name="Metadata">Additional machine-readable metadata for the error.</param>
[PublicAPI]
public sealed record WsqValidationError(
    string Code,
    string Message,
    string? Field = null,
    Uri? Documentation = null,
    IReadOnlyDictionary<string, object?>? Metadata = null)
    : OpenNistValidationError(Code, Message, Field, Documentation, Metadata);
