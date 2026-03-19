namespace OpenNist.Wsq.Internal.Decoding;

internal ref struct WsqBitReader(ReadOnlySpan<byte> buffer)
{
    private readonly ReadOnlySpan<byte> _buffer = buffer;
    private int _byteIndex = 0;
    private int _bitsRemaining = 0;
    private byte _currentByte = 0;

    public ushort ReadBits(int bitCount)
    {
        if (bitCount is < 1 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount), bitCount, "Bit count must be between 1 and 16.");
        }

        ushort bits = 0;
        var remainingBits = bitCount;

        while (remainingBits > 0)
        {
            if (_bitsRemaining == 0)
            {
                LoadNextByte();
            }

            var bitsToRead = Math.Min(remainingBits, _bitsRemaining);
            var shift = _bitsRemaining - bitsToRead;
            var mask = (1 << bitsToRead) - 1;
            bits = (ushort)((bits << bitsToRead) | ((_currentByte >> shift) & mask));

            _bitsRemaining -= bitsToRead;
            remainingBits -= bitsToRead;
        }

        return bits;
    }

    private void LoadNextByte()
    {
        if (_byteIndex >= _buffer.Length)
        {
            throw new InvalidDataException("Compressed WSQ block ended unexpectedly while more Huffman data was required.");
        }

        _currentByte = _buffer[_byteIndex++];
        _bitsRemaining = 8;

        if (_currentByte != 0xFF)
        {
            return;
        }

        if (_byteIndex >= _buffer.Length)
        {
            throw new InvalidDataException("Compressed WSQ block ended with an unterminated 0xFF escape sequence.");
        }

        var stuffedZero = _buffer[_byteIndex++];

        if (stuffedZero != 0x00)
        {
            throw new InvalidDataException("Compressed WSQ block contains an unexpected marker byte.");
        }
    }
}
