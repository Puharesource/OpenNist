namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq;

[Category("Contract: WSQ - NIST Reference Decoding")]
[Skip("Official bundled NIST WSQ fixtures use a different wire-format target than local NBIS 5.0.0; while NBIS 5.0.0 byte parity is the active target this exact reconstruction contract is diagnostic only.")]
internal sealed class WsqReferenceDecodingContractTests
{
    [Test]
    [DisplayName("should decode every official NIST WSQ reference image to the exact NBIS reference reconstruction")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.AllDecodeReferenceCases))]
    public async Task ShouldDecodeEveryOfficialNistWsqReferenceImageToTheExactNbisReferenceReconstruction(
        WsqDecodingReferenceCase testCase)
    {
        await AssertDecodedOutputMatchesReferenceReconstructionExactlyAsync(testCase);
    }

    [Test]
    [DisplayName("should decode official NIST WSQ non-standard tap-set vectors to the exact NBIS reference reconstruction")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.NonStandardDecodeCases))]
    public async Task ShouldDecodeOfficialNistWsqVectorsThatUseNonStandardFilterTapSetsToTheExactReferenceReconstruction(
        WsqDecodingReferenceCase testCase)
    {
        await AssertDecodedOutputMatchesReferenceReconstructionExactlyAsync(testCase);
    }

    private static async Task AssertDecodedOutputMatchesReferenceReconstructionExactlyAsync(
        WsqDecodingReferenceCase testCase)
    {
        var result = await DecodeAndCompareAsync(testCase);

        await Assert.That(result.RawImage.Width).IsEqualTo(testCase.RawImage.Width);
        await Assert.That(result.RawImage.Height).IsEqualTo(testCase.RawImage.Height);
        await Assert.That(result.RawImage.BitsPerPixel).IsEqualTo(testCase.RawImage.BitsPerPixel);
        await Assert.That(result.RawImage.PixelsPerInch).IsEqualTo(testCase.RawImage.PixelsPerInch);
        await Assert.That(result.DecodedRawBytes.Length).IsEqualTo(result.ExpectedRawBytes.Length);
        await Assert.That(result.MaximumAbsoluteDifference).IsEqualTo(0);
        await Assert.That(result.MismatchCount).IsEqualTo(0);
        await Assert.That(result.DecodedRawBytes.SequenceEqual(result.ExpectedRawBytes)).IsTrue();
    }

    private static async Task<WsqDecodeComparisonResult> DecodeAndCompareAsync(WsqDecodingReferenceCase testCase)
    {
        var codec = new WsqCodec();
        var expectedRawBytes = await File.ReadAllBytesAsync(testCase.ReconstructionRawPath);

        await using var wsqStream = File.OpenRead(testCase.ReferencePath);
        await using var decodedRawStream = new MemoryStream();

        var rawImage = await codec.DecodeAsync(wsqStream, decodedRawStream);
        var decodedRawBytes = decodedRawStream.ToArray();
        var mismatchCount = 0;
        var maximumAbsoluteDifference = 0;

        var byteCount = Math.Min(decodedRawBytes.Length, expectedRawBytes.Length);

        for (var index = 0; index < byteCount; index++)
        {
            var absoluteDifference = Math.Abs(decodedRawBytes[index] - expectedRawBytes[index]);

            if (absoluteDifference == 0)
            {
                continue;
            }

            mismatchCount++;
            maximumAbsoluteDifference = Math.Max(maximumAbsoluteDifference, absoluteDifference);
        }

        return new(
            rawImage,
            decodedRawBytes,
            expectedRawBytes,
            mismatchCount,
            maximumAbsoluteDifference);
    }
}

internal readonly record struct WsqDecodeComparisonResult(
    WsqRawImageDescription RawImage,
    byte[] DecodedRawBytes,
    byte[] ExpectedRawBytes,
    int MismatchCount,
    int MaximumAbsoluteDifference);
