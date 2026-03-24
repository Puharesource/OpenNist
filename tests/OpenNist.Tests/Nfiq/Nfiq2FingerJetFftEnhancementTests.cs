namespace OpenNist.Tests.Nfiq;

using OpenNist.Nfiq.Internal;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - FingerJet FFT Enhancement")]
internal sealed class Nfiq2FingerJetFftEnhancementTests
{
    [Test]
    [DisplayName("should reproduce native FingerJet fft enhancement for every bundled SFinGe image at 500 PPI")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceNativeFingerJetFftEnhancementForEveryBundledSFinGeImageAt500Ppi(
        Nfiq2ExampleCase exampleCase)
    {
        await AssertEnhancedImageMatchesNative(exampleCase.ImagePath, 500);
    }

    private static async Task AssertEnhancedImageMatchesNative(string imagePath, int pixelsPerInch)
    {
        var (pixels, width, height) = Nfiq2PortableGrayMapReader.Read(imagePath);
        var source = new Nfiq2FingerprintImage(pixels, width, height, ppi: (ushort)pixelsPerInch);
        var cropped = source.CopyRemovingNearWhiteFrame();
        var padded = PadToMinimumSize(cropped);
        var prepared = Nfiq2FingerJetImagePreparation.Prepare(padded);
        var managed = Nfiq2FingerJetFftEnhancement.Enhance(prepared);
        var native = Nfiq2FingerJetOracleReader.ReadEnhancedImage(imagePath, pixelsPerInch);

        await Assert.That(managed.Width).IsEqualTo(native.Width);
        await Assert.That(managed.Height).IsEqualTo(native.Height);
        await Assert.That(managed.PixelsPerInch).IsEqualTo(native.PixelsPerInch);
        await Assert.That(managed.XOffset).IsEqualTo(native.XOffset);
        await Assert.That(managed.YOffset).IsEqualTo(native.YOffset);
        await Assert.That(managed.OrientationMapWidth).IsEqualTo(native.OrientationMapWidth);
        await Assert.That(managed.OrientationMapSize).IsEqualTo(native.OrientationMapSize);
        AssertPixelsEqual(managed.Pixels.Span, native.Pixels);
    }

    private static void AssertPixelsEqual(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected)
    {
        if (actual.Length != expected.Length)
        {
            throw new InvalidOperationException($"Enhanced image length diverged from native FingerJet. expected={expected.Length}, actual={actual.Length}.");
        }

        for (var index = 0; index < actual.Length; index++)
        {
            if (actual[index] != expected[index])
            {
                throw new InvalidOperationException(
                    $"Enhanced image diverged from native FingerJet at index {index}. expected={expected[index]}, actual={actual[index]}.");
            }
        }
    }

    private static Nfiq2FingerprintImage PadToMinimumSize(Nfiq2FingerprintImage fingerprintImage)
    {
        const int minimumWidth = 196;
        const int minimumHeight = 196;

        if (fingerprintImage.Width >= minimumWidth && fingerprintImage.Height >= minimumHeight)
        {
            return fingerprintImage;
        }

        var paddedWidth = Math.Max(fingerprintImage.Width, minimumWidth);
        var paddedHeight = Math.Max(fingerprintImage.Height, minimumHeight);
        var paddedPixels = Enumerable.Repeat((byte)255, paddedWidth * paddedHeight).ToArray();
        var source = fingerprintImage.Pixels.Span;
        for (var row = 0; row < fingerprintImage.Height; row++)
        {
            source.Slice(row * fingerprintImage.Width, fingerprintImage.Width)
                .CopyTo(paddedPixels.AsSpan(row * paddedWidth, fingerprintImage.Width));
        }

        return new(paddedPixels, paddedWidth, paddedHeight, fingerprintImage.FingerCode, fingerprintImage.PixelsPerInch);
    }
}
