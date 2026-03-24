namespace OpenNist.Tests.Nfiq;

using OpenNist.Nfiq.Internal;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - FingerJet Minutia Extractor")]
internal sealed class Nfiq2FingerJetMinutiaExtractorTests
{
    [Test]
    public async Task ShouldReproduceNativeRawExtractionOnSyntheticPhasemap()
    {
        var phasemap = CreateSyntheticPhasemap(width: 48, height: 48);

        var managed = Nfiq2FingerJetMinutiaExtractor.ExtractRaw(phasemap, width: 48, capacity: 32);
        var native = Nfiq2FingerJetOracleReader.ReadExtractedRawMinutiaeFromPhasemap(
            width: 48,
            size: phasemap.Length,
            capacity: 32,
            phasemap);

        await Assert.That(managed.Count).IsEqualTo(native.Count);
        for (var index = 0; index < managed.Count; index++)
        {
            await Assert.That(managed[index].X).IsEqualTo(native[index].X);
            await Assert.That(managed[index].Y).IsEqualTo(native[index].Y);
            await Assert.That(managed[index].Angle).IsEqualTo(native[index].Angle);
            await Assert.That(managed[index].Confidence).IsEqualTo(native[index].Confidence);
            await Assert.That(managed[index].Type).IsEqualTo(native[index].Type);
        }
    }

    private static byte[] CreateSyntheticPhasemap(int width, int height)
    {
        var phasemap = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                byte value;
                if (x < width / 2)
                {
                    value = (byte)(((y / 4) & 1) == 0 ? 208 : 32);
                }
                else
                {
                    value = (byte)(((x / 4) & 1) == 0 ? 176 : 48);
                }

                if (Math.Abs(x - (width / 2)) <= 2 || Math.Abs(y - (height / 2)) <= 2)
                {
                    value = (byte)Math.Min(240, value + 32);
                }

                if (x < 4 || y < 4 || x >= width - 4 || y >= height - 4)
                {
                    value = 127;
                }

                phasemap[(y * width) + x] = value;
            }
        }

        return phasemap;
    }
}
