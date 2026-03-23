namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestDiagnostics;
using OpenNist.Tests.Wsq.TestFixtures;

[Category("Diagnostic: WSQ - NBIS High-Rate DQT Mismatch Parts")]
internal sealed class WsqNbisHighRateDqtMismatchTests
{
    [Test]
    [DisplayName("Should isolate the representative 2.25 bpp NBIS DQT-only mismatch field")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeNbis225RepresentativeDqtOnlyMismatchReferenceCases))]
    public async Task ShouldIsolateTheRepresentative225BppNbisDqtOnlyMismatchField(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqNbisHighRateDqtSnapshotBuilder.CreateAsync(testCase).ConfigureAwait(false);
        var expected = GetExpectedProfile(testCase.FileName);

        await Assert.That(snapshot.FirstByteMismatchIndex).IsEqualTo(expected.FirstByteMismatchIndex);
        await Assert.That(snapshot.SubbandIndex).IsEqualTo(expected.SubbandIndex);
        await Assert.That(snapshot.FieldKind).IsEqualTo(WsqDqtFieldKind.QuantizationBin);
        await Assert.That(snapshot.ManagedRawValue).IsEqualTo(expected.ManagedRawValue);
        await Assert.That(snapshot.NbisRawValue).IsEqualTo(expected.NbisRawValue);
        await Assert.That((int)snapshot.ManagedScale).IsEqualTo(4);
        await Assert.That((int)snapshot.NbisScale).IsEqualTo(4);
    }

    [Test]
    [DisplayName("Should show the representative 2.25 bpp NBIS DQT-only mismatches are one-LSB high only in the production high-rate artifacts")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeNbis225RepresentativeDqtOnlyMismatchReferenceCases))]
    public async Task ShouldShowTheRepresentative225BppNbisDqtOnlyMismatchesAreOneLsbHighOnlyInTheProductionHighRateArtifacts(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqNbisHighRateDqtSnapshotBuilder.CreateAsync(testCase).ConfigureAwait(false);

        await Assert.That(snapshot.CurrentArtifactRawValue).IsEqualTo(snapshot.ManagedRawValue);
        await Assert.That((int)snapshot.CurrentArtifactScale).IsEqualTo((int)snapshot.ManagedScale);
        await Assert.That(snapshot.FloatArtifactRawValue).IsEqualTo(snapshot.NbisRawValue);
        await Assert.That((int)snapshot.FloatArtifactScale).IsEqualTo((int)snapshot.NbisScale);
        await Assert.That(snapshot.NbisArtifactRawValue).IsEqualTo(snapshot.NbisRawValue);
        await Assert.That((int)snapshot.NbisArtifactScale).IsEqualTo((int)snapshot.NbisScale);
        await Assert.That(snapshot.ManagedRawValue - snapshot.NbisRawValue).IsEqualTo((ushort)1);
    }

    [Test]
    [DisplayName("Should show the representative 2.25 bpp NBIS DQT-only mismatches already have exact coefficient streams")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeNbis225RepresentativeDqtOnlyMismatchReferenceCases))]
    public async Task ShouldShowTheRepresentative225BppNbisDqtOnlyMismatchesAlreadyHaveExactCoefficientStreams(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqNbisHighRateDqtSnapshotBuilder.CreateAsync(testCase).ConfigureAwait(false);

        await Assert.That(snapshot.CoefficientsMatchExactly).IsTrue();
        await Assert.That(snapshot.FirstCoefficientMismatchIndex).IsEqualTo(-1);
    }

    private static WsqHighRateDqtMismatchProfile GetExpectedProfile(string fileName)
    {
        return fileName switch
        {
            "a001.raw" => new(152, 15, 40401, 40400),
            "a076.raw" => new(147, 14, 63101, 63100),
            "cmp00008.raw" => new(182, 21, 42697, 42696),
            _ => throw new ArgumentOutOfRangeException(nameof(fileName), fileName, "Unexpected representative high-rate NBIS DQT mismatch file."),
        };
    }

    private readonly record struct WsqHighRateDqtMismatchProfile(
        int FirstByteMismatchIndex,
        int SubbandIndex,
        ushort ManagedRawValue,
        ushort NbisRawValue);
}
