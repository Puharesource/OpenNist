namespace OpenNist.Wsq.Errors;

using JetBrains.Annotations;

/// <summary>
/// Creates non-throwing WSQ operation results.
/// </summary>
[PublicAPI]
public static class WsqResults
{
    /// <summary>Creates a successful non-generic result.</summary>
    public static WsqResult Success() => WsqResult.Success();

    /// <summary>Creates a successful generic result.</summary>
    public static WsqResult<T> Success<T>(T value) => new(true, value, null);

    /// <summary>Creates a failed non-generic result.</summary>
    public static WsqResult Failure(WsqErrorInfo error) => WsqResult.Failure(error);

    /// <summary>Creates a failed generic result.</summary>
    public static WsqResult<T> Failure<T>(WsqErrorInfo error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, default, error);
    }
}
