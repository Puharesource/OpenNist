namespace OpenNist.Nist.Errors;

using JetBrains.Annotations;

/// <summary>
/// Creates non-throwing NIST operation results.
/// </summary>
[PublicAPI]
public static class NistResults
{
    /// <summary>Creates a successful non-generic result.</summary>
    public static NistResult Success() => NistResult.Success();

    /// <summary>Creates a successful generic result.</summary>
    public static NistResult<T> Success<T>(T value) => new(true, value, null);

    /// <summary>Creates a failed non-generic result.</summary>
    public static NistResult Failure(NistErrorInfo error) => NistResult.Failure(error);

    /// <summary>Creates a failed generic result.</summary>
    public static NistResult<T> Failure<T>(NistErrorInfo error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, default, error);
    }
}
