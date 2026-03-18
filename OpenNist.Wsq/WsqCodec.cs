namespace OpenNist.Wsq;

using OpenNist.Wsq.Internal;

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

        throw new NotSupportedException(
            "WSQ encoding is not implemented yet. The current build contains the managed bitstream parser foundation.");
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

        throw new NotSupportedException(
            $"WSQ decoding is not implemented yet. The current build can parse a {container.FrameHeader.Width}x{container.FrameHeader.Height} WSQ bitstream with {container.Blocks.Count} encoded block(s).");
    }
}
