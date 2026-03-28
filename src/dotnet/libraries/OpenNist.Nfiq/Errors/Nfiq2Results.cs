namespace OpenNist.Nfiq.Errors;

using JetBrains.Annotations;

/// <summary>
/// Creates non-throwing NFIQ 2 operation results.
/// </summary>
[PublicAPI]
public static class Nfiq2Results
{
    /// <summary>
    /// Creates a successful non-generic result.
    /// </summary>
    /// <returns>The successful result.</returns>
    public static Nfiq2Result Success() => Nfiq2Result.Success();

    /// <summary>
    /// Creates a successful generic result.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="value">The successful value.</param>
    /// <returns>The successful result.</returns>
    public static Nfiq2Result<T> Success<T>(T value) => new(true, value, null);

    /// <summary>
    /// Creates a failed non-generic result.
    /// </summary>
    /// <param name="error">The structured failure.</param>
    /// <returns>The failed result.</returns>
    public static Nfiq2Result Failure(Nfiq2ErrorInfo error) => Nfiq2Result.Failure(error);

    /// <summary>
    /// Creates a failed generic result.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="error">The structured failure.</param>
    /// <returns>The failed result.</returns>
    public static Nfiq2Result<T> Failure<T>(Nfiq2ErrorInfo error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, default, error);
    }
}
