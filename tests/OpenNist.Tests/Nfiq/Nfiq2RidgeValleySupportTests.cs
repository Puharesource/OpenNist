namespace OpenNist.Tests.Nfiq;

using OpenNist.Nfiq.Internal;
using OpenNist.Nfiq.Internal.Model;
using OpenNist.Nfiq.Internal.Support;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Unit: NFIQ2 - Ridge/Valley Support")]
internal sealed class Nfiq2RidgeValleySupportTests
{
    [Test]
    public async Task ShouldProduceTheSameCenteredCropAsRotateThenCropWithoutPadding()
    {
        var (pixels, width, height) = Nfiq2PortableGrayMapReader.Read(
            Path.Combine(Nfiq2TestPaths.TestDataRootPath, "Examples", "Images", "SFinGe_Test01.pgm"));
        var fingerprintImage = new Nfiq2FingerprintImage(pixels, width, height).CopyRemovingNearWhiteFrame();
        var geometry = Nfiq2RidgeValleySupport.GetOverlappingBlockGeometry(32, 32, 16);
        var origin = Nfiq2RidgeValleySupport.EnumerateInteriorBlockOrigins(fingerprintImage.Width, fingerprintImage.Height, 32, 32, 16)[0];
        var orientation = Nfiq2BlockFeatureSupport.ComputeRidgeOrientation(
            fingerprintImage.Pixels.Span,
            fingerprintImage.Width,
            origin.Row,
            origin.Column,
            32,
            32);

        var block = Nfiq2RidgeValleySupport.ExtractBlock(
            fingerprintImage.Pixels.Span,
            fingerprintImage.Width,
            origin.Row - geometry.BlockOffset,
            origin.Column - geometry.BlockOffset,
            geometry.ExtractedBlockSize,
            geometry.ExtractedBlockSize);
        var rotated = Nfiq2RidgeValleySupport.GetRotatedBlock(
            block,
            geometry.ExtractedBlockSize,
            geometry.ExtractedBlockSize,
            orientation,
            padFlag: false);
        var expected = Nfiq2RidgeValleySupport.CropCenteredRotatedBlock(
            rotated,
            geometry.ExtractedBlockSize,
            geometry.ExtractedBlockSize,
            32,
            16);
        var actual = Nfiq2RidgeValleySupport.GetCenteredRotatedBlock(
            fingerprintImage.Pixels.Span,
            fingerprintImage.Width,
            origin.Row - geometry.BlockOffset,
            origin.Column - geometry.BlockOffset,
            geometry.ExtractedBlockSize,
            geometry.ExtractedBlockSize,
            32,
            16,
            orientation,
            padFlag: false);

        await Assert.That(actual.AsSpan().SequenceEqual(expected)).IsTrue();
    }

    [Test]
    public async Task ShouldProduceTheSameCenteredCropAsRotateThenCropWithPadding()
    {
        var (pixels, width, height) = Nfiq2PortableGrayMapReader.Read(
            Path.Combine(Nfiq2TestPaths.TestDataRootPath, "Examples", "Images", "SFinGe_Test01.pgm"));
        var fingerprintImage = new Nfiq2FingerprintImage(pixels, width, height).CopyRemovingNearWhiteFrame();
        var geometry = Nfiq2RidgeValleySupport.GetOverlappingBlockGeometry(32, 32, 16);
        var origin = Nfiq2RidgeValleySupport.EnumerateInteriorBlockOrigins(fingerprintImage.Width, fingerprintImage.Height, 32, 32, 16)[0];
        var orientation = Nfiq2BlockFeatureSupport.ComputeRidgeOrientation(
            fingerprintImage.Pixels.Span,
            fingerprintImage.Width,
            origin.Row,
            origin.Column,
            32,
            32) + Math.PI / 2.0;

        var rotated = Nfiq2RidgeValleySupport.GetRotatedBlock(
            fingerprintImage.Pixels.Span,
            fingerprintImage.Width,
            origin.Row - geometry.BlockOffset,
            origin.Column - geometry.BlockOffset,
            geometry.ExtractedBlockSize,
            geometry.ExtractedBlockSize,
            orientation,
            padFlag: true);
        var expected = Nfiq2RidgeValleySupport.CropCenteredRotatedBlock(
            rotated,
            geometry.ExtractedBlockSize,
            geometry.ExtractedBlockSize,
            16,
            32);
        var actual = Nfiq2RidgeValleySupport.GetCenteredRotatedBlock(
            fingerprintImage.Pixels.Span,
            fingerprintImage.Width,
            origin.Row - geometry.BlockOffset,
            origin.Column - geometry.BlockOffset,
            geometry.ExtractedBlockSize,
            geometry.ExtractedBlockSize,
            16,
            32,
            orientation,
            padFlag: true);

        await Assert.That(actual.AsSpan().SequenceEqual(expected)).IsTrue();
    }
}
