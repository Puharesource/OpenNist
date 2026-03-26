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

        var buffer = new ArrayBufferWriter<byte>(GetEncodedSize(container));
        WriteMarker(buffer, WsqMarker.StartOfImage);

        foreach (var comment in container.Comments)
        {
            WriteComment(buffer, comment);
        }

        WriteTransformTable(buffer, container.TransformTable);
        WriteQuantizationTable(buffer, container.QuantizationTable);
        WriteFrameHeader(buffer, container.FrameHeader);

        Span<bool> writtenTableIds = stackalloc bool[byte.MaxValue + 1];
        foreach (var block in container.Blocks)
        {
            if (!writtenTableIds[block.HuffmanTableId])
            {
                writtenTableIds[block.HuffmanTableId] = true;
                WriteHuffmanTable(buffer, block.HuffmanTable);
            }

            WriteBlock(buffer, block);
        }

        WriteMarker(buffer, WsqMarker.EndOfImage);
        await wsqStream.WriteAsync(buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
    }

    private static void WriteTransformTable(IBufferWriter<byte> writer, WsqTransformTable transformTable)
    {
        var lowPassFilter = GetValueSpan(transformTable.LowPassFilterCoefficients);
        var highPassFilter = GetValueSpan(transformTable.HighPassFilterCoefficients);
        var lowPassTailLength = (transformTable.LowPassFilterLength + 1) / 2;
        var highPassTailLength = (transformTable.HighPassFilterLength + 1) / 2;
        WriteMarker(writer, WsqMarker.DefineTransformTable);
        writer.WriteUInt16BigEndian(checked((ushort)(2 + lowPassTailLength * 6 + highPassTailLength * 6 + sizeof(ushort))));
        writer.WriteByte(transformTable.LowPassFilterLength);
        writer.WriteByte(transformTable.HighPassFilterLength);

        WriteCollapsedLowPassFilterTail(writer, lowPassFilter, transformTable.LowPassFilterLength);
        WriteCollapsedHighPassFilterTail(writer, highPassFilter, transformTable.HighPassFilterLength);
    }

    private static void WriteQuantizationTable(IBufferWriter<byte> writer, WsqQuantizationTable quantizationTable)
    {
        WriteMarker(writer, WsqMarker.DefineQuantizationTable);
        writer.WriteUInt16BigEndian(checked((ushort)(1 + sizeof(ushort) + 64 * 2 * 3 + sizeof(ushort))));
        writer.WriteByte(quantizationTable.SerializedBinCenter.Scale);
        writer.WriteUInt16BigEndian(quantizationTable.SerializedBinCenter.RawValue);

        for (var subbandIndex = 0; subbandIndex < quantizationTable.QuantizationBins.Count; subbandIndex++)
        {
            WriteScaledUInt16(writer, quantizationTable.SerializedQuantizationBins[subbandIndex]);
            WriteScaledUInt16(writer, quantizationTable.SerializedZeroBins[subbandIndex]);
        }
    }

    private static void WriteComment(IBufferWriter<byte> writer, WsqCommentSegment comment)
    {
        WriteMarker(writer, WsqMarker.Comment);
        writer.WriteUInt16BigEndian(checked((ushort)(System.Text.Encoding.ASCII.GetByteCount(comment.Text) + sizeof(ushort))));
        writer.WriteAscii(comment.Text);
    }

    private static void WriteFrameHeader(IBufferWriter<byte> writer, WsqFrameHeader frameHeader)
    {
        WriteMarker(writer, WsqMarker.StartOfFrame);
        writer.WriteUInt16BigEndian(checked((ushort)(15 + sizeof(ushort))));
        writer.WriteByte(frameHeader.Black);
        writer.WriteByte(frameHeader.White);
        writer.WriteUInt16BigEndian(frameHeader.Height);
        writer.WriteUInt16BigEndian(frameHeader.Width);
        WriteScaledUInt16(writer, frameHeader.Shift);
        WriteScaledUInt16(writer, frameHeader.Scale);
        writer.WriteByte(frameHeader.WsqEncoder);
        writer.WriteUInt16BigEndian(frameHeader.SoftwareImplementationNumber);
    }

    private static void WriteHuffmanTable(IBufferWriter<byte> writer, WsqHuffmanTable huffmanTable)
    {
        var codeLengthCounts = GetValueSpan(huffmanTable.CodeLengthCounts);
        var values = GetValueSpan(huffmanTable.Values);
        WriteMarker(writer, WsqMarker.DefineHuffmanTable);
        writer.WriteUInt16BigEndian(checked((ushort)(1 + codeLengthCounts.Length + values.Length + sizeof(ushort))));
        writer.WriteByte(huffmanTable.TableId);
        writer.WriteBytes(codeLengthCounts);
        writer.WriteBytes(values);
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

    private static int GetEncodedSize(WsqContainer container)
    {
        var totalSize = sizeof(ushort) + sizeof(ushort);

        foreach (var comment in container.Comments)
        {
            totalSize += sizeof(ushort) + sizeof(ushort) + System.Text.Encoding.ASCII.GetByteCount(comment.Text);
        }

        totalSize += sizeof(ushort) + sizeof(ushort) + GetTransformPayloadSize(container.TransformTable);
        totalSize += sizeof(ushort) + sizeof(ushort) + GetQuantizationPayloadSize();
        totalSize += sizeof(ushort) + sizeof(ushort) + 15;

        Span<bool> writtenTableIds = stackalloc bool[byte.MaxValue + 1];
        foreach (var block in container.Blocks)
        {
            if (!writtenTableIds[block.HuffmanTableId])
            {
                writtenTableIds[block.HuffmanTableId] = true;
                totalSize += sizeof(ushort) + sizeof(ushort) + GetHuffmanPayloadSize(block.HuffmanTable);
            }

            totalSize += sizeof(ushort) + sizeof(ushort) + 1 + block.EncodedData.Length;
        }

        return totalSize;
    }

    private static int GetTransformPayloadSize(WsqTransformTable transformTable)
    {
        return 2
            + ((transformTable.LowPassFilterLength + 1) / 2) * 6
            + ((transformTable.HighPassFilterLength + 1) / 2) * 6;
    }

    private static int GetQuantizationPayloadSize()
    {
        return 1 + sizeof(ushort) + 64 * 2 * 3;
    }

    private static int GetHuffmanPayloadSize(WsqHuffmanTable huffmanTable)
    {
        return 1 + huffmanTable.CodeLengthCounts.Count + huffmanTable.Values.Count;
    }

    private static void WriteCollapsedLowPassFilterTail(
        IBufferWriter<byte> writer,
        ReadOnlySpan<float> lowPassFilter,
        int filterLength)
    {
        var tailLength = (filterLength + 1) / 2;
        var pivot = tailLength - 1;

        for (var index = 0; index < tailLength; index++)
        {
            var rightIndex = (filterLength & 1) == 1
                ? index + pivot
                : index + pivot + 1;
            WriteScaledCoefficient(writer, lowPassFilter[rightIndex]);
        }
    }

    private static void WriteCollapsedHighPassFilterTail(
        IBufferWriter<byte> writer,
        ReadOnlySpan<float> highPassFilter,
        int filterLength)
    {
        var tailLength = (filterLength + 1) / 2;
        var pivot = tailLength - 1;

        for (var index = 0; index < tailLength; index++)
        {
            var rightIndex = (filterLength & 1) == 1
                ? index + pivot
                : index + pivot + 1;
            WriteScaledCoefficient(writer, highPassFilter[rightIndex]);
        }
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

    public static void WriteAscii(this IBufferWriter<byte> writer, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var byteCount = System.Text.Encoding.ASCII.GetByteCount(value);
        var destination = writer.GetSpan(byteCount);
        var bytesWritten = System.Text.Encoding.ASCII.GetBytes(value.AsSpan(), destination);
        writer.Advance(bytesWritten);
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
