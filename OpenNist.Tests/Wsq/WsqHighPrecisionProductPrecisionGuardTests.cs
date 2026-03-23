namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestDiagnostics;
using OpenNist.Tests.Wsq.TestFixtures;

[Category("Diagnostic: WSQ - Encoder QBin Synthesis")]
internal sealed class WsqHighPrecisionProductPrecisionGuardTests
{
    [Test]
    [Skip("NIST-only high-rate product-precision guards are diagnostic only under the NBIS 5.0.0 exact codestream baseline.")]
    [DisplayName("Should show float product precision plus scale casting is unsafe for the recovered exact 2.25 bpp guard cases")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionProductPrecisionGuardReferenceCases))]
    public async Task ShouldShowFloatProductPrecisionPlusScaleCastingIsUnsafeForTheRecoveredExact225BppGuardCases(
        WsqEncodingReferenceCase testCase)
    {
        var snapshot = await WsqHighPrecisionProductPrecisionGuardSnapshotBuilder.CreateAsync(testCase);

        await Assert.That(snapshot.CurrentFirstMismatchIndex).IsEqualTo(-1);
        await Assert.That(snapshot.ProductAndScaleFirstMismatchIndex >= 0).IsTrue();
    }
}
