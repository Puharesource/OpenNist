namespace OpenNist.Nist;

using JetBrains.Annotations;

/// <summary>
/// Represents one logical record inside a NIST file.
/// </summary>
[PublicAPI]
public sealed class NistRecord
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NistRecord"/> class.
    /// </summary>
    /// <param name="type">The logical record type.</param>
    /// <param name="fields">The fields inside the logical record.</param>
    public NistRecord(int type, IEnumerable<NistField> fields)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(type);
        ArgumentNullException.ThrowIfNull(fields);

        Type = type;

        NistField[] copiedFields;
        if (fields is ICollection<NistField> collection)
        {
            copiedFields = new NistField[collection.Count];
            collection.CopyTo(copiedFields, 0);
        }
        else
        {
            copiedFields = [.. fields];
        }

        for (var index = 0; index < copiedFields.Length; index++)
        {
            if (copiedFields[index].Tag.RecordType == type)
            {
                continue;
            }

            throw new ArgumentException(
                $"Field '{copiedFields[index].Tag}' does not belong to logical record type {type}.",
                nameof(fields));
        }

        Fields = copiedFields;
        EncodedBytes = ReadOnlyMemory<byte>.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NistRecord"/> class from an opaque encoded record body.
    /// </summary>
    /// <param name="type">The logical record type.</param>
    /// <param name="encodedBytes">The exact encoded logical record bytes, including the record terminator.</param>
    public NistRecord(int type, ReadOnlySpan<byte> encodedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(type);

        Type = type;
        Fields = [];
        EncodedBytes = encodedBytes.ToArray();
    }

    /// <summary>
    /// Gets the logical record type number.
    /// </summary>
    public int Type { get; }

    /// <summary>
    /// Gets the fields inside the logical record.
    /// </summary>
    public IReadOnlyList<NistField> Fields { get; }

    /// <summary>
    /// Gets the exact encoded bytes for opaque binary records.
    /// </summary>
    public ReadOnlyMemory<byte> EncodedBytes { get; }

    /// <summary>
    /// Gets a value indicating whether the logical record is stored as opaque encoded bytes.
    /// </summary>
    public bool IsOpaqueBinaryRecord => !EncodedBytes.IsEmpty;

    /// <summary>
    /// Finds one field by field number.
    /// </summary>
    /// <param name="fieldNumber">The field number inside the record.</param>
    /// <returns>The matching field, or <see langword="null"/> when absent.</returns>
    public NistField? FindField(int fieldNumber)
    {
        for (var index = 0; index < Fields.Count; index++)
        {
            if (Fields[index].Tag.FieldNumber == fieldNumber)
            {
                return Fields[index];
            }
        }

        return null;
    }

    /// <summary>
    /// Finds one field by tag.
    /// </summary>
    /// <param name="tag">The tag to search for.</param>
    /// <returns>The matching field, or <see langword="null"/> when absent.</returns>
    public NistField? FindField(NistTag tag)
    {
        for (var index = 0; index < Fields.Count; index++)
        {
            if (Fields[index].Tag == tag)
            {
                return Fields[index];
            }
        }

        return null;
    }
}
