namespace OpenNist.Nist;

using System.Buffers.Text;
using System.Globalization;
using JetBrains.Annotations;

/// <summary>
/// Identifies one field inside a logical record.
/// </summary>
[PublicAPI]
public readonly record struct NistTag
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NistTag"/> struct.
    /// </summary>
    /// <param name="recordType">The logical record type number.</param>
    /// <param name="fieldNumber">The field number inside the logical record.</param>
    public NistTag(int recordType, int fieldNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(recordType);

        if (fieldNumber is < 0 or > 999)
        {
            throw new ArgumentOutOfRangeException(nameof(fieldNumber), "Field number must be between 0 and 999.");
        }

        RecordType = recordType;
        FieldNumber = fieldNumber;
    }

    /// <summary>
    /// Gets the logical record type number.
    /// </summary>
    public int RecordType { get; }

    /// <summary>
    /// Gets the field number inside the logical record.
    /// </summary>
    public int FieldNumber { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{RecordType}.{FieldNumber:000}");
    }

    /// <summary>
    /// Parses a textual tag representation such as <c>1.003</c>.
    /// </summary>
    /// <param name="value">The textual tag value.</param>
    /// <returns>The parsed tag.</returns>
    public static NistTag Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Parse(value.AsSpan());
    }

    /// <summary>
    /// Parses a Latin-1 encoded textual tag representation such as <c>1.003</c>.
    /// </summary>
    /// <param name="value">The encoded tag bytes.</param>
    /// <returns>The parsed tag.</returns>
    public static NistTag Parse(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            throw new FormatException("Tag value must not be empty.");
        }

        var separatorIndex = value.IndexOf((byte)'.');
        if (separatorIndex <= 0 || separatorIndex >= value.Length - 1)
        {
            throw new FormatException($"'{value.ToString()}' is not a valid NIST tag.");
        }

        var recordTypeSpan = value[..separatorIndex];
        var fieldNumberSpan = value[(separatorIndex + 1)..];

        if (fieldNumberSpan.Length != 3)
        {
            throw new FormatException($"'{value.ToString()}' is not a valid NIST tag.");
        }

        if (!Utf8Parser.TryParse(recordTypeSpan, out int recordType, out var recordBytesConsumed) ||
            recordBytesConsumed != recordTypeSpan.Length ||
            !Utf8Parser.TryParse(fieldNumberSpan, out int fieldNumber, out var fieldBytesConsumed) ||
            fieldBytesConsumed != fieldNumberSpan.Length)
        {
            throw new FormatException($"'{value.ToString()}' is not a valid NIST tag.");
        }

        return new NistTag(recordType, fieldNumber);
    }

    /// <summary>
    /// Parses a textual tag representation such as <c>1.003</c>.
    /// </summary>
    /// <param name="value">The textual tag value.</param>
    /// <returns>The parsed tag.</returns>
    public static NistTag Parse(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            throw new FormatException("Tag value must not be empty.");
        }

        var separatorIndex = value.IndexOf('.');
        if (separatorIndex <= 0 || separatorIndex >= value.Length - 1)
        {
            throw new FormatException($"'{value.ToString()}' is not a valid NIST tag.");
        }

        var recordTypeSpan = value[..separatorIndex];
        var fieldNumberSpan = value[(separatorIndex + 1)..];

        if (fieldNumberSpan.Length != 3)
        {
            throw new FormatException($"'{value.ToString()}' is not a valid NIST tag.");
        }

        if (!int.TryParse(recordTypeSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var recordType) ||
            !int.TryParse(fieldNumberSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var fieldNumber))
        {
            throw new FormatException($"'{value.ToString()}' is not a valid NIST tag.");
        }

        return new NistTag(recordType, fieldNumber);
    }
}
