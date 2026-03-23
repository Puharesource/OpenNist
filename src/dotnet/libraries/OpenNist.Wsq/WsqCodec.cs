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

        return EncodeCoreAsync(rawImageStream, wsqStream, rawImage, options, cancellationToken);
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

        var container = await WsqContainerReader.ReadAsync(wsqStream, cancellationToken).ConfigureAwait(false);
        return CreateFileInfo(container);
    }

    private static WsqFileInfo CreateFileInfo(WsqContainer container)
    {
        var comments = container.Comments
            .Select(static comment => new WsqCommentInfo(comment.Text, comment.Fields))
            .ToArray();

        return new(
            Width: container.FrameHeader.Width,
            Height: container.FrameHeader.Height,
            BitsPerPixel: 8,
            PixelsPerInch: container.PixelsPerInch ?? 500,
            Black: container.FrameHeader.Black,
            White: container.FrameHeader.White,
            Shift: container.FrameHeader.Shift,
            Scale: container.FrameHeader.Scale,
            WsqEncoder: container.FrameHeader.WsqEncoder,
            SoftwareImplementationNumber: container.FrameHeader.SoftwareImplementationNumber,
            HighPassFilterLength: container.TransformTable.HighPassFilterLength,
            LowPassFilterLength: container.TransformTable.LowPassFilterLength,
            QuantizationBinCenter: container.QuantizationTable.BinCenter,
            HuffmanTableIds: container.HuffmanTables.Select(static table => table.TableId).ToArray(),
            BlockCount: container.Blocks.Count,
            EncodedBlockByteCount: container.Blocks.Sum(static block => block.EncodedByteCount),
            CommentCount: container.Comments.Count,
            NistCommentCount: container.Comments.Count(static comment => comment.IsNistComment),
            Comments: comments);
    }
}
