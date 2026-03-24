namespace OpenNist.Tests.Nfiq;

using System.Globalization;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;
using OpenNist.Nfiq;

[Category("Integration: NFIQ2 - Official CLI")]
internal sealed class Nfiq2IntegrationTests
{
    private static readonly Nfiq2Algorithm s_algorithm = new(Nfiq2TestPaths.Installation);

    [Test]
    [DisplayName("should analyze all official SFinGe examples in one batch run")]
    public async Task ShouldAnalyzeAllOfficialSfinGeExamplesInOneBatchRun()
    {
        var examplePaths = Nfiq2TestDataSources.EnumerateExampleCases()
            .Select(static exampleCase => exampleCase.ImagePath)
            .ToArray();

        var report = await s_algorithm.AnalyzeFilesAsync(
            examplePaths,
            new(IncludeMappedQualityMeasures: true, ThreadCount: 2)).ConfigureAwait(false);

        await Assert.That(report.Results.Count).IsEqualTo(examplePaths.Length);
        await Assert.That(report.Columns.Contains("QualityScore")).IsTrue();
        await Assert.That(report.Columns.Any(static column => column.StartsWith("QB_", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    [DisplayName("should produce the same score from raw in-memory pixels as from the source PGM file")]
    public async Task ShouldProduceTheSameScoreFromRawInMemoryPixelsAsFromTheSourcePgmFile()
    {
        var exampleCase = Nfiq2TestDataSources.EnumerateExampleCases().First();
        var (pixels, width, height) = Nfiq2PortableGrayMapReader.Read(exampleCase.ImagePath);

        var fileResult = await s_algorithm.AnalyzeFileAsync(exampleCase.ImagePath).ConfigureAwait(false);
        var rawResult = await s_algorithm.AnalyzeAsync(
            pixels,
            new(width, height, BitsPerPixel: 8, PixelsPerInch: 500)).ConfigureAwait(false);

        await Assert.That(rawResult.QualityScore).IsEqualTo(fileResult.QualityScore);
        await Assert.That(rawResult.NativeQualityMeasures["MMB"]).IsEqualTo(fileResult.NativeQualityMeasures["MMB"]);
        await Assert.That(rawResult.NativeQualityMeasures["Mu"]).IsEqualTo(fileResult.NativeQualityMeasures["Mu"]);
    }

    [Test]
    [DisplayName("should expose mapped quality-block values for the first official example image")]
    public async Task ShouldExposeMappedQualityBlockValuesForTheFirstOfficialExampleImage()
    {
        var exampleCase = Nfiq2TestDataSources.EnumerateExampleCases().First();
        var result = await s_algorithm.AnalyzeFileAsync(
            exampleCase.ImagePath,
            new(IncludeMappedQualityMeasures: true)).ConfigureAwait(false);

        AssertNullableMeasureEquals(result.MappedQualityMeasures, "QB_FDA_Bin10_Mean", 46);
        AssertNullableMeasureEquals(result.MappedQualityMeasures, "QB_FingerJetFX_MinutiaeCount", 69);
        AssertNullableMeasureEquals(result.MappedQualityMeasures, "QB_ImgProcROIArea_Mean", 70);
        AssertNullableMeasureEquals(result.MappedQualityMeasures, "QB_MMB", 73);
        AssertNullableMeasureEquals(result.MappedQualityMeasures, "QB_OrientationMap_ROIFilter_CoherenceSum", 4);
    }

    [Test]
    [DisplayName("should match the official NFIQ 2 example outputs for every bundled SFinGe image")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldMatchTheOfficialNfiq2ExampleOutputsForEveryBundledSfinGeImage(Nfiq2ExampleCase exampleCase)
    {
        var result = await s_algorithm.AnalyzeFileAsync(
            exampleCase.ImagePath,
            new(IncludeMappedQualityMeasures: true)).ConfigureAwait(false);

        var expectedOutput = Nfiq2ExpectedOutputParser.Parse(exampleCase.ExpectedOutputPath);

        await Assert.That(result.QualityScore).IsEqualTo(expectedOutput.QualityScore);

        foreach (var (measureName, expectedValue) in expectedOutput.ActionableFeedback)
        {
            AssertApproximatelyEqual(
                context: $"{exampleCase.Name} actionable measure '{measureName}'",
                expectedValue,
                result.ActionableFeedback[measureName],
                tolerance: 0.001);
        }

        foreach (var (measureName, expectedValue) in expectedOutput.NativeQualityMeasures)
        {
            AssertApproximatelyEqual(
                context: $"{exampleCase.Name} native measure '{measureName}'",
                expectedValue,
                result.NativeQualityMeasures[measureName],
                tolerance: 0.001);
        }
    }

    private static void AssertNullableMeasureEquals(
        IReadOnlyDictionary<string, double?> measures,
        string measureName,
        double expectedValue)
    {
        if (!measures.TryGetValue(measureName, out var actualValue))
        {
            throw new InvalidOperationException($"The mapped NFIQ 2 result did not contain '{measureName}'.");
        }

        if (actualValue is null || Math.Abs(actualValue.Value - expectedValue) > 0.0001)
        {
            throw new InvalidOperationException(
                $"Expected mapped measure '{measureName}' to equal {expectedValue.ToString(CultureInfo.InvariantCulture)}, "
                + $"but received {(actualValue?.ToString(CultureInfo.InvariantCulture) ?? "NA")}.");
        }
    }

    private static void AssertApproximatelyEqual(
        string context,
        double expectedValue,
        double? actualValue,
        double tolerance)
    {
        if (actualValue is null)
        {
            throw new InvalidOperationException($"{context} was NA, but {expectedValue.ToString(CultureInfo.InvariantCulture)} was expected.");
        }

        if (Math.Abs(expectedValue - actualValue.Value) > tolerance)
        {
            throw new InvalidOperationException(
                $"{context} diverged from the official NFIQ 2 value. "
                + $"expected={expectedValue.ToString(CultureInfo.InvariantCulture)}, "
                + $"actual={actualValue.Value.ToString(CultureInfo.InvariantCulture)}, "
                + $"tolerance={tolerance.ToString(CultureInfo.InvariantCulture)}.");
        }
    }
}
