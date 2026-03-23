namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq.Internal.Encoding;

[Category("Diagnostic: WSQ - NBIS Low-Rate Normalization Oracle")]
internal sealed class WsqNbisLowRateNormalizationOracleTests
{
    [Test]
    [DisplayName("Should match the NBIS normalization stage for the focused 0.75 bpp DQT-only mismatch cases")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeNbis075DqtOnlyMismatchReferenceCases))]
    public async Task ShouldMatchTheNbisNormalizationStageForTheFocused075BppDqtOnlyMismatchCases(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath).ConfigureAwait(false);
        var normalizedImage = WsqFloatImageNormalizer.Normalize(rawBytes);
        var nbisNormalizedPixels = await WsqNbisOracleReader.ReadNormalizedPixelsAsync(testCase).ConfigureAwait(false);

        await Assert.That(FindFirstFloatDifference(normalizedImage.Pixels, nbisNormalizedPixels)).IsEqualTo(-1);
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
}
