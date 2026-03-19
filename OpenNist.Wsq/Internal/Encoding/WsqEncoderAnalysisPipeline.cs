namespace OpenNist.Wsq.Internal.Encoding;

using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Decoding;

internal static class WsqEncoderAnalysisPipeline
{
    public static async ValueTask<WsqEncoderAnalysisResult> AnalyzeAsync(
        Stream rawImageStream,
        WsqRawImageDescription rawImage,
        WsqEncodeOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawImageStream);

        var rawPixels = await WsqRawImageReader.ReadAsync(rawImageStream, rawImage, cancellationToken).ConfigureAwait(false);
        return Analyze(rawPixels, rawImage, options);
    }

    public static WsqEncoderAnalysisResult Analyze(
        ReadOnlySpan<byte> rawPixels,
        WsqRawImageDescription rawImage,
        WsqEncodeOptions options)
    {
        if (options.BitRate <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.BitRate, "WSQ bit rate must be greater than zero.");
        }

        if (options.EncoderNumber is < byte.MinValue or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.EncoderNumber, "WSQ encoder number must fit in a byte.");
        }

        if (options.SoftwareImplementationNumber is < ushort.MinValue or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.SoftwareImplementationNumber,
                "WSQ software implementation number must fit in an unsigned 16-bit value.");
        }

        var normalizedImage = WsqFloatImageNormalizer.Normalize(rawPixels);
        var transformTable = WsqReferenceTables.StandardTransformTable;
        WsqWaveletTreeBuilder.Build(rawImage.Width, rawImage.Height, out var waveletTree, out var quantizationTree);

        var decomposedPixels = WsqDecomposition.Decompose(
            normalizedImage.Pixels,
            rawImage.Width,
            rawImage.Height,
            waveletTree,
            transformTable);

        var quantizationResult = WsqQuantizer.Quantize(
            decomposedPixels,
            waveletTree,
            quantizationTree,
            rawImage.Width,
            rawImage.Height,
            (float)options.BitRate);

        var frameHeader = new WsqFrameHeader(
            Black: 0,
            White: 255,
            Height: checked((ushort)rawImage.Height),
            Width: checked((ushort)rawImage.Width),
            Shift: normalizedImage.Shift,
            Scale: normalizedImage.Scale,
            WsqEncoder: checked((byte)options.EncoderNumber),
            SoftwareImplementationNumber: checked((ushort)(options.SoftwareImplementationNumber ?? 0)));

        return new(
            frameHeader,
            transformTable,
            quantizationResult.QuantizationTable,
            quantizationResult.QuantizedCoefficients,
            quantizationResult.BlockSizes);
    }
}
