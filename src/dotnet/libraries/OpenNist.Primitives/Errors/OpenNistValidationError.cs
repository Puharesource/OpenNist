namespace OpenNist.Primitives.Errors;

using JetBrains.Annotations;

/// <summary>
/// Represents a single validation issue.
/// </summary>
/// <param name="Code">The stable validation code.</param>
/// <param name="Message">The human-readable validation message.</param>
/// <param name="Field">The associated logical field or parameter, when available.</param>
/// <param name="Documentation">The documentation URI for the validation code.</param>
/// <param name="Metadata">Additional machine-readable metadata for the error.</param>
[PublicAPI]
public record OpenNistValidationError(
    string Code,
    string Message,
    string? Field = null,
    Uri? Documentation = null,
    IReadOnlyDictionary<string, object?>? Metadata = null);
