namespace OpenNist.Tests.Nfiq;

using System.Globalization;
using OpenNist.Nfiq;
using OpenNist.Nfiq.Internal;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - Managed FDA Module")]
internal sealed class Nfiq2FrequencyDomainAnalysisModuleTests
{
    private const double s_nativeFloatingPointTolerance = 0.1;
    private static readonly string[] s_featureNames =
    [
        "FDA_Bin10_0",
        "FDA_Bin10_1",
        "FDA_Bin10_2",
        "FDA_Bin10_3",
        "FDA_Bin10_4",
        "FDA_Bin10_5",
        "FDA_Bin10_6",
        "FDA_Bin10_7",
        "FDA_Bin10_8",
        "FDA_Bin10_9",
        "FDA_Bin10_Mean",
        "FDA_Bin10_StdDev",
    ];

    private static readonly Nfiq2Algorithm s_algorithm = Nfiq2TestContext.Algorithm;

    [Test]
    [DisplayName("should reproduce the official frequency-domain-analysis histogram features for every bundled SFinGe image")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceTheOfficialFrequencyDomainAnalysisHistogramFeaturesForEveryBundledSfinGeImage(Nfiq2ExampleCase exampleCase)
    {
        var (pixels, width, height) = Nfiq2PortableGrayMapReader.Read(exampleCase.ImagePath);
        var sourceImage = new Nfiq2FingerprintImage(pixels, width, height);
        var croppedImage = sourceImage.CopyRemovingNearWhiteFrame();
        var managed = Nfiq2FrequencyDomainAnalysisModule.Compute(croppedImage);

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
