namespace OpenNist.Tests.Nfiq;

using System.Text;
using OpenNist.Nfiq.Internal;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - FingerJet Image Preparation")]
internal sealed class Nfiq2FingerJetImagePreparationTests
{
    [Test]
    [DisplayName("should reproduce native FingerJet prepared images for every bundled SFinGe image at 500 PPI")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceNativePreparedImagesForEveryBundledSfinGeImageAt500Ppi(Nfiq2ExampleCase exampleCase)
    {
        await AssertPreparedImageMatchesNative(exampleCase.ImagePath, 500);
    }

    [Test]
    [DisplayName("should reproduce the native near-333 copy path for a representative bundled image")]
    public async Task ShouldReproduceTheNativeNear333CopyPathForARepresentativeBundledImage()
    {
        var imagePath = Nfiq2TestDataSources.EnumerateExampleCases().First().ImagePath;
        await AssertPreparedImageMatchesNative(imagePath, 333);
    }

    [Test]
    [DisplayName("should reproduce the native general resample path for a representative bundled image")]
    public async Task ShouldReproduceTheNativeGeneralResamplePathForARepresentativeBundledImage()
    {
        var imagePath = Nfiq2TestDataSources.EnumerateExampleCases().First().ImagePath;
        await AssertPreparedImageMatchesNative(imagePath, 640);
    }

    [Test]
    [DisplayName("should reproduce the native 333dpi center-crop path for a synthetic oversized image")]
    public async Task ShouldReproduceTheNative333DpiCenterCropPathForASyntheticOversizedImage()
    {
        var pixels = new byte[416 * 560];
        for (var index = 0; index < pixels.Length; index++)
        {
            pixels[index] = (byte)(index % 251);
        }

        using var tempImage = CreateTemporaryPortableGrayMap(pixels, 416, 560);
        await AssertPreparedImageMatchesNative(tempImage.Path, 333);
    }

    private static async Task AssertPreparedImageMatchesNative(string imagePath, int pixelsPerInch)
    {
        var (pixels, width, height) = Nfiq2PortableGrayMapReader.Read(imagePath);
        var managed = Nfiq2FingerJetImagePreparation.Prepare(new(pixels, width, height, ppi: (ushort)pixelsPerInch));
        var native = Nfiq2FingerJetOracleReader.ReadPreparedImage(imagePath, pixelsPerInch);

        await Assert.That(managed.Width).IsEqualTo(native.Width);
        await Assert.That(managed.Height).IsEqualTo(native.Height);
        await Assert.That(managed.PixelsPerInch).IsEqualTo(native.PixelsPerInch);
        await Assert.That(managed.XOffset).IsEqualTo(native.XOffset);
        await Assert.That(managed.YOffset).IsEqualTo(native.YOffset);
        await Assert.That(managed.OrientationMapWidth).IsEqualTo(native.OrientationMapWidth);
        await Assert.That(managed.OrientationMapSize).IsEqualTo(native.OrientationMapSize);
        AssertPixelsEqual(managed.Pixels.Span, native.Pixels);
    }

    private static TemporaryPortableGrayMap CreateTemporaryPortableGrayMap(byte[] pixels, int width, int height)
    {
        var path = Path.Combine(Path.GetTempPath(), "OpenNist.Nfiq", $"{Guid.NewGuid():N}.pgm");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var stream = File.Create(path);
        var header = Encoding.ASCII.GetBytes($"P5\n{width} {height}\n255\n");
        stream.Write(header);
        stream.Write(pixels);
        stream.Flush();

        return new(path);
    }

    private sealed class TemporaryPortableGrayMap(string path) : IDisposable
    {
        public string Path { get; } = path;

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }

    private static void AssertPixelsEqual(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected)
    {
        if (actual.Length != expected.Length)
        {
            throw new InvalidOperationException($"Prepared image length diverged from native FingerJet. expected={expected.Length}, actual={actual.Length}.");
        }

        for (var index = 0; index < actual.Length; index++)
        {
            if (actual[index] != expected[index])
            {
                throw new InvalidOperationException(
                    $"Prepared image diverged from native FingerJet at index {index}. expected={expected[index]}, actual={actual[index]}.");
            }
        }
    }
}
