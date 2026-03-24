namespace OpenNist.Tests.Nfiq;

using System.Globalization;
using OpenNist.Nfiq;
using OpenNist.Nfiq.Internal;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - Managed OCLHistogram Module")]
internal sealed class Nfiq2OclHistogramModuleTests
{
    private const double NativeFloatingPointTolerance = 0.02;
    private static readonly string[] s_featureNames =
    [
        "OCL_Bin10_0",
        "OCL_Bin10_1",
        "OCL_Bin10_2",
        "OCL_Bin10_3",
        "OCL_Bin10_4",
        "OCL_Bin10_5",
        "OCL_Bin10_6",
        "OCL_Bin10_7",
        "OCL_Bin10_8",
        "OCL_Bin10_9",
        "OCL_Bin10_Mean",
        "OCL_Bin10_StdDev",
    ];

    private static readonly Nfiq2Algorithm s_algorithm = Nfiq2TestContext.Algorithm;

    [Test]
    [DisplayName("should reproduce the official OCL histogram features for every bundled SFinGe image")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceTheOfficialOclHistogramFeaturesForEveryBundledSfinGeImage(Nfiq2ExampleCase exampleCase)
    {
        var (pixels, width, height) = Nfiq2PortableGrayMapReader.Read(exampleCase.ImagePath);
        var sourceImage = new Nfiq2FingerprintImage(pixels, width, height);
        var croppedImage = sourceImage.CopyRemovingNearWhiteFrame();
        var managed = Nfiq2OclHistogramModule.Compute(croppedImage);

        var native = await s_algorithm.AnalyzeFileAsync(exampleCase.ImagePath).ConfigureAwait(false);

        foreach (var featureName in s_featureNames)
        {
            AssertApproximatelyEqual(
                $"{exampleCase.Name} {featureName}",
                native.NativeQualityMeasures[featureName],
                managed.Features[featureName]);
        }
    }

    [Test]
    [DisplayName("should exclude flat blocks from the OCL histogram just like the native implementation")]
    public async Task ShouldExcludeFlatBlocksFromTheOclHistogramJustLikeTheNativeImplementation()
    {
        const int width = 32;
        const int height = 32;
        var pixels = Enumerable.Repeat((byte)127, width * height).ToArray();

        var result = Nfiq2OclHistogramModule.Compute(new Nfiq2FingerprintImage(pixels, width, height));

        await Assert.That(result.Values.Count).IsEqualTo(0);
        await Assert.That(result.Features["OCL_Bin10_0"]).IsEqualTo(0.0);
        await Assert.That(result.Features["OCL_Bin10_Mean"]).IsEqualTo(0.0);
        await Assert.That(result.Features["OCL_Bin10_StdDev"]).IsEqualTo(0.0);
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
