namespace OpenNist.Nfiq.Internal;

internal sealed record Nfiq2FingerJetOrientationMapResult(
    IReadOnlyList<Nfiq2FingerJetComplex> Orientation,
    IReadOnlyList<byte> Footprint,
    int Width,
    int Size);

internal static class Nfiq2FingerJetOrientationMap
{
    private const int OrientationScale = 4;
    private const int FootprintSmoothThreshold3 = 3;
    private const int FootprintSmoothThreshold11 = 11;
    private const int FootprintSmoothThreshold14 = 14;

    public static Nfiq2FingerJetOrientationMapResult Compute(
        Nfiq2FingerJetPreparedImage footprintImage,
        Nfiq2FingerJetPreparedImage orientationImage)
    {
        ArgumentNullException.ThrowIfNull(footprintImage);
        ArgumentNullException.ThrowIfNull(orientationImage);

        if (footprintImage.Width != orientationImage.Width
            || footprintImage.Height != orientationImage.Height
            || footprintImage.OrientationMapWidth != orientationImage.OrientationMapWidth
            || footprintImage.OrientationMapSize != orientationImage.OrientationMapSize)
        {
            throw new ArgumentException("Footprint and orientation images must share the same FingerJet layout.");
        }

        var footprintRaw = ComputeRaw(footprintImage);
        var orientationRaw = ComputeRaw(orientationImage);
        var orientation = orientationRaw.Orientation.ToArray();
        var footprint = footprintRaw.Footprint.ToArray();

        Nfiq2FingerJetOrientationSupport.BoxFilterByte(
            footprint,
            footprintImage.OrientationMapWidth,
            footprintImage.OrientationMapSize,
            boxSize: 3,
            threshold: FootprintSmoothThreshold3);
        Nfiq2FingerJetOrientationSupport.BoxFilterByte(
            footprint,
            footprintImage.OrientationMapWidth,
            footprintImage.OrientationMapSize,
            boxSize: 5,
            threshold: FootprintSmoothThreshold11);
        Nfiq2FingerJetOrientationSupport.BoxFilterByte(
            footprint,
            footprintImage.OrientationMapWidth,
            footprintImage.OrientationMapSize,
            boxSize: 5,
            threshold: FootprintSmoothThreshold11);
        Nfiq2FingerJetOrientationSupport.FillHoles(
            footprint,
            strideX: 1,
            sizeX: footprintImage.OrientationMapWidth,
            strideY: footprintImage.OrientationMapWidth,
            sizeY: footprintImage.OrientationMapSize);
        Nfiq2FingerJetOrientationSupport.FillHoles(
            footprint,
            strideX: footprintImage.OrientationMapWidth,
            sizeX: footprintImage.OrientationMapSize,
            strideY: 1,
            sizeY: footprintImage.OrientationMapWidth);
        Nfiq2FingerJetOrientationSupport.BoxFilterByte(
            footprint,
            footprintImage.OrientationMapWidth,
            footprintImage.OrientationMapSize,
            boxSize: 5,
            threshold: FootprintSmoothThreshold14);
        Nfiq2FingerJetOrientationSupport.BoxFilterByte(
            footprint,
            footprintImage.OrientationMapWidth,
            footprintImage.OrientationMapSize,
            boxSize: 5,
            threshold: FootprintSmoothThreshold14);

        SmoothOrientationMap(orientation, footprint, footprintImage.OrientationMapWidth, threshold: 128);
        SmoothOrientationMap(orientation, footprint, footprintImage.OrientationMapWidth, threshold: null);
        return new(orientation, footprint, footprintImage.OrientationMapWidth, footprintImage.OrientationMapSize);
    }

    public static Nfiq2FingerJetOrientationMapResult ComputeRaw(Nfiq2FingerJetPreparedImage preparedImage)
    {
        ArgumentNullException.ThrowIfNull(preparedImage);

        var orientation = new Nfiq2FingerJetComplex[preparedImage.OrientationMapSize];
        var footprint = new byte[preparedImage.OrientationMapSize];
        ComputeRawOrientationMap(preparedImage.Pixels.Span, preparedImage.Width, orientation, footprint);
        return new(orientation, footprint, preparedImage.OrientationMapWidth, preparedImage.OrientationMapSize);
    }

    private static void ComputeRawOrientationMap(
        ReadOnlySpan<byte> pixels,
        int width,
        Nfiq2FingerJetComplex[] orientation,
        byte[] footprint)
    {
        var orientationWidth = width / OrientationScale;
        var orientationSize = orientation.Length;
        const short filler = 255;

        var x100 = new ShortDelay(width + 1, Wrap16(filler * 2));
        var x102 = new ShortDelay(width + 1, Wrap16(filler * 5));
        var x103 = new ShortDelay(width - 1, Wrap16(filler * 10));
        var x10c = new ShortDelay(width, Wrap16(filler * 5));
        var x101 = new ShortDelay(width, Wrap16(filler * 25));
        var x201 = new ShortDelay(width);
        var x221 = new ShortDelay(width);
        var x301 = new ShortDelay(width);
        var x321 = new ShortDelay(width);
        var x0 = new SingleShortDelay();
        var x10 = new SingleShortDelay(Wrap16(filler * 50));
        var x11 = new SingleShortDelay();
        var x20 = new SingleShortDelay();
        var x21 = new SingleShortDelay();
        var x22 = new SingleShortDelay();
        var x30 = new SingleShortDelay();
        var x31 = new SingleShortDelay();
        var x32 = new SingleShortDelay();
        var x33 = new SingleShortDelay();
        var dom = new SingleComplexDelay();

        var p0Index = ((width + 1) * 2) + 4;
        var p1Index = p0Index + width - 1;

        for (var y = 0; y < orientationSize - orientationWidth; y += orientationWidth)
        {
            var orientationMagnitude = new Nfiq2FingerJetComplex[orientationWidth];
            for (var i = 0; i < OrientationScale; i++)
            {
                for (var x = 0; x < orientationWidth; x++)
                {
                    var z = Nfiq2FingerJetComplex.Zero;
                    for (var j = 0; j < OrientationScale; j++, p0Index++, p1Index++)
                    {
                        var current = Wrap16(pixels[p0Index] + pixels[p1Index]);
                        var v1 = Wrap16(x100.Next(current) + current + x0.Next(pixels[p0Index]));
                        var v2 = Wrap16(x102.Next(v1) + v1);
                        v1 = Wrap16(x103.Next(v2) + v2 + x10c.Next(v1));
                        var v1x = x101.Next(v1);
                        var v10 = Wrap16(v1x + v1);
                        v10 = Wrap16(x10.Next(v10) - v10);
                        var v11 = Wrap16(v1x - v1);
                        v11 = Wrap16(x11.Next(v11) + v11);
                        var v10x = x201.Next(v10);
                        var v20 = Wrap16(v10x + v10);
                        v20 = Wrap16(x20.Next(v20) - v20);
                        var v21 = Wrap16(v10x - v10);
                        v21 = Wrap16(x21.Next(v21) + v21);
                        var v22 = Wrap16(x221.Next(v11) - v11);
                        v22 = Wrap16(x22.Next(v22) + v22);

                        var g20 = v20 + v22;
                        var g21 = v20 - v22;
                        var g22 = 2 * v21;
                        var z2 = new Nfiq2FingerJetComplex(g20 * g21, g20 * g22);

                        var v20x = x301.Next(v20);
                        var v30 = Wrap16(v20x + v20);
                        _ = x30.Next(v30) - v30;
                        var v31 = Wrap16(v20x - v20);
                        _ = x31.Next(v31) + v31;
                        var v22x = x321.Next(v22);
                        var v32 = Wrap16(v22x + v22);
                        _ = x32.Next(v32) - v32;
                        var v33 = Wrap16(v22x - v22);
                        _ = x33.Next(v33) + v33;

                        z += z2;
                    }

                    orientationMagnitude[x] += z;
                }
            }

            for (var x = 0; x < orientationWidth; x++)
            {
                var value = Nfiq2FingerJetOrientationSupport.OctSign(dom.Next(orientationMagnitude[x]), threshold: 50000);
                footprint[x + y] = value == Nfiq2FingerJetComplex.Zero ? (byte)0 : (byte)1;
                orientation[x + y] = value;
            }
        }

        for (var index = orientationSize - orientationWidth; index < orientationSize; index++)
        {
            footprint[index] = 0;
            orientation[index] = Nfiq2FingerJetComplex.Zero;
        }
    }

    private static void SmoothOrientationMap(
        Nfiq2FingerJetComplex[] orientation,
        byte[] footprint,
        int width,
        int? threshold)
    {
        const int filterSize = 5;
        const int filterTail = filterSize - 1;
        var size = orientation.Length;
        var o1 = new Nfiq2FingerJetComplex[width];
        var o2 = new Nfiq2FingerJetComplex[width + filterSize];
        var od1 = new ComplexDelay(filterSize * width);
        var od2 = new ComplexDelay(filterSize * width);

        for (var y = 0; y < size + (filterTail * width); y += width)
        {
            var od3 = new ComplexDelay(filterSize);
            var od4 = new ComplexDelay(filterSize);
            var o3 = Nfiq2FingerJetComplex.Zero;
            var o4 = Nfiq2FingerJetComplex.Zero;
            for (var x = 0; x < width + filterTail; x++)
            {
                var window = Nfiq2FingerJetComplex.Zero;
                if (x < width)
                {
                    var current = y < size ? orientation[y + x] : Nfiq2FingerJetComplex.Zero;
                    o1[x] += current;
                    o2[x] += o1[x] - od1.Next(o1[x]);
                    window = o2[x] - od2.Next(o2[x]);
                }

                if (y >= filterTail * width)
                {
                    o3 += window;
                    o4 += o3 - od3.Next(o3);
                    if (x < filterTail)
                    {
                        od4.Next(o4);
                    }
                    else
                    {
                        window = o4 - od4.Next(o4);
                        var index = y + x - ((width + 1) * filterTail);
                        if (footprint[index] == 0)
                        {
                            orientation[index] = Nfiq2FingerJetComplex.Zero;
                        }
                        else if (threshold.HasValue)
                        {
                            orientation[index] = Nfiq2FingerJetOrientationSupport.OctSign(window, threshold.Value);
                        }
                        else
                        {
                            orientation[index] = Nfiq2FingerJetOrientationSupport.Div2(window);
                        }
                    }
                }
            }
        }
    }

    private static short Wrap16(int value)
    {
        return unchecked((short)value);
    }

    private sealed class ShortDelay
    {
        private readonly short[] buffer;
        private int index;

        public ShortDelay(int length, short initialValue = 0)
        {
            buffer = new short[length];
            if (initialValue != 0)
            {
                Array.Fill(buffer, initialValue);
            }
        }

        public short Next(short value)
        {
            var output = buffer[index];
            buffer[index] = value;
            index++;
            if (index >= buffer.Length)
            {
                index = 0;
            }

            return output;
        }
    }

    private sealed class SingleShortDelay
    {
        private short previous;

        public SingleShortDelay(short initialValue = 0)
        {
            previous = initialValue;
        }

        public short Next(int value)
        {
            var output = previous;
            previous = Wrap16(value);
            return output;
        }
    }

    private sealed class SingleComplexDelay
    {
        private Nfiq2FingerJetComplex previous;

        public Nfiq2FingerJetComplex Next(Nfiq2FingerJetComplex value)
        {
            var output = previous;
            previous = value;
            return output;
        }
    }

    private sealed class ComplexDelay
    {
        private readonly Nfiq2FingerJetComplex[] buffer;
        private int index;

        public ComplexDelay(int length)
        {
            buffer = new Nfiq2FingerJetComplex[length];
        }

        public Nfiq2FingerJetComplex Next(Nfiq2FingerJetComplex value)
        {
            var output = buffer[index];
            buffer[index] = value;
            index++;
            if (index >= buffer.Length)
            {
                index = 0;
            }

            return output;
        }
    }
}
