namespace OpenNist.Tests.Nfiq;

using OpenNist.Nfiq.Internal;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - FingerJet Orientation Map")]
internal sealed class Nfiq2FingerJetOrientationMapTests
{
    [Test]
    [DisplayName("should reproduce native enhanced raw orientation maps for every bundled SFinGe image at 500 PPI")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceNativeEnhancedRawOrientationMapsForEveryBundledSfinGeImageAt500Ppi(
        Nfiq2ExampleCase exampleCase)
    {
        var (_, enhanced) = CreatePreparedImages(exampleCase.ImagePath, 500);
        var managed = Nfiq2FingerJetOrientationMap.ComputeRaw(enhanced);
        var native = Nfiq2FingerJetOracleReader.ReadEnhancedRawOrientationMap(exampleCase.ImagePath, 500);

        await AssertOrientationMapEqual(managed, native);
    }

    [Test]
    [DisplayName("should reproduce native enhanced orientation maps for every bundled SFinGe image at 500 PPI")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceNativeEnhancedOrientationMapsForEveryBundledSfinGeImageAt500Ppi(
        Nfiq2ExampleCase exampleCase)
    {
        var (prepared, enhanced) = CreatePreparedImages(exampleCase.ImagePath, 500);
        var managed = Nfiq2FingerJetOrientationMap.Compute(prepared, enhanced);
        var native = Nfiq2FingerJetOracleReader.ReadEnhancedOrientationMap(exampleCase.ImagePath, 500);

        await AssertOrientationMapEqual(managed, native);
    }

    private static (Nfiq2FingerJetPreparedImage Prepared, Nfiq2FingerJetPreparedImage Enhanced) CreatePreparedImages(
        string imagePath,
        int pixelsPerInch)
    {
        var (pixels, width, height) = Nfiq2PortableGrayMapReader.Read(imagePath);
        var source = new Nfiq2FingerprintImage(pixels, width, height, ppi: (ushort)pixelsPerInch);
        var cropped = source.CopyRemovingNearWhiteFrame();
        var padded = PadToMinimumSize(cropped);
        var prepared = Nfiq2FingerJetImagePreparation.Prepare(padded);
        var enhanced = Nfiq2FingerJetFftEnhancement.Enhance(prepared);
        return (prepared, enhanced);
    }

    private static async Task AssertOrientationMapEqual(
        Nfiq2FingerJetOrientationMapResult managed,
        Nfiq2FingerJetOrientationMapOracleResult native)
    {
        await Assert.That(managed.Width).IsEqualTo(native.OrientationMapWidth);
        await Assert.That(managed.Size).IsEqualTo(native.OrientationMapSize);
        AssertComplexEqual(managed.Orientation, native.Orientation);
        AssertByteEqual(managed.Footprint.ToArray(), native.Footprint);
    }

    private static void AssertComplexEqual(
        IReadOnlyList<Nfiq2FingerJetComplex> actual,
        IReadOnlyList<Nfiq2FingerJetComplex> expected)
    {
        if (actual.Count != expected.Count)
        {
            throw new InvalidOperationException($"Orientation vector length diverged from native FingerJet. expected={expected.Count}, actual={actual.Count}.");
        }

        for (var index = 0; index < actual.Count; index++)
        {
            if (actual[index] != expected[index])
            {
                throw new InvalidOperationException(
                    $"Orientation vector diverged from native FingerJet at index {index}. expected={expected[index]}, actual={actual[index]}.");
            }
        }
    }

    private static void AssertByteEqual(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected)
    {
        if (actual.Length != expected.Length)
        {
            throw new InvalidOperationException($"Footprint length diverged from native FingerJet. expected={expected.Length}, actual={actual.Length}.");
        }

        for (var index = 0; index < actual.Length; index++)
        {
            if (actual[index] != expected[index])
            {
                throw new InvalidOperationException(
                    $"Footprint diverged from native FingerJet at index {index}. expected={expected[index]}, actual={actual[index]}.");
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
