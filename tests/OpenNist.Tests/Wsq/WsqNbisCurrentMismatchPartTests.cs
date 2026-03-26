namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestDiagnostics;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Tests.Wsq.TestSupport;

[Category("Diagnostic: WSQ - NBIS Current Mismatch Parts")]
internal sealed class WsqNbisCurrentMismatchPartTests
{
    private static readonly Dictionary<string, WsqNbisCurrentMismatchProfile> s_expectedProfiles =
        new(StringComparer.Ordinal)
        {
            [WsqTestCaseDefinitions.CreateCaseKey("a002.raw", WsqTestCaseDefinitions.s_highBitRate)] = new(201557, 38, 16, 41, 1, 2),
            [WsqTestCaseDefinitions.CreateCaseKey("a018.raw", WsqTestCaseDefinitions.s_highBitRate)] = new(465, 0, 9, 42, 270, 271),
            [WsqTestCaseDefinitions.CreateCaseKey("a089.raw", WsqTestCaseDefinitions.s_highBitRate)] = new(66271, 13, 43, 8, 6, 5),
            [WsqTestCaseDefinitions.CreateCaseKey("a107.raw", WsqTestCaseDefinitions.s_highBitRate)] = new(257586, 38, 28, 4, 3, 4),
            [WsqTestCaseDefinitions.CreateCaseKey("b157.raw", WsqTestCaseDefinitions.s_highBitRate)] = new(97598, 18, 53, 59, 1, 0),
            [WsqTestCaseDefinitions.CreateCaseKey("b158.raw", WsqTestCaseDefinitions.s_highBitRate)] = new(1270, 3, 8, 9, -1, 0),
            [WsqTestCaseDefinitions.CreateCaseKey("cmp00001.raw", WsqTestCaseDefinitions.s_highBitRate)] = new(12235, 11, 26, 25, -220, -221),
            [WsqTestCaseDefinitions.CreateCaseKey("cmp00004.raw", WsqTestCaseDefinitions.s_highBitRate)] = new(2057, 3, 9, 10, -54, -55),
            [WsqTestCaseDefinitions.CreateCaseKey("cmp00005.raw", WsqTestCaseDefinitions.s_lowBitRate)] = new(37142, 17, 42, 14, -1, -2),
            [WsqTestCaseDefinitions.CreateCaseKey("cmp00005.raw", WsqTestCaseDefinitions.s_highBitRate)] = new(164, 0, 6, 8, -4463, -4464),
            [WsqTestCaseDefinitions.CreateCaseKey("cmp00006.raw", WsqTestCaseDefinitions.s_highBitRate)] = new(6649, 5, 31, 45, 39, 40),
            [WsqTestCaseDefinitions.CreateCaseKey("cmp00007.raw", WsqTestCaseDefinitions.s_highBitRate)] = new(15836, 14, 21, 2, 36, 37),
            [WsqTestCaseDefinitions.CreateCaseKey("cmp00008.raw", WsqTestCaseDefinitions.s_highBitRate)] = new(355488, 57, 90, 88, -2, -3),
            [WsqTestCaseDefinitions.CreateCaseKey("cmp00011.raw", WsqTestCaseDefinitions.s_lowBitRate)] = new(18090, 13, 19, 24, -4, -3),
            [WsqTestCaseDefinitions.CreateCaseKey("cmp00011.raw", WsqTestCaseDefinitions.s_highBitRate)] = new(445, 0, 22, 5, 161, 160),
            [WsqTestCaseDefinitions.CreateCaseKey("cmp00017.raw", WsqTestCaseDefinitions.s_highBitRate)] = new(3791, 5, 30, 29, 78, 77),
            [WsqTestCaseDefinitions.CreateCaseKey("sample_11.raw", WsqTestCaseDefinitions.s_highBitRate)] = new(91038, 12, 65, 38, -39, -40),
            [WsqTestCaseDefinitions.CreateCaseKey("sample_19.raw", WsqTestCaseDefinitions.s_lowBitRate)] = new(46463, 7, 88, 63, 7, 6),
            [WsqTestCaseDefinitions.CreateCaseKey("sample_19.raw", WsqTestCaseDefinitions.s_highBitRate)] = new(1770, 0, 35, 20, -1714, -1713),
        };

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
        var caseKey = WsqTestCaseDefinitions.CreateCaseKey(fileName, bitRate);
        if (s_expectedProfiles.TryGetValue(caseKey, out var profile))
        {
            return profile;
        }

        throw new ArgumentOutOfRangeException(nameof(fileName), caseKey, "Unexpected current NBIS mismatch case.");
    }

    private readonly record struct WsqNbisCurrentMismatchProfile(
        int MismatchIndex,
        int SubbandIndex,
        int Row,
        int Column,
        short ProductionQuantizedCoefficient,
        short NbisQuantizedCoefficient);
}
