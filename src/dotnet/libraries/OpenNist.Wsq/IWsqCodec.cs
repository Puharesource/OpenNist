namespace OpenNist.Wsq;

/// <summary>
/// Defines a stream-based codec for WSQ encoding and decoding.
/// </summary>
public interface IWsqCodec
{
    /// <summary>
    /// Encodes a headerless grayscale raw image stream into WSQ format.
    /// </summary>
    /// <param name="rawImageStream">The source raw image bytes.</param>
    /// <param name="wsqStream">The destination WSQ stream.</param>
    /// <param name="rawImage">The raw image dimensions and raster metadata.</param>
    /// <param name="options">The WSQ encoding options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when encoding has finished.</returns>
    ValueTask EncodeAsync(
        Stream rawImageStream,
        Stream wsqStream,
        WsqRawImageDescription rawImage,
        WsqEncodeOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decodes a WSQ image stream into a headerless grayscale raw image stream.
    /// </summary>
    /// <param name="wsqStream">The source WSQ stream.</param>
    /// <param name="rawImageStream">The destination raw image stream.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The decoded raw image metadata.</returns>
    ValueTask<WsqRawImageDescription> DecodeAsync(
        Stream wsqStream,
        Stream rawImageStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inspects a WSQ image stream and returns container metadata without decoding the raster.
    /// </summary>
    /// <param name="wsqStream">The source WSQ stream.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The WSQ container metadata.</returns>
    ValueTask<WsqFileInfo> InspectAsync(
        Stream wsqStream,
        CancellationToken cancellationToken = default);
}
