namespace OpenNist.Nfiq.Errors;

using JetBrains.Annotations;
using OpenNist.Primitives.Errors;

/// <summary>
/// Represents the outcome of validating an NFIQ 2 request.
/// </summary>
[PublicAPI]
public sealed record Nfiq2ValidationResult : OpenNistValidationResult<Nfiq2ValidationError>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Nfiq2ValidationResult"/> class.
    /// </summary>
    /// <param name="errors">The collected validation errors.</param>
    public Nfiq2ValidationResult(IReadOnlyList<Nfiq2ValidationError> errors)
        : base(errors)
    {
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <returns>The successful validation result.</returns>
    public static Nfiq2ValidationResult Success() => new(Array.Empty<Nfiq2ValidationError>());

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <returns>The failed validation result.</returns>
    public static Nfiq2ValidationResult Failure(IReadOnlyList<Nfiq2ValidationError> errors) => new(errors);
}
