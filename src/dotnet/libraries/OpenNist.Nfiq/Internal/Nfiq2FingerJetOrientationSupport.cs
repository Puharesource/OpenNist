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
        var verticalAccumulators = new byte[width];
        var verticalDelay = new RingDelay<byte>(boxSize * width);

        for (var y = 0; y < size + (n2 * width); y += width)
        {
            var horizontalDelay = new RingDelay<byte>(boxSize);
            byte horizontalAccumulator = 0;
            for (var x = 0; x < width + n2; x++)
            {
                byte filtered = 0;
                if (x < width)
                {
                    byte input = y < size ? values[y + x] : (byte)0;
                    var accumulator = unchecked((byte)(verticalAccumulators[x] + input));
                    accumulator = unchecked((byte)(accumulator - verticalDelay.Next(input)));
                    verticalAccumulators[x] = accumulator;
                    filtered = accumulator;
                }

                if (y >= n2 * width)
                {
                    horizontalAccumulator = unchecked((byte)(horizontalAccumulator + filtered));
                    if (x < n2)
                    {
                        horizontalDelay.Next(filtered);
                    }
                    else
                    {
                        horizontalAccumulator = unchecked((byte)(horizontalAccumulator - horizontalDelay.Next(filtered)));
                        filtered = horizontalAccumulator;
                        values[y - ((width + 1) * n2) + x] = filtered > threshold ? (byte)1 : (byte)0;
                    }
                }
            }
        }
    }

    private sealed class RingDelay<T>
    {
        private readonly T[] buffer;
        private int index;

        public RingDelay(int length)
        {
            buffer = new T[length];
        }

        public T Next(T input)
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
