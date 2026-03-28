namespace OpenNist.Nist.Errors;

using JetBrains.Annotations;

/// <summary>
/// Classifies public NIST failures.
/// </summary>
[PublicAPI]
public enum NistErrorKind
{
    /// <summary>Malformed or unsupported transaction content.</summary>
    Format,

    /// <summary>Unexpected internal library failure.</summary>
    Internal
}
