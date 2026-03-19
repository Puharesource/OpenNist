namespace OpenNist.Wsq.Internal.Encoding;

using System.Buffers;

internal sealed class WsqBitWriter
{
    private readonly ArrayBufferWriter<byte> _buffer = new();
    private int _currentByte;
    private int _bitsInCurrentByte;

    public void WriteBits(int value, int bitCount)
    {
        if (bitCount is < 0 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount), bitCount, "Bit count must be between 0 and 16.");
        }

        if (bitCount == 0)
        {
            return;
        }

        for (var bitIndex = bitCount - 1; bitIndex >= 0; bitIndex--)
        {
            var bit = (value >> bitIndex) & 0x01;
            _currentByte = (_currentByte << 1) | bit;
            _bitsInCurrentByte++;

            if (_bitsInCurrentByte == 8)
            {
                WriteCompletedByte((byte)_currentByte);
                _currentByte = 0;
                _bitsInCurrentByte = 0;
            }
        }
    }

    public byte[] ToArray()
    {
        if (_bitsInCurrentByte > 0)
        {
            var paddedByte = (byte)((_currentByte << (8 - _bitsInCurrentByte)) | ((1 << (8 - _bitsInCurrentByte)) - 1));
            WriteCompletedByte(paddedByte);
            _currentByte = 0;
            _bitsInCurrentByte = 0;
        }

        return _buffer.WrittenSpan.ToArray();
    }

    private void WriteCompletedByte(byte value)
    {
        var outputLength = value == 0xFF ? 2 : 1;
        var destination = _buffer.GetSpan(outputLength);
        destination[0] = value;
        _buffer.Advance(1);

        if (value != 0xFF)
        {
            return;
        }

        destination[1] = 0x00;
        _buffer.Advance(1);
    }
}
