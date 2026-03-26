namespace OpenNist.Wsq.Internal;

internal static class WsqContainerReader
{
    public static async ValueTask<WsqContainer> ReadAsync(
        Stream wsqStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wsqStream);

        if (wsqStream is MemoryStream memoryStream && memoryStream.TryGetBuffer(out var bufferSegment))
        {
            var remainingLength = checked((int)(memoryStream.Length - memoryStream.Position));
            return Read(bufferSegment.AsSpan(checked((int)memoryStream.Position), remainingLength));
        }

        if (wsqStream.CanSeek)
        {
            var remainingLength = checked((int)(wsqStream.Length - wsqStream.Position));
            var exactBuffer = GC.AllocateUninitializedArray<byte>(remainingLength);
            await wsqStream.ReadExactlyAsync(exactBuffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            return Read(exactBuffer);
        }

        using var buffer = new MemoryStream();
        await wsqStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        return buffer.TryGetBuffer(out var bufferedData)
            ? Read(bufferedData.AsSpan(0, checked((int)buffer.Length)))
            : Read(buffer.ToArray());
    }

    public static WsqContainer Read(ReadOnlySpan<byte> wsqData)
    {
        var reader = new WsqBufferReader(wsqData);

        var startMarker = reader.ReadMarker();

        if (startMarker != WsqMarker.StartOfImage)
        {
            throw new InvalidDataException($"WSQ bitstream must start with SOI, but started with {startMarker}.");
        }

        WsqTransformTable? transformTable = null;
        WsqQuantizationTable? quantizationTable = null;
        var huffmanTables = new WsqHuffmanTable?[byte.MaxValue + 1];
        var huffmanTableCount = 0;
        var comments = new List<WsqCommentSegment>(capacity: 2);
        var blocks = new List<WsqBlock>(capacity: WsqConstants.BlockCount);
        int? pixelsPerInch = null;

        var marker = reader.ReadMarker();

        while (marker != WsqMarker.StartOfFrame)
        {
            if (!marker.IsTableOrCommentBeforeFrame())
            {
                throw new InvalidDataException(
                    $"Encountered unexpected WSQ marker {marker} before the frame header.");
            }

            ReadSegment(
                marker,
                ref reader,
                ref transformTable,
                ref quantizationTable,
                huffmanTables,
                ref huffmanTableCount,
                comments,
                ref pixelsPerInch);

            marker = reader.ReadMarker();
        }

        var frameHeader = ReadFrameHeader(ref reader);
        marker = reader.ReadMarker();

        while (marker != WsqMarker.EndOfImage)
        {
            if (marker == WsqMarker.StartOfBlock)
            {
                var block = ReadBlock(ref reader, huffmanTables, out var nextMarker);
                blocks.Add(block);
                marker = nextMarker;
                continue;
            }

            if (!marker.IsTableOrCommentBeforeBlock())
            {
                throw new InvalidDataException(
                    $"Encountered unsupported WSQ marker {marker} after the frame header.");
            }

            ReadSegment(
                marker,
                ref reader,
                ref transformTable,
                ref quantizationTable,
                huffmanTables,
                ref huffmanTableCount,
                comments,
                ref pixelsPerInch);

            marker = reader.ReadMarker();
        }

        return new(
            frameHeader,
            transformTable ?? throw new InvalidDataException("WSQ transform table is missing."),
            quantizationTable ?? throw new InvalidDataException("WSQ quantization table is missing."),
            GetSortedHuffmanTables(huffmanTables, huffmanTableCount),
            comments,
            blocks,
            pixelsPerInch);
    }

    private static void ReadSegment(
        WsqMarker marker,
        ref WsqBufferReader reader,
        ref WsqTransformTable? transformTable,
        ref WsqQuantizationTable? quantizationTable,
        WsqHuffmanTable?[] huffmanTables,
        ref int huffmanTableCount,
        List<WsqCommentSegment> comments,
        ref int? pixelsPerInch)
    {
        switch (marker)
        {
            case WsqMarker.DefineTransformTable:
                transformTable = ReadTransformTable(ref reader);
                break;
            case WsqMarker.DefineQuantizationTable:
                quantizationTable = ReadQuantizationTable(ref reader);
                break;
            case WsqMarker.DefineHuffmanTable:
                foreach (var table in ReadHuffmanTables(ref reader))
                {
                    if (huffmanTables[table.TableId] is null)
                    {
                        huffmanTableCount++;
                    }

                    huffmanTables[table.TableId] = table;
                }

                break;
            case WsqMarker.Comment:
                var comment = ReadComment(ref reader);
                comments.Add(comment);

                if (pixelsPerInch is null
                    && comment.Fields.TryGetValue("PPI", out var ppiValue)
                    && int.TryParse(ppiValue, out var parsedPpi))
                {
                    pixelsPerInch = parsedPpi;
                }

                break;
            default:
                throw new InvalidDataException($"Unsupported WSQ segment marker {marker}.");
        }
    }

    private static WsqTransformTable ReadTransformTable(ref WsqBufferReader reader)
    {
        var segmentReader = new WsqBufferReader(reader.ReadSegmentPayload());
        var highPassFilterLength = segmentReader.ReadByte();
        var lowPassFilterLength = segmentReader.ReadByte();
        var storedLowPassTail = ReadFilterTail(ref segmentReader, highPassFilterLength);
        var storedHighPassTail = ReadFilterTail(ref segmentReader, lowPassFilterLength);

        var highPassFilterCoefficients = ExpandStoredLowPassTailToHighPassFilter(
            storedLowPassTail,
            highPassFilterLength);
        var lowPassFilterCoefficients = ExpandStoredHighPassTailToLowPassFilter(
            storedHighPassTail,
            lowPassFilterLength);

        return new(
            highPassFilterLength,
            lowPassFilterLength,
            lowPassFilterCoefficients,
            highPassFilterCoefficients);
    }

    private static float[] ReadFilterTail(ref WsqBufferReader reader, int filterLength)
    {
        var coefficientCount = (filterLength + 1) / 2;
        var coefficients = new float[coefficientCount];

        for (var index = 0; index < coefficientCount; index++)
        {
            coefficients[index] = ReadScaledCoefficient(ref reader);
        }

        return coefficients;
    }

    private static WsqQuantizationTable ReadQuantizationTable(ref WsqBufferReader reader)
    {
        var segmentReader = new WsqBufferReader(reader.ReadSegmentPayload());
        var binCenterScale = segmentReader.ReadByte();
        var binCenterRaw = segmentReader.ReadUInt16BigEndian();
        var binCenter = WsqScaledValueCodec.ScaleUInt16ToDouble(binCenterRaw, binCenterScale);
        var serializedBinCenter = new WsqScaledUInt16(binCenterRaw, binCenterScale);
        var quantizationBins = new double[64];
        var zeroBins = new double[64];
        var serializedQuantizationBins = new WsqScaledUInt16[64];
        var serializedZeroBins = new WsqScaledUInt16[64];

        for (var index = 0; index < 64; index++)
        {
            var quantizationScale = segmentReader.ReadByte();
            var quantizationValue = segmentReader.ReadUInt16BigEndian();
            serializedQuantizationBins[index] = new(quantizationValue, quantizationScale);
            quantizationBins[index] = WsqScaledValueCodec.ScaleUInt16ToDouble(quantizationValue, quantizationScale);

            var zeroScale = segmentReader.ReadByte();
            var zeroValue = segmentReader.ReadUInt16BigEndian();
            serializedZeroBins[index] = new(zeroValue, zeroScale);
            zeroBins[index] = WsqScaledValueCodec.ScaleUInt16ToDouble(zeroValue, zeroScale);
        }

        return new(
            binCenter,
            serializedBinCenter,
            quantizationBins,
            zeroBins,
            serializedQuantizationBins,
            serializedZeroBins);
    }

    private static List<WsqHuffmanTable> ReadHuffmanTables(ref WsqBufferReader reader)
    {
        var segmentReader = new WsqBufferReader(reader.ReadSegmentPayload());
        var tables = new List<WsqHuffmanTable>(capacity: 2);

        while (segmentReader.Remaining > 0)
        {
            var tableId = segmentReader.ReadByte();
            var codeLengthCounts = segmentReader.ReadBytes(16).ToArray();
            var valueCount = 0;
            for (var index = 0; index < codeLengthCounts.Length; index++)
            {
                valueCount += codeLengthCounts[index];
            }

            var values = segmentReader.ReadBytes(valueCount).ToArray();

            tables.Add(new(tableId, codeLengthCounts, values));
        }

        return tables;
    }

    private static WsqCommentSegment ReadComment(ref WsqBufferReader reader)
    {
        return WsqCommentParser.ReadCommentSegment(ref reader);
    }

    private static WsqFrameHeader ReadFrameHeader(ref WsqBufferReader reader)
    {
        var segmentReader = new WsqBufferReader(reader.ReadSegmentPayload());
        var black = segmentReader.ReadByte();
        var white = segmentReader.ReadByte();
        var height = segmentReader.ReadUInt16BigEndian();
        var width = segmentReader.ReadUInt16BigEndian();
        var shiftScale = segmentReader.ReadByte();
        var shiftValue = segmentReader.ReadUInt16BigEndian();
        var scaleScale = segmentReader.ReadByte();
        var scaleValue = segmentReader.ReadUInt16BigEndian();
        var wsqEncoder = segmentReader.ReadByte();
        var softwareImplementationNumber = segmentReader.ReadUInt16BigEndian();

        return new(
            black,
            white,
            height,
            width,
            WsqScaledValueCodec.ScaleUInt16ToDouble(shiftValue, shiftScale),
            WsqScaledValueCodec.ScaleUInt16ToDouble(scaleValue, scaleScale),
            wsqEncoder,
            softwareImplementationNumber);
    }

    private static WsqBlock ReadBlock(
        ref WsqBufferReader reader,
        WsqHuffmanTable?[] huffmanTables,
        out WsqMarker nextMarker)
    {
        var segmentReader = new WsqBufferReader(reader.ReadSegmentPayload());
        var huffmanTableId = segmentReader.ReadByte();

        if (segmentReader.Remaining != 0)
        {
            throw new InvalidDataException("WSQ block header contains unexpected trailing data.");
        }

        var huffmanTable = huffmanTables[huffmanTableId];
        if (huffmanTable is null)
        {
            throw new InvalidDataException($"WSQ block references undefined Huffman table {huffmanTableId}.");
        }

        var encodedData = reader.ReadCompressedDataUntilNextMarker(out nextMarker).ToArray();
        return new(huffmanTableId, huffmanTable, encodedData);
    }

    private static float ReadScaledCoefficient(ref WsqBufferReader reader)
    {
        var sign = reader.ReadByte();
        var scale = reader.ReadByte();
        var value = WsqScaledValueCodec.ScaleUInt32ToSingle(reader.ReadUInt32BigEndian(), scale);
        return sign == 0 ? value : -value;
    }

    private static float[] ExpandStoredLowPassTailToHighPassFilter(
        float[] storedTail,
        int filterLength)
    {
        var coefficients = new float[filterLength];
        var pivot = storedTail.Length - 1;

        for (var index = 0; index < storedTail.Length; index++)
        {
            if ((filterLength & 1) == 1)
            {
                var rightIndex = index + pivot;
                coefficients[rightIndex] = IntSign(index) * storedTail[index];

                if (index > 0)
                {
                    coefficients[pivot - index] = coefficients[rightIndex];
                }
            }
            else
            {
                var rightIndex = index + pivot + 1;
                coefficients[rightIndex] = IntSign(index) * storedTail[index];
                coefficients[pivot - index] = -coefficients[rightIndex];
            }
        }

        return coefficients;
    }

    private static float[] ExpandStoredHighPassTailToLowPassFilter(
        float[] storedTail,
        int filterLength)
    {
        var coefficients = new float[filterLength];
        var pivot = storedTail.Length - 1;

        for (var index = 0; index < storedTail.Length; index++)
        {
            if ((filterLength & 1) == 1)
            {
                var rightIndex = index + pivot;
                coefficients[rightIndex] = IntSign(index) * storedTail[index];

                if (index > 0)
                {
                    coefficients[pivot - index] = coefficients[rightIndex];
                }
            }
            else
            {
                var rightIndex = index + pivot + 1;
                coefficients[rightIndex] = IntSign(index + 1) * storedTail[index];
                coefficients[pivot - index] = coefficients[rightIndex];
            }
        }

        return coefficients;
    }

    private static int IntSign(int power)
    {
        return (power & 1) == 0 ? 1 : -1;
    }

    private static WsqHuffmanTable[] GetSortedHuffmanTables(WsqHuffmanTable?[] huffmanTables, int huffmanTableCount)
    {
        var tables = new WsqHuffmanTable[huffmanTableCount];
        var index = 0;

        for (var tableId = 0; tableId < huffmanTables.Length; tableId++)
        {
            var table = huffmanTables[tableId];
            if (table is null)
            {
                continue;
            }

            tables[index++] = table;
        }

        return tables;
    }
}
