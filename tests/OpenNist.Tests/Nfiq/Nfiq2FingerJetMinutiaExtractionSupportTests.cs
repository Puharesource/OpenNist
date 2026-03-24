namespace OpenNist.Tests.Nfiq;

using OpenNist.Nfiq.Internal;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - FingerJet Minutia Extraction Support")]
internal sealed class Nfiq2FingerJetMinutiaExtractionSupportTests
{
    [Test]
    [Arguments(20, 20)]
    [Arguments(0, 0)]
    [Arguments(-1, 20)]
    [Arguments(46, 20)]
    public async Task ShouldReproduceNativeIsInFootprint(int x, int y)
    {
        var phasemap = CreateSyntheticPhasemap(width: 48, height: 48);
        var managed = Nfiq2FingerJetMinutiaExtractionSupport.IsInFootprint(x, y, 48, phasemap.Length, phasemap);
        var native = Nfiq2FingerJetOracleReader.ReadIsInFootprint(x, y, 48, phasemap.Length, phasemap);
        await Assert.That(managed).IsEqualTo(native);
    }

    [Test]
    [Arguments(64, false)]
    [Arguments(64, true)]
    [Arguments(96, false)]
    public async Task ShouldReproduceNativeAdjustAngle(int angle, bool relative)
    {
        var phasemap = CreateSyntheticPhasemap(width: 48, height: 48);
        byte managedAngle = (byte)angle;
        var managedSuccess = Nfiq2FingerJetMinutiaExtractionSupport.TryAdjustAngle(
            ref managedAngle,
            x: 20,
            y: 24,
            phasemap,
            width: 48,
            size: phasemap.Length,
            relative);

        var native = Nfiq2FingerJetOracleReader.ReadAdjustAngle(
            x: 20,
            y: 24,
            width: 48,
            size: phasemap.Length,
            angle,
            relative,
            phasemap);

        await Assert.That(managedSuccess).IsEqualTo(native.Success);
        await Assert.That(managedAngle).IsEqualTo(native.Angle);
    }

    [Test]
    public async Task ShouldReproduceNativeMax2D5Fast()
    {
        int[] values =
        [
            -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1,  1,  2,  3,  4,  3,  2,  1, -1,
            -1,  2,  5,  6,  7,  6,  5,  2, -1,
            -1,  3,  6,  9, 10,  9,  6,  3, -1,
            -1,  2,  5,  6,  8,  6,  5,  2, -1,
            -1,  1,  2,  3,  4,  3,  2,  1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1,  0,  1,  0, 12,  0,  1,  0, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1,
        ];

        var managed = Nfiq2FingerJetMinutiaExtractionSupport.RunMax2D5Fast(values, width: 9);
        var native = Nfiq2FingerJetOracleReader.ReadMax2D5Fast(width: 9, size: values.Length, values);

        if (managed.Length != native.Length)
        {
            throw new InvalidOperationException($"max2d5fast length diverged from native FingerJet. expected={native.Length}, actual={managed.Length}.");
        }

        for (var index = 0; index < managed.Length; index++)
        {
            if (managed[index] != native[index])
            {
                throw new InvalidOperationException(
                    $"max2d5fast diverged from native FingerJet at index {index}. expected={native[index]}, actual={managed[index]}.");
            }
        }

        await Assert.That(managed.Length).IsEqualTo(native.Length);
    }

    [Test]
    public async Task ShouldReproduceNativeConv2D3()
    {
        int[] values =
        [
             0,  0,  0,  0,  0,  0,  0,  0,  0,
             0,  1,  2,  3,  4,  3,  2,  1,  0,
             0,  2,  4,  8, 12,  8,  4,  2,  0,
             0,  3,  9, 15, 21, 15,  9,  3,  0,
             0,  2,  4,  8, 12,  8,  4,  2,  0,
             0,  1,  2,  3,  4,  3,  2,  1,  0,
             0,  0,  0,  0,  0,  0,  0,  0,  0,
        ];

        var managed = Nfiq2FingerJetMinutiaExtractionSupport.RunConv2D3(values, width: 9, t0: 2, t1: 1, normBits: 5);
        var native = Nfiq2FingerJetOracleReader.ReadConv2D3(width: 9, size: values.Length, t0: 2, t1: 1, normBits: 5, values);

        if (managed.Length != native.Length)
        {
            throw new InvalidOperationException($"conv2d3 length diverged from native FingerJet. expected={native.Length}, actual={managed.Length}.");
        }

        for (var index = 0; index < managed.Length; index++)
        {
            if (managed[index] != native[index])
            {
                throw new InvalidOperationException(
                    $"conv2d3 diverged from native FingerJet at index {index}. expected={native[index]}, actual={managed[index]}.");
            }
        }

        await Assert.That(managed.Length).IsEqualTo(native.Length);
    }

    [Test]
    public async Task ShouldReproduceNativePackedBoolDelay()
    {
        bool[] values =
        [
            false, true, true, false, true, false, false, true,
            true, false, false, false, true, true, false, true,
        ];

        var managed = Nfiq2FingerJetMinutiaExtractionSupport.RunBoolDelay(values, delayLength: 7);
        var native = Nfiq2FingerJetOracleReader.ReadBoolDelay(delayLength: 7, initialValue: false, values);

        if (managed.Length != native.Length)
        {
            throw new InvalidOperationException($"bool delay length diverged from native FingerJet. expected={native.Length}, actual={managed.Length}.");
        }

        for (var index = 0; index < managed.Length; index++)
        {
            if (managed[index] != native[index])
            {
                throw new InvalidOperationException(
                    $"bool delay diverged from native FingerJet at index {index}. expected={native[index]}, actual={managed[index]}.");
            }
        }

        await Assert.That(managed.Length).IsEqualTo(native.Length);
    }

    [Test]
    public async Task ShouldReproduceNativeDirectionAccumulator()
    {
        Nfiq2FingerJetComplex[] values =
        [
            new(12, 3), new(10, 4), new(8, 4), new(6, 5),
            new(12, 3), new(10, 4), new(8, 4), new(6, 5),
            new(14, 2), new(12, 3), new(10, 3), new(8, 4),
            new(15, 1), new(13, 2), new(11, 3), new(9, 4),
        ];

        var managed = Nfiq2FingerJetMinutiaExtractionSupport.RunDirectionAccumulator(
            values,
            widthHalf: 4,
            rowCount: 4,
            orientationFilterSize: 13);
        var native = Nfiq2FingerJetOracleReader.ReadDirectionAccumulator(
            widthHalf: 4,
            rowCount: 4,
            filterSize: 13,
            values);

        if (managed.Length != native.Length)
        {
            throw new InvalidOperationException($"direction accumulator length diverged from native FingerJet. expected={native.Length}, actual={managed.Length}.");
        }

        for (var index = 0; index < managed.Length; index++)
        {
            if (managed[index] != native[index])
            {
                throw new InvalidOperationException(
                    $"direction accumulator diverged from native FingerJet at index {index}. expected={native[index]}, actual={managed[index]}.");
            }
        }

        await Assert.That(managed.Length).IsEqualTo(native.Length);
    }

    [Test]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceNativeDirectionAccumulatorOnRealEnhancedRawOrientationMap(Nfiq2ExampleCase exampleCase)
    {
        var rawOrientation = Nfiq2FingerJetOracleReader.ReadEnhancedRawOrientationMap(exampleCase.ImagePath, pixelsPerInch: 500);

        var rowCount = rawOrientation.Orientation.Count / rawOrientation.OrientationMapWidth;
        var managed = Nfiq2FingerJetMinutiaExtractionSupport.RunDirectionAccumulator(
            rawOrientation.Orientation,
            rawOrientation.OrientationMapWidth,
            rowCount,
            orientationFilterSize: 13);
        var native = Nfiq2FingerJetOracleReader.ReadDirectionAccumulator(
            rawOrientation.OrientationMapWidth,
            rowCount,
            filterSize: 13,
            rawOrientation.Orientation);

        if (managed.Length != native.Length)
        {
            throw new InvalidOperationException(
                $"direction accumulator length diverged from native FingerJet for {exampleCase.Name}. expected={native.Length}, actual={managed.Length}.");
        }

        for (var index = 0; index < managed.Length; index++)
        {
            if (managed[index] != native[index])
            {
                throw new InvalidOperationException(
                    $"direction accumulator diverged from native FingerJet for {exampleCase.Name} at index {index}. expected={native[index]}, actual={managed[index]}.");
            }
        }

        await Assert.That(managed.Length).IsEqualTo(native.Length);
    }

    [Test]
    public async Task ShouldReproduceNativeSmmeOrientationSequenceOnSyntheticPhasemap()
    {
        var phasemap = CreateSyntheticPhasemap(width: 48, height: 48);

        var managed = Nfiq2FingerJetMinutiaExtractionSupport.RunSmmeOrientationSequence(phasemap, width: 48);
        var native = Nfiq2FingerJetOracleReader.ReadSmmeOrientationSequence(width: 48, size: phasemap.Length, phasemap);

        if (managed.Length != native.Count)
        {
            throw new InvalidOperationException(
                $"SMME orientation sequence length diverged from native FingerJet. expected={native.Count}, actual={managed.Length}.");
        }

        for (var index = 0; index < managed.Length; index++)
        {
            if (managed[index] != native[index])
            {
                throw new InvalidOperationException(
                    $"SMME orientation sequence diverged from native FingerJet at index {index}. expected={native[index]}, actual={managed[index]}.");
            }
        }

        await Assert.That(managed.Length).IsEqualTo(native.Count);
    }

    [Test]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceNativeSmmeOrientationSequenceOnRealPhasemap(Nfiq2ExampleCase exampleCase)
    {
        var phasemap = Nfiq2FingerJetOracleReader.ReadPhasemap(exampleCase.ImagePath, pixelsPerInch: 500);

        var managed = Nfiq2FingerJetMinutiaExtractionSupport.RunSmmeOrientationSequence(phasemap.Pixels, phasemap.Width);
        var native = Nfiq2FingerJetOracleReader.ReadSmmeOrientationSequence(phasemap.Width, phasemap.Pixels.Length, phasemap.Pixels);

        if (managed.Length != native.Count)
        {
            throw new InvalidOperationException(
                $"SMME orientation sequence length diverged from native FingerJet for {exampleCase.Name}. expected={native.Count}, actual={managed.Length}.");
        }

        for (var index = 0; index < managed.Length; index++)
        {
            if (managed[index] != native[index])
            {
                throw new InvalidOperationException(
                    $"SMME orientation sequence diverged from native FingerJet for {exampleCase.Name} at index {index}. expected={native[index]}, actual={managed[index]}.");
            }
        }

        await Assert.That(managed.Length).IsEqualTo(native.Count);
    }

    private static byte[] CreateSyntheticPhasemap(int width, int height)
    {
        var phasemap = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = (byte)180;
                if (y is 23 or 25)
                {
                    value = 220;
                }
                else if (y == 24)
                {
                    value = 240;
                }

                if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                {
                    value = 127;
                }

                phasemap[(y * width) + x] = value;
            }
        }

        return phasemap;
    }
}
