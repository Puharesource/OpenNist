namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Encoding;

[Category("Diagnostic: WSQ - NBIS Low-Rate Wavelet Oracle")]
internal sealed class WsqNbisLowRateWaveletOracleTests
{
    [Test]
    [DisplayName("Should identify the first NBIS row-pass divergence for the focused 0.75 bpp DQT-only cases")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeNbis075DqtOnlyMismatchReferenceCases))]
    public async Task ShouldIdentifyTheFirstNbisRowPassDivergenceForTheFocused075BppDqtOnlyCases(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath).ConfigureAwait(false);
        var transformTable = WsqReferenceTables.CreateStandardTransformTable();
        WsqWaveletTreeBuilder.Build(testCase.RawImage.Width, testCase.RawImage.Height, out var waveletTree, out _);

        var normalizedImage = WsqFloatImageNormalizer.Normalize(rawBytes);
        var steps = WsqDecomposition.Trace(
            normalizedImage.Pixels,
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            waveletTree,
            transformTable);
        var nbisRowPassData = await WsqNbisOracleReader.ReadRowPassDataAsync(testCase, stopNode: 0).ConfigureAwait(false);
        var firstDifference = FindFirstFloatDifference(steps[0].RowPassData, nbisRowPassData);
        var expected = GetExpectedProfile(testCase.FileName);

        await Assert.That(firstDifference).IsEqualTo(expected.FirstRowPassDifferenceIndex);
    }

    [Test]
    [DisplayName("Should show the focused 0.75 bpp DQT-only cases already diverge at node-0 row-pass against NBIS 5.0.0")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeNbis075DqtOnlyMismatchReferenceCases))]
    public async Task ShouldShowTheFocused075BppDqtOnlyCasesAlreadyDivergeAtNode0RowPassAgainstNbis500(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath).ConfigureAwait(false);
        var transformTable = WsqReferenceTables.CreateStandardTransformTable();
        WsqWaveletTreeBuilder.Build(testCase.RawImage.Width, testCase.RawImage.Height, out var waveletTree, out _);

        var normalizedImage = WsqFloatImageNormalizer.Normalize(rawBytes);
        var steps = WsqDecomposition.Trace(
            normalizedImage.Pixels,
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            waveletTree,
            transformTable);
        var nbisRowPassData = await WsqNbisOracleReader.ReadRowPassDataAsync(testCase, stopNode: 0).ConfigureAwait(false);
        var firstDifference = FindFirstFloatDifference(steps[0].RowPassData, nbisRowPassData);

        await Assert.That(firstDifference >= 0).IsTrue();
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

    private static WsqLowRateWaveletOracleProfile GetExpectedProfile(string fileName)
    {
        return fileName switch
        {
            "a001.raw" => new(0),
            "a018.raw" => new(9),
            "a107.raw" => new(0),
            _ => throw new ArgumentOutOfRangeException(nameof(fileName), fileName, "Unexpected focused low-rate NBIS DQT mismatch file."),
        };
    }

    private readonly record struct WsqLowRateWaveletOracleProfile(
        int FirstRowPassDifferenceIndex);
}
