namespace OpenNist.Wsq.Errors;

using JetBrains.Annotations;

/// <summary>
/// Classifies public WSQ failures.
/// </summary>
[PublicAPI]
public enum WsqErrorKind
{
    /// <summary>Validation or supported-input failure.</summary>
    Validation,

    /// <summary>Malformed or unsupported WSQ bitstream content.</summary>
    Format,

    /// <summary>Unexpected internal library failure.</summary>
    Internal,
}
