namespace OpenNist.Wsq.Internal.Encoding;

using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Decoding;

internal static class WsqEncoderAnalysisPipeline
{
    private const double s_highPrecisionAnalysisBitRateThreshold = 2.0;
    private const byte s_blackPixelValue = 0;
    private const byte s_whitePixelValue = byte.MaxValue;
    private const ushort s_defaultSoftwareImplementationNumber = 0;

    public static async ValueTask<WsqEncoderAnalysisResult> AnalyzeAsync(
        Stream rawImageStream,
        WsqRawImageDescription rawImage,
        WsqEncodeOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawImageStream);

        if (WsqRawImageReader.TryGetExactBuffer(rawImageStream, rawImage, out var rawPixels))
        {
            return Analyze(rawPixels, rawImage, options);
        }

        var rawBytes = await WsqRawImageReader.ReadAsync(rawImageStream, rawImage, cancellationToken).ConfigureAwait(false);
        return Analyze(rawBytes, rawImage, options);
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

        var transformTable = WsqReferenceTables.StandardTransformTable;
        WsqWaveletTreeBuilder.Build(rawImage.Width, rawImage.Height, out var waveletTree, out var quantizationTree);

        if (options.BitRate >= s_highPrecisionAnalysisBitRateThreshold)
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
        var dualNormalizedImage = WsqDualImageNormalizer.Normalize(rawPixels);
        var floatDecomposedPixels = WsqDecomposition.Decompose(
            dualNormalizedImage.FloatImage.Pixels,
            rawImage.Width,
            rawImage.Height,
            waveletTree,
            transformTable);
        var serializedArtifacts = WsqQuantizer.CreateQuantizationArtifacts(
            floatDecomposedPixels,
            quantizationTree,
            rawImage.Width,
            (float)options.BitRate);

        var serializedQuantizationTable = WsqQuantizationTableFactory.Create(
            serializedArtifacts.QuantizationBins,
            serializedArtifacts.ZeroBins);
        var quantizedCoefficients = WsqCoefficientQuantizer.Quantize(
            floatDecomposedPixels,
            quantizationTree,
            rawImage.Width,
            serializedArtifacts.QuantizationBins,
            serializedArtifacts.ZeroBins);
        var serializedBlockSizes = WsqQuantizationDecoder.ComputeBlockSizes(
            serializedQuantizationTable,
            waveletTree,
            quantizationTree);

        return new(
            CreateFrameHeader(
                rawImage,
                options,
                dualNormalizedImage.DoubleImage.Shift,
                dualNormalizedImage.DoubleImage.Scale),
            transformTable,
            serializedQuantizationTable,
            quantizedCoefficients,
            serializedBlockSizes);
    }

    internal static WsqHighPrecisionAnalysisArtifacts AnalyzeHighPrecisionArtifacts(
        ReadOnlySpan<byte> rawPixels,
        WsqRawImageDescription rawImage,
        WsqTransformTable transformTable,
        WsqWaveletNode[] waveletTree)
    {
        var dualNormalizedImage = WsqDualImageNormalizer.Normalize(rawPixels);
        var doubleDecomposedPixels = WsqDoubleDecomposition.Decompose(
            dualNormalizedImage.DoubleImage.Pixels,
            rawImage.Width,
            rawImage.Height,
            waveletTree,
            transformTable);
        var floatDecomposedPixels = WsqDecomposition.Decompose(
            dualNormalizedImage.FloatImage.Pixels,
            rawImage.Width,
            rawImage.Height,
            waveletTree,
            transformTable);

        return new(
            dualNormalizedImage.DoubleImage,
            dualNormalizedImage.FloatImage,
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
            Black: s_blackPixelValue,
            White: s_whitePixelValue,
            Height: checked((ushort)rawImage.Height),
            Width: checked((ushort)rawImage.Width),
            Shift: shift,
            Scale: scale,
            WsqEncoder: checked((byte)options.EncoderNumber),
            SoftwareImplementationNumber: checked((ushort)(options.SoftwareImplementationNumber ?? s_defaultSoftwareImplementationNumber)));
    }
}
