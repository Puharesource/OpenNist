namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestAssertions;
using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq.Internal.Encoding;

[Category("Contract: WSQ - NIST Reference Quantization")]
internal sealed class WsqHighPrecisionEncoderBlockerTests
{
    [Test]
    [Skip("Enable when the remaining targeted 2.25 bpp blocker cases match the NIST coefficient corpus exactly.")]
    [DisplayName("Should match the remaining targeted exact NIST 2.25 bpp blocker references through the high-precision encoder analysis path")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldMatchTargetedExactNist225BlockerReferences(WsqEncodingReferenceCase testCase)
    {
        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath);
        var analysis = WsqEncoderAnalysisPipeline.Analyze(rawBytes, testCase.RawImage, new(testCase.BitRate));
        await WsqReferenceCoefficientAssertions.AssertExactMatchAsync(testCase, analysis);
    }
}
