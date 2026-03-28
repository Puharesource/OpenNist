namespace OpenNist.Primitives.Exceptions;

using JetBrains.Annotations;
using OpenNist.Primitives.Errors;

/// <summary>
/// Provides a shared structured exception base for OpenNist libraries.
/// </summary>
/// <typeparam name="TKind">The error-kind enum type.</typeparam>
/// <typeparam name="TValidationError">The validation error type.</typeparam>
[PublicAPI]
public abstract class OpenNistException<TKind, TValidationError> : Exception
    where TKind : struct
    where TValidationError : OpenNistValidationError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenNistException{TKind, TValidationError}"/> class from a structured error.
    /// </summary>
    /// <param name="defaultMessage">The fallback message when <paramref name="error"/> is null.</param>
    /// <param name="error">The structured error.</param>
    /// <param name="innerException">The inner exception.</param>
    protected OpenNistException(
        string defaultMessage,
        OpenNistErrorInfo<TKind, TValidationError>? error,
        Exception? innerException = null)
        : base(error?.Message ?? defaultMessage, innerException)
    {
        if (error is null)
        {
            ValidationErrors = [];
            return;
        }

        ErrorCode = error.Code;
        DocumentationUri = error.Documentation;
        ErrorKind = error.Kind;
        IsRetryable = error.IsRetryable;
        Metadata = error.Metadata;
        ValidationErrors = error.ValidationErrors ?? [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenNistException{TKind, TValidationError}"/> class.
    /// </summary>
    protected OpenNistException()
        : base("An OpenNist failure occurred.")
    {
        ValidationErrors = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenNistException{TKind, TValidationError}"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    protected OpenNistException(string message)
        : base(message)
    {
        ValidationErrors = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenNistException{TKind, TValidationError}"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    protected OpenNistException(string message, Exception innerException)
        : base(message, innerException)
    {
        ValidationErrors = [];
    }

    /// <summary>Gets the stable error code for the failure, when available.</summary>
    public string? ErrorCode { get; }

    /// <summary>Gets the documentation URI for the failure, when available.</summary>
    public Uri? DocumentationUri { get; }

    /// <summary>Gets the public error classification for the failure, when available.</summary>
    public TKind? ErrorKind { get; }

    /// <summary>Gets a value indicating whether the failure is retryable, when available.</summary>
    public bool? IsRetryable { get; }

    /// <summary>Gets additional machine-readable metadata for the failure, when available.</summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; }

    /// <summary>Gets the validation issues associated with the failure, when available.</summary>
    public IReadOnlyList<TValidationError> ValidationErrors { get; }
}
