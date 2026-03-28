namespace OpenNist.Nist.Codecs;

using System.Buffers.Binary;
using System.Buffers.Text;
using System.Text;
using JetBrains.Annotations;
using OpenNist.Nist.Errors;
using OpenNist.Nist.Internal.Errors;
using OpenNist.Nist.Model;

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
        var result = TryDecode(bytes.AsSpan());
        return result.IsSuccess ? result.Value! : throw NistErrors.ExceptionFrom(result.Error!);
    }

    /// <summary>
    /// Decodes one NIST file from a stream.
    /// </summary>
    /// <param name="stream">The encoded file stream.</param>
    /// <returns>The decoded file model.</returns>
    public static NistFile Decode(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var result = TryDecode(stream);
        return result.IsSuccess ? result.Value! : throw NistErrors.ExceptionFrom(result.Error!);
    }

    /// <summary>
    /// Decodes one NIST file from bytes.
    /// </summary>
    /// <param name="bytes">The encoded file bytes.</param>
    /// <returns>The decoded file model.</returns>
    public static NistFile Decode(ReadOnlySpan<byte> bytes)
    {
        var result = TryDecode(bytes);
        return result.IsSuccess ? result.Value! : throw NistErrors.ExceptionFrom(result.Error!);
    }

    /// <summary>
    /// Tries to decode one NIST file from bytes.
    /// </summary>
    /// <param name="bytes">The encoded file bytes.</param>
    /// <returns>A non-throwing structured result.</returns>
    public static NistResult<NistFile> TryDecode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return TryDecode(bytes.AsSpan());
    }

    /// <summary>
    /// Tries to decode one NIST file from a stream.
    /// </summary>
    /// <param name="stream">The encoded file stream.</param>
    /// <returns>A non-throwing structured result.</returns>
    public static NistResult<NistFile> TryDecode(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream is MemoryStream memoryStream && memoryStream.TryGetBuffer(out var buffer))
        {
            return TryDecode(
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
                return TryDecode(bytes);
            }
        }

        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return TryDecode(copy.GetBuffer().AsSpan(0, checked((int)copy.Length)));
    }

    /// <summary>
    /// Tries to decode one NIST file from bytes.
    /// </summary>
    /// <param name="bytes">The encoded file bytes.</param>
    /// <returns>A non-throwing structured result.</returns>
    public static NistResult<NistFile> TryDecode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            return NistResults.Success(ParseFile(bytes));
        }
        catch (NistException exception)
        {
            return NistResults.Failure<NistFile>(NistErrors.ErrorFromException(exception));
        }
        catch (FormatException exception)
        {
            return NistResults.Failure<NistFile>(NistErrors.MalformedFile(exception.Message));
        }
    }

    private static NistFile ParseFile(ReadOnlySpan<byte> bytes)
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
                    throw NistErrors.ExceptionFrom(NistErrors.RecordTypeMismatch(record.Type, expectedRecordType));
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
            throw NistErrors.ExceptionFrom(NistErrors.RecordLengthExceedsRemainingBytes());
        }

        var recordBytes = bytes[..recordLength];
        if (recordBytes[^1] != NistSeparators.s_fileSeparatorByte)
        {
            throw NistErrors.ExceptionFrom(NistErrors.MissingFileSeparator());
        }

        return ParseRecord(recordBytes[..^1]);
    }

    private static NistRecord ParseBinaryRecord(ReadOnlySpan<byte> bytes, int expectedRecordType)
    {
        var recordLength = ReadBinaryRecordLength(bytes);
        if (recordLength <= 0 || recordLength > bytes.Length)
        {
            throw NistErrors.ExceptionFrom(NistErrors.RecordLengthExceedsRemainingBytes());
        }

        return new(expectedRecordType, bytes[..recordLength]);
    }

    private static int ReadRecordLength(ReadOnlySpan<byte> bytes)
    {
        var colonIndex = bytes.IndexOf((byte)':');
        if (colonIndex <= 0)
        {
            throw NistErrors.ExceptionFrom(NistErrors.MalformedFile("Logical record does not start with a valid LEN field."));
        }

        var tag = NistTag.Parse(bytes[..colonIndex]);
        if (tag.FieldNumber != 1)
        {
            throw NistErrors.ExceptionFrom(NistErrors.MissingLenField());
        }

        var separatorIndex = bytes[(colonIndex + 1)..].IndexOfAny(NistSeparators.s_groupSeparatorByte, NistSeparators.s_fileSeparatorByte);
        if (separatorIndex < 0)
        {
            throw NistErrors.ExceptionFrom(NistErrors.LenFieldTerminatorMissing());
        }

        var valueStart = colonIndex + 1;
        var valueLength = separatorIndex;
        if (!Utf8Parser.TryParse(bytes.Slice(valueStart, valueLength), out int recordLength, out var bytesConsumed) ||
            bytesConsumed != valueLength)
        {
            throw NistErrors.ExceptionFrom(NistErrors.LenFieldIntegerInvalid());
        }

        return recordLength;
    }

    private static int ReadBinaryRecordLength(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < sizeof(int))
        {
            throw NistErrors.ExceptionFrom(NistErrors.BinaryLengthHeaderIncomplete());
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
                throw NistErrors.ExceptionFrom(NistErrors.FieldTagSeparatorMissing());
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
            throw NistErrors.ExceptionFrom(NistErrors.EmptyLogicalRecord());
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
                throw NistErrors.ExceptionFrom(NistErrors.InvalidCntDescriptor());
            }

            if (!int.TryParse(firstItem, out expectedRecordTypes[subfieldIndex - 2]))
            {
                throw NistErrors.ExceptionFrom(NistErrors.InvalidCntRecordType());
            }
        }

        return expectedRecordTypes;
    }

    private static int GetExpectedBinaryRecordType(int[]? expectedRecordTypes, int expectedRecordTypeIndex)
    {
        if (expectedRecordTypes is null || expectedRecordTypeIndex >= expectedRecordTypes.Length)
        {
            throw NistErrors.ExceptionFrom(NistErrors.BinaryRecordTypeInferenceFailed());
        }

        return expectedRecordTypes[expectedRecordTypeIndex];
    }
}
