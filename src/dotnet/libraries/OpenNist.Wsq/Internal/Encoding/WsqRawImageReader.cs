namespace OpenNist.Wsq.Internal.Encoding;

using OpenNist.Wsq.Errors;
using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Errors;
using OpenNist.Wsq.Model;

internal static class WsqRawImageReader
{
    public static async ValueTask<byte[]> ReadAsync(
        Stream rawImageStream,
        WsqRawImageDescription rawImage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawImageStream);
        ValidateRawImageOrThrow(rawImage);

        var expectedByteCount = checked(rawImage.Width * rawImage.Height);

        if (rawImageStream is MemoryStream memoryStream && memoryStream.TryGetBuffer(out var bufferSegment))
        {
            var remainingLength = checked((int)(memoryStream.Length - memoryStream.Position));

            if (remainingLength != expectedByteCount)
            {
                throw WsqErrors.ExceptionFrom(WsqErrors.ValidationFailed([WsqErrors.RawImageByteCountMismatch(remainingLength, expectedByteCount)]));
            }

            return bufferSegment.AsSpan(checked((int)memoryStream.Position), remainingLength).ToArray();
        }

        if (rawImageStream.CanSeek)
        {
            var remainingLength = checked((int)(rawImageStream.Length - rawImageStream.Position));

            if (remainingLength != expectedByteCount)
            {
                throw WsqErrors.ExceptionFrom(WsqErrors.ValidationFailed([WsqErrors.RawImageByteCountMismatch(remainingLength, expectedByteCount)]));
            }

            var rawBytes = GC.AllocateUninitializedArray<byte>(remainingLength);
            await rawImageStream.ReadExactlyAsync(rawBytes.AsMemory(), cancellationToken).ConfigureAwait(false);
            return rawBytes;
        }

        using var buffer = new MemoryStream();
        await rawImageStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        if (buffer.Length != expectedByteCount)
        {
            throw WsqErrors.ExceptionFrom(
                WsqErrors.ValidationFailed([WsqErrors.RawImageByteCountMismatch(checked((int)buffer.Length), expectedByteCount)]));
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
        ValidateRawImageOrThrow(rawImage);

        var expectedByteCount = checked(rawImage.Width * rawImage.Height);
        if (rawImageStream is not MemoryStream memoryStream || !memoryStream.TryGetBuffer(out var bufferSegment))
        {
            rawPixels = default;
            return false;
        }

        var remainingLength = checked((int)(memoryStream.Length - memoryStream.Position));
        if (remainingLength != expectedByteCount)
        {
            throw WsqErrors.ExceptionFrom(WsqErrors.ValidationFailed([WsqErrors.RawImageByteCountMismatch(remainingLength, expectedByteCount)]));
        }

        rawPixels = bufferSegment.AsSpan(checked((int)memoryStream.Position), remainingLength);
        return true;
    }

    internal static WsqValidationResult ValidateRawImage(WsqRawImageDescription rawImage)
    {
        var errors = new List<WsqValidationError>(capacity: 3);

        if (rawImage.Width <= 0)
        {
            errors.Add(WsqErrors.RawImageWidthMustBePositive(rawImage.Width));
        }

        if (rawImage.Height <= 0)
        {
            errors.Add(WsqErrors.RawImageHeightMustBePositive(rawImage.Height));
        }

        if (rawImage.BitsPerPixel != 8)
        {
            errors.Add(WsqErrors.RawImageBitsPerPixelUnsupported(rawImage.BitsPerPixel));
        }

        return errors.Count == 0
            ? WsqValidationResult.Success()
            : WsqValidationResult.Failure(errors);
    }

    private static void ValidateRawImageOrThrow(WsqRawImageDescription rawImage)
    {
        var validation = ValidateRawImage(rawImage);
        if (!validation.IsValid)
        {
            throw WsqErrors.ExceptionFrom(WsqErrors.ValidationFailed(validation.Errors));
        }
    }
}
