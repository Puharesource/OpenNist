namespace OpenNist.Wsq.Internal;

using System.Buffers;
using System.Buffers.Binary;

internal static class WsqContainerWriter
{
    public static async ValueTask WriteAsync(
        Stream wsqStream,
        WsqContainer container,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wsqStream);
        ArgumentNullException.ThrowIfNull(container);

        var buffer = new ArrayBufferWriter<byte>();
        WriteMarker(buffer, WsqMarker.StartOfImage);

        foreach (var comment in container.Comments)
        {
            WriteComment(buffer, comment);
        }

        WriteTransformTable(buffer, container.TransformTable);
        WriteQuantizationTable(buffer, container.QuantizationTable);
        WriteFrameHeader(buffer, container.FrameHeader);

        var writtenTableIds = new HashSet<byte>();
        foreach (var block in container.Blocks)
        {
            if (writtenTableIds.Add(block.HuffmanTableId))
            {
                WriteHuffmanTable(buffer, block.HuffmanTable);
            }

            WriteBlock(buffer, block);
        }

        WriteMarker(buffer, WsqMarker.EndOfImage);
        await wsqStream.WriteAsync(buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
    }

    private static void WriteTransformTable(IBufferWriter<byte> writer, WsqTransformTable transformTable)
    {
        var payload = new ArrayBufferWriter<byte>();
        payload.WriteByte(transformTable.LowPassFilterLength);
        payload.WriteByte(transformTable.HighPassFilterLength);

        var storedLowPassTail = CollapseLowPassFilterToStoredHighPassTail(
            GetValueSpan(transformTable.LowPassFilterCoefficients),
            transformTable.LowPassFilterLength);
        var storedHighPassTail = CollapseHighPassFilterToStoredLowPassTail(
            GetValueSpan(transformTable.HighPassFilterCoefficients),
            transformTable.HighPassFilterLength);

        foreach (var coefficient in storedLowPassTail)
        {
            WriteScaledCoefficient(payload, coefficient);
        }

        foreach (var coefficient in storedHighPassTail)
        {
            WriteScaledCoefficient(payload, coefficient);
        }

        WriteSegment(writer, WsqMarker.DefineTransformTable, payload.WrittenSpan);
    }

    private static void WriteQuantizationTable(IBufferWriter<byte> writer, WsqQuantizationTable quantizationTable)
    {
        var payload = new ArrayBufferWriter<byte>();
        payload.WriteByte(quantizationTable.SerializedBinCenter.Scale);
        payload.WriteUInt16BigEndian(quantizationTable.SerializedBinCenter.RawValue);

        for (var subbandIndex = 0; subbandIndex < quantizationTable.QuantizationBins.Count; subbandIndex++)
        {
            WriteScaledUInt16(payload, quantizationTable.SerializedQuantizationBins[subbandIndex]);
            WriteScaledUInt16(payload, quantizationTable.SerializedZeroBins[subbandIndex]);
        }

        WriteSegment(writer, WsqMarker.DefineQuantizationTable, payload.WrittenSpan);
    }

    private static void WriteComment(IBufferWriter<byte> writer, WsqCommentSegment comment)
    {
        var payload = System.Text.Encoding.ASCII.GetBytes(comment.Text);
        WriteSegment(writer, WsqMarker.Comment, payload);
    }

    private static void WriteFrameHeader(IBufferWriter<byte> writer, WsqFrameHeader frameHeader)
    {
        var payload = new ArrayBufferWriter<byte>();
        payload.WriteByte(frameHeader.Black);
        payload.WriteByte(frameHeader.White);
        payload.WriteUInt16BigEndian(frameHeader.Height);
        payload.WriteUInt16BigEndian(frameHeader.Width);
        WriteScaledUInt16(payload, frameHeader.Shift);
        WriteScaledUInt16(payload, frameHeader.Scale);
        payload.WriteByte(frameHeader.WsqEncoder);
        payload.WriteUInt16BigEndian(frameHeader.SoftwareImplementationNumber);
        WriteSegment(writer, WsqMarker.StartOfFrame, payload.WrittenSpan);
    }

    private static void WriteHuffmanTable(IBufferWriter<byte> writer, WsqHuffmanTable huffmanTable)
    {
        var payload = new ArrayBufferWriter<byte>();
        payload.WriteByte(huffmanTable.TableId);
        payload.WriteBytes(GetValueSpan(huffmanTable.CodeLengthCounts));
        payload.WriteBytes(GetValueSpan(huffmanTable.Values));
        WriteSegment(writer, WsqMarker.DefineHuffmanTable, payload.WrittenSpan);
    }

    private static void WriteBlock(IBufferWriter<byte> writer, WsqBlock block)
    {
        Span<byte> headerPayload = stackalloc byte[1];
        headerPayload[0] = block.HuffmanTableId;
        WriteSegment(writer, WsqMarker.StartOfBlock, headerPayload);
        writer.WriteBytes(block.EncodedData);
    }

    private static void WriteSegment(IBufferWriter<byte> writer, WsqMarker marker, ReadOnlySpan<byte> payload)
    {
        WriteMarker(writer, marker);
        writer.WriteUInt16BigEndian(checked((ushort)(payload.Length + sizeof(ushort))));
        writer.WriteBytes(payload);
    }

    private static void WriteMarker(IBufferWriter<byte> writer, WsqMarker marker)
    {
        writer.WriteUInt16BigEndian((ushort)marker);
    }

    private static void WriteScaledCoefficient(IBufferWriter<byte> writer, float value)
    {
        if (value < 0.0f)
        {
            writer.WriteByte(1);
            value = -value;
        }
        else
        {
            writer.WriteByte(0);
        }

        var scaledValue = WsqScaledValueCodec.ScaleToUInt32(value);
        writer.WriteByte(scaledValue.Scale);
        writer.WriteUInt32BigEndian(scaledValue.RawValue);
    }

    private static void WriteScaledUInt16(IBufferWriter<byte> writer, WsqScaledUInt16 value)
    {
        writer.WriteByte(value.Scale);
        writer.WriteUInt16BigEndian(value.RawValue);
    }

    private static void WriteScaledUInt16(IBufferWriter<byte> writer, double value)
    {
        WriteScaledUInt16(writer, WsqScaledValueCodec.ScaleToUInt16(value));
    }

    private static float[] CollapseHighPassFilterToStoredLowPassTail(
        ReadOnlySpan<float> highPassFilter,
        int filterLength)
    {
        var storedTail = new float[(filterLength + 1) / 2];
        var pivot = storedTail.Length - 1;

        for (var index = 0; index < storedTail.Length; index++)
        {
            var rightIndex = (filterLength & 1) == 1
                ? index + pivot
                : index + pivot + 1;
            storedTail[index] = highPassFilter[rightIndex];
        }

        return storedTail;
    }

    private static float[] CollapseLowPassFilterToStoredHighPassTail(
        ReadOnlySpan<float> lowPassFilter,
        int filterLength)
    {
        var storedTail = new float[(filterLength + 1) / 2];
        var pivot = storedTail.Length - 1;

        for (var index = 0; index < storedTail.Length; index++)
        {
            var rightIndex = (filterLength & 1) == 1
                ? index + pivot
                : index + pivot + 1;
            storedTail[index] = lowPassFilter[rightIndex];
        }

        return storedTail;
    }

    private static ReadOnlySpan<byte> GetValueSpan(IReadOnlyList<byte> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return values as byte[] ?? [.. values];
    }

    private static ReadOnlySpan<float> GetValueSpan(IReadOnlyList<float> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return values as float[] ?? [.. values];
    }
}

internal static class WsqBufferWriterExtensions
{
    public static void WriteByte(this IBufferWriter<byte> writer, byte value)
    {
        var destination = writer.GetSpan(1);
        destination[0] = value;
        writer.Advance(1);
    }

    public static void WriteBytes(this IBufferWriter<byte> writer, ReadOnlySpan<byte> values)
    {
        var destination = writer.GetSpan(values.Length);
        values.CopyTo(destination);
        writer.Advance(values.Length);
    }

    public static void WriteUInt16BigEndian(this IBufferWriter<byte> writer, ushort value)
    {
        var destination = writer.GetSpan(sizeof(ushort));
        BinaryPrimitives.WriteUInt16BigEndian(destination, value);
        writer.Advance(sizeof(ushort));
    }

    public static void WriteUInt32BigEndian(this IBufferWriter<byte> writer, uint value)
    {
        var destination = writer.GetSpan(sizeof(uint));
        BinaryPrimitives.WriteUInt32BigEndian(destination, value);
        writer.Advance(sizeof(uint));
    }
}
