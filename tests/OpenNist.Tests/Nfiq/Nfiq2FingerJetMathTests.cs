namespace OpenNist.Tests.Nfiq;

using OpenNist.Nfiq.Internal;
using OpenNist.Nfiq.Internal.FingerJet;

[Category("Unit: NFIQ2 - FingerJet Math")]
internal sealed class Nfiq2FingerJetMathTests
{
    [Test]
    [Arguments(0, 0)]
    [Arguments(1, 1)]
    [Arguments(2, 1)]
    [Arguments(3, 2)]
    [Arguments(199, 100)]
    [Arguments(255, 100)]
    public async Task ShouldMatchNativeConfidenceQuantization(byte confidence, byte expectedQuality)
    {
        await Assert.That(Nfiq2FingerJetMath.QualityFromConfidence(confidence)).IsEqualTo(expectedQuality);
    }

    [Test]
    [Arguments(416, 197, 167, 491)]
    [Arguments(-416, 197, 167, -491)]
    [Arguments(333, 5, 3, 555)]
    [Arguments(500, 197, 500, 197)]
    public async Task ShouldMatchNativeMulDivRounding(int x, int y, int z, int expected)
    {
        await Assert.That(Nfiq2FingerJetMath.MulDiv(x, y, z)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(5, 1, 3)]
    [Arguments(6, 1, 3)]
    [Arguments(-5, 1, -2)]
    [Arguments(127, 2, 32)]
    public async Task ShouldMatchNativeScaleDownRounding(int value, int bits, int expected)
    {
        await Assert.That(Nfiq2FingerJetMath.ScaleDown(value, bits)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(408, 433, 33)]
    [Arguments(433, 408, 31)]
    [Arguments(-408, 433, 95)]
    [Arguments(408, -433, 223)]
    public async Task ShouldMatchNativeAtan2Lookup(int c, int s, byte expected)
    {
        await Assert.That(Nfiq2FingerJetMath.Atan2(c, s)).IsEqualTo(expected);
    }
}
