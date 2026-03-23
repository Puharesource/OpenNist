namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestDiagnostics;
using OpenNist.Tests.Wsq.TestFixtures;

[Category("Diagnostic: WSQ - NBIS Low-Rate DQT Mismatch Parts")]
internal sealed class WsqNbisLowRateDqtMismatchTests
{
    [Test]
    [DisplayName("Should isolate the exact first DQT field for the focused 0.75 bpp NBIS codestream mismatches")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeNbis075DqtOnlyMismatchReferenceCases))]
    public async Task ShouldIsolateTheExactFirstDqtFieldForTheFocused075BppNbisCodestreamMismatches(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqNbisLowRateDqtSnapshotBuilder.CreateAsync(testCase);
        var expected = GetExpectedProfile(testCase.FileName);

        await Assert.That(snapshot.FirstByteMismatchIndex).IsEqualTo(expected.FirstByteMismatchIndex);
        await Assert.That(snapshot.SubbandIndex).IsEqualTo(expected.SubbandIndex);
        await Assert.That(snapshot.FieldKind).IsEqualTo(expected.FieldKind);
        await Assert.That(snapshot.ManagedRawValue).IsEqualTo(expected.ManagedRawValue);
        await Assert.That(snapshot.NbisRawValue).IsEqualTo(expected.NbisRawValue);
        await Assert.That((int)snapshot.ManagedScale).IsEqualTo(3);
        await Assert.That((int)snapshot.NbisScale).IsEqualTo(3);
    }

    [Test]
    [DisplayName("Should show the focused 0.75 bpp NBIS DQT-only mismatches already exist in the pre-serialization artifacts")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeNbis075DqtOnlyMismatchReferenceCases))]
    public async Task ShouldShowTheFocused075BppNbisDqtOnlyMismatchesAlreadyExistInThePreSerializationArtifacts(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqNbisLowRateDqtSnapshotBuilder.CreateAsync(testCase);

        await Assert.That(snapshot.ManagedArtifactRawValue).IsEqualTo(snapshot.ManagedRawValue);
        await Assert.That((int)snapshot.ManagedArtifactScale).IsEqualTo((int)snapshot.ManagedScale);
        await Assert.That(snapshot.NbisArtifactRawValue).IsEqualTo(snapshot.NbisRawValue);
        await Assert.That((int)snapshot.NbisArtifactScale).IsEqualTo((int)snapshot.NbisScale);
        await Assert.That(Math.Abs(snapshot.ManagedRawValue - snapshot.NbisRawValue)).IsEqualTo(1);
    }

    [Test]
    [DisplayName("Should show a018 remains a low-rate quantizer-stage overshoot even on NBIS 5.0.0 wavelet data")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeNbis075DqtOnlyMismatchReferenceCases))]
    public async Task ShouldShowA018RemainsALowRateQuantizerStageOvershootEvenOnNbis500WaveletData(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable() || !string.Equals(testCase.FileName, "a018.raw", StringComparison.Ordinal))
        {
            return;
        }

        var snapshot = await WsqNbisLowRateDqtSnapshotBuilder.CreateAsync(testCase);

        await Assert.That(snapshot.FieldKind).IsEqualTo(WsqDqtFieldKind.QuantizationBin);
        await Assert.That(snapshot.ManagedOnNbisWaveletQuantizationBin > snapshot.NbisArtifactRawValue / 1000.0f).IsTrue();
        await Assert.That(snapshot.ManagedArtifactRawValue).IsEqualTo((ushort)18546);
        await Assert.That(snapshot.NbisArtifactRawValue).IsEqualTo((ushort)18545);
    }

    private static WsqLowRateDqtMismatchProfile GetExpectedProfile(string fileName)
    {
        return fileName switch
        {
            "a001.raw" => new(488, 48, WsqDqtFieldKind.ZeroBin, 30029, 30030),
            "a018.raw" => new(311, 19, WsqDqtFieldKind.QuantizationBin, 18546, 18545),
            "a107.raw" => new(482, 47, WsqDqtFieldKind.ZeroBin, 26202, 26201),
            _ => throw new ArgumentOutOfRangeException(nameof(fileName), fileName, "Unexpected focused low-rate NBIS DQT mismatch file."),
        };
    }

    private readonly record struct WsqLowRateDqtMismatchProfile(
        int FirstByteMismatchIndex,
        int SubbandIndex,
        WsqDqtFieldKind FieldKind,
        ushort ManagedRawValue,
        ushort NbisRawValue);
}
