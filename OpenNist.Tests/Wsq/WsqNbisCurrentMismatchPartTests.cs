namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestDiagnostics;
using OpenNist.Tests.Wsq.TestFixtures;

[Category("Diagnostic: WSQ - NBIS Current Mismatch Parts")]
internal sealed class WsqNbisCurrentMismatchPartTests
{
    [Test]
    [DisplayName("Should pinpoint the exact first NBIS mismatch coordinate for every current non-exact encoder case")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeNbisCurrentMismatchReferenceCases))]
    public async Task ShouldPinpointTheExactFirstNbisMismatchCoordinateForEveryCurrentNonExactEncoderCase(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAgainstNbisAsync(testCase);
        var expected = GetExpectedProfile(testCase.FileName, testCase.BitRate);

        await Assert.That(snapshot.MismatchIndex).IsEqualTo(expected.MismatchIndex);
        await Assert.That(snapshot.MismatchLocation.SubbandIndex).IsEqualTo(expected.SubbandIndex);
        await Assert.That(snapshot.MismatchLocation.Row).IsEqualTo(expected.Row);
        await Assert.That(snapshot.MismatchLocation.Column).IsEqualTo(expected.Column);
        await Assert.That(snapshot.ProductionQuantizedCoefficient).IsEqualTo(expected.ProductionQuantizedCoefficient);
        await Assert.That(snapshot.NbisQuantizedCoefficient).IsEqualTo(expected.NbisQuantizedCoefficient);
    }

    [Test]
    [DisplayName("Should show every current NBIS mismatch is still a local single-bucket miss with a tiny subband-local bin delta")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeNbisCurrentMismatchReferenceCases))]
    public async Task ShouldShowEveryCurrentNbisMismatchIsStillALocalSingleBucketMissWithATinySubbandLocalBinDelta(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAgainstNbisAsync(testCase);
        var qbinDelta = Math.Abs(snapshot.ProductionQuantizationBin - snapshot.NbisQuantizationBin);
        var halfZeroBinDelta = Math.Abs(snapshot.ProductionHalfZeroBin - snapshot.NbisHalfZeroBin);

        await Assert.That(Math.Abs(snapshot.ProductionQuantizedCoefficient - snapshot.NbisQuantizedCoefficient)).IsEqualTo(1);
        await Assert.That(qbinDelta).IsLessThan(0.001);
        await Assert.That(halfZeroBinDelta).IsLessThan(0.001);
    }

    private static WsqNbisCurrentMismatchProfile GetExpectedProfile(string fileName, double bitRate)
    {
        var caseKey = $"{fileName}|{bitRate:0.##}";
        return caseKey switch
        {
            "a002.raw|2.25" => new(201557, 38, 16, 41, 1, 2),
            "a018.raw|2.25" => new(465, 0, 9, 42, 270, 271),
            "a089.raw|2.25" => new(66271, 13, 43, 8, 6, 5),
            "a107.raw|2.25" => new(257586, 38, 28, 4, 3, 4),
            "b157.raw|2.25" => new(97598, 18, 53, 59, 1, 0),
            "b158.raw|2.25" => new(1270, 3, 8, 9, -1, 0),
            "cmp00001.raw|2.25" => new(12235, 11, 26, 25, -220, -221),
            "cmp00004.raw|2.25" => new(2057, 3, 9, 10, -54, -55),
            "cmp00005.raw|0.75" => new(37142, 17, 42, 14, -1, -2),
            "cmp00005.raw|2.25" => new(164, 0, 6, 8, -4463, -4464),
            "cmp00006.raw|2.25" => new(6649, 5, 31, 45, 39, 40),
            "cmp00007.raw|2.25" => new(15836, 14, 21, 2, 36, 37),
            "cmp00008.raw|2.25" => new(355488, 57, 90, 88, -2, -3),
            "cmp00011.raw|0.75" => new(18090, 13, 19, 24, -4, -3),
            "cmp00011.raw|2.25" => new(445, 0, 22, 5, 161, 160),
            "cmp00017.raw|2.25" => new(3791, 5, 30, 29, 78, 77),
            "sample_11.raw|2.25" => new(91038, 12, 65, 38, -39, -40),
            "sample_19.raw|0.75" => new(46463, 7, 88, 63, 7, 6),
            "sample_19.raw|2.25" => new(1770, 0, 35, 20, -1714, -1713),
            _ => throw new ArgumentOutOfRangeException(nameof(fileName), caseKey, "Unexpected current NBIS mismatch case."),
        };
    }

    private readonly record struct WsqNbisCurrentMismatchProfile(
        int MismatchIndex,
        int SubbandIndex,
        int Row,
        int Column,
        short ProductionQuantizedCoefficient,
        short NbisQuantizedCoefficient);
}
