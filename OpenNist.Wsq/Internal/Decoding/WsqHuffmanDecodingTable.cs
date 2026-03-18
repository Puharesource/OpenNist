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

        if (table.CodeLengthCounts.Count != WsqConstants.MaxHuffmanBits)
        {
            throw new InvalidDataException(
                $"WSQ Huffman table {table.TableId} must define {WsqConstants.MaxHuffmanBits} code-length counts.");
        }

        var codeCount = table.CodeLengthCounts.Sum(static value => value);

        if (codeCount != table.Values.Count)
        {
            throw new InvalidDataException(
                $"WSQ Huffman table {table.TableId} declares {codeCount} code values but contains {table.Values.Count}.");
        }

        var sizes = new int[codeCount + 1];
        var sizeIndex = 0;

        for (var codeSize = 1; codeSize <= WsqConstants.MaxHuffmanBits; codeSize++)
        {
            for (var occurrence = 0; occurrence < table.CodeLengthCounts[codeSize - 1]; occurrence++)
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
            var count = table.CodeLengthCounts[codeLength - 1];

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
            Values = table.Values.ToArray(),
        };
    }
}
