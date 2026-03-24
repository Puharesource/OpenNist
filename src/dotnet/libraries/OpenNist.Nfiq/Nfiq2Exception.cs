namespace OpenNist.Nfiq;

using JetBrains.Annotations;

/// <summary>
/// Represents an NFIQ 2 integration failure.
/// </summary>
[PublicAPI]
public sealed class Nfiq2Exception : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Nfiq2Exception"/> class.
    /// </summary>
    public Nfiq2Exception()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Nfiq2Exception"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public Nfiq2Exception(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Nfiq2Exception"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public Nfiq2Exception(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
