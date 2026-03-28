namespace OpenNist.Tests.Nfiq;

using System.Globalization;
using OpenNist.Nfiq;
using OpenNist.Nfiq.Internal;
using OpenNist.Nfiq.Internal.Model;
using OpenNist.Nfiq.Internal.Modules;
using OpenNist.Nfiq.Runtime;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - Managed LCS Module")]
internal sealed class Nfiq2LocalClarityModuleTests
{
    private const double s_nativeFloatingPointTolerance = 0.1;
    private static readonly string[] s_featureNames =
    [
        "LCS_Bin10_0",
        "LCS_Bin10_1",
        "LCS_Bin10_2",
        "LCS_Bin10_3",
        "LCS_Bin10_4",
        "LCS_Bin10_5",
        "LCS_Bin10_6",
        "LCS_Bin10_7",
        "LCS_Bin10_8",
        "LCS_Bin10_9",
        "LCS_Bin10_Mean",
        "LCS_Bin10_StdDev",
    ];

    private static readonly Nfiq2Algorithm s_algorithm = Nfiq2TestContext.Algorithm;

    [Test]
    [DisplayName("should reproduce the official local-clarity histogram features for every bundled SFinGe image")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceTheOfficialLocalClarityHistogramFeaturesForEveryBundledSfinGeImage(Nfiq2ExampleCase exampleCase)
    {
        var (pixels, width, height) = Nfiq2PortableGrayMapReader.Read(exampleCase.ImagePath);
        var sourceImage = new Nfiq2FingerprintImage(pixels, width, height);
        var croppedImage = sourceImage.CopyRemovingNearWhiteFrame();
        var managed = Nfiq2LocalClarityModule.Compute(croppedImage);

        var native = await s_algorithm.AnalyzeFileAsync(exampleCase.ImagePath).ConfigureAwait(false);

        foreach (var featureName in s_featureNames)
        {
            AssertApproximatelyEqual(
                $"{exampleCase.Name} {featureName}",
                native.NativeQualityMeasures[featureName],
                managed.Features[featureName]);
        }
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
