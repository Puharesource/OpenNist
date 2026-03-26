namespace OpenNist.Wsq.Internal.Encoding;

internal static class WsqRawImageReader
{
    public static async ValueTask<byte[]> ReadAsync(
        Stream rawImageStream,
        WsqRawImageDescription rawImage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawImageStream);
        ValidateRawImage(rawImage);

        var expectedByteCount = checked(rawImage.Width * rawImage.Height);

        if (rawImageStream is MemoryStream memoryStream && memoryStream.TryGetBuffer(out var bufferSegment))
        {
            var remainingLength = checked((int)(memoryStream.Length - memoryStream.Position));

            if (remainingLength != expectedByteCount)
            {
                throw new InvalidDataException(
                    $"Expected {expectedByteCount} raw bytes for a {rawImage.Width}x{rawImage.Height} image, but found {remainingLength}.");
            }

            return bufferSegment.AsSpan(checked((int)memoryStream.Position), remainingLength).ToArray();
        }

        if (rawImageStream.CanSeek)
        {
            var remainingLength = checked((int)(rawImageStream.Length - rawImageStream.Position));

            if (remainingLength != expectedByteCount)
            {
                throw new InvalidDataException(
                    $"Expected {expectedByteCount} raw bytes for a {rawImage.Width}x{rawImage.Height} image, but found {remainingLength}.");
            }

            var rawBytes = GC.AllocateUninitializedArray<byte>(remainingLength);
            await rawImageStream.ReadExactlyAsync(rawBytes.AsMemory(), cancellationToken).ConfigureAwait(false);
            return rawBytes;
        }

        using var buffer = new MemoryStream();
        await rawImageStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        if (buffer.Length != expectedByteCount)
        {
            throw new InvalidDataException(
                $"Expected {expectedByteCount} raw bytes for a {rawImage.Width}x{rawImage.Height} image, but found {buffer.Length}.");
        }

        return buffer.TryGetBuffer(out var bufferedData)
            ? bufferedData.AsSpan(0, checked((int)buffer.Length)).ToArray()
            : buffer.ToArray();
    }

    public static bool TryGetExactBuffer(
        Stream rawImageStream,
        WsqRawImageDescription rawImage,
        out ReadOnlySpan<byte> rawPixels)
    {
        ArgumentNullException.ThrowIfNull(rawImageStream);
        ValidateRawImage(rawImage);

        var expectedByteCount = checked(rawImage.Width * rawImage.Height);
        if (rawImageStream is not MemoryStream memoryStream || !memoryStream.TryGetBuffer(out var bufferSegment))
        {
            rawPixels = default;
            return false;
        }

        var remainingLength = checked((int)(memoryStream.Length - memoryStream.Position));
        if (remainingLength != expectedByteCount)
        {
            throw new InvalidDataException(
                $"Expected {expectedByteCount} raw bytes for a {rawImage.Width}x{rawImage.Height} image, but found {remainingLength}.");
        }

        rawPixels = bufferSegment.AsSpan(checked((int)memoryStream.Position), remainingLength);
        return true;
    }

    private static void ValidateRawImage(WsqRawImageDescription rawImage)
    {
        if (rawImage.Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rawImage), rawImage.Width, "Image width must be positive.");
        }

        if (rawImage.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rawImage), rawImage.Height, "Image height must be positive.");
        }

        if (rawImage.BitsPerPixel != 8)
        {
            throw new NotSupportedException($"WSQ encoding currently requires 8-bit grayscale input, but received {rawImage.BitsPerPixel} bits per pixel.");
        }
    }
}
