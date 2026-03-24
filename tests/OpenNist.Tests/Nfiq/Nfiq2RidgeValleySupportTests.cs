namespace OpenNist.Tests.Nfiq;

using System.Globalization;
using OpenNist.Nfiq.Internal;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - Managed Ridge Valley Support")]
internal sealed class Nfiq2RidgeValleySupportTests
{
    private const int BlockSize = 32;
    private const int SlantedBlockWidth = 32;
    private const int SlantedBlockHeight = 16;
    private const int ScannerResolution = 500;
    private const double NativeDoubleTolerance = 1e-9;

    [Test]
    [DisplayName("should reproduce native ridge-valley structure and local clarity for representative bundled SFinGe blocks")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceNativeRidgeValleyStructureAndLocalClarityForRepresentativeBundledSfinGeBlocks(Nfiq2ExampleCase exampleCase)
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
            var native = Nfiq2CommonFunctionOracleReader.ReadRidgeStructure(exampleCase.ImagePath, origin.Row, origin.Column);
            var managedStructure = Nfiq2RidgeValleySupport.GetRidgeValleyStructure(native.Pixels.ToArray(), native.Width, native.Height);

            AssertEqual($"{exampleCase.Name} ridval ({origin.Row},{origin.Column})", native.RidgeValleyPattern, managedStructure.RidgeValleyPattern);
            AssertApproximatelyEqual($"{exampleCase.Name} dt ({origin.Row},{origin.Column})", native.TrendLine, managedStructure.TrendLine);

            var managedRatios = Nfiq2RidgeValleySupport.ComputeRidgeValleyUniformityRatios(managedStructure.RidgeValleyPattern.ToArray());
            AssertApproximatelyEqual($"{exampleCase.Name} rvu ({origin.Row},{origin.Column})", native.RidgeValleyUniformityRatios, managedRatios);

            var managedLocalClarity = Nfiq2RidgeValleySupport.ComputeLocalClarityScore(
                native.Pixels.ToArray(),
                native.Width,
                native.Height,
                managedStructure.RidgeValleyPattern.ToArray(),
                managedStructure.TrendLine.ToArray(),
                ScannerResolution);

            if (Math.Abs(native.LocalClarity - managedLocalClarity) > NativeDoubleTolerance)
            {
                throw new InvalidOperationException(
                    $"{exampleCase.Name} lcs ({origin.Row},{origin.Column}) diverged from native NFIQ 2. "
                    + $"expected={native.LocalClarity.ToString(CultureInfo.InvariantCulture)}, "
                    + $"actual={managedLocalClarity.ToString(CultureInfo.InvariantCulture)}.");
            }
        }
    }

    private static IEnumerable<Nfiq2BlockOrigin> SelectRepresentativeOrigins(Nfiq2BlockOrigin[] origins)
    {
        yield return origins[0];
        yield return origins[origins.Length / 2];
        yield return origins[^1];
    }

    private static void AssertEqual(string context, IReadOnlyList<byte> expected, IReadOnlyList<byte> actual)
    {
        if (expected.Count != actual.Count)
        {
            throw new InvalidOperationException($"{context} length diverged from native NFIQ 2. expected={expected.Count}, actual={actual.Count}.");
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

    private static void AssertApproximatelyEqual(string context, IReadOnlyList<double> expected, IReadOnlyList<double> actual)
    {
        if (expected.Count != actual.Count)
        {
            throw new InvalidOperationException($"{context} length diverged from native NFIQ 2. expected={expected.Count}, actual={actual.Count}.");
        }

        for (var index = 0; index < expected.Count; index++)
        {
            if (Math.Abs(expected[index] - actual[index]) > NativeDoubleTolerance)
            {
                throw new InvalidOperationException(
                    $"{context} diverged from native NFIQ 2 at index {index}. "
                    + $"expected={expected[index].ToString(CultureInfo.InvariantCulture)}, "
                    + $"actual={actual[index].ToString(CultureInfo.InvariantCulture)}.");
            }
        }
    }
}
