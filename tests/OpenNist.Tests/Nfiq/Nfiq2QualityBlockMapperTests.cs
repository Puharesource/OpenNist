namespace OpenNist.Tests.Nfiq;

using OpenNist.Nfiq;
using OpenNist.Nfiq.Runtime;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Contract: NFIQ2 - Quality Block Mapping")]
internal sealed class Nfiq2QualityBlockMapperTests
{
    private static readonly Nfiq2Algorithm s_algorithm = Nfiq2TestContext.Algorithm;

    [Test]
    [DisplayName("should reproduce the official mapped quality-block values for the first public example image")]
    public async Task ShouldReproduceTheOfficialMappedQualityBlockValuesForTheFirstPublicExampleImage()
    {
        var exampleCase = Nfiq2TestDataSources.EnumerateExampleCases().First();
        var result = await s_algorithm.AnalyzeFileAsync(
            exampleCase.ImagePath,
            new(IncludeMappedQualityMeasures: true, Force: true));

        var nativeValues = result.NativeQualityMeasures
            .Where(static pair => pair.Value is not null)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value!.Value, StringComparer.Ordinal);

        var managedMappedValues = Nfiq2QualityBlockMapper.GetQualityBlockValues(nativeValues);

        foreach (var nativeValueKey in nativeValues.Keys)
        {
            var expectedMappedMeasureName = $"QB_{nativeValueKey}";
            result.MappedQualityMeasures.TryGetValue(expectedMappedMeasureName, out var expectedValue);
            managedMappedValues.TryGetValue(nativeValueKey, out var actualValue);

            await Assert.That(actualValue).IsEqualTo(expectedValue is null ? null : checked((byte)expectedValue.Value));
        }
    }

    [Test]
    [DisplayName("should return no mapped value for unsupported native histogram bins")]
    public async Task ShouldReturnNoMappedValueForUnsupportedNativeHistogramBins()
    {
        var mappedValue = Nfiq2QualityBlockMapper.GetQualityBlockValue("FDA_Bin10_0", 3.0);
        await Assert.That(mappedValue).IsNull();
    }

    [Test]
    [DisplayName("should compute known mapped values for representative native measures")]
    public async Task ShouldComputeKnownMappedValuesForRepresentativeNativeMeasures()
    {
        await Assert.That(Nfiq2QualityBlockMapper.GetQualityBlockValue("MMB", 185.52249)).IsEqualTo((byte)73);
        await Assert.That(Nfiq2QualityBlockMapper.GetQualityBlockValue("Mu", 183.97505)).IsEqualTo((byte)72);
        await Assert.That(Nfiq2QualityBlockMapper.GetQualityBlockValue("ImgProcROIArea_Mean", 177.36347)).IsEqualTo((byte)70);
        await Assert.That(Nfiq2QualityBlockMapper.GetQualityBlockValue("FingerJetFX_MinutiaeCount", 69.0)).IsEqualTo((byte)69);
        await Assert.That(Nfiq2QualityBlockMapper.GetQualityBlockValue("OrientationMap_ROIFilter_CoherenceSum", 143.41207)).IsEqualTo((byte)4);
    }
}
