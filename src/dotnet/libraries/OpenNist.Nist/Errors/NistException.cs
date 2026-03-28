namespace OpenNist.Nist.Errors;

using JetBrains.Annotations;
using OpenNist.Primitives.Documentation;
using OpenNist.Primitives.Errors;
using OpenNist.Primitives.Exceptions;

/// <summary>
/// Represents an OpenNist NIST failure.
/// </summary>
[PublicAPI]
public sealed class NistException : OpenNistException<NistErrorKind, OpenNistValidationError>
{
    /// <summary>Initializes a new instance of the <see cref="NistException"/> class.</summary>
    public NistException(NistErrorInfo error, Exception? innerException = null)
        : base("A NIST processing failure occurred.", error, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NistException"/> class.</summary>
    public NistException()
        : this("A NIST processing failure occurred.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NistException"/> class.</summary>
    public NistException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NistException"/> class.</summary>
    public NistException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates a new <see cref="NistException"/> from a structured error.</summary>
    public static NistException From(NistErrorInfo error, Exception? innerException = null) => new(error, innerException);

    internal NistErrorInfo ToErrorInfo()
    {
        if (ErrorCode is null || DocumentationUri is null || ErrorKind is null || IsRetryable is null)
        {
            return new(
                NistErrorCodes.UnexpectedFailure,
                Message,
                NistErrorKind.Internal,
                false,
                OpenNistDocumentation.ErrorCode(NistErrorCodes.UnexpectedFailure));
        }

        return new(ErrorCode, Message, ErrorKind.Value, IsRetryable.Value, DocumentationUri, Metadata);
    }
}
