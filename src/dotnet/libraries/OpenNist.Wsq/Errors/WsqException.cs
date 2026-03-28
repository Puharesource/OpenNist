namespace OpenNist.Wsq.Errors;

using JetBrains.Annotations;
using OpenNist.Primitives.Documentation;
using OpenNist.Primitives.Exceptions;

/// <summary>
/// Represents an OpenNist WSQ failure.
/// </summary>
[PublicAPI]
public sealed class WsqException : OpenNistException<WsqErrorKind, WsqValidationError>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WsqException"/> class.
    /// </summary>
    /// <param name="error">The structured error.</param>
    /// <param name="innerException">The inner exception.</param>
    public WsqException(WsqErrorInfo error, Exception? innerException = null)
        : base("A WSQ processing failure occurred.", error, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="WsqException"/> class.</summary>
    public WsqException()
        : this("A WSQ processing failure occurred.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="WsqException"/> class.</summary>
    public WsqException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="WsqException"/> class.</summary>
    public WsqException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates a new <see cref="WsqException"/> from a structured error.</summary>
    public static WsqException From(WsqErrorInfo error, Exception? innerException = null) => new(error, innerException);

    internal WsqErrorInfo ToErrorInfo()
    {
        if (ErrorCode is null || DocumentationUri is null || ErrorKind is null || IsRetryable is null)
        {
            return new(
                WsqErrorCodes.UnexpectedFailure,
                Message,
                WsqErrorKind.Internal,
                false,
                OpenNistDocumentation.ErrorCode(WsqErrorCodes.UnexpectedFailure));
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
