namespace OpenNist.Tests.Nfiq;

using OpenNist.Nfiq.Internal;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - FingerJet BifFilter")]
internal sealed class Nfiq2FingerJetBifFilterSupportTests
{
    [Test]
    public async Task ShouldReproduceNativeBiffiltSample()
    {
        var phasemap = CreateSyntheticPhasemap(width: 40, height: 40);
        var sampleX = (20 << 7) + 43;
        var sampleY = (18 << 7) + 77;

        var managed = Nfiq2FingerJetBifFilterSupport.SampleImage(phasemap, width: 40, sampleX, sampleY);
        var native = Nfiq2FingerJetOracleReader.ReadBiffiltSample(
            width: 40,
            size: phasemap.Length,
            x: sampleX,
            y: sampleY,
            phasemap);

        await Assert.That(managed).IsEqualTo(native);
    }

    [Test]
    [Arguments(64)]
    [Arguments(80)]
    [Arguments(112)]
    public async Task ShouldReproduceNativeBiffiltEvaluate(int angle)
    {
        var phasemap = CreateSyntheticPhasemap(width: 40, height: 40);
        var c = Nfiq2FingerJetMath.Cos(angle);
        var s = Nfiq2FingerJetMath.Sin(angle);

        var managed = Nfiq2FingerJetBifFilterSupport.Evaluate(
            phasemap,
            width: 40,
            x: 20,
            y: 20,
            c,
            s);

        var native = Nfiq2FingerJetOracleReader.ReadBiffiltEvaluate(
            width: 40,
            size: phasemap.Length,
            x: 20,
            y: 20,
            c,
            s,
            phasemap);

        await Assert.That(managed.Confirmed).IsEqualTo(native.Confirmed);
        await Assert.That(managed.Type).IsEqualTo(native.Type);
        await Assert.That(managed.Rotate180).IsEqualTo(native.Rotate180);
        await Assert.That(managed.XOffset).IsEqualTo(native.XOffset);
        await Assert.That(managed.YOffset).IsEqualTo(native.YOffset);
        await Assert.That(managed.Period).IsEqualTo(native.Period);
        await Assert.That(managed.Confidence).IsEqualTo(native.Confidence);
    }

    private static byte[] CreateSyntheticPhasemap(int width, int height)
    {
        var phasemap = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = (byte)160;
                var stripe = (x / 3) & 1;
                if (stripe == 0)
                {
                    value = (byte)(value + 60);
                }

                var centerDistance = Math.Abs(y - (height / 2));
                if (centerDistance <= 2)
                {
                    value = (byte)Math.Min(250, value + 20);
                }

                if (x < 2 || y < 2 || x >= width - 2 || y >= height - 2)
                {
                    value = 127;
                }

                phasemap[(y * width) + x] = value;
            }
        }

        return phasemap;
    }
}
