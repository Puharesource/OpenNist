namespace OpenNist.Tests.Wsq.TestDiagnostics;

using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Encoding;

internal static class WsqHighPrecisionSubband0SensitivitySnapshotBuilder
{
    public static async Task<WsqHighPrecisionSubband0SensitivitySnapshot> CreateAsync(WsqEncodingReferenceCase testCase)
    {
        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath);
        var analysis = WsqEncoderAnalysisPipeline.Analyze(rawBytes, testCase.RawImage, new(testCase.BitRate));
        var referenceCoefficients = await ReadReferenceCoefficientsAsync(testCase.ReferencePath);

        WsqWaveletTreeBuilder.Build(
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            out var waveletTree,
            out var quantizationTree);

        var highPrecisionArtifacts = WsqEncoderAnalysisPipeline.AnalyzeHighPrecisionArtifacts(
            rawBytes,
            testCase.RawImage,
            WsqReferenceTables.CreateStandardTransformTable(),
            waveletTree);
        var rawQuantizationArtifacts = WsqHighPrecisionQuantizer.CreateQuantizationArtifacts(
            highPrecisionArtifacts.DoubleDecomposedPixels,
            quantizationTree,
            testCase.RawImage.Width,
            testCase.BitRate);

        var currentMismatchIndex = FindFirstMismatchIndex(
            analysis.QuantizedCoefficients,
            referenceCoefficients.QuantizedCoefficients);

        var overriddenQuantizationBins = rawQuantizationArtifacts.QuantizationBins.ToArray();
        var overriddenZeroBins = rawQuantizationArtifacts.ZeroBins.ToArray();
        overriddenQuantizationBins[0] = analysis.QuantizationTable.QuantizationBins[0];
        overriddenZeroBins[0] = analysis.QuantizationTable.ZeroBins[0];

        var subbandZeroSerializedOverrideCoefficients = WsqCoefficientQuantizer.Quantize(
            highPrecisionArtifacts.DoubleDecomposedPixels,
            quantizationTree,
            testCase.RawImage.Width,
            overriddenQuantizationBins,
            overriddenZeroBins);
        var subbandZeroSerializedOverrideMismatchIndex = FindFirstMismatchIndex(
            subbandZeroSerializedOverrideCoefficients,
            referenceCoefficients.QuantizedCoefficients);

        return new(
            testCase.FileName,
            testCase.BitRate,
            currentMismatchIndex,
            subbandZeroSerializedOverrideMismatchIndex);
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

    private static int FindFirstMismatchIndex(ReadOnlySpan<short> actualValues, ReadOnlySpan<short> expectedValues)
    {
        for (var index = 0; index < actualValues.Length; index++)
        {
            if (actualValues[index] == expectedValues[index])
            {
                continue;
            }

            return index;
        }

        return -1;
    }

    private readonly record struct WsqReferenceQuantizedCoefficients(short[] QuantizedCoefficients);
}

internal readonly record struct WsqHighPrecisionSubband0SensitivitySnapshot(
    string FileName,
    double BitRate,
    int CurrentMismatchIndex,
    int SubbandZeroSerializedOverrideMismatchIndex);
