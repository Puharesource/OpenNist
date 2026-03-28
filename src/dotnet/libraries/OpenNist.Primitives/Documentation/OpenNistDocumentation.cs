namespace OpenNist.Primitives.Documentation;

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

/// <summary>
/// Provides shared documentation-link helpers for OpenNist libraries.
/// </summary>
[PublicAPI]
public static class OpenNistDocumentation
{
    [SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded", Justification = "OpenNist public docs host is an intentional stable contract.")]
    private static readonly Uri s_errorCodesBaseUri = new("https://opennist.tarkan.dev/docs/error-codes#");

    /// <summary>
    /// Creates a documentation URI that points to a lowercase error-code anchor.
    /// </summary>
    /// <param name="baseUri">The base documentation URI ending with an anchor prefix.</param>
    /// <param name="code">The stable error code.</param>
    /// <returns>The documentation URI.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Docs heading ids are lowercase slugs.")]
    public static Uri LowercaseCodeAnchor(Uri baseUri, string code)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return new($"{baseUri}{code.ToLowerInvariant()}");
    }

    /// <summary>
    /// Creates a documentation URI for an OpenNist public error code.
    /// </summary>
    /// <param name="code">The stable error code.</param>
    /// <returns>The documentation URI.</returns>
    public static Uri ErrorCode(string code) => LowercaseCodeAnchor(s_errorCodesBaseUri, code);
}
