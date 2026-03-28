namespace OpenNist.Wsq.Errors;

using JetBrains.Annotations;
using OpenNist.Primitives.Results;

/// <summary>
/// Represents the outcome of a non-throwing WSQ operation.
/// </summary>
[PublicAPI]
public class WsqResult : OpenNistResult<WsqErrorInfo>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WsqResult"/> class.
    /// </summary>
    protected WsqResult(bool isSuccess, WsqErrorInfo? error)
        : base(isSuccess, error)
    {
    }

    /// <summary>Creates a successful result.</summary>
    public static WsqResult Success() => new(true, null);

    /// <summary>Creates a failed result.</summary>
    public static WsqResult Failure(WsqErrorInfo error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, error);
    }
}

/// <summary>
/// Represents the outcome of a non-throwing WSQ operation that returns a value.
/// </summary>
[PublicAPI]
public sealed class WsqResult<T> : OpenNistResult<T, WsqErrorInfo>
{
    internal WsqResult(bool isSuccess, T? value, WsqErrorInfo? error)
        : base(isSuccess, value, error)
    {
    }
}
