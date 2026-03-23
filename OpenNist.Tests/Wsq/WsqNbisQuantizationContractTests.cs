namespace OpenNist.Tests.Wsq;

using System.Globalization;
using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Encoding;

[Category("Contract: WSQ - NBIS Reference Quantization")]
internal sealed class WsqNbisQuantizationContractTests
{
    private const int RequiredExactParityFloor = 80;

    [Test]
    [DisplayName("Should preserve the current exact NBIS encoder coefficient-parity floor across the public encoder corpus")]
    public async Task ShouldPreserveTheCurrentExactNbisEncoderCoefficientParityFloorAcrossThePublicEncoderCorpus()
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var exactCases = new List<string>();
        var mismatchCases = new List<string>();

        foreach (var testCase in EnumerateAllEncodeReferenceCases())
        {
            var parity = await AnalyzeNbisParityAsync(testCase);
            var formattedCase = FormatCaseName(testCase);

            if (parity.IsExactMatch)
            {
                exactCases.Add(formattedCase);
                continue;
            }

            mismatchCases.Add($"{formattedCase} ({parity.MismatchSummary})");
        }

        if (exactCases.Count < RequiredExactParityFloor)
        {
            throw new InvalidOperationException(
                $"The managed encoder only matches {exactCases.Count} NBIS reference cases exactly, below the required floor of {RequiredExactParityFloor}. "
                + $"Exact cases: {string.Join(", ", exactCases)}. "
                + $"Mismatches: {string.Join("; ", mismatchCases)}.");
        }
    }

    [Test]
    [DisplayName("Should match the local NBIS encoder quantized coefficient bins for the current active exact NBIS cases")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeNbisActiveExactReferenceCases))]
    public async Task ShouldMatchTheLocalNbisEncoderQuantizedCoefficientBinsForTheCurrentActiveExactNbisCases(WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var parity = await AnalyzeNbisParityAsync(testCase);
        if (!parity.IsExactMatch)
        {
            throw new InvalidOperationException($"{FormatCaseName(testCase)} regressed from the active exact NBIS set. {parity.MismatchSummary}");
        }
    }

    [Test]
    [DisplayName("Should match the local NBIS encoder quantized coefficient bins for every encoder reference case")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.AllEncodeReferenceCases))]
    public async Task ShouldMatchTheLocalNbisEncoderQuantizedCoefficientBinsForEveryEncoderReferenceCase(WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var parity = await AnalyzeNbisParityAsync(testCase);

        if (!parity.IsExactMatch)
        {
            throw new InvalidOperationException($"{FormatCaseName(testCase)} diverges from the local NBIS encoder analysis output. {parity.MismatchSummary}");
        }
    }

    private static IEnumerable<WsqEncodingReferenceCase> EnumerateAllEncodeReferenceCases()
    {
        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            yield return new(
                fixture.FileName,
                0.75,
                fixture.RawImage,
                fixture.RawPath,
                fixture.ReferenceBitRate075Path);

            yield return new(
                fixture.FileName,
                2.25,
                fixture.RawImage,
                fixture.RawPath,
                fixture.ReferenceBitRate225Path);
        }
    }

    private static async Task<WsqNbisParityResult> AnalyzeNbisParityAsync(WsqEncodingReferenceCase testCase)
    {
        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath).ConfigureAwait(false);
        var analysis = WsqEncoderAnalysisPipeline.Analyze(rawBytes, testCase.RawImage, new(testCase.BitRate));
        var nbisAnalysis = await WsqNbisOracleReader.ReadAnalysisAsync(testCase).ConfigureAwait(false);
        WsqWaveletTreeBuilder.Build(
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            out var waveletTree,
            out var quantizationTree);

        var nbisQuantizationTable = WsqQuantizationTableFactory.Create(
            nbisAnalysis.QuantizationBins,
            nbisAnalysis.ZeroBins);
        var nbisBlockSizes = WsqQuantizationDecoder.ComputeBlockSizes(nbisQuantizationTable, waveletTree, quantizationTree);

        if (!analysis.BlockSizes.SequenceEqual(nbisBlockSizes))
        {
            return new(
                IsExactMatch: false,
                MismatchSummary: CreateBlockSizeMismatchSummary(analysis.BlockSizes, nbisBlockSizes));
        }

        if (!analysis.QuantizedCoefficients.SequenceEqual(nbisAnalysis.QuantizedCoefficients))
        {
            return new(
                IsExactMatch: false,
                MismatchSummary: CreateCoefficientMismatchSummary(
                    quantizationTree,
                    analysis.QuantizationTable,
                    analysis.QuantizedCoefficients,
                    nbisAnalysis));
        }

        return new(IsExactMatch: true, MismatchSummary: "exact");
    }

    private static string CreateBlockSizeMismatchSummary(
        ReadOnlySpan<int> actualBlockSizes,
        ReadOnlySpan<int> expectedBlockSizes)
    {
        return $"block sizes [{string.Join(", ", actualBlockSizes.ToArray())}] vs NBIS [{string.Join(", ", expectedBlockSizes.ToArray())}]";
    }

    private static string CreateCoefficientMismatchSummary(
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
        WsqQuantizationTable actualQuantizationTable,
        ReadOnlySpan<short> actualCoefficients,
        WsqNbisAnalysisDump nbisAnalysis)
    {
        var quantizationBinDifference = FindFirstBinDifference(
            actualQuantizationTable.QuantizationBins,
            nbisAnalysis.QuantizationBins);
        var zeroBinDifference = FindFirstBinDifference(
            actualQuantizationTable.ZeroBins,
            nbisAnalysis.ZeroBins);

        for (var index = 0; index < actualCoefficients.Length; index++)
        {
            if (actualCoefficients[index] == nbisAnalysis.QuantizedCoefficients[index])
            {
                continue;
            }

            var coefficientLocation = FindCoefficientLocation(
                actualQuantizationTable.QuantizationBins,
                quantizationTree,
                index);

            return $"first coefficient mismatch at index {index}: actual={actualCoefficients[index]}, NBIS={nbisAnalysis.QuantizedCoefficients[index]}, "
                + $"location={coefficientLocation}, first qbin delta={quantizationBinDifference}, first zbin delta={zeroBinDifference}";
        }

        return "coefficient mismatch with no differing index";
    }

    private static string FindFirstBinDifference(
        IReadOnlyList<double> actualBins,
        double[] expectedBins)
    {
        for (var index = 0; index < actualBins.Count; index++)
        {
            if (BitConverter.DoubleToInt64Bits(actualBins[index]) == BitConverter.DoubleToInt64Bits(expectedBins[index]))
            {
                continue;
            }

            return $"subband {index}: actual={actualBins[index].ToString("G17", CultureInfo.InvariantCulture)}, expected={expectedBins[index].ToString("G17", CultureInfo.InvariantCulture)}";
        }

        return "none";
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

    private static string FormatCaseName(WsqEncodingReferenceCase testCase)
    {
        return $"{testCase.FileName} @ {testCase.BitRate.ToString("0.##", CultureInfo.InvariantCulture)}";
    }

    private readonly record struct WsqNbisParityResult(
        bool IsExactMatch,
        string MismatchSummary);
}
