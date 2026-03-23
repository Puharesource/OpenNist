namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestDiagnostics;
using OpenNist.Tests.Wsq.TestFixtures;

[Category("Diagnostic: WSQ - Encoder Blocker Parts")]
internal sealed class WsqHighPrecisionSubband0SensitivityTests
{
    [Test]
    [DisplayName("Should show the serialized subband 0 override now regresses earlier for every remaining blocker case")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldShowTheSerializedSubband0OverrideNowRegressesEarlierForEveryRemainingBlockerCase(
        WsqEncodingReferenceCase testCase)
    {
        var snapshot = await WsqHighPrecisionSubband0SensitivitySnapshotBuilder.CreateAsync(testCase);

        await Assert.That(snapshot.CurrentMismatchIndex >= 0).IsTrue();
        await Assert.That(snapshot.SubbandZeroSerializedOverrideMismatchIndex >= 0).IsTrue();
        await Assert.That(snapshot.SubbandZeroSerializedOverrideMismatchIndex < snapshot.CurrentMismatchIndex).IsTrue();
    }

    [Test]
    [Skip("This NIST-side contrast set was calibrated to the earlier serialized-bin diagnostic baseline. Re-enable after rebasing it to the current NBIS 5.0.0 codestream target.")]
    [DisplayName("Should show the serialized subband 0 override is unsafe for the curated exact 2.25 bpp contrast set")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionSerializedBinSensitiveExactReferenceCases))]
    public async Task ShouldShowTheSerializedSubband0OverrideIsUnsafeForTheCuratedExact225BppContrastSet(
        WsqEncodingReferenceCase testCase)
    {
        await Task.CompletedTask;
    }
}
