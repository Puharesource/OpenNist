namespace OpenNist.Tests.Wsq.TestDiagnostics;

using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Encoding;

internal static class WsqHighPrecisionProductPrecisionGuardSnapshotBuilder
{
    public static async Task<WsqHighPrecisionProductPrecisionGuardSnapshot> CreateAsync(WsqEncodingReferenceCase testCase)
    {
        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath);
        var currentAnalysis = WsqEncoderAnalysisPipeline.Analyze(rawBytes, testCase.RawImage, new(testCase.BitRate));
        var referenceCoefficients = await ReadReferenceCoefficientsAsync(testCase.ReferencePath);

        WsqWaveletTreeBuilder.Build(
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            out var waveletTree,
            out var quantizationTree);

        var normalizedImage = WsqDoubleImageNormalizer.Normalize(rawBytes);
        var decomposedPixels = WsqDoubleDecomposition.Decompose(
            normalizedImage.Pixels,
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            waveletTree,
            WsqReferenceTables.CreateStandardTransformTable());

        var productAndScaleTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
            WsqHighPrecisionVarianceCalculator.Compute(decomposedPixels, quantizationTree, testCase.RawImage.Width),
            testCase.BitRate,
            new(UseSinglePrecisionProduct: true, UseSinglePrecisionScaleFactor: true));
        var productAndScaleCoefficients = WsqCoefficientQuantizer.Quantize(
            decomposedPixels,
            quantizationTree,
            testCase.RawImage.Width,
            productAndScaleTrace.QuantizationBins,
            productAndScaleTrace.ZeroBins);

        return new(
            testCase.FileName,
            FindFirstMismatchIndex(currentAnalysis.QuantizedCoefficients, referenceCoefficients.QuantizedCoefficients),
            FindFirstMismatchIndex(productAndScaleCoefficients, referenceCoefficients.QuantizedCoefficients));
    }

    private static async Task<WsqReferenceQuantizedCoefficients> ReadReferenceCoefficientsAsync(string referencePath)
    {
        await using var referenceStream = File.OpenRead(referencePath);
        var container = await WsqContainerReader.ReadAsync(referenceStream);
        WsqWaveletTreeBuilder.Build(
            container.FrameHeader.Width,
            container.FrameHeader.Height,
            out var waveletTree,
            out var quantizationTree);

        var quantizedCoefficients = WsqHuffmanDecoder.DecodeQuantizedCoefficients(container, waveletTree, quantizationTree);
        return new(quantizedCoefficients);
    }

    private static int FindFirstMismatchIndex(ReadOnlySpan<short> actual, ReadOnlySpan<short> expected)
    {
        for (var index = 0; index < actual.Length; index++)
        {
            if (actual[index] != expected[index])
            {
                return index;
            }
        }

        return -1;
    }

    private readonly record struct WsqReferenceQuantizedCoefficients(short[] QuantizedCoefficients);
}

internal sealed record WsqHighPrecisionProductPrecisionGuardSnapshot(
    string FileName,
    int CurrentFirstMismatchIndex,
    int ProductAndScaleFirstMismatchIndex);
