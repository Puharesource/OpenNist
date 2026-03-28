namespace OpenNist.Nfiq.Errors;

using JetBrains.Annotations;
using OpenNist.Primitives.Errors;

/// <summary>
/// Describes a single validation problem encountered by the NFIQ 2 integration surface.
/// </summary>
/// <param name="Code">The stable machine-readable error code.</param>
/// <param name="Message">The human-readable validation message.</param>
/// <param name="Field">The field or parameter associated with the validation issue, if known.</param>
/// <param name="Documentation">The documentation URI for the error code.</param>
/// <param name="Metadata">Additional machine-readable metadata for the error.</param>
[PublicAPI]
public sealed record Nfiq2ValidationError(
    string Code,
    string Message,
    string? Field = null,
    Uri? Documentation = null,
    IReadOnlyDictionary<string, object?>? Metadata = null)
    : OpenNistValidationError(Code, Message, Field, Documentation, Metadata);
