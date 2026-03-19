namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Encoding;

[Category("Contract: WSQ - NIST Reference Quantization")]
internal sealed class WsqReferenceQuantizationContractTests
{
    private const double QuantizationTolerancePercent = 0.051;
    private const double RequiredIdenticalCoefficientPercent = 99.99;
    private const int AllowedMaximumCoefficientDifference = 1;

    [Test]
    [DisplayName("Should satisfy the published NIST WSQ quantization and coefficient thresholds for every encoder reference case")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeCertificationReferenceCases))]
    public async Task ShouldSatisfyThePublishedNistWsqQuantizationAndCoefficientThresholds(WsqEncodingReferenceCase testCase)
    {
        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath);
        var analysis = WsqEncoderAnalysisPipeline.Analyze(rawBytes, testCase.RawImage, new(testCase.BitRate));
        var referenceCoefficients = await ReadReferenceCoefficientsAsync(testCase.ReferencePath);

        if (!analysis.BlockSizes.SequenceEqual(referenceCoefficients.BlockSizes))
        {
            throw new InvalidOperationException(CreateBlockSizeMismatchMessage(testCase, analysis.BlockSizes, referenceCoefficients.BlockSizes));
        }

        var maximumQuantizationDeltaPercent = FindMaximumRelativePercentDelta(
            analysis.QuantizationTable.QuantizationBins,
            referenceCoefficients.QuantizationTable.QuantizationBins);
        var coefficientDifferences = AnalyzeCoefficientDifferences(
            analysis.QuantizedCoefficients,
            referenceCoefficients.QuantizedCoefficients);

        if (maximumQuantizationDeltaPercent > QuantizationTolerancePercent
            || coefficientDifferences.IdenticalPercent < RequiredIdenticalCoefficientPercent
            || coefficientDifferences.MaximumAbsoluteDifference > AllowedMaximumCoefficientDifference)
        {
            throw new InvalidOperationException(CreateCertificationThresholdMismatchMessage(
                testCase,
                maximumQuantizationDeltaPercent,
                coefficientDifferences));
        }
    }

    [Test]
    [Skip("Enable when the forward WSQ transform and quantizer match the official NIST encoder coefficient bins exactly.")]
    [DisplayName("Should match the official NIST quantized coefficient bins for every encoder reference case")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeCoefficientReferenceCases))]
    public async Task ShouldMatchTheOfficialNistQuantizedCoefficientBins(WsqEncodingReferenceCase testCase)
    {
        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath);
        var analysis = WsqEncoderAnalysisPipeline.Analyze(rawBytes, testCase.RawImage, new(testCase.BitRate));
        var referenceCoefficients = await ReadReferenceCoefficientsAsync(testCase.ReferencePath);
        WsqWaveletTreeBuilder.Build(
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            out _,
            out var quantizationTree);

        if (!analysis.BlockSizes.SequenceEqual(referenceCoefficients.BlockSizes))
        {
            throw new InvalidOperationException(CreateBlockSizeMismatchMessage(testCase, analysis.BlockSizes, referenceCoefficients.BlockSizes));
        }

        await Assert.That(analysis.QuantizedCoefficients.Length).IsEqualTo(referenceCoefficients.QuantizedCoefficients.Length);

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

    private readonly record struct WsqReferenceQuantizedCoefficients(
        WsqQuantizationTable QuantizationTable,
        short[] QuantizedCoefficients,
        int[] BlockSizes);

    private readonly record struct WsqCoefficientDifferenceSummary(
        double IdenticalPercent,
        int MaximumAbsoluteDifference);

    private static string CreateBlockSizeMismatchMessage(
        WsqEncodingReferenceCase testCase,
        ReadOnlySpan<int> actualBlockSizes,
        ReadOnlySpan<int> expectedBlockSizes)
    {
        return $"{testCase.FileName} at {testCase.BitRate:0.##} bpp produced block sizes [{string.Join(", ", actualBlockSizes.ToArray())}] "
            + $"but the NIST reference uses [{string.Join(", ", expectedBlockSizes.ToArray())}].";
    }

    private static string CreateCertificationThresholdMismatchMessage(
        WsqEncodingReferenceCase testCase,
        double maximumQuantizationDeltaPercent,
        WsqCoefficientDifferenceSummary coefficientDifferences)
    {
        return $"{testCase.FileName} at {testCase.BitRate:0.##} bpp exceeded the published NIST encoder quantization/coefficient thresholds. "
            + $"Maximum quantization-bin delta: {maximumQuantizationDeltaPercent:F6}% (limit {QuantizationTolerancePercent:F3}%). "
            + $"Identical coefficients: {coefficientDifferences.IdenticalPercent:F6}% (minimum {RequiredIdenticalCoefficientPercent:F2}%). "
            + $"Maximum coefficient delta: {coefficientDifferences.MaximumAbsoluteDifference} (limit {AllowedMaximumCoefficientDifference}).";
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

    private static WsqCoefficientDifferenceSummary AnalyzeCoefficientDifferences(
        ReadOnlySpan<short> actualCoefficients,
        ReadOnlySpan<short> expectedCoefficients)
    {
        var identicalCount = 0;
        var maximumAbsoluteDifference = 0;

        for (var index = 0; index < actualCoefficients.Length; index++)
        {
            var absoluteDifference = Math.Abs(actualCoefficients[index] - expectedCoefficients[index]);
            if (absoluteDifference == 0)
            {
                identicalCount++;
            }

            maximumAbsoluteDifference = Math.Max(maximumAbsoluteDifference, absoluteDifference);
        }

        var identicalPercent = (double)identicalCount / actualCoefficients.Length * 100.0;
        return new(identicalPercent, maximumAbsoluteDifference);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell",
        "S1244:Do not check floating point equality with exact values, use a range instead",
        Justification = "Zero-valued quantization bins are structural markers in the WSQ reference codestream and are compared exactly.")]
    private static double FindMaximumRelativePercentDelta(
        IReadOnlyList<double> actualBins,
        IReadOnlyList<double> expectedBins)
    {
        var maximumDeltaPercent = 0.0;

        for (var index = 0; index < actualBins.Count; index++)
        {
            var expected = expectedBins[index];
            var actual = actualBins[index];

            if (expected.CompareTo(0.0) == 0)
            {
                if (actual.CompareTo(0.0) != 0)
                {
                    return double.PositiveInfinity;
                }

                continue;
            }

            var deltaPercent = Math.Abs(actual - expected) / expected * 100.0;
            maximumDeltaPercent = Math.Max(maximumDeltaPercent, deltaPercent);
        }

        return maximumDeltaPercent;
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
