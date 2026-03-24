namespace OpenNist.Tests.Nfiq;

using System.Globalization;
using OpenNist.Nfiq.Internal;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - Managed Common Function Support")]
internal sealed class Nfiq2BlockFeatureSupportTests
{
    private const int LocalRegionSquare = 32;
    private const double SegmentationThreshold = 0.1;
    private const double OrientationTolerance = 1e-9;

    [Test]
    [DisplayName("should reproduce the native segmentation mask and orientation grid for every bundled SFinGe image")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceTheNativeSegmentationMaskAndOrientationGridForEveryBundledSfinGeImage(Nfiq2ExampleCase exampleCase)
    {
        var native = Nfiq2CommonFunctionOracleReader.ReadBlockGrid(exampleCase.ImagePath);
        var (pixels, width, height) = Nfiq2PortableGrayMapReader.Read(exampleCase.ImagePath);
        var sourceImage = new Nfiq2FingerprintImage(pixels, width, height);
        var croppedImage = sourceImage.CopyRemovingNearWhiteFrame();

        await Assert.That(croppedImage.Width).IsEqualTo(native.Width);
        await Assert.That(croppedImage.Height).IsEqualTo(native.Height);

        var segmentationMask = Nfiq2BlockFeatureSupport.CreateSegmentationMask(croppedImage, LocalRegionSquare, SegmentationThreshold);
        foreach (var block in native.Blocks)
        {
            var managedAllNonZero = Nfiq2BlockFeatureSupport.AreAllNonZero(
                segmentationMask,
                croppedImage.Width,
                block.Row,
                block.Column,
                LocalRegionSquare,
                LocalRegionSquare);

            if (managedAllNonZero != block.AllNonZero)
            {
                throw new InvalidOperationException(
                    $"{exampleCase.Name} block ({block.Row},{block.Column}) segmentation mask diverged from native NFIQ 2. "
                    + $"expectedAllNonZero={block.AllNonZero}, actualAllNonZero={managedAllNonZero}.");
            }

            var managedOrientation = Nfiq2BlockFeatureSupport.ComputeRidgeOrientation(
                croppedImage.Pixels.Span,
                croppedImage.Width,
                block.Row,
                block.Column,
                LocalRegionSquare,
                LocalRegionSquare);

            if (Math.Abs(managedOrientation - block.Orientation) > OrientationTolerance)
            {
                throw new InvalidOperationException(
                    $"{exampleCase.Name} block ({block.Row},{block.Column}) orientation diverged from native NFIQ 2. "
                    + $"expected={block.Orientation.ToString(CultureInfo.InvariantCulture)}, "
                    + $"actual={managedOrientation.ToString(CultureInfo.InvariantCulture)}.");
            }
        }
    }

    [Test]
    [DisplayName("should report false when a block contains a zero byte")]
    public async Task ShouldReportFalseWhenABlockContainsAZeroByte()
    {
        byte[] mask =
        [
            1, 1, 1, 1,
            1, 0, 1, 1,
            1, 1, 1, 1,
            1, 1, 1, 1,
        ];

        var result = Nfiq2BlockFeatureSupport.AreAllNonZero(mask, 4, 0, 0, 4, 4);
        await Assert.That(result).IsFalse();
    }
}
