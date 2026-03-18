namespace OpenNist.Wsq.Internal.Decoding;

internal sealed class WsqHuffmanDecodingTable
{
    public required int[] MaxCodes { get; init; }

    public required int[] MinCodes { get; init; }

    public required int[] ValuePointers { get; init; }

    public required byte[] Values { get; init; }

    public static WsqHuffmanDecodingTable Create(WsqHuffmanTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var codeLengthCounts = GetValueSpan(table.CodeLengthCounts);
        var values = GetValueSpan(table.Values);

        if (codeLengthCounts.Length != WsqConstants.MaxHuffmanBits)
        {
            throw new InvalidDataException(
                $"WSQ Huffman table {table.TableId} must define {WsqConstants.MaxHuffmanBits} code-length counts.");
        }

        var codeCount = 0;

        for (var index = 0; index < codeLengthCounts.Length; index++)
        {
            codeCount += codeLengthCounts[index];
        }

        if (codeCount != values.Length)
        {
            throw new InvalidDataException(
                $"WSQ Huffman table {table.TableId} declares {codeCount} code values but contains {values.Length}.");
        }

        var sizes = new int[codeCount + 1];
        var sizeIndex = 0;

        for (var codeSize = 1; codeSize <= WsqConstants.MaxHuffmanBits; codeSize++)
        {
            for (var occurrence = 0; occurrence < codeLengthCounts[codeSize - 1]; occurrence++)
            {
                sizes[sizeIndex++] = codeSize;
            }
        }

        var codes = new int[codeCount];

        if (codeCount > 0)
        {
            var code = 0;
            var pointer = 0;
            var currentSize = sizes[0];

            while (currentSize != 0)
            {
                do
                {
                    codes[pointer] = code;
                    code++;
                    pointer++;
                }
                while (sizes[pointer] == currentSize);

                if (sizes[pointer] == 0)
                {
                    break;
                }

                do
                {
                    code <<= 1;
                    currentSize++;
                }
                while (sizes[pointer] != currentSize);
            }
        }

        var maxCodes = new int[WsqConstants.MaxHuffmanBits + 1];
        var minCodes = new int[WsqConstants.MaxHuffmanBits + 1];
        var valuePointers = new int[WsqConstants.MaxHuffmanBits + 1];
        Array.Fill(maxCodes, -1);

        var valueIndex = 0;

        for (var codeLength = 1; codeLength <= WsqConstants.MaxHuffmanBits; codeLength++)
        {
            var count = codeLengthCounts[codeLength - 1];

            if (count == 0)
            {
                continue;
            }

            valuePointers[codeLength] = valueIndex;
            minCodes[codeLength] = codes[valueIndex];
            valueIndex += count - 1;
            maxCodes[codeLength] = codes[valueIndex];
            valueIndex++;
        }

        return new()
        {
            MaxCodes = maxCodes,
            MinCodes = minCodes,
            ValuePointers = valuePointers,
            Values = table.Values is byte[] valuesArray
                ? valuesArray
                : table.Values.ToArray(),
        };
    }

    private static ReadOnlySpan<byte> GetValueSpan(IReadOnlyList<byte> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return values is byte[] array
            ? array
            : values.ToArray();
    }
}
