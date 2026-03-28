namespace OpenNist.Nist.Model;

using JetBrains.Annotations;

/// <summary>
/// Represents one subfield value and its contained item values.
/// </summary>
[PublicAPI]
public sealed class NistSubfield
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NistSubfield"/> class.
    /// </summary>
    /// <param name="items">The item values inside the subfield.</param>
    public NistSubfield(IEnumerable<string> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = items.Select(static item => item ?? string.Empty).ToArray();
    }

    internal NistSubfield(string[] items, bool takeOwnership)
    {
        Items = takeOwnership ? items : [.. items];
    }

    /// <summary>
    /// Gets the item values contained inside the subfield.
    /// </summary>
    public IReadOnlyList<string> Items { get; }
}
