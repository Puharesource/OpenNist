namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2FingerJetOrientationSupport
{
    public static Nfiq2FingerJetComplex OctSign(Nfiq2FingerJetComplex value, int threshold = 0)
    {
        var x = Math.Abs(value.Real);
        var y = Math.Abs(value.Imaginary);
        var n = Math.Max(x, y) / 127;
        if (n <= threshold)
        {
            return Nfiq2FingerJetComplex.Zero;
        }

        n = Math.Max(n, (x + y) / 180);
        return new(
            unchecked((sbyte)(value.Real / n)),
            unchecked((sbyte)(value.Imaginary / n)));
    }

    public static Nfiq2FingerJetComplex Div2(Nfiq2FingerJetComplex value)
    {
        var angle = Nfiq2FingerJetMath.Atan2IntMath(value.Real, value.Imaginary) / 2;
        return new(Nfiq2FingerJetMath.Cos(angle), Nfiq2FingerJetMath.Sin(angle));
    }

    public static void FillHoles(Span<byte> footprint, int strideX, int sizeX, int strideY, int sizeY)
    {
        for (var y = 0; y < sizeY; y += strideY)
        {
            var x1 = 0;
            for (; x1 < sizeX; x1 += strideX)
            {
                if (footprint[y + x1] != 0)
                {
                    break;
                }
            }

            var x2 = sizeX - strideX;
            for (; x2 > x1; x2 -= strideX)
            {
                if (footprint[y + x2] != 0)
                {
                    break;
                }
            }

            for (var x = x1 + strideX; x < x2; x += strideX)
            {
                footprint[y + x] = 1;
            }
        }
    }

    public static void BoxFilterByte(Span<byte> values, int width, int size, int boxSize, byte threshold)
    {
        var n2 = boxSize / 2;
        var verticalAccumulatorBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(width);
        var verticalDelayBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(boxSize * width);
        var horizontalDelayBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(boxSize);
        try
        {
            var verticalAccumulators = verticalAccumulatorBuffer.AsSpan(0, width);
            verticalAccumulators.Clear();
            var verticalDelay = verticalDelayBuffer.AsSpan(0, boxSize * width);
            verticalDelay.Clear();
            var verticalDelayIndex = 0;

            for (var y = 0; y < size + (n2 * width); y += width)
            {
                var horizontalDelay = horizontalDelayBuffer.AsSpan(0, boxSize);
                horizontalDelay.Clear();
                var horizontalDelayIndex = 0;
                byte horizontalAccumulator = 0;
                for (var x = 0; x < width + n2; x++)
                {
                    byte filtered = 0;
                    if (x < width)
                    {
                        var input = y < size ? values[y + x] : (byte)0;
                        var accumulator = unchecked((byte)(verticalAccumulators[x] + input));
                        accumulator = unchecked((byte)(accumulator - NextDelay(verticalDelay, ref verticalDelayIndex, input)));
                        verticalAccumulators[x] = accumulator;
                        filtered = accumulator;
                    }

                    if (y >= n2 * width)
                    {
                        horizontalAccumulator = unchecked((byte)(horizontalAccumulator + filtered));
                        if (x < n2)
                        {
                            NextDelay(horizontalDelay, ref horizontalDelayIndex, filtered);
                        }
                        else
                        {
                            horizontalAccumulator = unchecked((byte)(horizontalAccumulator - NextDelay(horizontalDelay, ref horizontalDelayIndex, filtered)));
                            filtered = horizontalAccumulator;
                            values[y - ((width + 1) * n2) + x] = filtered > threshold ? (byte)1 : (byte)0;
                        }
                    }
                }
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(verticalAccumulatorBuffer, clearArray: false);
            System.Buffers.ArrayPool<byte>.Shared.Return(verticalDelayBuffer, clearArray: false);
            System.Buffers.ArrayPool<byte>.Shared.Return(horizontalDelayBuffer, clearArray: false);
        }

        static byte NextDelay(Span<byte> buffer, ref int index, byte input)
        {
            var output = buffer[index];
            buffer[index] = input;
            index++;
            if (index >= buffer.Length)
            {
                index = 0;
            }

            return output;
        }
    }
}

internal readonly record struct Nfiq2FingerJetComplex(int Real, int Imaginary)
{
    public static readonly Nfiq2FingerJetComplex Zero = new(0, 0);

    public static Nfiq2FingerJetComplex operator +(Nfiq2FingerJetComplex left, Nfiq2FingerJetComplex right)
    {
        return new(left.Real + right.Real, left.Imaginary + right.Imaginary);
    }

    public static Nfiq2FingerJetComplex operator -(Nfiq2FingerJetComplex left, Nfiq2FingerJetComplex right)
    {
        return new(left.Real - right.Real, left.Imaginary - right.Imaginary);
    }

    public static Nfiq2FingerJetComplex operator *(Nfiq2FingerJetComplex value, int scalar)
    {
        return new(value.Real * scalar, value.Imaginary * scalar);
    }

    public static Nfiq2FingerJetComplex operator *(int scalar, Nfiq2FingerJetComplex value)
    {
        return value * scalar;
    }

    public static Nfiq2FingerJetComplex Square(Nfiq2FingerJetComplex value)
    {
        return new(
            (value.Real * value.Real) - (value.Imaginary * value.Imaginary),
            2 * value.Real * value.Imaginary);
    }
}
