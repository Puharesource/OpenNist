namespace OpenNist.Nfiq.Errors;

using JetBrains.Annotations;
using OpenNist.Primitives.Results;

/// <summary>
/// Represents the outcome of a non-throwing NFIQ 2 operation.
/// </summary>
[PublicAPI]
public class Nfiq2Result : OpenNistResult<Nfiq2ErrorInfo>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Nfiq2Result"/> class.
    /// </summary>
    /// <param name="isSuccess">Whether the operation succeeded.</param>
    /// <param name="error">The structured failure.</param>
    protected Nfiq2Result(bool isSuccess, Nfiq2ErrorInfo? error)
        : base(isSuccess, error)
    {
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>The successful result.</returns>
    public static Nfiq2Result Success() => new(true, null);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="error">The failure.</param>
    /// <returns>The failed result.</returns>
    public static Nfiq2Result Failure(Nfiq2ErrorInfo error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, error);
    }
}

/// <summary>
/// Represents the outcome of a non-throwing NFIQ 2 operation that returns a value.
/// </summary>
/// <typeparam name="T">The successful result type.</typeparam>
[PublicAPI]
public sealed class Nfiq2Result<T> : OpenNistResult<T, Nfiq2ErrorInfo>
{
    internal Nfiq2Result(bool isSuccess, T? value, Nfiq2ErrorInfo? error)
        : base(isSuccess, value, error)
    {
    }
}
