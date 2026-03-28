namespace OpenNist.Tests.Nfiq;

using System.Globalization;
using OpenNist.Nfiq;
using OpenNist.Nfiq.Internal;
using OpenNist.Nfiq.Internal.Model;
using OpenNist.Nfiq.Internal.Runtime;
using OpenNist.Nfiq.Runtime;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - Managed Feature Vector Builder")]
internal sealed class Nfiq2ManagedFeatureVectorBuilderTests
{
    private static readonly Nfiq2Algorithm s_algorithm = Nfiq2TestContext.Algorithm;
    private static readonly Nfiq2ManagedModel s_managedModel = Nfiq2ManagedModel.LoadDefault();

    [Test]
    [DisplayName("should reproduce the full currently ported managed native-measure vector and final score for every bundled SFinGe image")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceTheFullCurrentlyPortedManagedNativeMeasureVectorAndFinalScoreForEveryBundledSfinGeImage(
        Nfiq2ExampleCase exampleCase)
    {
        var minutiae = Nfiq2MinutiaeOracleReader.ReadMinutiae(exampleCase.ImagePath);
        var (pixels, width, height) = Nfiq2PortableGrayMapReader.Read(exampleCase.ImagePath);
        var croppedImage = new Nfiq2FingerprintImage(pixels, width, height).CopyRemovingNearWhiteFrame();
        var managedFeatures = Nfiq2ManagedFeatureVectorBuilder.BuildNativeQualityMeasures(croppedImage, minutiae);

        var native = await s_algorithm.AnalyzeFileAsync(exampleCase.ImagePath).ConfigureAwait(false);

        foreach (var featureName in Nfiq2RandomForestFeatureOrder.NativeMeasureOrder)
        {
            if (!managedFeatures.TryGetValue(featureName, out var managedValue))
            {
                throw new InvalidOperationException($"Managed feature vector did not contain required feature '{featureName}'.");
            }

            AssertApproximatelyEqual(
                $"{exampleCase.Name} {featureName}",
                featureName,
                native.NativeQualityMeasures[featureName],
                managedValue);
        }

        var managedScore = s_managedModel.ComputeUnifiedQualityScore(
            managedFeatures.ToDictionary(static entry => entry.Key, static entry => (double?)entry.Value, StringComparer.Ordinal));

        if (managedScore != native.QualityScore)
        {
            throw new InvalidOperationException(
                $"{exampleCase.Name} managed unified score diverged from official NFIQ 2. "
                + $"expected={native.QualityScore.ToString(CultureInfo.InvariantCulture)}, "
                + $"actual={managedScore.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static void AssertApproximatelyEqual(
        string context,
        string featureName,
        double? expectedValue,
        double actualValue)
    {
        if (expectedValue is null)
        {
            throw new InvalidOperationException($"{context} was NA in the official NFIQ 2 result.");
        }

        if (Math.Abs(expectedValue.Value - actualValue) > GetTolerance(featureName))
        {
            throw new InvalidOperationException(
                $"{context} diverged from the official NFIQ 2 value. "
                + $"expected={expectedValue.Value.ToString(CultureInfo.InvariantCulture)}, "
                + $"actual={actualValue.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static double GetTolerance(string featureName)
    {
        if (featureName.StartsWith("FDA_Bin10_", StringComparison.Ordinal))
        {
            return 0.1;
        }

        if (featureName.StartsWith("LCS_Bin10_", StringComparison.Ordinal))
        {
            return 0.1;
        }

        if (featureName.StartsWith("RVUP_Bin10_", StringComparison.Ordinal))
        {
            return 0.1;
        }

        if (featureName.StartsWith("OCL_Bin10_", StringComparison.Ordinal))
        {
            return 0.02;
        }

        if (featureName.StartsWith("OF_Bin10_", StringComparison.Ordinal))
        {
            return 0.02;
        }

        if (featureName is "ImgProcROIArea_Mean")
        {
            return 0.02;
        }

        if (featureName is "OrientationMap_ROIFilter_CoherenceRel" or "OrientationMap_ROIFilter_CoherenceSum")
        {
            return 0.5;
        }

        return 0.01;
    }
}
