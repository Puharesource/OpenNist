namespace OpenNist.Wsq.Internal.Decoding;

using OpenNist.Wsq.Internal.Container;
using OpenNist.Wsq.Internal.Metadata;

internal static class WsqHuffmanDecoder
{
    public static short[] DecodeQuantizedCoefficients(
        WsqContainer container,
        WsqWaveletNode[] waveletTree,
        WsqQuantizationNode[] quantizationTree)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(waveletTree);
        ArgumentNullException.ThrowIfNull(quantizationTree);

        if (container.Blocks.Count != WsqConstants.BlockCount)
        {
            throw new InvalidDataException(
                $"WSQ decoding currently expects {WsqConstants.BlockCount} encoded blocks, but found {container.Blocks.Count}.");
        }

        var blockSizes = WsqQuantizationDecoder.ComputeBlockSizes(container.QuantizationTable, waveletTree, quantizationTree);
        var totalCoefficientCount = 0;
        for (var index = 0; index < blockSizes.Length; index++)
        {
            totalCoefficientCount += blockSizes[index];
        }

        var quantizedCoefficients = new short[totalCoefficientCount];
        var coefficientOffset = 0;
        var decodingTables = new WsqHuffmanDecodingTable?[byte.MaxValue + 1];

        for (var blockIndex = 0; blockIndex < container.Blocks.Count; blockIndex++)
        {
            var block = container.Blocks[blockIndex];
            var blockCoefficientCount = blockSizes[blockIndex];

            if (blockCoefficientCount == 0)
            {
                if (block.EncodedByteCount != 0)
                {
                    throw new InvalidDataException(
                        $"WSQ block {blockIndex + 1} contains encoded data even though its quantized coefficient region is empty.");
                }

                continue;
            }

            var decodingTable = decodingTables[block.HuffmanTableId];
            if (decodingTable is null)
            {
                decodingTable = WsqHuffmanDecodingTable.Create(block.HuffmanTable);
                decodingTables[block.HuffmanTableId] = decodingTable;
            }

            DecodeBlock(
                block.EncodedData,
                decodingTable,
                quantizedCoefficients.AsSpan(coefficientOffset, blockCoefficientCount));

            coefficientOffset += blockCoefficientCount;
        }

        return quantizedCoefficients;
    }

    private static void DecodeBlock(
        ReadOnlySpan<byte> encodedData,
        WsqHuffmanDecodingTable decodingTable,
        Span<short> destination)
    {
        if (destination.IsEmpty)
        {
            return;
        }

        var bitReader = new WsqBitReader(encodedData);
        var destinationIndex = 0;

        while (destinationIndex < destination.Length)
        {
            var symbol = DecodeCategory(ref bitReader, decodingTable);

            switch (symbol)
            {
                case > 0 and <= 100:
                    AppendZeroRun(destination, ref destinationIndex, symbol);
                    break;
                case > 106 and < 0xFF:
                    destination[destinationIndex++] = (short)(symbol - 180);
                    break;
                case 101:
                    destination[destinationIndex++] = unchecked((short)bitReader.ReadBits(8));
                    break;
                case 102:
                    destination[destinationIndex++] = unchecked((short)-bitReader.ReadBits(8));
                    break;
                case 103:
                    destination[destinationIndex++] = unchecked((short)bitReader.ReadBits(16));
                    break;
                case 104:
                    destination[destinationIndex++] = unchecked((short)-bitReader.ReadBits(16));
                    break;
                case 105:
                    AppendZeroRun(destination, ref destinationIndex, bitReader.ReadBits(8));
                    break;
                case 106:
                    AppendZeroRun(destination, ref destinationIndex, bitReader.ReadBits(16));
                    break;
                default:
                    throw new InvalidDataException($"Encountered unsupported WSQ Huffman symbol {symbol}.");
            }
        }
    }

    private static int DecodeCategory(ref WsqBitReader bitReader, WsqHuffmanDecodingTable decodingTable)
    {
        var codeLength = 1;
        var code = bitReader.ReadBit();
        var maxCodes = decodingTable.MaxCodes;
        var minCodes = decodingTable.MinCodes;
        var valuePointers = decodingTable.ValuePointers;
        var values = decodingTable.Values;

        while (codeLength <= WsqConstants.MaxHuffmanBits && code > maxCodes[codeLength])
        {
            codeLength++;

            if (codeLength > WsqConstants.MaxHuffmanBits)
            {
                throw new InvalidDataException("WSQ Huffman code exceeded the maximum supported code length.");
            }

            code = (code << 1) | bitReader.ReadBit();
        }

        if (maxCodes[codeLength] < 0)
        {
            throw new InvalidDataException($"WSQ Huffman code length {codeLength} does not map to a defined value.");
        }

        var valueIndex = valuePointers[codeLength] + code - minCodes[codeLength];

        if ((uint)valueIndex >= (uint)values.Length)
        {
            throw new InvalidDataException("WSQ Huffman code resolved to an out-of-range value index.");
        }

        return values[valueIndex];
    }

    private static void AppendZeroRun(Span<short> destination, ref int destinationIndex, int runLength)
    {
        if (runLength < 0 || destinationIndex + runLength > destination.Length)
        {
            throw new InvalidDataException("Decoded WSQ zero run extends past the expected coefficient buffer.");
        }

        destination.Slice(destinationIndex, runLength).Clear();
        destinationIndex += runLength;
    }
}
