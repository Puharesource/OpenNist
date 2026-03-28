namespace OpenNist.Nfiq.Errors;

using JetBrains.Annotations;
using OpenNist.Primitives.Documentation;
using OpenNist.Primitives.Exceptions;

/// <summary>
/// Represents an NFIQ 2 integration failure.
/// </summary>
[PublicAPI]
public sealed class Nfiq2Exception : OpenNistException<Nfiq2ErrorKind, Nfiq2ValidationError>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Nfiq2Exception"/> class.
    /// </summary>
    /// <param name="error">The structured error.</param>
    /// <param name="innerException">The inner exception.</param>
    public Nfiq2Exception(Nfiq2ErrorInfo error, Exception? innerException = null)
        : base("An NFIQ 2 integration failure occurred.", error, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Nfiq2Exception"/> class.
    /// </summary>
    public Nfiq2Exception()
        : this("An NFIQ 2 integration failure occurred.")
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

    /// <summary>
    /// Creates a new <see cref="Nfiq2Exception"/> from a structured error.
    /// </summary>
    /// <param name="error">The structured error.</param>
    /// <param name="innerException">The inner exception.</param>
    /// <returns>The exception.</returns>
    public static Nfiq2Exception From(Nfiq2ErrorInfo error, Exception? innerException = null) => new(error, innerException);

    internal Nfiq2ErrorInfo ToErrorInfo()
    {
        if (ErrorCode is null || DocumentationUri is null || ErrorKind is null || IsRetryable is null)
        {
            return new(
                Code: Nfiq2ErrorCodes.UnexpectedFailure,
                Message: Message,
                Kind: Nfiq2ErrorKind.Internal,
                IsRetryable: false,
                Documentation: OpenNistDocumentation.ErrorCode(Nfiq2ErrorCodes.UnexpectedFailure));
        }

        return new(
            ErrorCode,
            Message,
            ErrorKind.Value,
            IsRetryable.Value,
            DocumentationUri,
            Metadata,
            ValidationErrors.Count == 0 ? null : ValidationErrors);
    }
}
