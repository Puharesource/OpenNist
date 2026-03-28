namespace OpenNist.Primitives.Errors;

using JetBrains.Annotations;

/// <summary>
/// Represents the outcome of validating an OpenNist request.
/// </summary>
/// <typeparam name="TValidationError">The validation error type.</typeparam>
[PublicAPI]
public record OpenNistValidationResult<TValidationError>
    where TValidationError : OpenNistValidationError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenNistValidationResult{TValidationError}"/> class.
    /// </summary>
    /// <param name="errors">The collected validation errors.</param>
    public OpenNistValidationResult(IReadOnlyList<TValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        Errors = errors;
    }

    /// <summary>
    /// Gets a value indicating whether validation succeeded.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets the collected validation errors.
    /// </summary>
    public IReadOnlyList<TValidationError> Errors { get; }
}
