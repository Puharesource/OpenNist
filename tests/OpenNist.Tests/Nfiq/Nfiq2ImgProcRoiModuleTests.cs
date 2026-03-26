namespace OpenNist.Tests.Nfiq;

using System.Globalization;
using OpenNist.Nfiq;
using OpenNist.Nfiq.Internal;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - Managed ImgProcROI Module")]
internal sealed class Nfiq2ImgProcRoiModuleTests
{
    private const double s_nativeFloatingPointTolerance = 0.02;
    private static readonly Nfiq2Algorithm s_algorithm = Nfiq2TestContext.Algorithm;

    [Test]
    [DisplayName("should reproduce the official ROI mean for every bundled SFinGe image")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceTheOfficialRoiMeanForEveryBundledSfinGeImage(Nfiq2ExampleCase exampleCase)
    {
        var (pixels, width, height) = Nfiq2PortableGrayMapReader.Read(exampleCase.ImagePath);
        var sourceImage = new Nfiq2FingerprintImage(pixels, width, height);
        var croppedImage = sourceImage.CopyRemovingNearWhiteFrame();
        var managed = Nfiq2ImgProcRoiModule.Compute(croppedImage);

        var native = await s_algorithm.AnalyzeFileAsync(exampleCase.ImagePath).ConfigureAwait(false);

        AssertApproximatelyEqual(
            $"{exampleCase.Name} ImgProcROIArea_Mean",
            native.NativeQualityMeasures["ImgProcROIArea_Mean"],
            managed.MeanOfRoiPixels);
    }

    [Test]
    [DisplayName("should report a white-image ROI result when no ROI pixels are present")]
    public async Task ShouldReportAWhiteImageRoiResultWhenNoRoiPixelsArePresent()
    {
        const int width = 32;
        const int height = 32;
        var pixels = Enumerable.Repeat((byte)255, width * height).ToArray();

        var result = Nfiq2ImgProcRoiModule.Compute(new(pixels, width, height));

        await Assert.That(result.RoiPixels).IsEqualTo(0U);
        await Assert.That(result.MeanOfRoiPixels).IsEqualTo(255.0);
        await Assert.That(result.RoiBlocks.Count).IsEqualTo(0);
        await Assert.That(result.ImagePixels).IsEqualTo((uint)(width * height));
    }

    private static void AssertApproximatelyEqual(string context, double? expectedValue, double actualValue)
    {
        if (expectedValue is null)
        {
            throw new InvalidOperationException($"{context} was NA in the official NFIQ 2 result.");
        }

        if (Math.Abs(expectedValue.Value - actualValue) > s_nativeFloatingPointTolerance)
        {
            throw new InvalidOperationException(
                $"{context} diverged from the official NFIQ 2 value. "
                + $"expected={expectedValue.Value.ToString(CultureInfo.InvariantCulture)}, "
                + $"actual={actualValue.ToString(CultureInfo.InvariantCulture)}.");
        }
    }
}
