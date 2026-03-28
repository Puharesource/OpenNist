namespace OpenNist.Wsq.Internal.Container;

using OpenNist.Wsq.Internal.IO;
using OpenNist.Wsq.Internal.Metadata;
using OpenNist.Wsq.Internal.Scaling;
using OpenNist.Wsq.Model;

internal static class WsqFileInfoReader
{
    public static async ValueTask<WsqFileInfo> ReadAsync(
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

        return await ReadStreamingAsync(wsqStream, cancellationToken).ConfigureAwait(false);
    }

    public static WsqFileInfo Read(ReadOnlySpan<byte> wsqData)
    {
        var reader = new WsqBufferReader(wsqData);

        var startMarker = reader.ReadMarker();
        if (startMarker != WsqMarker.StartOfImage)
        {
            throw new InvalidDataException($"WSQ bitstream must start with SOI, but started with {startMarker}.");
        }

        byte? highPassFilterLength = null;
        byte? lowPassFilterLength = null;
        double? quantizationBinCenter = null;
        var seenHuffmanTables = new bool[byte.MaxValue + 1];
        var huffmanTableCount = 0;
        var comments = new List<WsqCommentInfo>(capacity: 2);
        var encodedBlockByteCount = 0;
        var blockCount = 0;
        var nistCommentCount = 0;
        int? pixelsPerInch = null;

        var marker = reader.ReadMarker();

        while (marker != WsqMarker.StartOfFrame)
        {
            if (!marker.IsTableOrCommentBeforeFrame())
            {
                throw new InvalidDataException(
                    $"Encountered unexpected WSQ marker {marker} before the frame header.");
            }

            ReadMetadataSegment(
                marker,
                ref reader,
                ref highPassFilterLength,
                ref lowPassFilterLength,
                ref quantizationBinCenter,
                seenHuffmanTables,
                ref huffmanTableCount,
                comments,
                ref nistCommentCount,
                ref pixelsPerInch);

            marker = reader.ReadMarker();
        }

        var frameHeader = ReadFrameHeader(ref reader);
        marker = reader.ReadMarker();

        while (marker != WsqMarker.EndOfImage)
        {
            if (marker == WsqMarker.StartOfBlock)
            {
                var huffmanTableId = ReadBlockHeader(ref reader);
                if (!seenHuffmanTables[huffmanTableId])
                {
                    throw new InvalidDataException($"WSQ block references undefined Huffman table {huffmanTableId}.");
                }

                encodedBlockByteCount += reader.SkipCompressedDataUntilNextMarker(out marker);
                blockCount++;
                continue;
            }

            if (!marker.IsTableOrCommentBeforeBlock())
            {
                throw new InvalidDataException(
                    $"Encountered unsupported WSQ marker {marker} after the frame header.");
            }

            ReadMetadataSegment(
                marker,
                ref reader,
                ref highPassFilterLength,
                ref lowPassFilterLength,
                ref quantizationBinCenter,
                seenHuffmanTables,
                ref huffmanTableCount,
                comments,
                ref nistCommentCount,
                ref pixelsPerInch);

            marker = reader.ReadMarker();
        }

        if (highPassFilterLength is null || lowPassFilterLength is null)
        {
            throw new InvalidDataException("WSQ transform table is missing.");
        }

        if (quantizationBinCenter is null)
        {
            throw new InvalidDataException("WSQ quantization table is missing.");
        }

        return new(
            Width: frameHeader.Width,
            Height: frameHeader.Height,
            BitsPerPixel: 8,
            PixelsPerInch: pixelsPerInch ?? 500,
            Black: frameHeader.Black,
            White: frameHeader.White,
            Shift: frameHeader.Shift,
            Scale: frameHeader.Scale,
            WsqEncoder: frameHeader.WsqEncoder,
            SoftwareImplementationNumber: frameHeader.SoftwareImplementationNumber,
            HighPassFilterLength: highPassFilterLength.Value,
            LowPassFilterLength: lowPassFilterLength.Value,
            QuantizationBinCenter: quantizationBinCenter.Value,
            HuffmanTableIds: GetSortedHuffmanTableIds(seenHuffmanTables, huffmanTableCount),
            BlockCount: blockCount,
            EncodedBlockByteCount: encodedBlockByteCount,
            CommentCount: comments.Count,
            NistCommentCount: nistCommentCount,
            Comments: comments);
    }

    private static async ValueTask<WsqFileInfo> ReadStreamingAsync(
        Stream wsqStream,
        CancellationToken cancellationToken)
    {
        var reader = new WsqStreamReader(wsqStream);

        var startMarker = await reader.ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        if (startMarker != WsqMarker.StartOfImage)
        {
            throw new InvalidDataException($"WSQ bitstream must start with SOI, but started with {startMarker}.");
        }

        byte? highPassFilterLength = null;
        byte? lowPassFilterLength = null;
        double? quantizationBinCenter = null;
        var seenHuffmanTables = new bool[byte.MaxValue + 1];
        var huffmanTableCount = 0;
        var comments = new List<WsqCommentInfo>(capacity: 2);
        var encodedBlockByteCount = 0;
        var blockCount = 0;
        var nistCommentCount = 0;
        int? pixelsPerInch = null;

        var marker = await reader.ReadMarkerAsync(cancellationToken).ConfigureAwait(false);

        while (marker != WsqMarker.StartOfFrame)
        {
            if (!marker.IsTableOrCommentBeforeFrame())
            {
                throw new InvalidDataException(
                    $"Encountered unexpected WSQ marker {marker} before the frame header.");
            }

            var metadataState = await ReadMetadataSegmentAsync(
                    marker,
                    reader,
                    highPassFilterLength,
                    lowPassFilterLength,
                    quantizationBinCenter,
                    seenHuffmanTables,
                    huffmanTableCount,
                    comments,
                    nistCommentCount,
                    pixelsPerInch,
                    cancellationToken)
                .ConfigureAwait(false);
            highPassFilterLength = metadataState.HighPassFilterLength;
            lowPassFilterLength = metadataState.LowPassFilterLength;
            quantizationBinCenter = metadataState.QuantizationBinCenter;
            huffmanTableCount = metadataState.HuffmanTableCount;
            nistCommentCount = metadataState.NistCommentCount;
            pixelsPerInch = metadataState.PixelsPerInch;

            marker = await reader.ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        }

        var frameHeader = await ReadFrameHeaderAsync(reader, cancellationToken).ConfigureAwait(false);
        marker = await reader.ReadMarkerAsync(cancellationToken).ConfigureAwait(false);

        while (marker != WsqMarker.EndOfImage)
        {
            if (marker == WsqMarker.StartOfBlock)
            {
                var huffmanTableId = await ReadBlockHeaderAsync(reader, cancellationToken).ConfigureAwait(false);
                if (!seenHuffmanTables[huffmanTableId])
                {
                    throw new InvalidDataException($"WSQ block references undefined Huffman table {huffmanTableId}.");
                }

                var blockInfo = await reader.SkipCompressedDataUntilNextMarkerAsync(cancellationToken).ConfigureAwait(false);
                encodedBlockByteCount += blockInfo.EncodedByteCount;
                blockCount++;
                marker = blockInfo.NextMarker;
                continue;
            }

            if (!marker.IsTableOrCommentBeforeBlock())
            {
                throw new InvalidDataException(
                    $"Encountered unsupported WSQ marker {marker} after the frame header.");
            }

            var metadataState = await ReadMetadataSegmentAsync(
                    marker,
                    reader,
                    highPassFilterLength,
                    lowPassFilterLength,
                    quantizationBinCenter,
                    seenHuffmanTables,
                    huffmanTableCount,
                    comments,
                    nistCommentCount,
                    pixelsPerInch,
                    cancellationToken)
                .ConfigureAwait(false);
            highPassFilterLength = metadataState.HighPassFilterLength;
            lowPassFilterLength = metadataState.LowPassFilterLength;
            quantizationBinCenter = metadataState.QuantizationBinCenter;
            huffmanTableCount = metadataState.HuffmanTableCount;
            nistCommentCount = metadataState.NistCommentCount;
            pixelsPerInch = metadataState.PixelsPerInch;

            marker = await reader.ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        }

        if (highPassFilterLength is null || lowPassFilterLength is null)
        {
            throw new InvalidDataException("WSQ transform table is missing.");
        }

        if (quantizationBinCenter is null)
        {
            throw new InvalidDataException("WSQ quantization table is missing.");
        }

        return new(
            Width: frameHeader.Width,
            Height: frameHeader.Height,
            BitsPerPixel: 8,
            PixelsPerInch: pixelsPerInch ?? 500,
            Black: frameHeader.Black,
            White: frameHeader.White,
            Shift: frameHeader.Shift,
            Scale: frameHeader.Scale,
            WsqEncoder: frameHeader.WsqEncoder,
            SoftwareImplementationNumber: frameHeader.SoftwareImplementationNumber,
            HighPassFilterLength: highPassFilterLength.Value,
            LowPassFilterLength: lowPassFilterLength.Value,
            QuantizationBinCenter: quantizationBinCenter.Value,
            HuffmanTableIds: GetSortedHuffmanTableIds(seenHuffmanTables, huffmanTableCount),
            BlockCount: blockCount,
            EncodedBlockByteCount: encodedBlockByteCount,
            CommentCount: comments.Count,
            NistCommentCount: nistCommentCount,
            Comments: comments);
    }

    private static void ReadMetadataSegment(
        WsqMarker marker,
        ref WsqBufferReader reader,
        ref byte? highPassFilterLength,
        ref byte? lowPassFilterLength,
        ref double? quantizationBinCenter,
        bool[] seenHuffmanTables,
        ref int huffmanTableCount,
        List<WsqCommentInfo> comments,
        ref int nistCommentCount,
        ref int? pixelsPerInch)
    {
        switch (marker)
        {
            case WsqMarker.DefineTransformTable:
                ReadTransformTableHeader(ref reader, out var parsedHighPassFilterLength, out var parsedLowPassFilterLength);
                highPassFilterLength = parsedHighPassFilterLength;
                lowPassFilterLength = parsedLowPassFilterLength;
                break;
            case WsqMarker.DefineQuantizationTable:
                quantizationBinCenter = ReadQuantizationBinCenter(ref reader);
                break;
            case WsqMarker.DefineHuffmanTable:
                ReadHuffmanTableIds(ref reader, seenHuffmanTables, ref huffmanTableCount);
                break;
            case WsqMarker.Comment:
                var comment = ReadComment(ref reader);
                comments.Add(comment);

                if (comment.Fields.Count > 0)
                {
                    nistCommentCount++;
                }

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

    private static async ValueTask<WsqStreamingMetadataState> ReadMetadataSegmentAsync(
        WsqMarker marker,
        WsqStreamReader reader,
        byte? highPassFilterLength,
        byte? lowPassFilterLength,
        double? quantizationBinCenter,
        bool[] seenHuffmanTables,
        int huffmanTableCount,
        List<WsqCommentInfo> comments,
        int nistCommentCount,
        int? pixelsPerInch,
        CancellationToken cancellationToken)
    {
        switch (marker)
        {
            case WsqMarker.DefineTransformTable:
                {
                    var segmentReader = new WsqBufferReader(await reader.ReadSegmentPayloadAsync(cancellationToken).ConfigureAwait(false));
                    highPassFilterLength = segmentReader.ReadByte();
                    lowPassFilterLength = segmentReader.ReadByte();
                    return new(
                        highPassFilterLength,
                        lowPassFilterLength,
                        quantizationBinCenter,
                        huffmanTableCount,
                        nistCommentCount,
                        pixelsPerInch);
                }
            case WsqMarker.DefineQuantizationTable:
                {
                    var segmentReader = new WsqBufferReader(await reader.ReadSegmentPayloadAsync(cancellationToken).ConfigureAwait(false));
                    var binCenterScale = segmentReader.ReadByte();
                    var binCenterRaw = segmentReader.ReadUInt16BigEndian();
                    quantizationBinCenter = WsqScaledValueCodec.ScaleUInt16ToDouble(binCenterRaw, binCenterScale);
                    return new(
                        highPassFilterLength,
                        lowPassFilterLength,
                        quantizationBinCenter,
                        huffmanTableCount,
                        nistCommentCount,
                        pixelsPerInch);
                }
            case WsqMarker.DefineHuffmanTable:
                {
                    var segmentReader = new WsqBufferReader(await reader.ReadSegmentPayloadAsync(cancellationToken).ConfigureAwait(false));

                    while (segmentReader.Remaining > 0)
                    {
                        var tableId = segmentReader.ReadByte();
                        var codeLengthCounts = segmentReader.ReadBytes(16);
                        var valueCount = 0;
                        for (var index = 0; index < codeLengthCounts.Length; index++)
                        {
                            valueCount += codeLengthCounts[index];
                        }

                        segmentReader.ReadBytes(valueCount);

                        if (seenHuffmanTables[tableId])
                        {
                            continue;
                        }

                        seenHuffmanTables[tableId] = true;
                        huffmanTableCount++;
                    }

                    return new(
                        highPassFilterLength,
                        lowPassFilterLength,
                        quantizationBinCenter,
                        huffmanTableCount,
                        nistCommentCount,
                        pixelsPerInch);
                }
            case WsqMarker.Comment:
                {
                    var comment = WsqCommentParser.ReadCommentInfo(
                        await reader.ReadSegmentPayloadAsync(cancellationToken).ConfigureAwait(false));
                    comments.Add(comment);

                    if (comment.Fields.Count > 0)
                    {
                        nistCommentCount++;
                    }

                    if (pixelsPerInch is null
                        && comment.Fields.TryGetValue("PPI", out var ppiValue)
                        && int.TryParse(ppiValue, out var parsedPpi))
                    {
                        pixelsPerInch = parsedPpi;
                    }

                    return new(
                        highPassFilterLength,
                        lowPassFilterLength,
                        quantizationBinCenter,
                        huffmanTableCount,
                        nistCommentCount,
                        pixelsPerInch);
                }
            default:
                throw new InvalidDataException($"Unsupported WSQ segment marker {marker}.");
        }
    }

    private static void ReadTransformTableHeader(
        ref WsqBufferReader reader,
        out byte highPassFilterLength,
        out byte lowPassFilterLength)
    {
        var segmentReader = new WsqBufferReader(reader.ReadSegmentPayload());
        highPassFilterLength = segmentReader.ReadByte();
        lowPassFilterLength = segmentReader.ReadByte();
    }

    private static double ReadQuantizationBinCenter(ref WsqBufferReader reader)
    {
        var segmentReader = new WsqBufferReader(reader.ReadSegmentPayload());
        var binCenterScale = segmentReader.ReadByte();
        var binCenterRaw = segmentReader.ReadUInt16BigEndian();
        return WsqScaledValueCodec.ScaleUInt16ToDouble(binCenterRaw, binCenterScale);
    }

    private static void ReadHuffmanTableIds(
        ref WsqBufferReader reader,
        bool[] seenHuffmanTables,
        ref int huffmanTableCount)
    {
        var segmentReader = new WsqBufferReader(reader.ReadSegmentPayload());

        while (segmentReader.Remaining > 0)
        {
            var tableId = segmentReader.ReadByte();
            var codeLengthCounts = segmentReader.ReadBytes(16);
            var valueCount = 0;
            for (var index = 0; index < codeLengthCounts.Length; index++)
            {
                valueCount += codeLengthCounts[index];
            }

            segmentReader.ReadBytes(valueCount);

            if (seenHuffmanTables[tableId])
            {
                continue;
            }

            seenHuffmanTables[tableId] = true;
            huffmanTableCount++;
        }
    }

    private static WsqCommentInfo ReadComment(ref WsqBufferReader reader)
    {
        return WsqCommentParser.ReadCommentInfo(ref reader);
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

    private static async ValueTask<WsqFrameHeader> ReadFrameHeaderAsync(
        WsqStreamReader reader,
        CancellationToken cancellationToken)
    {
        var segmentReader = new WsqBufferReader(await reader.ReadSegmentPayloadAsync(cancellationToken).ConfigureAwait(false));
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

    private static byte ReadBlockHeader(ref WsqBufferReader reader)
    {
        var segmentReader = new WsqBufferReader(reader.ReadSegmentPayload());
        var huffmanTableId = segmentReader.ReadByte();

        if (segmentReader.Remaining != 0)
        {
            throw new InvalidDataException("WSQ block header contains unexpected trailing data.");
        }

        return huffmanTableId;
    }

    private static async ValueTask<byte> ReadBlockHeaderAsync(
        WsqStreamReader reader,
        CancellationToken cancellationToken)
    {
        var segmentReader = new WsqBufferReader(await reader.ReadSegmentPayloadAsync(cancellationToken).ConfigureAwait(false));
        var huffmanTableId = segmentReader.ReadByte();

        if (segmentReader.Remaining != 0)
        {
            throw new InvalidDataException("WSQ block header contains unexpected trailing data.");
        }

        return huffmanTableId;
    }

    private static byte[] GetSortedHuffmanTableIds(bool[] seenHuffmanTables, int huffmanTableCount)
    {
        var tableIds = new byte[huffmanTableCount];
        var destinationIndex = 0;

        for (var tableId = 0; tableId < seenHuffmanTables.Length; tableId++)
        {
            if (!seenHuffmanTables[tableId])
            {
                continue;
            }

            tableIds[destinationIndex++] = (byte)tableId;
        }

        return tableIds;
    }

    private readonly record struct WsqStreamingMetadataState(
        byte? HighPassFilterLength,
        byte? LowPassFilterLength,
        double? QuantizationBinCenter,
        int HuffmanTableCount,
        int NistCommentCount,
        int? PixelsPerInch);
}
