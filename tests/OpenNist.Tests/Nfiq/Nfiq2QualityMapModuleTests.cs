namespace OpenNist.Tests.Nfiq;

using System.Globalization;
using OpenNist.Nfiq;
using OpenNist.Nfiq.Internal;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - Managed QualityMap Module")]
internal sealed class Nfiq2QualityMapModuleTests
{
    private const double NativeFloatingPointTolerance = 0.5;
    private static readonly Nfiq2Algorithm s_algorithm = Nfiq2TestContext.Algorithm;

    [Test]
    [DisplayName("should reproduce the official ROI coherence features for every bundled SFinGe image")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceTheOfficialRoiCoherenceFeaturesForEveryBundledSfinGeImage(Nfiq2ExampleCase exampleCase)
    {
        var (pixels, width, height) = Nfiq2PortableGrayMapReader.Read(exampleCase.ImagePath);
        var sourceImage = new Nfiq2FingerprintImage(pixels, width, height);
        var croppedImage = sourceImage.CopyRemovingNearWhiteFrame();
        var roiResult = Nfiq2ImgProcRoiModule.Compute(croppedImage);
        var managed = Nfiq2QualityMapModule.Compute(croppedImage, roiResult);

        var native = await s_algorithm.AnalyzeFileAsync(exampleCase.ImagePath).ConfigureAwait(false);

        AssertApproximatelyEqual(
            $"{exampleCase.Name} OrientationMap_ROIFilter_CoherenceRel",
            native.NativeQualityMeasures["OrientationMap_ROIFilter_CoherenceRel"],
            managed.CoherenceRelative);
        AssertApproximatelyEqual(
            $"{exampleCase.Name} OrientationMap_ROIFilter_CoherenceSum",
            native.NativeQualityMeasures["OrientationMap_ROIFilter_CoherenceSum"],
            managed.CoherenceSum);
    }

    private static void AssertApproximatelyEqual(string context, double? expectedValue, double actualValue)
    {
        if (expectedValue is null)
        {
            throw new InvalidOperationException($"{context} was NA in the official NFIQ 2 result.");
        }

        if (Math.Abs(expectedValue.Value - actualValue) > NativeFloatingPointTolerance)
        {
            throw new InvalidOperationException(
                $"{context} diverged from the official NFIQ 2 value. "
                + $"expected={expectedValue.Value.ToString(CultureInfo.InvariantCulture)}, "
                + $"actual={actualValue.ToString(CultureInfo.InvariantCulture)}.");
        }
    }
}
