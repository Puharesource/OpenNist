namespace OpenNist.Wsq.Internal.Encoding;

using OpenNist.Wsq.Internal;

internal static class WsqHuffmanEncoder
{
    private const byte Block1HuffmanTableId = 0;
    private const byte Block23HuffmanTableId = 1;
    private const int HuffmanCategoryCount = 256;
    private const int MaximumCoefficientInTable = 74;
    private const int MaximumZeroRunInTable = 100;

    public static WsqHuffmanEncodingResult EncodeBlocks(
        ReadOnlySpan<short> quantizedCoefficients,
        ReadOnlySpan<int> blockSizes)
    {
        if (blockSizes.Length != WsqConstants.BlockCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(blockSizes),
                blockSizes.Length,
                $"WSQ encoding expects exactly {WsqConstants.BlockCount} coefficient blocks.");
        }

        var block1Size = blockSizes[0];
        var block2Size = blockSizes[1];
        var block3Size = blockSizes[2];
        var totalBlockSize = block1Size + block2Size + block3Size;

        if (totalBlockSize != quantizedCoefficients.Length)
        {
            throw new InvalidDataException("WSQ coefficient blocks do not cover the full quantized-coefficient buffer.");
        }

        var block1Coefficients = quantizedCoefficients[..block1Size];
        var block23Coefficients = quantizedCoefficients.Slice(block1Size, block2Size + block3Size);
        var block2Coefficients = block23Coefficients[..block2Size];
        var block3Coefficients = block23Coefficients.Slice(block2Size, block3Size);

        Span<int> block1Sizes = stackalloc int[1];
        block1Sizes[0] = block1Size;

        Span<int> block23Sizes = stackalloc int[2];
        block23Sizes[0] = block2Size;
        block23Sizes[1] = block3Size;

        var block1Table = CreateHuffmanTable(block1Coefficients, block1Sizes, Block1HuffmanTableId);
        var block23Table = CreateHuffmanTable(block23Coefficients, block23Sizes, Block23HuffmanTableId);
        var block1EncodingTable = WsqHuffmanEncodingTable.Create(block1Table);
        var block23EncodingTable = WsqHuffmanEncodingTable.Create(block23Table);

        var blocks = new WsqBlock[WsqConstants.BlockCount];
        blocks[0] = new(Block1HuffmanTableId, block1Table, EncodeBlock(block1Coefficients, block1EncodingTable));
        blocks[1] = new(Block23HuffmanTableId, block23Table, EncodeBlock(block2Coefficients, block23EncodingTable));
        blocks[2] = new(Block23HuffmanTableId, block23Table, EncodeBlock(block3Coefficients, block23EncodingTable));

        return new([block1Table, block23Table], blocks);
    }

    private static WsqHuffmanTable CreateHuffmanTable(
        ReadOnlySpan<short> coefficients,
        ReadOnlySpan<int> blockSizes,
        byte tableId)
    {
        var categoryFrequencies = CountHuffmanCategories(coefficients, blockSizes);
        var codeSizes = FindHuffmanCodeSizes(categoryFrequencies);
        var huffmanBitCounts = FindHuffmanBitCounts(codeSizes, out var requiresAdjustment);

        if (requiresAdjustment)
        {
            AdjustHuffmanBitCountsToWsqLimit(huffmanBitCounts);
        }

        var orderedValues = SortSymbolsByCodeSize(codeSizes);
        var sizedCodes = BuildHuffmanSizes(huffmanBitCounts, out var codeCount);
        BuildCanonicalHuffmanCodes(sizedCodes);
        ValidateWsqHuffmanCodes(sizedCodes, codeCount);

        var codeLengthCounts = new byte[WsqConstants.MaxHuffmanBits];
        Array.Copy(huffmanBitCounts, codeLengthCounts, codeLengthCounts.Length);
        var values = orderedValues[..codeCount];

        return new(tableId, codeLengthCounts, values);
    }

    private static int[] CountHuffmanCategories(
        ReadOnlySpan<short> coefficients,
        ReadOnlySpan<int> blockSizes)
    {
        var categoryFrequencies = new int[HuffmanCategoryCount + 1];
        categoryFrequencies[HuffmanCategoryCount] = 1;
        var coefficientOffset = 0;

        for (var blockIndex = 0; blockIndex < blockSizes.Length; blockIndex++)
        {
            var blockSize = blockSizes[blockIndex];
            CountBlockCategories(coefficients.Slice(coefficientOffset, blockSize), categoryFrequencies);
            coefficientOffset += blockSize;
        }

        if (coefficientOffset != coefficients.Length)
        {
            throw new InvalidDataException("WSQ Huffman blocks do not cover the full quantized-coefficient buffer.");
        }

        return categoryFrequencies;
    }

    private static void CountBlockCategories(ReadOnlySpan<short> coefficients, Span<int> categoryFrequencies)
    {
        var lowerMaximumCoefficient = 1 - MaximumCoefficientInTable;
        var currentState = WsqHuffmanState.Coefficient;
        ushort zeroRunLength = 0;

        for (var coefficientIndex = 0; coefficientIndex < coefficients.Length; coefficientIndex++)
        {
            var coefficient = coefficients[coefficientIndex];

            switch (currentState)
            {
                case WsqHuffmanState.Coefficient:
                    if (coefficient == 0)
                    {
                        currentState = WsqHuffmanState.ZeroRun;
                        zeroRunLength = 1;
                        break;
                    }

                    CountCoefficientCategory(categoryFrequencies, coefficient, lowerMaximumCoefficient);
                    break;
                case WsqHuffmanState.ZeroRun:
                    if (coefficient == 0 && zeroRunLength < ushort.MaxValue)
                    {
                        zeroRunLength++;
                        break;
                    }

                    CountZeroRunCategory(categoryFrequencies, zeroRunLength);

                    if (coefficient != 0)
                    {
                        CountCoefficientCategory(categoryFrequencies, coefficient, lowerMaximumCoefficient);
                        currentState = WsqHuffmanState.Coefficient;
                    }
                    else
                    {
                        currentState = WsqHuffmanState.ZeroRun;
                        zeroRunLength = 1;
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unsupported WSQ Huffman state {currentState}.");
            }
        }

        if (currentState == WsqHuffmanState.ZeroRun)
        {
            CountZeroRunCategory(categoryFrequencies, zeroRunLength);
        }
    }

    private static void CountCoefficientCategory(Span<int> categoryFrequencies, short coefficient, int lowerMaximumCoefficient)
    {
        if (coefficient > MaximumCoefficientInTable)
        {
            categoryFrequencies[coefficient > byte.MaxValue ? 103 : 101]++;
            return;
        }

        if (coefficient < lowerMaximumCoefficient)
        {
            categoryFrequencies[coefficient < -byte.MaxValue ? 104 : 102]++;
            return;
        }

        categoryFrequencies[coefficient + 180]++;
    }

    private static void CountZeroRunCategory(Span<int> categoryFrequencies, ushort zeroRunLength)
    {
        if (zeroRunLength <= MaximumZeroRunInTable)
        {
            categoryFrequencies[zeroRunLength]++;
            return;
        }

        categoryFrequencies[zeroRunLength <= byte.MaxValue ? 105 : 106]++;
    }

    private static int[] FindHuffmanCodeSizes(ReadOnlySpan<int> categoryFrequencies)
    {
        var mutableFrequencies = categoryFrequencies.ToArray();
        var codeSizes = new int[HuffmanCategoryCount + 1];
        var chainedSymbols = new int[HuffmanCategoryCount + 1];
        Array.Fill(chainedSymbols, -1);

        while (true)
        {
            FindLeastFrequentSymbols(mutableFrequencies, out var firstSymbol, out var secondSymbol);

            if (secondSymbol == -1)
            {
                return codeSizes;
            }

            mutableFrequencies[firstSymbol] += mutableFrequencies[secondSymbol];
            mutableFrequencies[secondSymbol] = 0;

            var firstTail = IncrementCodeSizes(firstSymbol, codeSizes, chainedSymbols);
            chainedSymbols[firstTail] = secondSymbol;
            IncrementCodeSizes(secondSymbol, codeSizes, chainedSymbols);
        }
    }

    private static void FindLeastFrequentSymbols(
        ReadOnlySpan<int> frequencies,
        out int firstSymbol,
        out int secondSymbol)
    {
        firstSymbol = -1;
        secondSymbol = -1;
        var firstFrequency = 0;
        var secondFrequency = 0;
        var seenNonZeroFrequencyCount = 0;

        for (var symbol = 0; symbol < frequencies.Length; symbol++)
        {
            var frequency = frequencies[symbol];
            if (frequency == 0)
            {
                continue;
            }

            if (seenNonZeroFrequencyCount == 0)
            {
                firstFrequency = frequency;
                firstSymbol = symbol;
                seenNonZeroFrequencyCount++;
                continue;
            }

            if (seenNonZeroFrequencyCount == 1)
            {
                secondFrequency = frequency;
                secondSymbol = symbol;
                seenNonZeroFrequencyCount++;
            }

            if (firstFrequency < frequency && secondFrequency < frequency)
            {
                continue;
            }

            if (frequency < firstFrequency || (frequency == firstFrequency && symbol > firstSymbol))
            {
                secondFrequency = firstFrequency;
                secondSymbol = firstSymbol;
                firstFrequency = frequency;
                firstSymbol = symbol;
                continue;
            }

            if (frequency < secondFrequency || (frequency == secondFrequency && symbol > secondSymbol))
            {
                secondFrequency = frequency;
                secondSymbol = symbol;
            }
        }
    }

    private static int IncrementCodeSizes(int symbol, Span<int> codeSizes, ReadOnlySpan<int> chainedSymbols)
    {
        var currentSymbol = symbol;
        codeSizes[currentSymbol]++;

        while (chainedSymbols[currentSymbol] != -1)
        {
            currentSymbol = chainedSymbols[currentSymbol];
            codeSizes[currentSymbol]++;
        }

        return currentSymbol;
    }

    private static byte[] FindHuffmanBitCounts(ReadOnlySpan<int> codeSizes, out bool requiresAdjustment)
    {
        var huffmanBitCounts = new byte[WsqConstants.MaxHuffmanBits << 1];
        requiresAdjustment = false;

        for (var symbol = 0; symbol < HuffmanCategoryCount; symbol++)
        {
            var codeSize = codeSizes[symbol];
            if (codeSize == 0)
            {
                continue;
            }

            huffmanBitCounts[codeSize - 1]++;
            if (codeSize > WsqConstants.MaxHuffmanBits)
            {
                requiresAdjustment = true;
            }
        }

        return huffmanBitCounts;
    }

    private static void AdjustHuffmanBitCountsToWsqLimit(Span<byte> huffmanBitCounts)
    {
        var adjustedBitCounts = new short[huffmanBitCounts.Length];

        for (var index = 0; index < huffmanBitCounts.Length; index++)
        {
            adjustedBitCounts[index] = huffmanBitCounts[index];
        }

        var longestCodeLengthIndex = adjustedBitCounts.Length - 1;
        var maximumAllowedCodeLengthIndex = WsqConstants.MaxHuffmanBits - 1;

        for (var currentCodeLengthIndex = longestCodeLengthIndex;
            currentCodeLengthIndex > maximumAllowedCodeLengthIndex;
            currentCodeLengthIndex--)
        {
            while (adjustedBitCounts[currentCodeLengthIndex] > 0)
            {
                var redistributionIndex = currentCodeLengthIndex - 2;
                while (adjustedBitCounts[redistributionIndex] == 0)
                {
                    redistributionIndex--;
                }

                adjustedBitCounts[currentCodeLengthIndex] -= 2;
                adjustedBitCounts[currentCodeLengthIndex - 1] += 1;
                adjustedBitCounts[redistributionIndex + 1] += 2;
                adjustedBitCounts[redistributionIndex] -= 1;
            }

            adjustedBitCounts[currentCodeLengthIndex] = 0;
        }

        while (adjustedBitCounts[maximumAllowedCodeLengthIndex] == 0)
        {
            maximumAllowedCodeLengthIndex--;
        }

        adjustedBitCounts[maximumAllowedCodeLengthIndex] -= 1;

        for (var codeLengthIndex = 0; codeLengthIndex < huffmanBitCounts.Length; codeLengthIndex++)
        {
            huffmanBitCounts[codeLengthIndex] = checked((byte)adjustedBitCounts[codeLengthIndex]);
        }
    }

    private static byte[] SortSymbolsByCodeSize(ReadOnlySpan<int> codeSizes)
    {
        var orderedValues = new byte[HuffmanCategoryCount + 1];
        var orderedValueCount = 0;

        for (var codeLength = 1; codeLength <= (WsqConstants.MaxHuffmanBits << 1); codeLength++)
        {
            for (var symbol = 0; symbol < HuffmanCategoryCount; symbol++)
            {
                if (codeSizes[symbol] != codeLength)
                {
                    continue;
                }

                orderedValues[orderedValueCount++] = (byte)symbol;
            }
        }

        Array.Resize(ref orderedValues, orderedValueCount);
        return orderedValues;
    }

    private static HuffmanCodeDefinition[] BuildHuffmanSizes(
        ReadOnlySpan<byte> huffmanBitCounts,
        out int codeCount)
    {
        var sizedCodes = new HuffmanCodeDefinition[HuffmanCategoryCount + 1];
        codeCount = 0;

        for (var codeLength = 1; codeLength <= WsqConstants.MaxHuffmanBits; codeLength++)
        {
            var remainingCodesAtCurrentLength = huffmanBitCounts[codeLength - 1];

            while (remainingCodesAtCurrentLength > 0)
            {
                sizedCodes[codeCount].BitCount = codeLength;
                codeCount++;
                remainingCodesAtCurrentLength--;
            }
        }

        sizedCodes[codeCount].BitCount = 0;
        return sizedCodes;
    }

    private static void BuildCanonicalHuffmanCodes(Span<HuffmanCodeDefinition> sizedCodes)
    {
        var codeIndex = 0;
        var currentBitCount = sizedCodes[codeIndex].BitCount;
        if (currentBitCount == 0)
        {
            return;
        }

        ushort currentCode = 0;

        do
        {
            do
            {
                sizedCodes[codeIndex].Code = currentCode;
                currentCode++;
                codeIndex++;
            }
            while (sizedCodes[codeIndex].BitCount == currentBitCount);

            if (sizedCodes[codeIndex].BitCount == 0)
            {
                return;
            }

            do
            {
                currentCode <<= 1;
                currentBitCount++;
            }
            while (sizedCodes[codeIndex].BitCount != currentBitCount);
        }
        while (sizedCodes[codeIndex].BitCount == currentBitCount);
    }

    private static void ValidateWsqHuffmanCodes(ReadOnlySpan<HuffmanCodeDefinition> sizedCodes, int codeCount)
    {
        for (var codeIndex = 0; codeIndex < codeCount; codeIndex++)
        {
            var huffmanCode = sizedCodes[codeIndex];
            var isAllOnesCode = true;

            for (var bitIndex = 0; bitIndex < huffmanCode.BitCount && isAllOnesCode; bitIndex++)
            {
                isAllOnesCode = ((huffmanCode.Code >> bitIndex) & 0x0001) == 1;
            }

            if (!isAllOnesCode)
            {
                continue;
            }

            throw new InvalidDataException("WSQ Huffman table generation produced an all-ones code, which is not WSQ-compliant.");
        }
    }

    private static byte[] EncodeBlock(ReadOnlySpan<short> coefficients, WsqHuffmanEncodingTable encodingTable)
    {
        if (coefficients.IsEmpty)
        {
            return [];
        }

        var lowerMaximumCoefficient = 1 - MaximumCoefficientInTable;
        var bitWriter = new WsqBitWriter();
        var currentState = WsqHuffmanState.Coefficient;
        ushort zeroRunLength = 0;

        for (var coefficientIndex = 0; coefficientIndex < coefficients.Length; coefficientIndex++)
        {
            var coefficient = coefficients[coefficientIndex];

            switch (currentState)
            {
                case WsqHuffmanState.Coefficient:
                    if (coefficient == 0)
                    {
                        currentState = WsqHuffmanState.ZeroRun;
                        zeroRunLength = 1;
                        break;
                    }

                    WriteCoefficient(bitWriter, encodingTable, coefficient, lowerMaximumCoefficient);
                    break;
                case WsqHuffmanState.ZeroRun:
                    if (coefficient == 0 && zeroRunLength < ushort.MaxValue)
                    {
                        zeroRunLength++;
                        break;
                    }

                    WriteZeroRun(bitWriter, encodingTable, zeroRunLength);

                    if (coefficient != 0)
                    {
                        WriteCoefficient(bitWriter, encodingTable, coefficient, lowerMaximumCoefficient);
                        currentState = WsqHuffmanState.Coefficient;
                    }
                    else
                    {
                        currentState = WsqHuffmanState.ZeroRun;
                        zeroRunLength = 1;
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unsupported WSQ Huffman state {currentState}.");
            }
        }

        if (currentState == WsqHuffmanState.ZeroRun)
        {
            WriteZeroRun(bitWriter, encodingTable, zeroRunLength);
        }

        return bitWriter.ToArray();
    }

    private static void WriteCoefficient(
        WsqBitWriter bitWriter,
        WsqHuffmanEncodingTable encodingTable,
        short coefficient,
        int lowerMaximumCoefficient)
    {
        if (coefficient > MaximumCoefficientInTable)
        {
            if (coefficient > byte.MaxValue)
            {
                WriteSymbol(bitWriter, encodingTable, 103);
                bitWriter.WriteBits(coefficient, 16);
            }
            else
            {
                WriteSymbol(bitWriter, encodingTable, 101);
                bitWriter.WriteBits(coefficient, 8);
            }

            return;
        }

        if (coefficient < lowerMaximumCoefficient)
        {
            var magnitude = checked((ushort)-coefficient);

            if (coefficient < -byte.MaxValue)
            {
                WriteSymbol(bitWriter, encodingTable, 104);
                bitWriter.WriteBits(magnitude, 16);
            }
            else
            {
                WriteSymbol(bitWriter, encodingTable, 102);
                bitWriter.WriteBits(magnitude, 8);
            }

            return;
        }

        WriteSymbol(bitWriter, encodingTable, coefficient + 180);
    }

    private static void WriteZeroRun(
        WsqBitWriter bitWriter,
        WsqHuffmanEncodingTable encodingTable,
        ushort zeroRunLength)
    {
        if (zeroRunLength <= MaximumZeroRunInTable)
        {
            WriteSymbol(bitWriter, encodingTable, zeroRunLength);
            return;
        }

        if (zeroRunLength <= byte.MaxValue)
        {
            WriteSymbol(bitWriter, encodingTable, 105);
            bitWriter.WriteBits(zeroRunLength, 8);
            return;
        }

        WriteSymbol(bitWriter, encodingTable, 106);
        bitWriter.WriteBits(zeroRunLength, 16);
    }

    private static void WriteSymbol(WsqBitWriter bitWriter, WsqHuffmanEncodingTable encodingTable, int symbol)
    {
        var huffmanCode = encodingTable.Codes[symbol];
        bitWriter.WriteBits(huffmanCode.Bits, huffmanCode.BitCount);
    }

    private enum WsqHuffmanState
    {
        Coefficient,
        ZeroRun,
    }

    private struct HuffmanCodeDefinition
    {
        public ushort Code;
        public int BitCount;
    }
}

internal sealed record WsqHuffmanEncodingResult(
    IReadOnlyList<WsqHuffmanTable> HuffmanTables,
    IReadOnlyList<WsqBlock> Blocks);

internal sealed class WsqHuffmanEncodingTable
{
    public required WsqHuffmanCode[] Codes { get; init; }

    public static WsqHuffmanEncodingTable Create(WsqHuffmanTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var codeLengthCounts = table.CodeLengthCounts as byte[] ?? [.. table.CodeLengthCounts];
        var values = table.Values as byte[] ?? [.. table.Values];
        var codes = new WsqHuffmanCode[byte.MaxValue + 1];
        var code = 0;
        var valueIndex = 0;

        for (var codeLength = 1; codeLength <= WsqConstants.MaxHuffmanBits; codeLength++)
        {
            var count = codeLengthCounts[codeLength - 1];

            for (var index = 0; index < count; index++)
            {
                codes[values[valueIndex++]] = new(code, codeLength);
                code++;
            }

            code <<= 1;
        }

        return new()
        {
            Codes = codes,
        };
    }
}

internal readonly record struct WsqHuffmanCode(
    int Bits,
    int BitCount);
