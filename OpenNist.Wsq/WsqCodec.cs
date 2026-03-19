namespace OpenNist.Wsq;

using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Encoding;
using OpenNist.Wsq.Internal.Decoding;

/// <summary>
/// Default WSQ codec entry point.
/// </summary>
public sealed class WsqCodec : IWsqCodec
{
    /// <inheritdoc />
    public ValueTask EncodeAsync(
        Stream rawImageStream,
        Stream wsqStream,
        WsqRawImageDescription rawImage,
        WsqEncodeOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawImageStream);
        ArgumentNullException.ThrowIfNull(wsqStream);

        return EncodeCoreAsync(rawImageStream, rawImage, options, cancellationToken);
    }

    private static async ValueTask EncodeCoreAsync(
        Stream rawImageStream,
        WsqRawImageDescription rawImage,
        WsqEncodeOptions options,
        CancellationToken cancellationToken)
    {
        _ = await WsqEncoderAnalysisPipeline.AnalyzeAsync(rawImageStream, rawImage, options, cancellationToken).ConfigureAwait(false);

        throw new NotSupportedException(
            "WSQ bitstream emission is not implemented yet. The current build can normalize, decompose, and quantize raw images, but it cannot write WSQ markers, tables, Huffman blocks, or comments yet.");
    }

    /// <inheritdoc />
    public async ValueTask<WsqRawImageDescription> DecodeAsync(
        Stream wsqStream,
        Stream rawImageStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wsqStream);
        ArgumentNullException.ThrowIfNull(rawImageStream);

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
}
