namespace OpenNist.Tests.Nfiq;

using System.Globalization;
using OpenNist.Nfiq;
using OpenNist.Nfiq.Internal;
using OpenNist.Nfiq.Internal.Model;
using OpenNist.Nfiq.Internal.Runtime;
using OpenNist.Nfiq.Runtime;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - Managed Assessment Engine")]
internal sealed class Nfiq2ManagedAssessmentEngineTests
{
    private static readonly Nfiq2Algorithm s_algorithm = Nfiq2TestContext.Algorithm;
    private static readonly Nfiq2ManagedAssessmentEngine s_engine = Nfiq2ManagedAssessmentEngine.LoadDefault();

    [Test]
    [DisplayName("should reproduce the full managed assessment result for every bundled SFinGe image when native minutiae are supplied")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceTheFullManagedAssessmentResultForEveryBundledSfinGeImageWhenNativeMinutiaeAreSupplied(
        Nfiq2ExampleCase exampleCase)
    {
        var minutiae = Nfiq2MinutiaeOracleReader.ReadMinutiae(exampleCase.ImagePath);
        var (pixels, width, height) = Nfiq2PortableGrayMapReader.Read(exampleCase.ImagePath);
        var croppedImage = new Nfiq2FingerprintImage(pixels, width, height).CopyRemovingNearWhiteFrame();

        var native = await s_algorithm.AnalyzeFileAsync(exampleCase.ImagePath).ConfigureAwait(false);
        var managed = s_engine.Analyze(croppedImage, minutiae, native.Filename, includeMappedQualityMeasures: true, native.FingerCode);

        if (managed.Filename != native.Filename
            || managed.FingerCode != native.FingerCode
            || managed.QualityScore != native.QualityScore
            || managed.OptionalError != native.OptionalError
            || managed.Quantized != native.Quantized
            || managed.Resampled != native.Resampled)
        {
            throw new InvalidOperationException(
                $"{exampleCase.Name} managed fixed assessment fields diverged from official NFIQ 2.");
        }

        foreach (var feedback in native.ActionableFeedback)
        {
            AssertApproximatelyEqual(
                $"{exampleCase.Name} {feedback.Key}",
                feedback.Key,
                feedback.Value,
                managed.ActionableFeedback[feedback.Key]);
        }

        foreach (var feature in native.NativeQualityMeasures)
        {
            AssertApproximatelyEqual(
                $"{exampleCase.Name} {feature.Key}",
                feature.Key,
                feature.Value,
                managed.NativeQualityMeasures[feature.Key]);
        }

    }

    private static void AssertApproximatelyEqual(
        string context,
        string key,
        double? expectedValue,
        double? actualValue)
    {
        if (expectedValue.HasValue != actualValue.HasValue)
        {
            throw new InvalidOperationException($"{context} nullability diverged from the official NFIQ 2 value.");
        }

        if (!expectedValue.HasValue)
        {
            return;
        }

        var expected = expectedValue.GetValueOrDefault();
        var actual = actualValue.GetValueOrDefault();
        if (Math.Abs(expected - actual) > GetTolerance(key))
        {
            throw new InvalidOperationException(
                $"{context} diverged from the official NFIQ 2 value. "
                + $"expected={expected.ToString(CultureInfo.InvariantCulture)}, "
                + $"actual={actual.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static double GetTolerance(string key)
    {
        if (key.StartsWith("FDA_Bin10_", StringComparison.Ordinal))
        {
            return 0.1;
        }

        if (key.StartsWith("LCS_Bin10_", StringComparison.Ordinal))
        {
            return 0.1;
        }

        if (key.StartsWith("RVUP_Bin10_", StringComparison.Ordinal))
        {
            return 0.1;
        }

        if (key.StartsWith("OCL_Bin10_", StringComparison.Ordinal))
        {
            return 0.02;
        }

        if (key.StartsWith("OF_Bin10_", StringComparison.Ordinal))
        {
            return 0.02;
        }

        if (key is "ImgProcROIArea_Mean")
        {
            return 0.02;
        }

        if (key is "OrientationMap_ROIFilter_CoherenceRel" or "OrientationMap_ROIFilter_CoherenceSum")
        {
            return 0.5;
        }

        if (key is "SufficientFingerprintForeground")
        {
            return 100.0;
        }

        return 0.01;
    }
}
