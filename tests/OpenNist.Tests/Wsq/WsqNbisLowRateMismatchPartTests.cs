namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestDiagnostics;
using OpenNist.Tests.Wsq.TestFixtures;

[Category("Diagnostic: WSQ - NBIS Low-Rate Mismatch Parts")]
internal sealed class WsqNbisLowRateMismatchPartTests
{
    private static readonly Dictionary<string, WsqLowRateMismatchProfile> s_expectedProfiles =
        new(StringComparer.Ordinal)
        {
            ["cmp00005.raw"] = new(37142, 17, 42, 14, -1, -2),
            ["cmp00011.raw"] = new(18090, 13, 19, 24, -4, -3),
            ["sample_19.raw"] = new(46463, 7, 88, 63, 7, 6),
        };

    [Test]
    [DisplayName("Should isolate the remaining focused 0.75 bpp NBIS mismatch coordinates")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeNbis075FocusedMismatchReferenceCases))]
    public async Task ShouldIsolateTheRemainingFocused075BppNbisMismatchCoordinates(WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAgainstNbisAsync(testCase);
        var expected = GetExpectedProfile(testCase.FileName);

        await Assert.That(snapshot.MismatchIndex).IsEqualTo(expected.MismatchIndex);
        await Assert.That(snapshot.MismatchLocation.SubbandIndex).IsEqualTo(expected.SubbandIndex);
        await Assert.That(snapshot.MismatchLocation.Row).IsEqualTo(expected.Row);
        await Assert.That(snapshot.MismatchLocation.Column).IsEqualTo(expected.Column);
        await Assert.That(snapshot.ProductionQuantizedCoefficient).IsEqualTo(expected.ProductionQuantizedCoefficient);
        await Assert.That(snapshot.NbisQuantizedCoefficient).IsEqualTo(expected.NbisQuantizedCoefficient);
    }

    [Test]
    [DisplayName("Should distinguish the NIST-aligned and shared low-rate NBIS mismatch classes")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeNbis075FocusedMismatchReferenceCases))]
    public async Task ShouldDistinguishTheNistAlignedAndSharedLowRateNbisMismatchClasses(WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAgainstNbisAsync(testCase);

        await Assert.That(Math.Abs(snapshot.ProductionQuantizedCoefficient - snapshot.NbisQuantizedCoefficient)).IsEqualTo(1);
        await Assert.That(Math.Abs(snapshot.ProductionQuantizationBin - snapshot.NbisQuantizationBin)).IsLessThan(0.001);

        if (string.Equals(testCase.FileName, "cmp00011.raw", StringComparison.Ordinal))
        {
            await Assert.That(snapshot.ReferenceQuantizedCoefficient).IsEqualTo(snapshot.NbisQuantizedCoefficient);
            await Assert.That(snapshot.FloatWaveletCoefficient).IsEqualTo((float)snapshot.NbisWaveletCoefficient);
            return;
        }

        await Assert.That(snapshot.ReferenceQuantizedCoefficient).IsEqualTo(snapshot.ProductionQuantizedCoefficient);
    }

    private static WsqLowRateMismatchProfile GetExpectedProfile(string fileName)
    {
        if (s_expectedProfiles.TryGetValue(fileName, out var profile))
        {
            return profile;
        }

        throw new ArgumentOutOfRangeException(nameof(fileName), fileName, "Unexpected focused low-rate NBIS mismatch file.");
    }

    private readonly record struct WsqLowRateMismatchProfile(
        int MismatchIndex,
        int SubbandIndex,
        int Row,
        int Column,
        short ProductionQuantizedCoefficient,
        short NbisQuantizedCoefficient);
}
