namespace OpenNist.Wsq.Internal;

using System.Buffers.Binary;

internal ref struct WsqBufferReader(ReadOnlySpan<byte> buffer)
{
    private ReadOnlySpan<byte> _buffer = buffer;
    private int _position = 0;

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

        if (!markerValue.IsValid())
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

    public ReadOnlySpan<byte> ReadCompressedDataUntilNextMarker(out WsqMarker nextMarker)
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

            if (!markerValue.IsValid())
            {
                throw new InvalidDataException(
                    $"Encountered invalid WSQ marker candidate 0x{markerValue:X4} inside compressed block data.");
            }

            var encodedByteCount = _position - blockStart - sizeof(ushort);
            nextMarker = (WsqMarker)markerValue;
            return _buffer.Slice(blockStart, encodedByteCount);
        }

        throw new InvalidDataException("WSQ compressed block terminated without a following marker.");
    }

    public int SkipCompressedDataUntilNextMarker(out WsqMarker nextMarker)
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

            if (!markerValue.IsValid())
            {
                throw new InvalidDataException(
                    $"Encountered invalid WSQ marker candidate 0x{markerValue:X4} inside compressed block data.");
            }

            nextMarker = (WsqMarker)markerValue;
            return _position - blockStart - sizeof(ushort);
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
