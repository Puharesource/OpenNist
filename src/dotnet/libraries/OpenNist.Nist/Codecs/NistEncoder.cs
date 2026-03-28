namespace OpenNist.Nist.Codecs;

using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;
using JetBrains.Annotations;
using OpenNist.Nist.Model;

/// <summary>
/// Encodes a NIST file model into fielded bytes.
/// </summary>
[PublicAPI]
public static class NistEncoder
{
    /// <summary>
    /// Encodes one NIST file into a destination stream.
    /// </summary>
    /// <param name="output">The destination stream.</param>
    /// <param name="file">The file to encode.</param>
    public static void Encode(Stream output, NistFile file)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(file);

        foreach (var record in file.Records)
        {
            EncodeRecord(output, record);
        }
    }

    /// <summary>
    /// Encodes one NIST file into bytes.
    /// </summary>
    /// <param name="file">The file to encode.</param>
    /// <returns>The encoded bytes.</returns>
    public static byte[] Encode(NistFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        using var output = new MemoryStream();
        Encode(output, file);
        return output.ToArray();
    }

    private static void EncodeRecord(Stream output, NistRecord record)
    {
        if (record.IsOpaqueBinaryRecord)
        {
            output.Write(record.EncodedBytes.Span);
            return;
        }

        var lengthTag = new NistTag(record.Type, 1);
        var fields = CreateEncodedFieldSnapshot(record, lengthTag);

        using var recordOutput = new MemoryStream();
        var logicalRecordLength = "0";

        for (var iteration = 0; iteration < 8; iteration++)
        {
            recordOutput.SetLength(0);
            BuildRecordBytes(recordOutput, fields, lengthTag, logicalRecordLength);

            var nextLength = checked((int)recordOutput.Length).ToString(CultureInfo.InvariantCulture);
            if (nextLength == logicalRecordLength)
            {
                recordOutput.Position = 0;
                recordOutput.CopyTo(output);
                return;
            }

            logicalRecordLength = nextLength;
        }

        recordOutput.SetLength(0);
        BuildRecordBytes(recordOutput, fields, lengthTag, logicalRecordLength);
        recordOutput.Position = 0;
        recordOutput.CopyTo(output);
    }

    private static NistField[] CreateEncodedFieldSnapshot(NistRecord record, NistTag lengthTag)
    {
        if (record.Fields.Count > 0 && record.Fields[0].Tag == lengthTag)
        {
            return [.. record.Fields];
        }

        var fields = new NistField[record.Fields.Count + 1];
        fields[0] = new(lengthTag, string.Empty);

        for (var index = 0; index < record.Fields.Count; index++)
        {
            fields[index + 1] = record.Fields[index];
        }

        return fields;
    }

    private static void BuildRecordBytes(
        Stream output,
        NistField[] fields,
        NistTag lengthTag,
        string logicalRecordLength)
    {
        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            var value = field.Tag == lengthTag ? logicalRecordLength : field.Value;

            WriteTag(output, field.Tag);
            output.WriteByte((byte)':');
            WriteText(output, value.AsSpan());
            output.WriteByte(index == fields.Length - 1 ? NistSeparators.s_fileSeparatorByte : NistSeparators.s_groupSeparatorByte);
        }
    }

    private static void WriteTag(Stream output, NistTag tag)
    {
        Span<byte> buffer = stackalloc byte[16];
        if (!Utf8Formatter.TryFormat(tag.RecordType, buffer, out var bytesWritten))
        {
            throw new InvalidOperationException("Failed to format the record type.");
        }

        buffer[bytesWritten++] = (byte)'.';
        buffer[bytesWritten++] = checked((byte)('0' + tag.FieldNumber / 100));
        buffer[bytesWritten++] = checked((byte)('0' + tag.FieldNumber / 10 % 10));
        buffer[bytesWritten++] = checked((byte)('0' + tag.FieldNumber % 10));
        output.Write(buffer[..bytesWritten]);
    }

    private static void WriteText(Stream output, ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return;
        }

        const int stackLimit = 256;
        if (value.Length <= stackLimit)
        {
            Span<byte> stackBuffer = stackalloc byte[stackLimit];
            var bytesWritten = Encoding.Latin1.GetBytes(value, stackBuffer);
            output.Write(stackBuffer[..bytesWritten]);
            return;
        }

        var rented = ArrayPool<byte>.Shared.Rent(value.Length);

        try
        {
            var bytesWritten = Encoding.Latin1.GetBytes(value, rented);
            output.Write(rented, 0, bytesWritten);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
