namespace OpenNist.Tests.Nfiq;

using OpenNist.Nfiq.Internal;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - Managed Rotated Block Parity")]
internal sealed class Nfiq2RotatedBlockParityTests
{
    private const int BlockSize = 32;
    private const int SlantedBlockWidth = 32;
    private const int SlantedBlockHeight = 16;

    [Test]
    [DisplayName("should reproduce native padded rotated blocks for representative bundled SFinGe blocks")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceNativePaddedRotatedBlocksForRepresentativeBundledSfinGeBlocks(Nfiq2ExampleCase exampleCase)
    {
        var (pixels, width, height) = Nfiq2PortableGrayMapReader.Read(exampleCase.ImagePath);
        var croppedImage = new Nfiq2FingerprintImage(pixels, width, height).CopyRemovingNearWhiteFrame();
        var geometry = Nfiq2RidgeValleySupport.GetOverlappingBlockGeometry(BlockSize, SlantedBlockWidth, SlantedBlockHeight);
        var origins = Nfiq2RidgeValleySupport
            .EnumerateInteriorBlockOrigins(croppedImage.Width, croppedImage.Height, BlockSize, SlantedBlockWidth, SlantedBlockHeight)
            .Where(static origin => origin.Row > BlockSize && origin.Column > BlockSize)
            .ToArray();

        await Assert.That(origins.Length).IsGreaterThan(2);

        foreach (var origin in SelectRepresentativeOrigins(origins))
        {
            var native = Nfiq2CommonFunctionOracleReader.ReadRotatedBlock(exampleCase.ImagePath, origin.Row, origin.Column);
            var managed = Nfiq2RidgeValleySupport.GetRotatedBlock(
                croppedImage.Pixels.Span,
                croppedImage.Width,
                origin.Row - geometry.BlockOffset,
                origin.Column - geometry.BlockOffset,
                geometry.ExtractedBlockSize,
                geometry.ExtractedBlockSize,
                native.Orientation,
                padFlag: true);
            AssertEqual($"{exampleCase.Name} rotated ({origin.Row},{origin.Column})", native.Pixels, managed);
        }
    }

    private static IEnumerable<Nfiq2BlockOrigin> SelectRepresentativeOrigins(Nfiq2BlockOrigin[] origins)
    {
        yield return origins[0];
        yield return origins[origins.Length / 2];
        yield return origins[^1];
    }

    private static void AssertEqual(string context, IReadOnlyList<byte> expected, byte[] actual)
    {
        if (expected.Count != actual.Length)
        {
            throw new InvalidOperationException($"{context} length diverged from native NFIQ 2. expected={expected.Count}, actual={actual.Length}.");
        }

        for (var index = 0; index < expected.Count; index++)
        {
            if (expected[index] != actual[index])
            {
                throw new InvalidOperationException(
                    $"{context} diverged from native NFIQ 2 at index {index}. expected={expected[index]}, actual={actual[index]}.");
            }
        }
    }
}
