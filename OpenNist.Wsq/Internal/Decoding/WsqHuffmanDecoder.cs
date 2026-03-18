namespace OpenNist.Wsq.Internal.Decoding;

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
        var quantizedCoefficients = new short[blockSizes.Sum()];
        var coefficientOffset = 0;

        for (var blockIndex = 0; blockIndex < container.Blocks.Count; blockIndex++)
        {
            var blockCoefficientCount = blockSizes[blockIndex];

            if (blockCoefficientCount == 0)
            {
                if (container.Blocks[blockIndex].EncodedByteCount != 0)
                {
                    throw new InvalidDataException(
                        $"WSQ block {blockIndex + 1} contains encoded data even though its quantized coefficient region is empty.");
                }

                continue;
            }

            DecodeBlock(
                container.Blocks[blockIndex],
                quantizedCoefficients.AsSpan(coefficientOffset, blockCoefficientCount));

            coefficientOffset += blockCoefficientCount;
        }

        return quantizedCoefficients;
    }

    private static void DecodeBlock(WsqBlock block, Span<short> destination)
    {
        if (destination.IsEmpty)
        {
            return;
        }

        var decodingTable = WsqHuffmanDecodingTable.Create(block.HuffmanTable);
        var bitReader = new WsqBitReader(block.EncodedData);
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
        var code = (int)bitReader.ReadBits(1);

        while (codeLength <= WsqConstants.MaxHuffmanBits && code > decodingTable.MaxCodes[codeLength])
        {
            codeLength++;

            if (codeLength > WsqConstants.MaxHuffmanBits)
            {
                throw new InvalidDataException("WSQ Huffman code exceeded the maximum supported code length.");
            }

            code = (code << 1) | bitReader.ReadBits(1);
        }

        if (decodingTable.MaxCodes[codeLength] < 0)
        {
            throw new InvalidDataException($"WSQ Huffman code length {codeLength} does not map to a defined value.");
        }

        var valueIndex = decodingTable.ValuePointers[codeLength] + code - decodingTable.MinCodes[codeLength];

        if ((uint)valueIndex >= (uint)decodingTable.Values.Length)
        {
            throw new InvalidDataException("WSQ Huffman code resolved to an out-of-range value index.");
        }

        return decodingTable.Values[valueIndex];
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
