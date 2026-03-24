namespace OpenNist.Tests.Nfiq;

using OpenNist.Nfiq.Internal;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - FingerJet Minutia Post Processor")]
internal sealed class Nfiq2FingerJetMinutiaPostProcessorTests
{
    [Test]
    public async Task ShouldReproduceNativeFingerJetMinutiaPostProcessing()
    {
        Nfiq2FingerJetRawMinutia[] minutiae =
        [
            new(40, 60, 32, 199, 1),
            new(120, 140, 80, 255, 2),
            new(180, 220, 250, 10, 7),
        ];

        var managed = Nfiq2FingerJetMinutiaPostProcessor.Process(
            minutiae,
            imageResolution: 500,
            xOffset: 1,
            yOffset: 6);
        var native = Nfiq2FingerJetOracleReader.ReadPostprocessedMinutiae(
            imageResolution: 500,
            xOffset: 1,
            yOffset: 6,
            minutiae);

        AssertEqual(managed, native);
        await Assert.That(managed.Count).IsEqualTo(native.Count);
    }

    [Test]
    public async Task ShouldReproduceNativePostProcessingForNonDefaultOffsetsAndResolution()
    {
        Nfiq2FingerJetRawMinutia[] minutiae =
        [
            new(15, 18, 0, 0, 0),
            new(90, 150, 64, 128, 1),
            new(210, 300, 127, 201, 2),
        ];

        var managed = Nfiq2FingerJetMinutiaPostProcessor.Process(
            minutiae,
            imageResolution: 333,
            xOffset: 9,
            yOffset: 14);
        var native = Nfiq2FingerJetOracleReader.ReadPostprocessedMinutiae(
            imageResolution: 333,
            xOffset: 9,
            yOffset: 14,
            minutiae);

        AssertEqual(managed, native);
        await Assert.That(managed.Count).IsEqualTo(native.Count);
    }

    [Test]
    [DisplayName("should reproduce the final native minutiae for every bundled SFinGe image when supplied raw native minutiae")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceTheFinalNativeMinutiaeForEveryBundledSfinGeImageWhenSuppliedRawNativeMinutiae(
        Nfiq2ExampleCase exampleCase)
    {
        var raw = Nfiq2FingerJetOracleReader.ReadRawMinutiae(exampleCase.ImagePath, pixelsPerInch: 500);
        var managed = Nfiq2FingerJetMinutiaPostProcessor.Process(
            raw.Minutiae,
            raw.ImageResolution,
            raw.XOffset,
            raw.YOffset);
        var native = Nfiq2MinutiaeOracleReader.ReadMinutiae(exampleCase.ImagePath);

        AssertEqual(managed, native);
        await Assert.That(managed.Count).IsEqualTo(native.Count);
    }

    private static void AssertEqual(IReadOnlyList<Nfiq2Minutia> actual, IReadOnlyList<Nfiq2Minutia> expected)
    {
        if (actual.Count != expected.Count)
        {
            throw new InvalidOperationException($"Minutia count diverged from native FingerJet. expected={expected.Count}, actual={actual.Count}.");
        }

        for (var index = 0; index < actual.Count; index++)
        {
            if (actual[index] != expected[index])
            {
                throw new InvalidOperationException(
                    $"Minutia diverged from native FingerJet at index {index}. expected={expected[index]}, actual={actual[index]}.");
            }
        }
    }
}
