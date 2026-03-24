namespace OpenNist.Tests.Nfiq;

using System.Globalization;
using OpenNist.Nfiq;
using OpenNist.Nfiq.Internal;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - Managed Mu Module")]
internal sealed class Nfiq2MuModuleTests
{
    private static readonly Nfiq2Algorithm s_algorithm = Nfiq2TestContext.Algorithm;

    [Test]
    [DisplayName("should reproduce the official Mu, MMB, and sigma values for every bundled SFinGe image")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceTheOfficialMuMmbAndSigmaValuesForEveryBundledSfinGeImage(Nfiq2ExampleCase exampleCase)
    {
        var (pixels, width, height) = Nfiq2PortableGrayMapReader.Read(exampleCase.ImagePath);
        var sourceImage = new Nfiq2FingerprintImage(pixels, width, height);
        var croppedImage = sourceImage.CopyRemovingNearWhiteFrame();
        var managed = Nfiq2MuModule.Compute(croppedImage);

        var native = await s_algorithm.AnalyzeFileAsync(exampleCase.ImagePath).ConfigureAwait(false);

        AssertApproximatelyEqual($"{exampleCase.Name} MMB", native.NativeQualityMeasures["MMB"], managed.MeanOfBlockMeans);
        AssertApproximatelyEqual($"{exampleCase.Name} Mu", native.NativeQualityMeasures["Mu"], managed.ImageMean);
        AssertApproximatelyEqual($"{exampleCase.Name} Sigma", native.ActionableFeedback["UniformImage"], managed.Sigma);
    }

    [Test]
    [DisplayName("should match the official Mu path on a synthetic image with a near-white border")]
    public async Task ShouldMatchTheOfficialMuPathOnASyntheticImageWithANearWhiteBorder()
    {
        const int width = 16;
        const int height = 14;
        var pixels = new byte[width * height];
        Array.Fill(pixels, (byte)255);

        for (var row = 2; row < height - 2; row++)
        {
            for (var column = 3; column < width - 3; column++)
            {
                pixels[(row * width) + column] = checked((byte)(40 + ((row + column) % 120)));
            }
        }

        var sourceImage = new Nfiq2FingerprintImage(pixels, width, height);
        var croppedImage = sourceImage.CopyRemovingNearWhiteFrame();
        var managed = Nfiq2MuModule.Compute(croppedImage);

        await Assert.That(croppedImage.Width).IsEqualTo(width - 6);
        await Assert.That(croppedImage.Height).IsEqualTo(height - 4);
        AssertApproximatelyEqual("Synthetic MMB", 54.0, managed.MeanOfBlockMeans);
        AssertApproximatelyEqual("Synthetic Mu", 54.0, managed.ImageMean);
        AssertApproximatelyEqual("Synthetic Sigma", 4.06201920231798, managed.Sigma);
    }

    private static void AssertApproximatelyEqual(string context, double? expectedValue, double actualValue)
    {
        if (expectedValue is null)
        {
            throw new InvalidOperationException($"{context} was NA in the official NFIQ 2 result.");
        }

        if (Math.Abs(expectedValue.Value - actualValue) > 0.001)
        {
            throw new InvalidOperationException(
                $"{context} diverged from the official NFIQ 2 value. "
                + $"expected={expectedValue.Value.ToString(CultureInfo.InvariantCulture)}, "
                + $"actual={actualValue.ToString(CultureInfo.InvariantCulture)}.");
        }
    }
}
