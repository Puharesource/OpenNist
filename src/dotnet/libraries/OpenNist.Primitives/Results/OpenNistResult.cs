namespace OpenNist.Primitives.Results;

using JetBrains.Annotations;

/// <summary>
/// Represents the outcome of a non-throwing OpenNist operation.
/// </summary>
/// <typeparam name="TError">The structured error type.</typeparam>
[PublicAPI]
public class OpenNistResult<TError>
    where TError : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenNistResult{TError}"/> class.
    /// </summary>
    /// <param name="isSuccess">Whether the operation succeeded.</param>
    /// <param name="error">The structured failure.</param>
    protected OpenNistResult(bool isSuccess, TError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the structured failure for a failed operation.
    /// </summary>
    public TError? Error { get; }
}

/// <summary>
/// Represents the outcome of a non-throwing OpenNist operation that returns a value.
/// </summary>
/// <typeparam name="TValue">The successful result type.</typeparam>
/// <typeparam name="TError">The structured error type.</typeparam>
[PublicAPI]
public class OpenNistResult<TValue, TError> : OpenNistResult<TError>
    where TError : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenNistResult{TValue, TError}"/> class.
    /// </summary>
    /// <param name="isSuccess">Whether the operation succeeded.</param>
    /// <param name="value">The successful result value.</param>
    /// <param name="error">The structured failure.</param>
    protected internal OpenNistResult(bool isSuccess, TValue? value, TError? error)
        : base(isSuccess, error)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the successful result value.
    /// </summary>
    public TValue? Value { get; }
}
