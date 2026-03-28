namespace OpenNist.Nfiq.Errors;

using JetBrains.Annotations;

/// <summary>
/// Classifies a structured NFIQ 2 failure.
/// </summary>
[PublicAPI]
public enum Nfiq2ErrorKind
{
    /// <summary>
    /// The operation failed validation.
    /// </summary>
    Validation,

    /// <summary>
    /// The operation failed because the requested resource was not found.
    /// </summary>
    NotFound,

    /// <summary>
    /// The operation failed because an external or environmental dependency timed out.
    /// </summary>
    Timeout,

    /// <summary>
    /// The operation failed due to a transient condition that may succeed on retry.
    /// </summary>
    Transient,

    /// <summary>
    /// The operation failed because the requested feature or input shape is not supported.
    /// </summary>
    Unsupported,

    /// <summary>
    /// The operation failed due to an unexpected internal condition.
    /// </summary>
    Internal
}
