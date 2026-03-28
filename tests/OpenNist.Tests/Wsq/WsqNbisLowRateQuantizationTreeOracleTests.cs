namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Metadata;

[Category("Diagnostic: WSQ - NBIS Low-Rate Quantization Tree Oracle")]
internal sealed class WsqNbisLowRateQuantizationTreeOracleTests
{
    [Test]
    [DisplayName("Should match the NBIS quantization-tree geometry for the focused 0.75 bpp DQT-only mismatch cases")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeNbis075DqtOnlyMismatchReferenceCases))]
    public async Task ShouldMatchTheNbisQuantizationTreeGeometryForTheFocused075BppDqtOnlyMismatchCases(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var nbisAnalysis = await WsqNbisOracleReader.ReadAnalysisAsync(testCase).ConfigureAwait(false);
        WsqWaveletTreeBuilder.Build(
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            out _,
            out var managedQuantizationTree);

        await Assert.That(nbisAnalysis.QuantizationTree.Length).IsEqualTo(WsqConstants.NumberOfSubbands);

        for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
        {
            var managedNode = managedQuantizationTree[subband];
            var nbisNode = nbisAnalysis.QuantizationTree[subband];

            await Assert.That(managedNode.X).IsEqualTo(nbisNode.X);
            await Assert.That(managedNode.Y).IsEqualTo(nbisNode.Y);
            await Assert.That(managedNode.Width).IsEqualTo(nbisNode.Width);
            await Assert.That(managedNode.Height).IsEqualTo(nbisNode.Height);
        }
    }
}
