namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Encoding;

[Category("Contract: WSQ - NIST Reference Quantization")]
internal sealed class WsqReferenceQuantizationContractTests
{
    [Test]
    [Skip("Enable when the forward WSQ transform and quantizer match the official NIST encoder coefficient bins exactly.")]
    [DisplayName("Should match the official NIST quantized coefficient bins for every encoder reference case")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeCoefficientReferenceCases))]
    public async Task ShouldMatchTheOfficialNistQuantizedCoefficientBins(WsqEncodingReferenceCase testCase)
    {
        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath);
        var analysis = WsqEncoderAnalysisPipeline.Analyze(rawBytes, testCase.RawImage, new(testCase.BitRate));
        var referenceCoefficients = await ReadReferenceCoefficientsAsync(testCase.ReferencePath);

        if (!analysis.BlockSizes.SequenceEqual(referenceCoefficients.BlockSizes))
        {
            throw new InvalidOperationException(CreateBlockSizeMismatchMessage(testCase, analysis.BlockSizes, referenceCoefficients.BlockSizes));
        }

        await Assert.That(analysis.QuantizedCoefficients.Length).IsEqualTo(referenceCoefficients.QuantizedCoefficients.Length);

        if (!analysis.QuantizedCoefficients.SequenceEqual(referenceCoefficients.QuantizedCoefficients))
        {
            throw new InvalidOperationException(CreateCoefficientMismatchMessage(
                testCase,
                analysis.QuantizationTable,
                referenceCoefficients.QuantizationTable,
                analysis.QuantizedCoefficients,
                referenceCoefficients.QuantizedCoefficients));
        }
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
        var blockSizes = WsqQuantizationDecoder.ComputeBlockSizes(container.QuantizationTable, waveletTree, quantizationTree);
        return new(container.QuantizationTable, quantizedCoefficients, blockSizes);
    }

    private readonly record struct WsqReferenceQuantizedCoefficients(
        WsqQuantizationTable QuantizationTable,
        short[] QuantizedCoefficients,
        int[] BlockSizes);

    private static string CreateBlockSizeMismatchMessage(
        WsqEncodingReferenceCase testCase,
        ReadOnlySpan<int> actualBlockSizes,
        ReadOnlySpan<int> expectedBlockSizes)
    {
        return $"{testCase.FileName} at {testCase.BitRate:0.##} bpp produced block sizes [{string.Join(", ", actualBlockSizes.ToArray())}] "
            + $"but the NIST reference uses [{string.Join(", ", expectedBlockSizes.ToArray())}].";
    }

    private static string CreateCoefficientMismatchMessage(
        WsqEncodingReferenceCase testCase,
        WsqQuantizationTable actualQuantizationTable,
        WsqQuantizationTable expectedQuantizationTable,
        ReadOnlySpan<short> actualCoefficients,
        ReadOnlySpan<short> expectedCoefficients)
    {
        var quantizationBinDifference = FindFirstBinDifference(
            actualQuantizationTable.QuantizationBins,
            expectedQuantizationTable.QuantizationBins);
        var zeroBinDifference = FindFirstBinDifference(
            actualQuantizationTable.ZeroBins,
            expectedQuantizationTable.ZeroBins);

        for (var index = 0; index < actualCoefficients.Length; index++)
        {
            if (actualCoefficients[index] == expectedCoefficients[index])
            {
                continue;
            }

            return $"{testCase.FileName} at {testCase.BitRate:0.##} bpp first diverges at quantized coefficient index {index}: "
                + $"actual={actualCoefficients[index]}, expected={expectedCoefficients[index]}. "
                + $"First quantization-bin delta: {quantizationBinDifference}. "
                + $"First zero-bin delta: {zeroBinDifference}.";
        }

        return $"{testCase.FileName} at {testCase.BitRate:0.##} bpp produced a coefficient mismatch despite matching every compared index.";
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell",
        "S1244:Do not check floating point equality with exact values, use a range instead",
        Justification = "This helper only reports the first exact quantization-table divergence against the NIST reference codestream.")]
    private static string FindFirstBinDifference(IReadOnlyList<double> actualBins, IReadOnlyList<double> expectedBins)
    {
        for (var index = 0; index < actualBins.Count; index++)
        {
            if (actualBins[index].CompareTo(expectedBins[index]) == 0)
            {
                continue;
            }

            return $"index {index}: actual={actualBins[index]:G17}, expected={expectedBins[index]:G17}";
        }

        return "none";
    }
}
