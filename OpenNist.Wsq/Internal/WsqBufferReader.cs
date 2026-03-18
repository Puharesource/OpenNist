namespace OpenNist.Wsq.Internal;

using System.Buffers.Binary;

internal ref struct WsqBufferReader
{
    private ReadOnlySpan<byte> _buffer;
    private int _position;

    public WsqBufferReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
        _position = 0;
    }

    public int Position => _position;

    public int Remaining => _buffer.Length - _position;

    public byte ReadByte()
    {
        EnsureRemaining(1);
        return _buffer[_position++];
    }

    public ushort ReadUInt16BigEndian()
    {
        EnsureRemaining(sizeof(ushort));
        var value = BinaryPrimitives.ReadUInt16BigEndian(_buffer[_position..]);
        _position += sizeof(ushort);
        return value;
    }

    public uint ReadUInt32BigEndian()
    {
        EnsureRemaining(sizeof(uint));
        var value = BinaryPrimitives.ReadUInt32BigEndian(_buffer[_position..]);
        _position += sizeof(uint);
        return value;
    }

    public ReadOnlySpan<byte> ReadBytes(int length)
    {
        EnsureRemaining(length);
        var slice = _buffer.Slice(_position, length);
        _position += length;
        return slice;
    }

    public WsqMarker ReadMarker()
    {
        var markerValue = ReadUInt16BigEndian();

        if (!Enum.IsDefined(typeof(WsqMarker), markerValue))
        {
            throw new InvalidDataException($"Encountered unsupported WSQ marker 0x{markerValue:X4}.");
        }

        return (WsqMarker)markerValue;
    }

    public ReadOnlySpan<byte> ReadSegmentPayload()
    {
        var segmentLength = ReadUInt16BigEndian();

        if (segmentLength < sizeof(ushort))
        {
            throw new InvalidDataException($"WSQ segment length {segmentLength} is invalid.");
        }

        return ReadBytes(segmentLength - sizeof(ushort));
    }

    public WsqMarker ReadCompressedDataUntilNextMarker(out int encodedByteCount)
    {
        var blockStart = _position;

        while (Remaining > 0)
        {
            var current = ReadByte();

            if (current != 0xFF)
            {
                continue;
            }

            if (Remaining == 0)
            {
                throw new InvalidDataException("Unexpected end of WSQ stream while scanning compressed block data.");
            }

            var markerLowByte = ReadByte();

            if (markerLowByte == 0x00)
            {
                continue;
            }

            var markerValue = (ushort)((current << 8) | markerLowByte);

            if (!Enum.IsDefined(typeof(WsqMarker), markerValue))
            {
                throw new InvalidDataException(
                    $"Encountered invalid WSQ marker candidate 0x{markerValue:X4} inside compressed block data.");
            }

            encodedByteCount = _position - blockStart - sizeof(ushort);
            return (WsqMarker)markerValue;
        }

        throw new InvalidDataException("WSQ compressed block terminated without a following marker.");
    }

    private void EnsureRemaining(int length)
    {
        if (Remaining < length)
        {
            throw new EndOfStreamException(
                $"Unexpected end of WSQ buffer. Needed {length} more byte(s), but only {Remaining} remained.");
        }
    }
}
