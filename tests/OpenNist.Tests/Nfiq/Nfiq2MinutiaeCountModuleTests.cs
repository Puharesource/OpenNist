namespace OpenNist.Tests.Nfiq;

using System.Globalization;
using OpenNist.Nfiq;
using OpenNist.Nfiq.Internal;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - Managed Minutiae Count Module")]
internal sealed class Nfiq2MinutiaeCountModuleTests
{
    private const double NativeFloatingPointTolerance = 0.01;
    private static readonly string[] s_featureNames =
    [
        "FingerJetFX_MinutiaeCount",
        "FingerJetFX_MinCount_COMMinRect200x200",
    ];

    private static readonly Nfiq2Algorithm s_algorithm = Nfiq2TestContext.Algorithm;

    [Test]
    [DisplayName("should reproduce the official minutiae-count features for every bundled SFinGe image")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceTheOfficialMinutiaeCountFeaturesForEveryBundledSfinGeImage(Nfiq2ExampleCase exampleCase)
    {
        var minutiae = Nfiq2MinutiaeOracleReader.ReadMinutiae(exampleCase.ImagePath);
        var (pixels, width, height) = Nfiq2PortableGrayMapReader.Read(exampleCase.ImagePath);
        var croppedImage = new Nfiq2FingerprintImage(pixels, width, height).CopyRemovingNearWhiteFrame();
        var managed = Nfiq2MinutiaeCountModule.Compute(minutiae, croppedImage.Width, croppedImage.Height);

        var native = await s_algorithm.AnalyzeFileAsync(exampleCase.ImagePath).ConfigureAwait(false);
        foreach (var featureName in s_featureNames)
        {
            AssertApproximatelyEqual($"{exampleCase.Name} {featureName}", native.NativeQualityMeasures[featureName], managed.Features[featureName]);
        }
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
