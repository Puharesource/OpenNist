namespace OpenNist.Wsq.Abstractions;

using OpenNist.Wsq.Errors;
using OpenNist.Wsq.Model;

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
    /// Tries to encode a headerless grayscale raw image stream into WSQ format without throwing for expected validation failures.
    /// </summary>
    /// <param name="rawImageStream">The source raw image bytes.</param>
    /// <param name="wsqStream">The destination WSQ stream.</param>
    /// <param name="rawImage">The raw image dimensions and raster metadata.</param>
    /// <param name="options">The WSQ encoding options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A non-throwing structured result.</returns>
    ValueTask<WsqResult> TryEncodeAsync(
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
    /// Tries to decode a WSQ image stream into a headerless grayscale raw image stream without throwing for malformed WSQ input.
    /// </summary>
    /// <param name="wsqStream">The source WSQ stream.</param>
    /// <param name="rawImageStream">The destination raw image stream.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A non-throwing structured result.</returns>
    ValueTask<WsqResult<WsqRawImageDescription>> TryDecodeAsync(
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

    /// <summary>
    /// Tries to inspect a WSQ image stream without throwing for malformed WSQ input.
    /// </summary>
    /// <param name="wsqStream">The source WSQ stream.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A non-throwing structured result.</returns>
    ValueTask<WsqResult<WsqFileInfo>> TryInspectAsync(
        Stream wsqStream,
        CancellationToken cancellationToken = default);
}
