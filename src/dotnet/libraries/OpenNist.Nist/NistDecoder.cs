namespace OpenNist.Nist;

using System.Buffers.Binary;
using System.Buffers.Text;
using System.Text;
using JetBrains.Annotations;

/// <summary>
/// Decodes ANSI/NIST-style fielded files into an object model.
/// </summary>
[PublicAPI]
public static class NistDecoder
{
    /// <summary>
    /// Decodes one NIST file from bytes.
    /// </summary>
    /// <param name="bytes">The encoded file bytes.</param>
    /// <returns>The decoded file model.</returns>
    public static NistFile Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Decode(bytes.AsSpan());
    }

    /// <summary>
    /// Decodes one NIST file from a stream.
    /// </summary>
    /// <param name="stream">The encoded file stream.</param>
    /// <returns>The decoded file model.</returns>
    public static NistFile Decode(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream is MemoryStream memoryStream && memoryStream.TryGetBuffer(out var buffer))
        {
            return Decode(
                buffer.AsSpan(
                    checked((int)memoryStream.Position),
                    checked((int)(memoryStream.Length - memoryStream.Position))));
        }

        if (stream.CanSeek)
        {
            var remainingLength = stream.Length - stream.Position;
            if (remainingLength is >= 0 and <= int.MaxValue)
            {
                var bytes = GC.AllocateUninitializedArray<byte>(checked((int)remainingLength));
                stream.ReadExactly(bytes);
                return Decode(bytes);
            }
        }

        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return Decode(copy.GetBuffer().AsSpan(0, checked((int)copy.Length)));
    }

    /// <summary>
    /// Decodes one NIST file from bytes.
    /// </summary>
    /// <param name="bytes">The encoded file bytes.</param>
    /// <returns>The decoded file model.</returns>
    public static NistFile Decode(ReadOnlySpan<byte> bytes)
    {
        var records = new List<NistRecord>();
        int[]? expectedRecordTypes = null;
        var expectedRecordTypeIndex = 0;
        var position = 0;

        while (position < bytes.Length)
        {
            var remainingBytes = bytes[position..];
            var record = records.Count == 0 || LooksLikeFieldedRecord(remainingBytes)
                ? ParseFieldedRecord(remainingBytes)
                : ParseBinaryRecord(remainingBytes, GetExpectedBinaryRecordType(expectedRecordTypes, expectedRecordTypeIndex));

            if (expectedRecordTypes is not null && expectedRecordTypeIndex < expectedRecordTypes.Length)
            {
                var expectedRecordType = expectedRecordTypes[expectedRecordTypeIndex];
                if (record.Type != expectedRecordType)
                {
                    throw new FormatException(
                        $"Logical record type {record.Type} did not match the transaction content entry {expectedRecordType}.");
                }

                expectedRecordTypeIndex++;
            }

            records.Add(record);
            position += record.IsOpaqueBinaryRecord
                ? record.EncodedBytes.Length
                : ReadRecordLength(remainingBytes);

            if (records.Count == 1)
            {
                expectedRecordTypes = ReadExpectedRecordTypes(record);
                expectedRecordTypeIndex = 0;
            }
        }

        return new(records);
    }

    private static NistRecord ParseFieldedRecord(ReadOnlySpan<byte> bytes)
    {
        var recordLength = ReadRecordLength(bytes);
        if (recordLength <= 0 || recordLength > bytes.Length)
        {
            throw new FormatException("Logical record length exceeds the remaining file size.");
        }

        var recordBytes = bytes[..recordLength];
        if (recordBytes[^1] != NistSeparators.s_fileSeparatorByte)
        {
            throw new FormatException("Logical record did not end with the expected file separator.");
        }

        return ParseRecord(recordBytes[..^1]);
    }

    private static NistRecord ParseBinaryRecord(ReadOnlySpan<byte> bytes, int expectedRecordType)
    {
        var recordLength = ReadBinaryRecordLength(bytes);
        if (recordLength <= 0 || recordLength > bytes.Length)
        {
            throw new FormatException("Logical record length exceeds the remaining file size.");
        }

        return new(expectedRecordType, bytes[..recordLength]);
    }

    private static int ReadRecordLength(ReadOnlySpan<byte> bytes)
    {
        var colonIndex = bytes.IndexOf((byte)':');
        if (colonIndex <= 0)
        {
            throw new FormatException("Logical record does not start with a valid LEN field.");
        }

        var tag = NistTag.Parse(bytes[..colonIndex]);
        if (tag.FieldNumber != 1)
        {
            throw new FormatException("Logical record does not start with a LEN field.");
        }

        var separatorIndex = bytes[(colonIndex + 1)..].IndexOfAny(NistSeparators.s_groupSeparatorByte, NistSeparators.s_fileSeparatorByte);
        if (separatorIndex < 0)
        {
            throw new FormatException("LEN field does not contain a terminating separator.");
        }

        var valueStart = colonIndex + 1;
        var valueLength = separatorIndex;
        if (!Utf8Parser.TryParse(bytes.Slice(valueStart, valueLength), out int recordLength, out var bytesConsumed) ||
            bytesConsumed != valueLength)
        {
            throw new FormatException("LEN field does not contain a valid integer.");
        }

        return recordLength;
    }

    private static int ReadBinaryRecordLength(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < sizeof(int))
        {
            throw new FormatException("Binary logical record length header was incomplete.");
        }

        return BinaryPrimitives.ReadInt32BigEndian(bytes);
    }

    private static NistRecord ParseRecord(ReadOnlySpan<byte> recordPayload)
    {
        var fields = new List<NistField>();
        var position = 0;
        int? recordType = null;

        while (position < recordPayload.Length)
        {
            var tagSeparatorIndex = recordPayload[position..].IndexOf((byte)':');
            if (tagSeparatorIndex < 0)
            {
                throw new FormatException("Field tag separator was not found.");
            }

            var colonIndex = position + tagSeparatorIndex;
            var tag = NistTag.Parse(recordPayload[position..colonIndex]);
            recordType ??= tag.RecordType;

            var valueStart = colonIndex + 1;
            var nextSeparatorIndex = FindNextFieldSeparator(recordPayload, valueStart);
            var valueEnd = nextSeparatorIndex >= 0 ? nextSeparatorIndex : recordPayload.Length;
            var value = valueStart == valueEnd
                ? string.Empty
                : Encoding.Latin1.GetString(recordPayload[valueStart..valueEnd]);
            fields.Add(new(tag, value));

            if (nextSeparatorIndex < 0)
            {
                break;
            }

            position = nextSeparatorIndex + 1;
        }

        if (recordType is null)
        {
            throw new FormatException("Logical record does not contain any fields.");
        }

        return new(recordType.Value, fields);
    }

    private static int FindNextFieldSeparator(ReadOnlySpan<byte> recordPayload, int searchStart)
    {
        for (var index = searchStart; index < recordPayload.Length; index++)
        {
            if (recordPayload[index] != NistSeparators.s_groupSeparatorByte)
            {
                continue;
            }

            if (IsTagStart(recordPayload[(index + 1)..]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsTagStart(ReadOnlySpan<byte> value)
    {
        if (value.Length < 6)
        {
            return false;
        }

        var index = 0;
        while (index < value.Length && char.IsAsciiDigit((char)value[index]))
        {
            index++;
        }

        if (index == 0 || index >= value.Length || value[index] != (byte)'.')
        {
            return false;
        }

        if (index + 4 >= value.Length)
        {
            return false;
        }

        return char.IsAsciiDigit((char)value[index + 1]) &&
               char.IsAsciiDigit((char)value[index + 2]) &&
               char.IsAsciiDigit((char)value[index + 3]) &&
               value[index + 4] == (byte)':';
    }

    private static bool LooksLikeFieldedRecord(ReadOnlySpan<byte> bytes)
    {
        return !bytes.IsEmpty && char.IsAsciiDigit((char)bytes[0]);
    }

    private static int[]? ReadExpectedRecordTypes(NistRecord type1Record)
    {
        if (type1Record.Type != 1)
        {
            return null;
        }

        var contentField = type1Record.FindField(3);
        if (contentField is null)
        {
            return null;
        }

        var source = contentField.Value.AsSpan();
        var subfieldCount = 1;
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] == NistSeparators.RecordSeparator)
            {
                subfieldCount++;
            }
        }

        if (subfieldCount <= 1)
        {
            return null;
        }

        var expectedRecordTypes = new int[subfieldCount - 1];
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

            if (subfieldIndex++ == 0)
            {
                continue;
            }

            var firstItemLength = subfieldSpan.IndexOf(NistSeparators.UnitSeparator);
            var firstItem = firstItemLength < 0 ? subfieldSpan : subfieldSpan[..firstItemLength];
            if (firstItem.IsEmpty)
            {
                throw new FormatException("Transaction content field contained an empty logical record descriptor.");
            }

            if (!int.TryParse(firstItem, out expectedRecordTypes[subfieldIndex - 2]))
            {
                throw new FormatException("Transaction content field contained an invalid logical record type.");
            }
        }

        return expectedRecordTypes;
    }

    private static int GetExpectedBinaryRecordType(int[]? expectedRecordTypes, int expectedRecordTypeIndex)
    {
        if (expectedRecordTypes is null || expectedRecordTypeIndex >= expectedRecordTypes.Length)
        {
            throw new FormatException("Binary logical record type could not be inferred from the transaction content field.");
        }

        return expectedRecordTypes[expectedRecordTypeIndex];
    }
}
