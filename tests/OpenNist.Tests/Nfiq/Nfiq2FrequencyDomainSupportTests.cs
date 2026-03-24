namespace OpenNist.Tests.Nfiq;

using System.Globalization;
using OpenNist.Nfiq.Internal;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - Managed Frequency Domain Support")]
internal sealed class Nfiq2FrequencyDomainSupportTests
{
    private const int BlockSize = 32;
    private const int SlantedBlockWidth = 32;
    private const int SlantedBlockHeight = 16;
    private const double NativeDoubleTolerance = 1e-9;

    [Test]
    [DisplayName("should reproduce native FDA scores for representative bundled SFinGe blocks")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceNativeFdaScoresForRepresentativeBundledSfinGeBlocks(Nfiq2ExampleCase exampleCase)
    {
        var (pixels, width, height) = Nfiq2PortableGrayMapReader.Read(exampleCase.ImagePath);
        var croppedImage = new Nfiq2FingerprintImage(pixels, width, height).CopyRemovingNearWhiteFrame();
        var origins = Nfiq2RidgeValleySupport
            .EnumerateInteriorBlockOrigins(croppedImage.Width, croppedImage.Height, BlockSize, SlantedBlockWidth, SlantedBlockHeight)
            .Where(static origin => origin.Row > BlockSize && origin.Column > BlockSize)
            .ToArray();

        await Assert.That(origins.Length).IsGreaterThan(2);

        foreach (var origin in SelectRepresentativeOrigins(origins))
        {
            var native = Nfiq2CommonFunctionOracleReader.ReadFrequencyDomainBlock(exampleCase.ImagePath, origin.Row, origin.Column);
            var managed = Nfiq2FrequencyDomainSupport.ComputeFrequencyDomainAnalysisScore(native.Pixels.ToArray(), native.Width, native.Height);
            if (Math.Abs(native.FrequencyDomainAnalysis - managed) > NativeDoubleTolerance)
            {
                throw new InvalidOperationException(
                    $"{exampleCase.Name} fda ({origin.Row},{origin.Column}) diverged from native NFIQ 2. "
                    + $"expected={native.FrequencyDomainAnalysis.ToString(CultureInfo.InvariantCulture)}, "
                    + $"actual={managed.ToString(CultureInfo.InvariantCulture)}.");
            }
        }
    }

    private static IEnumerable<Nfiq2BlockOrigin> SelectRepresentativeOrigins(Nfiq2BlockOrigin[] origins)
    {
        yield return origins[0];
        yield return origins[origins.Length / 2];
        yield return origins[^1];
    }
}
