namespace OpenNist.Wsq.Errors;

using JetBrains.Annotations;
using OpenNist.Primitives.Errors;

/// <summary>
/// Represents the outcome of validating a WSQ request.
/// </summary>
[PublicAPI]
public sealed record WsqValidationResult : OpenNistValidationResult<WsqValidationError>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WsqValidationResult"/> class.
    /// </summary>
    /// <param name="errors">The collected validation errors.</param>
    public WsqValidationResult(IReadOnlyList<WsqValidationError> errors)
        : base(errors)
    {
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <returns>The successful validation result.</returns>
    public static WsqValidationResult Success() => new(Array.Empty<WsqValidationError>());

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <returns>The failed validation result.</returns>
    public static WsqValidationResult Failure(IReadOnlyList<WsqValidationError> errors) => new(errors);
}
