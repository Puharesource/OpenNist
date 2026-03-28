namespace OpenNist.Wsq.Codecs;

using OpenNist.Wsq.Abstractions;
using OpenNist.Wsq.Errors;
using OpenNist.Wsq.Internal.Container;
using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Encoding;
using OpenNist.Wsq.Internal.Errors;
using OpenNist.Wsq.Model;

/// <summary>
/// Default WSQ codec entry point.
/// </summary>
public sealed class WsqCodec : IWsqCodec
{
    /// <inheritdoc />
    public async ValueTask EncodeAsync(
        Stream rawImageStream,
        Stream wsqStream,
        WsqRawImageDescription rawImage,
        WsqEncodeOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawImageStream);
        ArgumentNullException.ThrowIfNull(wsqStream);

        var result = await TryEncodeAsync(rawImageStream, wsqStream, rawImage, options, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw WsqErrors.ExceptionFrom(result.Error!);
        }
    }

    /// <inheritdoc />
    public async ValueTask<WsqResult> TryEncodeAsync(
        Stream rawImageStream,
        Stream wsqStream,
        WsqRawImageDescription rawImage,
        WsqEncodeOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawImageStream);
        ArgumentNullException.ThrowIfNull(wsqStream);

        var validation = ValidateEncodeRequest(rawImageStream, rawImage, options);
        if (!validation.IsValid)
        {
            return WsqResults.Failure(WsqErrors.ValidationFailed(validation.Errors));
        }

        try
        {
            await EncodeCoreAsync(rawImageStream, wsqStream, rawImage, options, cancellationToken).ConfigureAwait(false);
            return WsqResults.Success();
        }
        catch (WsqException exception)
        {
            return WsqResults.Failure(WsqErrors.ErrorFromException(exception));
        }
    }

    private static async ValueTask EncodeCoreAsync(
        Stream rawImageStream,
        Stream wsqStream,
        WsqRawImageDescription rawImage,
        WsqEncodeOptions options,
        CancellationToken cancellationToken)
    {
        var analysis = await WsqEncoderAnalysisPipeline.AnalyzeAsync(rawImageStream, rawImage, options, cancellationToken).ConfigureAwait(false);
        var container = WsqEncoderContainerBuilder.Build(analysis, rawImage, options);
        await WsqContainerWriter.WriteAsync(wsqStream, container, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<WsqRawImageDescription> DecodeAsync(
        Stream wsqStream,
        Stream rawImageStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wsqStream);
        ArgumentNullException.ThrowIfNull(rawImageStream);

        var result = await TryDecodeAsync(wsqStream, rawImageStream, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? result.Value!
            : throw WsqErrors.ExceptionFrom(result.Error!);
    }

    /// <inheritdoc />
    public async ValueTask<WsqResult<WsqRawImageDescription>> TryDecodeAsync(
        Stream wsqStream,
        Stream rawImageStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wsqStream);
        ArgumentNullException.ThrowIfNull(rawImageStream);

        try
        {
            var result = await DecodeCoreAsync(wsqStream, rawImageStream, cancellationToken).ConfigureAwait(false);
            return WsqResults.Success(result);
        }
        catch (WsqException exception)
        {
            return WsqResults.Failure<WsqRawImageDescription>(WsqErrors.ErrorFromException(exception));
        }
        catch (InvalidDataException exception)
        {
            return WsqResults.Failure<WsqRawImageDescription>(WsqErrors.MalformedBitstream(exception.Message));
        }
        catch (EndOfStreamException exception)
        {
            return WsqResults.Failure<WsqRawImageDescription>(WsqErrors.MalformedBitstream(exception.Message));
        }
    }

    private static async ValueTask<WsqRawImageDescription> DecodeCoreAsync(
        Stream wsqStream,
        Stream rawImageStream,
        CancellationToken cancellationToken)
    {
        var container = await WsqContainerReader.ReadAsync(wsqStream, cancellationToken).ConfigureAwait(false);
        WsqWaveletTreeBuilder.Build(
            container.FrameHeader.Width,
            container.FrameHeader.Height,
            out var waveletTree,
            out var quantizationTree);

        var quantizedCoefficients = WsqHuffmanDecoder.DecodeQuantizedCoefficients(container, waveletTree, quantizationTree);
        var floatingPointPixels = WsqQuantizationDecoder.Unquantize(
            container.QuantizationTable,
            quantizationTree,
            quantizedCoefficients,
            container.FrameHeader.Width,
            container.FrameHeader.Height);

        var rawPixels = WsqReconstruction.ReconstructToRawPixels(
            floatingPointPixels,
            container.FrameHeader.Width,
            container.FrameHeader.Height,
            waveletTree,
            container.TransformTable,
            (float)container.FrameHeader.Shift,
            (float)container.FrameHeader.Scale);

        await rawImageStream.WriteAsync(rawPixels, cancellationToken).ConfigureAwait(false);

        return new(
            container.FrameHeader.Width,
            container.FrameHeader.Height,
            BitsPerPixel: 8,
            PixelsPerInch: container.PixelsPerInch ?? 500);
    }

    /// <inheritdoc />
    public async ValueTask<WsqFileInfo> InspectAsync(
        Stream wsqStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wsqStream);
        var result = await TryInspectAsync(wsqStream, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? result.Value!
            : throw WsqErrors.ExceptionFrom(result.Error!);
    }

    /// <inheritdoc />
    public async ValueTask<WsqResult<WsqFileInfo>> TryInspectAsync(
        Stream wsqStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wsqStream);

        try
        {
            var info = await WsqFileInfoReader.ReadAsync(wsqStream, cancellationToken).ConfigureAwait(false);
            return WsqResults.Success(info);
        }
        catch (WsqException exception)
        {
            return WsqResults.Failure<WsqFileInfo>(WsqErrors.ErrorFromException(exception));
        }
        catch (InvalidDataException exception)
        {
            return WsqResults.Failure<WsqFileInfo>(WsqErrors.MalformedBitstream(exception.Message));
        }
        catch (EndOfStreamException exception)
        {
            return WsqResults.Failure<WsqFileInfo>(WsqErrors.MalformedBitstream(exception.Message));
        }
    }

    private static WsqValidationResult ValidateEncodeRequest(
        Stream rawImageStream,
        WsqRawImageDescription rawImage,
        WsqEncodeOptions options)
    {
        var errors = new List<WsqValidationError>(capacity: 7);

        errors.AddRange(WsqRawImageReader.ValidateRawImage(rawImage).Errors);

        if (options.BitRate <= 0.0)
        {
            errors.Add(WsqErrors.BitRateMustBePositive(options.BitRate));
        }

        if (options.EncoderNumber is < byte.MinValue or > byte.MaxValue)
        {
            errors.Add(WsqErrors.EncoderNumberOutOfRange(options.EncoderNumber));
        }

        if (options.SoftwareImplementationNumber is { } softwareImplementationNumber &&
            softwareImplementationNumber is < ushort.MinValue or > ushort.MaxValue)
        {
            errors.Add(WsqErrors.SoftwareImplementationNumberOutOfRange(softwareImplementationNumber));
        }

        if (errors.Count == 0 &&
            rawImage.Width > 0 &&
            rawImage.Height > 0 &&
            TryGetRemainingLength(rawImageStream, out var remainingLength))
        {
            var expectedByteCount = checked(rawImage.Width * rawImage.Height);
            if (remainingLength != expectedByteCount)
            {
                errors.Add(WsqErrors.RawImageByteCountMismatch(remainingLength, expectedByteCount));
            }
        }

        return errors.Count == 0
            ? WsqValidationResult.Success()
            : WsqValidationResult.Failure(errors);
    }

    private static bool TryGetRemainingLength(Stream stream, out int remainingLength)
    {
        if (!stream.CanSeek)
        {
            remainingLength = 0;
            return false;
        }

        var remaining = stream.Length - stream.Position;
        if (remaining is < 0 or > int.MaxValue)
        {
            remainingLength = 0;
            return false;
        }

        remainingLength = checked((int)remaining);
        return true;
    }
}
