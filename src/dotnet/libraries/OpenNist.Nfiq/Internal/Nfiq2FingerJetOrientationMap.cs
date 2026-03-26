namespace OpenNist.Nfiq.Internal;

internal sealed record Nfiq2FingerJetOrientationMapResult(
    Nfiq2FingerJetComplex[] Orientation,
    byte[] Footprint,
    int Width,
    int Size);

internal static class Nfiq2FingerJetOrientationMap
{
    private const int s_orientationScale = 4;
    private const int s_footprintSmoothThreshold3 = 3;
    private const int s_footprintSmoothThreshold11 = 11;
    private const int s_footprintSmoothThreshold14 = 14;

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

        var footprint = GC.AllocateUninitializedArray<byte>(footprintImage.OrientationMapSize);
        ComputeRawOutputs(footprintImage.Pixels.Span, footprintImage.Width, orientation: null, footprint);

        var orientation = GC.AllocateUninitializedArray<Nfiq2FingerJetComplex>(orientationImage.OrientationMapSize);
        ComputeRawOutputs(orientationImage.Pixels.Span, orientationImage.Width, orientation, footprint: null);

        Nfiq2FingerJetOrientationSupport.BoxFilterByte(
            footprint,
            footprintImage.OrientationMapWidth,
            footprintImage.OrientationMapSize,
            boxSize: 3,
            threshold: s_footprintSmoothThreshold3);
        Nfiq2FingerJetOrientationSupport.BoxFilterByte(
            footprint,
            footprintImage.OrientationMapWidth,
            footprintImage.OrientationMapSize,
            boxSize: 5,
            threshold: s_footprintSmoothThreshold11);
        Nfiq2FingerJetOrientationSupport.BoxFilterByte(
            footprint,
            footprintImage.OrientationMapWidth,
            footprintImage.OrientationMapSize,
            boxSize: 5,
            threshold: s_footprintSmoothThreshold11);
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
            threshold: s_footprintSmoothThreshold14);
        Nfiq2FingerJetOrientationSupport.BoxFilterByte(
            footprint,
            footprintImage.OrientationMapWidth,
            footprintImage.OrientationMapSize,
            boxSize: 5,
            threshold: s_footprintSmoothThreshold14);

        SmoothOrientationMap(orientation, footprint, footprintImage.OrientationMapWidth, threshold: 128);
        SmoothOrientationMap(orientation, footprint, footprintImage.OrientationMapWidth, threshold: null);
        return new(orientation, footprint, footprintImage.OrientationMapWidth, footprintImage.OrientationMapSize);
    }

    public static Nfiq2FingerJetOrientationMapResult ComputeRaw(Nfiq2FingerJetPreparedImage preparedImage)
    {
        ArgumentNullException.ThrowIfNull(preparedImage);

        var orientation = GC.AllocateUninitializedArray<Nfiq2FingerJetComplex>(preparedImage.OrientationMapSize);
        var footprint = GC.AllocateUninitializedArray<byte>(preparedImage.OrientationMapSize);
        ComputeRawOutputs(preparedImage.Pixels.Span, preparedImage.Width, orientation, footprint);
        return new(orientation, footprint, preparedImage.OrientationMapWidth, preparedImage.OrientationMapSize);
    }

    private static void ComputeRawOutputs(
        ReadOnlySpan<byte> pixels,
        int width,
        Nfiq2FingerJetComplex[]? orientation,
        byte[]? footprint)
    {
        var orientationSize = orientation?.Length ?? footprint?.Length
            ?? throw new ArgumentException("At least one output _buffer must be provided.");
        var orientationWidth = width / s_orientationScale;
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
        var orientationMagnitude = new Nfiq2FingerJetComplex[orientationWidth];

        for (var y = 0; y < orientationSize - orientationWidth; y += orientationWidth)
        {
            Array.Clear(orientationMagnitude);
            for (var i = 0; i < s_orientationScale; i++)
            {
                for (var x = 0; x < orientationWidth; x++)
                {
                    var z = Nfiq2FingerJetComplex.Zero;
                    for (var j = 0; j < s_orientationScale; j++, p0Index++, p1Index++)
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
                if (footprint is not null)
                {
                    footprint[x + y] = value == Nfiq2FingerJetComplex.Zero ? (byte)0 : (byte)1;
                }

                if (orientation is not null)
                {
                    orientation[x + y] = value;
                }
            }
        }

        for (var index = orientationSize - orientationWidth; index < orientationSize; index++)
        {
            if (footprint is not null)
            {
                footprint[index] = 0;
            }

            if (orientation is not null)
            {
                orientation[index] = Nfiq2FingerJetComplex.Zero;
            }
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
        var o1Buffer = System.Buffers.ArrayPool<Nfiq2FingerJetComplex>.Shared.Rent(width);
        var o2Buffer = System.Buffers.ArrayPool<Nfiq2FingerJetComplex>.Shared.Rent(width + filterSize);
        var od1Buffer = System.Buffers.ArrayPool<Nfiq2FingerJetComplex>.Shared.Rent(filterSize * width);
        var od2Buffer = System.Buffers.ArrayPool<Nfiq2FingerJetComplex>.Shared.Rent(filterSize * width);
        var od3Buffer = System.Buffers.ArrayPool<Nfiq2FingerJetComplex>.Shared.Rent(filterSize);
        var od4Buffer = System.Buffers.ArrayPool<Nfiq2FingerJetComplex>.Shared.Rent(filterSize);
        try
        {
            var o1 = o1Buffer.AsSpan(0, width);
            var o2 = o2Buffer.AsSpan(0, width + filterSize);
            var od1 = od1Buffer.AsSpan(0, filterSize * width);
            var od2 = od2Buffer.AsSpan(0, filterSize * width);
            o1.Clear();
            o2.Clear();
            od1.Clear();
            od2.Clear();
            var od1Index = 0;
            var od2Index = 0;

            for (var y = 0; y < size + (filterTail * width); y += width)
            {
                var od3 = od3Buffer.AsSpan(0, filterSize);
                var od4 = od4Buffer.AsSpan(0, filterSize);
                od3.Clear();
                od4.Clear();
                var od3Index = 0;
                var od4Index = 0;
                var o3 = Nfiq2FingerJetComplex.Zero;
                var o4 = Nfiq2FingerJetComplex.Zero;
                for (var x = 0; x < width + filterTail; x++)
                {
                    var window = Nfiq2FingerJetComplex.Zero;
                    if (x < width)
                    {
                        var current = y < size ? orientation[y + x] : Nfiq2FingerJetComplex.Zero;
                        o1[x] += current;
                        o2[x] += o1[x] - NextDelay(od1, ref od1Index, o1[x]);
                        window = o2[x] - NextDelay(od2, ref od2Index, o2[x]);
                    }

                    if (y >= filterTail * width)
                    {
                        o3 += window;
                        o4 += o3 - NextDelay(od3, ref od3Index, o3);
                        if (x < filterTail)
                        {
                            NextDelay(od4, ref od4Index, o4);
                        }
                        else
                        {
                            window = o4 - NextDelay(od4, ref od4Index, o4);
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
        finally
        {
            System.Buffers.ArrayPool<Nfiq2FingerJetComplex>.Shared.Return(o1Buffer, clearArray: false);
            System.Buffers.ArrayPool<Nfiq2FingerJetComplex>.Shared.Return(o2Buffer, clearArray: false);
            System.Buffers.ArrayPool<Nfiq2FingerJetComplex>.Shared.Return(od1Buffer, clearArray: false);
            System.Buffers.ArrayPool<Nfiq2FingerJetComplex>.Shared.Return(od2Buffer, clearArray: false);
            System.Buffers.ArrayPool<Nfiq2FingerJetComplex>.Shared.Return(od3Buffer, clearArray: false);
            System.Buffers.ArrayPool<Nfiq2FingerJetComplex>.Shared.Return(od4Buffer, clearArray: false);
        }

        static Nfiq2FingerJetComplex NextDelay(
            Span<Nfiq2FingerJetComplex> buffer,
            ref int index,
            Nfiq2FingerJetComplex value)
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

    private static short Wrap16(int value)
    {
        return unchecked((short)value);
    }

    private sealed class ShortDelay
    {
        private readonly short[] _buffer;
        private int _index;

        public ShortDelay(int length, short initialValue = 0)
        {
            _buffer = new short[length];
            if (initialValue != 0)
            {
                Array.Fill(_buffer, initialValue);
            }
        }

        public short Next(short value)
        {
            var output = _buffer[_index];
            _buffer[_index] = value;
            _index++;
            if (_index >= _buffer.Length)
            {
                _index = 0;
            }

            return output;
        }
    }

    private sealed class SingleShortDelay
    {
        private short _previous;

        public SingleShortDelay(short initialValue = 0)
        {
            _previous = initialValue;
        }

        public short Next(int value)
        {
            var output = _previous;
            _previous = Wrap16(value);
            return output;
        }
    }

    private sealed class SingleComplexDelay
    {
        private Nfiq2FingerJetComplex _previous;

        public Nfiq2FingerJetComplex Next(Nfiq2FingerJetComplex value)
        {
            var output = _previous;
            _previous = value;
            return output;
        }
    }
}
