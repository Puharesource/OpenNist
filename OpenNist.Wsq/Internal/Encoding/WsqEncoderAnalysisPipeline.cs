namespace OpenNist.Wsq.Internal.Encoding;

using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Decoding;

internal static class WsqEncoderAnalysisPipeline
{
    private const double HighPrecisionAnalysisBitRateThreshold = 2.0;

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

        var transformTable = WsqReferenceTables.CreateStandardTransformTable();
        WsqWaveletTreeBuilder.Build(rawImage.Width, rawImage.Height, out var waveletTree, out var quantizationTree);

        if (options.BitRate >= HighPrecisionAnalysisBitRateThreshold)
        {
            return AnalyzeHighPrecision(
                rawPixels,
                rawImage,
                options,
                transformTable,
                waveletTree,
                quantizationTree);
        }

        var normalizedImage = WsqFloatImageNormalizer.Normalize(rawPixels);
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

        return new(
            CreateFrameHeader(rawImage, options, normalizedImage.Shift, normalizedImage.Scale),
            transformTable,
            quantizationResult.QuantizationTable,
            quantizationResult.QuantizedCoefficients,
            quantizationResult.BlockSizes);
    }

    private static WsqEncoderAnalysisResult AnalyzeHighPrecision(
        ReadOnlySpan<byte> rawPixels,
        WsqRawImageDescription rawImage,
        WsqEncodeOptions options,
        WsqTransformTable transformTable,
        WsqWaveletNode[] waveletTree,
        WsqQuantizationNode[] quantizationTree)
    {
        var normalizedImage = WsqDoubleImageNormalizer.Normalize(rawPixels);
        var decomposedPixels = WsqDoubleDecomposition.Decompose(
            normalizedImage.Pixels,
            rawImage.Width,
            rawImage.Height,
            waveletTree,
            transformTable);

        var quantizationResult = WsqHighPrecisionQuantizer.Quantize(
            decomposedPixels,
            waveletTree,
            quantizationTree,
            rawImage.Width,
            rawImage.Height,
            options.BitRate);

        return new(
            CreateFrameHeader(
                rawImage,
                options,
                normalizedImage.Shift,
                normalizedImage.Scale),
            transformTable,
            quantizationResult.QuantizationTable,
            quantizationResult.QuantizedCoefficients,
            quantizationResult.BlockSizes);
    }

    internal static WsqHighPrecisionAnalysisArtifacts AnalyzeHighPrecisionArtifacts(
        ReadOnlySpan<byte> rawPixels,
        WsqRawImageDescription rawImage,
        WsqTransformTable transformTable,
        WsqWaveletNode[] waveletTree)
    {
        var doubleNormalizedImage = WsqDoubleImageNormalizer.Normalize(rawPixels);
        var floatNormalizedImage = WsqFloatImageNormalizer.Normalize(rawPixels);
        var doubleDecomposedPixels = WsqDoubleDecomposition.Decompose(
            doubleNormalizedImage.Pixels,
            rawImage.Width,
            rawImage.Height,
            waveletTree,
            transformTable);
        var floatDecomposedPixels = WsqDecomposition.Decompose(
            floatNormalizedImage.Pixels,
            rawImage.Width,
            rawImage.Height,
            waveletTree,
            transformTable);

        return new(
            doubleNormalizedImage,
            floatNormalizedImage,
            doubleDecomposedPixels,
            floatDecomposedPixels);
    }

    private static WsqFrameHeader CreateFrameHeader(
        WsqRawImageDescription rawImage,
        WsqEncodeOptions options,
        double shift,
        double scale)
    {
        return new(
            Black: 0,
            White: 255,
            Height: checked((ushort)rawImage.Height),
            Width: checked((ushort)rawImage.Width),
            Shift: shift,
            Scale: scale,
            WsqEncoder: checked((byte)options.EncoderNumber),
            SoftwareImplementationNumber: checked((ushort)(options.SoftwareImplementationNumber ?? 0)));
    }
}
