namespace OpenNist.Nist.Errors;

using JetBrains.Annotations;
using OpenNist.Primitives.Results;

/// <summary>
/// Represents the outcome of a non-throwing NIST operation.
/// </summary>
[PublicAPI]
public class NistResult : OpenNistResult<NistErrorInfo>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NistResult"/> class.
    /// </summary>
    protected NistResult(bool isSuccess, NistErrorInfo? error)
        : base(isSuccess, error)
    {
    }

    /// <summary>Creates a successful result.</summary>
    public static NistResult Success() => new(true, null);

    /// <summary>Creates a failed result.</summary>
    public static NistResult Failure(NistErrorInfo error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, error);
    }
}

/// <summary>
/// Represents the outcome of a non-throwing NIST operation that returns a value.
/// </summary>
[PublicAPI]
public sealed class NistResult<T> : OpenNistResult<T, NistErrorInfo>
{
    internal NistResult(bool isSuccess, T? value, NistErrorInfo? error)
        : base(isSuccess, value, error)
    {
    }
}
