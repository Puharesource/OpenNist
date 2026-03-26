namespace OpenNist.Wsq.Internal;

internal sealed class WsqStreamReader(Stream stream)
{
    private const int s_defaultBufferSize = 16 * 1024;

    private readonly Stream _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    private readonly byte[] _buffer = GC.AllocateUninitializedArray<byte>(s_defaultBufferSize);
    private int _start;
    private int _end;

    public async ValueTask<byte> ReadByteAsync(CancellationToken cancellationToken)
    {
        if (!await EnsureBufferedAsync(1, cancellationToken).ConfigureAwait(false))
        {
            throw new EndOfStreamException("Unexpected end of WSQ stream.");
        }

        return _buffer[_start++];
    }

    public async ValueTask<WsqMarker> ReadMarkerAsync(CancellationToken cancellationToken)
    {
        var highByte = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
        var lowByte = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
        var markerValue = (ushort)((highByte << 8) | lowByte);

        if (!markerValue.IsValid())
        {
            throw new InvalidDataException($"Encountered unsupported WSQ marker 0x{markerValue:X4}.");
        }

        return (WsqMarker)markerValue;
    }

    public async ValueTask<byte[]> ReadSegmentPayloadAsync(CancellationToken cancellationToken)
    {
        var segmentLength = (await ReadByteAsync(cancellationToken).ConfigureAwait(false) << 8)
            | await ReadByteAsync(cancellationToken).ConfigureAwait(false);

        if (segmentLength < sizeof(ushort))
        {
            throw new InvalidDataException($"WSQ segment length {segmentLength} is invalid.");
        }

        var payloadLength = segmentLength - sizeof(ushort);
        var payload = GC.AllocateUninitializedArray<byte>(payloadLength);
        await ReadExactlyAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
        return payload;
    }

    public async ValueTask<(int EncodedByteCount, WsqMarker NextMarker)> SkipCompressedDataUntilNextMarkerAsync(
        CancellationToken cancellationToken)
    {
        var encodedByteCount = 0;
        var previousWasMarkerPrefix = false;

        while (true)
        {
            if (!await EnsureBufferedAsync(1, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidDataException("WSQ compressed block terminated without a following marker.");
            }

            while (_start < _end)
            {
                var current = _buffer[_start++];
                encodedByteCount++;

                if (!previousWasMarkerPrefix)
                {
                    previousWasMarkerPrefix = current == 0xFF;
                    continue;
                }

                if (current == 0x00)
                {
                    previousWasMarkerPrefix = false;
                    continue;
                }

                var markerValue = (ushort)(0xFF00 | current);
                if (!markerValue.IsValid())
                {
                    throw new InvalidDataException(
                        $"Encountered invalid WSQ marker candidate 0x{markerValue:X4} inside compressed block data.");
                }

                return (encodedByteCount - sizeof(ushort), (WsqMarker)markerValue);
            }
        }
    }

    private async ValueTask ReadExactlyAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        var destinationOffset = 0;

        while (destinationOffset < destination.Length)
        {
            if (_start == _end && !await EnsureBufferedAsync(1, cancellationToken).ConfigureAwait(false))
            {
                throw new EndOfStreamException("Unexpected end of WSQ stream.");
            }

            var available = _end - _start;
            var bytesToCopy = Math.Min(available, destination.Length - destinationOffset);
            _buffer.AsSpan(_start, bytesToCopy).CopyTo(destination.Span[destinationOffset..]);
            _start += bytesToCopy;
            destinationOffset += bytesToCopy;
        }
    }

    private async ValueTask<bool> EnsureBufferedAsync(int requiredByteCount, CancellationToken cancellationToken)
    {
        while (_end - _start < requiredByteCount)
        {
            if (_start > 0 && _start != _end)
            {
                _buffer.AsSpan(_start, _end - _start).CopyTo(_buffer);
                _end -= _start;
                _start = 0;
            }
            else if (_start == _end)
            {
                _start = 0;
                _end = 0;
            }

            var bytesRead = await _stream.ReadAsync(_buffer.AsMemory(_end), cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                return _end - _start >= requiredByteCount;
            }

            _end += bytesRead;
        }

        return true;
    }
}
