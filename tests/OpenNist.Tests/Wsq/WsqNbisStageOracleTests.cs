namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Encoding;

[Category("Diagnostic: WSQ - NBIS Stage Oracle")]
internal sealed class WsqNbisStageOracleTests
{
    [Test]
    [DisplayName("Should match the NBIS normalization stage for the focused 2.25 bpp blocker cases")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldMatchTheNbisNormalizationStageForTheFocused225BppBlockerCases(WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath);
        var normalizedImage = WsqFloatImageNormalizer.Normalize(rawBytes);
        var nbisNormalizedPixels = await WsqNbisOracleReader.ReadNormalizedPixelsAsync(testCase);

        var firstDifference = FindFirstFloatDifference(normalizedImage.Pixels, nbisNormalizedPixels);
        await Assert.That(firstDifference).IsEqualTo(-1);
    }

    [Test]
    [DisplayName("Should identify the first NBIS wavelet divergence point for the focused 2.25 bpp blocker cases")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldIdentifyTheFirstNbisWaveletDivergencePointForTheFocused225BppBlockerCases(WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath);
        var transformTable = WsqReferenceTables.CreateStandardTransformTable();
        WsqWaveletTreeBuilder.Build(testCase.RawImage.Width, testCase.RawImage.Height, out var waveletTree, out _);

        var normalizedImage = WsqFloatImageNormalizer.Normalize(rawBytes);
        var decomposedPixels = WsqDecomposition.Decompose(
            normalizedImage.Pixels,
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            waveletTree,
            transformTable);
        var nbisWaveletData = await WsqNbisOracleReader.ReadWaveletDataAsync(testCase);

        var firstDifference = FindFirstFloatDifference(decomposedPixels, nbisWaveletData);
        var expected = GetExpectedStageProfile(testCase.FileName);

        await Assert.That(firstDifference).IsEqualTo(expected.FirstWaveletDifferenceIndex);
    }

    [Test]
    [DisplayName("Should identify the first NBIS decomposition step divergence for the focused 2.25 bpp blocker cases")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldIdentifyTheFirstNbisDecompositionStepDivergenceForTheFocused225BppBlockerCases(WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath);
        var transformTable = WsqReferenceTables.CreateStandardTransformTable();
        WsqWaveletTreeBuilder.Build(testCase.RawImage.Width, testCase.RawImage.Height, out var waveletTree, out _);

        var normalizedImage = WsqFloatImageNormalizer.Normalize(rawBytes);
        var steps = WsqDecomposition.Trace(
            normalizedImage.Pixels,
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            waveletTree,
            transformTable);
        var expected = GetExpectedStageProfile(testCase.FileName);
        var actualFirstStepNodeIndex = -1;
        var actualFirstStepPass = "none";

        for (var nodeIndex = 0; nodeIndex < steps.Length; nodeIndex++)
        {
            var managedStep = steps[nodeIndex];
            var nbisRowPassData = await WsqNbisOracleReader.ReadRowPassDataAsync(testCase, nodeIndex);
            var rowPassDifference = FindFirstFloatDifference(managedStep.RowPassData, nbisRowPassData);
            if (rowPassDifference != -1)
            {
                actualFirstStepNodeIndex = nodeIndex;
                actualFirstStepPass = "row";
                break;
            }

            var nbisWaveletData = await WsqNbisOracleReader.ReadWaveletDataAsync(testCase, nodeIndex);
            var columnPassDifference = FindFirstFloatDifference(managedStep.WaveletDataAfterColumnPass, nbisWaveletData);
            if (columnPassDifference != -1)
            {
                actualFirstStepNodeIndex = nodeIndex;
                actualFirstStepPass = "column";
                break;
            }
        }

        await Assert.That(actualFirstStepNodeIndex).IsEqualTo(expected.FirstStepNodeIndex);
        await Assert.That(actualFirstStepPass).IsEqualTo(expected.FirstStepPass);
    }

    [Test]
    [DisplayName("Should characterize the current NBIS variance, qbin, and coefficient divergence for the focused 2.25 bpp blocker cases")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldCharacterizeTheCurrentNbisVarianceQbinAndCoefficientDivergenceForTheFocused225BppBlockerCases(WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath);
        var transformTable = WsqReferenceTables.CreateStandardTransformTable();
        WsqWaveletTreeBuilder.Build(
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            out var waveletTree,
            out var quantizationTree);

        var normalizedImage = WsqFloatImageNormalizer.Normalize(rawBytes);
        var decomposedPixels = WsqDecomposition.Decompose(
            normalizedImage.Pixels,
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            waveletTree,
            transformTable);
        var variances = WsqVarianceCalculator.Compute(decomposedPixels, quantizationTree, testCase.RawImage.Width);
        var quantizationResult = WsqQuantizer.Quantize(
            decomposedPixels,
            waveletTree,
            quantizationTree,
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            (float)testCase.BitRate);
        var nbisAnalysis = await WsqNbisOracleReader.ReadAnalysisAsync(testCase);
        var expected = GetExpectedStageProfile(testCase.FileName);

        await Assert.That(FindFirstFloatDifference(variances, nbisAnalysis.Variances)).IsEqualTo(expected.FirstVarianceDifferenceIndex);
        await Assert.That(FindFirstDoubleDifference(quantizationResult.QuantizationTable.QuantizationBins, nbisAnalysis.QuantizationBins))
            .IsEqualTo(expected.FirstQuantizationBinDifferenceIndex);
        await Assert.That(FindFirstShortDifference(quantizationResult.QuantizedCoefficients, nbisAnalysis.QuantizedCoefficients))
            .IsEqualTo(expected.FirstQuantizedCoefficientDifferenceIndex);
    }

    private static int FindFirstFloatDifference(ReadOnlySpan<float> actualValues, double[] expectedValues)
    {
        for (var index = 0; index < actualValues.Length; index++)
        {
            if (BitConverter.SingleToInt32Bits(actualValues[index]) == BitConverter.SingleToInt32Bits((float)expectedValues[index]))
            {
                continue;
            }

            return index;
        }

        return -1;
    }

    private static int FindFirstFloatDifference(ReadOnlySpan<float> actualValues, ReadOnlySpan<float> expectedValues)
    {
        for (var index = 0; index < actualValues.Length; index++)
        {
            if (BitConverter.SingleToInt32Bits(actualValues[index]) == BitConverter.SingleToInt32Bits(expectedValues[index]))
            {
                continue;
            }

            return index;
        }

        return -1;
    }

    private static int FindFirstDoubleDifference(IReadOnlyList<double> actualValues, double[] expectedValues)
    {
        for (var index = 0; index < actualValues.Count; index++)
        {
            if (BitConverter.DoubleToInt64Bits(actualValues[index]) == BitConverter.DoubleToInt64Bits(expectedValues[index]))
            {
                continue;
            }

            return index;
        }

        return -1;
    }

    private static int FindFirstShortDifference(ReadOnlySpan<short> actualValues, ReadOnlySpan<short> expectedValues)
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

    private static WsqNbisStageProfile GetExpectedStageProfile(string fileName)
    {
        return fileName switch
        {
            "cmp00003.raw" => new(
                FirstStepNodeIndex: 0,
                FirstStepPass: "column",
                FirstWaveletDifferenceIndex: 0,
                FirstVarianceDifferenceIndex: 0,
                FirstQuantizationBinDifferenceIndex: 0,
                FirstQuantizedCoefficientDifferenceIndex: 549),
            "cmp00005.raw" => new(
                FirstStepNodeIndex: 0,
                FirstStepPass: "column",
                FirstWaveletDifferenceIndex: 0,
                FirstVarianceDifferenceIndex: 1,
                FirstQuantizationBinDifferenceIndex: 0,
                FirstQuantizedCoefficientDifferenceIndex: 26),
            "a070.raw" => new(
                FirstStepNodeIndex: 0,
                FirstStepPass: "column",
                FirstWaveletDifferenceIndex: 0,
                FirstVarianceDifferenceIndex: 0,
                FirstQuantizationBinDifferenceIndex: 0,
                FirstQuantizedCoefficientDifferenceIndex: 6),
            _ => throw new ArgumentOutOfRangeException(nameof(fileName), fileName, "Unexpected blocker file."),
        };
    }

    private readonly record struct WsqNbisStageProfile(
        int FirstStepNodeIndex,
        string FirstStepPass,
        int FirstWaveletDifferenceIndex,
        int FirstVarianceDifferenceIndex,
        int FirstQuantizationBinDifferenceIndex,
        int FirstQuantizedCoefficientDifferenceIndex);
}
