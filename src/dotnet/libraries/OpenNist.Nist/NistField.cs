namespace OpenNist.Nist;

using JetBrains.Annotations;

/// <summary>
/// Represents one field inside a logical record.
/// </summary>
[PublicAPI]
public sealed class NistField
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NistField"/> class from a raw field value.
    /// </summary>
    /// <param name="tag">The field tag.</param>
    /// <param name="value">The raw field value text.</param>
    public NistField(NistTag tag, string value)
    {
        Tag = tag;
        Value = value ?? string.Empty;
        Subfields = ParseSubfields(Value);
    }

    /// <summary>
    /// Gets the field tag.
    /// </summary>
    public NistTag Tag { get; }

    /// <summary>
    /// Gets the raw field value text.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the parsed subfield and item values.
    /// </summary>
    public IReadOnlyList<NistSubfield> Subfields { get; }

    /// <summary>
    /// Creates a field from one or more subfield item collections.
    /// </summary>
    /// <param name="tag">The field tag.</param>
    /// <param name="subfields">The subfield item collections.</param>
    /// <returns>The created field.</returns>
    public static NistField Create(NistTag tag, params string[][] subfields)
    {
        ArgumentNullException.ThrowIfNull(subfields);

        var rawValue = string.Join(
            NistSeparators.RecordSeparator,
            subfields.Select(static subfield => string.Join(NistSeparators.UnitSeparator, subfield ?? [])));

        return new NistField(tag, rawValue);
    }

    private static NistSubfield[] ParseSubfields(string value)
    {
        var source = value.AsSpan();
        if (source.IsEmpty)
        {
            return [new NistSubfield([string.Empty])];
        }

        var subfields = new List<NistSubfield>();
        var subfieldStart = 0;

        while (subfieldStart <= source.Length)
        {
            var subfieldLength = source[subfieldStart..].IndexOf(NistSeparators.RecordSeparator);
            ReadOnlySpan<char> subfieldSpan;

            if (subfieldLength < 0)
            {
                subfieldSpan = source[subfieldStart..];
                subfieldStart = source.Length + 1;
            }
            else
            {
                subfieldSpan = source.Slice(subfieldStart, subfieldLength);
                subfieldStart += subfieldLength + 1;
            }

            subfields.Add(ParseSubfield(subfieldSpan));
        }

        return [.. subfields];
    }

    private static NistSubfield ParseSubfield(ReadOnlySpan<char> value)
    {
        var items = new List<string>();
        var itemStart = 0;

        while (itemStart <= value.Length)
        {
            var itemLength = value[itemStart..].IndexOf(NistSeparators.UnitSeparator);
            ReadOnlySpan<char> itemSpan;

            if (itemLength < 0)
            {
                itemSpan = value[itemStart..];
                itemStart = value.Length + 1;
            }
            else
            {
                itemSpan = value.Slice(itemStart, itemLength);
                itemStart += itemLength + 1;
            }

            items.Add(itemSpan.IsEmpty ? string.Empty : itemSpan.ToString());
        }

        return new NistSubfield(items);
    }
}
