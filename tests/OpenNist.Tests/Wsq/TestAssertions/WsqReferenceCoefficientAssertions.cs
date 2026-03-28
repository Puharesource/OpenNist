namespace OpenNist.Tests.Wsq.TestAssertions;

using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Container;
using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Encoding;

internal static class WsqReferenceCoefficientAssertions
{
    public static async Task AssertExactMatchAsync(
        WsqEncodingReferenceCase testCase,
        WsqEncoderAnalysisResult analysis)
    {
        var referenceCoefficients = await ReadReferenceCoefficientsAsync(testCase.ReferencePath);
        WsqWaveletTreeBuilder.Build(
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            out _,
            out var quantizationTree);

        if (!analysis.BlockSizes.SequenceEqual(referenceCoefficients.BlockSizes))
        {
            throw new InvalidOperationException(CreateBlockSizeMismatchMessage(
                testCase,
                analysis.BlockSizes,
                referenceCoefficients.BlockSizes));
        }

        if (analysis.QuantizedCoefficients.Length != referenceCoefficients.QuantizedCoefficients.Length)
        {
            throw new InvalidOperationException(
                $"{testCase.FileName} at {testCase.BitRate:0.##} bpp produced {analysis.QuantizedCoefficients.Length} quantized coefficients "
                + $"but the NIST reference contains {referenceCoefficients.QuantizedCoefficients.Length}.");
        }

        if (!analysis.QuantizedCoefficients.SequenceEqual(referenceCoefficients.QuantizedCoefficients))
        {
            throw new InvalidOperationException(CreateCoefficientMismatchMessage(
                testCase,
                quantizationTree,
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
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
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

            var coefficientLocation = FindCoefficientLocation(
                actualQuantizationTable.QuantizationBins,
                quantizationTree,
                index);

            return $"{testCase.FileName} at {testCase.BitRate:0.##} bpp first diverges at quantized coefficient index {index}: "
                + $"actual={actualCoefficients[index]}, expected={expectedCoefficients[index]}. "
                + $"Location: {coefficientLocation}. "
                + $"First quantization-bin delta: {quantizationBinDifference}. "
                + $"First zero-bin delta: {zeroBinDifference}.";
        }

        return $"{testCase.FileName} at {testCase.BitRate:0.##} bpp produced a coefficient mismatch despite matching every compared index.";
    }

    private static string FindCoefficientLocation(
        IReadOnlyList<double> quantizationBins,
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
        int coefficientIndex)
    {
        var remainingCoefficientIndex = coefficientIndex;

        for (var subbandIndex = 0; subbandIndex < quantizationTree.Length; subbandIndex++)
        {
            if (quantizationBins[subbandIndex].CompareTo(0.0) == 0)
            {
                continue;
            }

            var node = quantizationTree[subbandIndex];
            var subbandCoefficientCount = node.Width * node.Height;

            if (remainingCoefficientIndex >= subbandCoefficientCount)
            {
                remainingCoefficientIndex -= subbandCoefficientCount;
                continue;
            }

            var row = remainingCoefficientIndex / node.Width;
            var column = remainingCoefficientIndex % node.Width;
            var imageX = node.X + column;
            var imageY = node.Y + row;

            return $"subband {subbandIndex}, row {row}, column {column}, image x {imageX}, image y {imageY}";
        }

        return "outside the active quantized subbands";
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

            return $"index {index}: actual={actualBins[index]}, expected={expectedBins[index]}";
        }

        return "none";
    }

    private readonly record struct WsqReferenceQuantizedCoefficients(
        WsqQuantizationTable QuantizationTable,
        short[] QuantizedCoefficients,
        int[] BlockSizes);
}
