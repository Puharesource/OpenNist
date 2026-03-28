namespace OpenNist.Nist.Model;

using JetBrains.Annotations;

/// <summary>
/// Represents one field inside a logical record.
/// </summary>
[PublicAPI]
public sealed class NistField
{
    private NistSubfield[]? _subfields;

    /// <summary>
    /// Initializes a new instance of the <see cref="NistField"/> class from a raw field value.
    /// </summary>
    /// <param name="tag">The field tag.</param>
    /// <param name="value">The raw field value text.</param>
    public NistField(NistTag tag, string value)
    {
        Tag = tag;
        Value = value ?? string.Empty;
    }

    private NistField(NistTag tag, string value, NistSubfield[]? subfields)
    {
        Tag = tag;
        Value = value;
        _subfields = subfields;
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
    public IReadOnlyList<NistSubfield> Subfields => _subfields ??= ParseSubfields(Value);

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

        var normalizedSubfields = new NistSubfield[subfields.Length];

        for (var index = 0; index < subfields.Length; index++)
        {
            var sourceItems = subfields[index] ?? [];
            var items = new string[sourceItems.Length];

            for (var itemIndex = 0; itemIndex < sourceItems.Length; itemIndex++)
            {
                items[itemIndex] = sourceItems[itemIndex] ?? string.Empty;
            }

            normalizedSubfields[index] = new(items, takeOwnership: true);
        }

        return new(tag, rawValue, normalizedSubfields);
    }

    private static NistSubfield[] ParseSubfields(string value)
    {
        var source = value.AsSpan();
        if (source.IsEmpty)
        {
            return [new([string.Empty], takeOwnership: true)];
        }

        var subfieldCount = 1;
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] == NistSeparators.RecordSeparator)
            {
                subfieldCount++;
            }
        }

        var subfields = new NistSubfield[subfieldCount];
        var subfieldStart = 0;
        var subfieldIndex = 0;

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

            subfields[subfieldIndex++] = ParseSubfield(subfieldSpan);
        }

        return subfields;
    }

    private static NistSubfield ParseSubfield(ReadOnlySpan<char> value)
    {
        var itemCount = 1;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == NistSeparators.UnitSeparator)
            {
                itemCount++;
            }
        }

        var items = new string[itemCount];
        var itemStart = 0;
        var itemIndex = 0;

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

            items[itemIndex++] = itemSpan.IsEmpty ? string.Empty : itemSpan.ToString();
        }

        return new(items, takeOwnership: true);
    }
}
