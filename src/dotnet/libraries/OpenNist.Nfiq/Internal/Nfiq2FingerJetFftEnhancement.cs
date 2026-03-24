namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2FingerJetFftEnhancement
{
    private const int BlockBits = 5;
    private const int BlockDim = 1 << BlockBits;
    private const int BlockSize = 1 << (BlockBits * 2);
    private const int Spacing = 17;
    private const int SinBits = 5;

    private static ReadOnlySpan<short> SinTable =>
    [
        0, 799, 1567, 2276, 2896, 3406, 3784, 4017,
        4096, 4017, 3784, 3406, 2896, 2276, 1567, 799,
    ];

    public static Nfiq2FingerJetPreparedImage Enhance(Nfiq2FingerJetPreparedImage preparedImage)
    {
        ArgumentNullException.ThrowIfNull(preparedImage);

        var width = preparedImage.Width;
        var size = preparedImage.Pixels.Length;
        var bandHeight = BlockDim * width;
        var buffer = new byte[size + bandHeight];
        preparedImage.Pixels.Span.CopyTo(buffer.AsSpan(bandHeight, size));

        var input = new FftImage(buffer, bandHeight, width, size, invertOnRead: true);
        var output = new FftImage(buffer, 0, width, size, invertOnRead: false);
        Initialize(output, 0, width, 0, bandHeight);

        var ySpacing = width * Spacing;
        for (var yw = ySpacing - bandHeight; yw < size; yw += ySpacing)
        {
            var yEnvelope = new Envelope(BlockDim, Spacing, left: false, right: false);
            for (var x = Spacing - BlockDim; x < width; x += Spacing)
            {
                var xEnvelope = new Envelope(BlockDim, Spacing, left: false, right: false);
                var block = new int[BlockSize];
                Copy(input, x, yw, block);
                EnhanceBlock(block, xEnvelope, yEnvelope);
                Add(output, x, yw, block);
            }

            Initialize(output, 0, width, yw + bandHeight, ySpacing);
        }

        output.Inverse();
        return preparedImage with { Pixels = buffer.AsMemory(0, size) };
    }

    private static void Initialize(FftImage image, int x0, int width, int y0, int bandHeight)
    {
        for (var y = y0; y < y0 + bandHeight; y += image.Width)
        {
            for (var x = x0; x < x0 + width; x++)
            {
                image.Write(x, y, 0);
            }
        }
    }

    private static void Copy(FftImage image, int x0, int y0, int[] block)
    {
        var index = 0;
        for (var y = y0; y < y0 + (BlockDim * image.Width); y += image.Width)
        {
            for (var x = x0; x < x0 + BlockDim; x++)
            {
                block[index++] = image.Read(x, y);
            }
        }
    }

    private static void Add(FftImage image, int x0, int y0, int[] block)
    {
        var index = 0;
        for (var y = y0; y < y0 + (BlockDim * image.Width); y += image.Width)
        {
            for (var x = x0; x < x0 + BlockDim; x++)
            {
                image.AddValue(x, y, unchecked((byte)block[index++]));
            }
        }
    }

    private static void EnhanceBlock(int[] data, Envelope xEnvelope, Envelope yEnvelope)
    {
        Fft2(real: true, inverse: false, data);
        EnhanceArray(data);
        Fft2(real: true, inverse: true, data);
        ReduceArray(data, BlockBits * 2);
        Normalize(data, xEnvelope, yEnvelope);
    }

    private static void ReduceArray(int[] data, int shift)
    {
        for (var index = 0; index < data.Length; index++)
        {
            data[index] = Reduce(data[index], shift);
        }
    }

    private static void EnhanceArray(int[] data)
    {
        var halfSize = BlockDim >> 1;
        var index = 0;
        for (var y = 0; y < BlockDim; y++)
        {
            var adjustedY = y - (y < halfSize ? 0 : BlockDim);
            for (var x = 0; x < halfSize; x++, index += 2)
            {
                var value = new Complex32(data[index], data[index + 1]);
                value = Enhance(value, x, adjustedY);
                data[index] = value.Real;
                data[index + 1] = value.Imaginary;
            }
        }
    }

    private static Complex32 Enhance(Complex32 value, int x, int y)
    {
        var r2 = (x * x) + (y * y);
        if (r2 <= 6 || r2 >= 169)
        {
            return Complex32.Zero;
        }

        var amplitude = Reduce(OctAbs(value), 5);
        var boosted = Reduce(value * amplitude, 7);
        return Reduce(value + boosted, 3);
    }

    private static void Normalize(int[] data, Envelope xEnvelope, Envelope yEnvelope)
    {
        var minValue = data.Min();
        var maxValue = data.Max();
        var range = maxValue - minValue;
        var divisor = range;
        const int threshold = 16;
        if (range < threshold)
        {
            divisor = threshold;
            minValue -= (threshold - range) / 2;
        }

        divisor *= xEnvelope.Norm * yEnvelope.Norm;
        var index = 0;
        for (var y = 0; y < BlockDim; y++)
        {
            var yWeight = yEnvelope[y];
            for (var x = 0; x < BlockDim; x++, index++)
            {
                data[index] = DivideRounded((data[index] - minValue) * 251 * xEnvelope[x] * yWeight, divisor);
            }
        }
    }

    private static void Fft2(bool real, bool inverse, int[] data)
    {
        var rowLength = 1 << BlockBits;
        if (!inverse)
        {
            for (var offset = 0; offset < data.Length; offset += rowLength)
            {
                Fft1(real, inverse, data, offset);
            }
        }

        for (var x = 0; x < rowLength; x += 2)
        {
            Fft(inverse, data, x, sizeBits: BlockBits * 2, strideBits: BlockBits);
        }

        if (inverse)
        {
            for (var offset = 0; offset < data.Length; offset += rowLength)
            {
                Fft1(real, inverse, data, offset);
            }
        }
    }

    private static void Fft1(bool real, bool inverse, int[] data, int offset)
    {
        if (!inverse)
        {
            Fft(inverse, data, offset, sizeBits: BlockBits, strideBits: 1);
        }

        if (real)
        {
            var size = 1 << BlockBits;
            var stride = 2;
            var dt = (inverse ? 1 : -1) * (1 << (SinBits - BlockBits));
            var angle = dt + (inverse ? (1 << (SinBits - 1)) : 0);
            for (var i = stride; i <= size / 2; i += stride, angle += dt)
            {
                var w = new Complex32(Cos(angle), Sin(angle));
                var p1 = offset + i;
                var p2 = offset + size - i;
                var h1 = new Complex32(data[p2] + data[p1], data[p1 + 1] - data[p2 + 1]) << 12;
                var h2 = w * new Complex32(data[p1 + 1] + data[p2 + 1], data[p2] - data[p1]);
                var f1 = Reduce(h1 + h2, 12 + (inverse ? 0 : 1));
                var f2 = Reduce((h1 - h2).Conjugate(), 12 + (inverse ? 0 : 1));
                data[p1] = f1.Real;
                data[p1 + 1] = f1.Imaginary;
                data[p2] = f2.Real;
                data[p2 + 1] = f2.Imaginary;
            }

            var real0 = data[offset];
            var imag0 = data[offset + 1];
            data[offset] = real0 + imag0;
            data[offset + 1] = inverse ? real0 - imag0 : 0;
        }

        if (inverse)
        {
            Fft(inverse, data, offset, sizeBits: BlockBits, strideBits: 1);
        }
    }

    private static void Fft(bool inverse, int[] data, int offset, int sizeBits, int strideBits)
    {
        Shuffle(data, offset, sizeBits, strideBits);
        var sampleCount = 1 << sizeBits;
        var stride = 1 << strideBits;
        var delta = (inverse ? 1 : -1) * (1 << SinBits);
        for (var level = strideBits; level < sizeBits; level++)
        {
            var blockSize = 1 << level;
            var step = blockSize << 1;
            delta >>= 1;
            for (int m = 0, angle = 0; m < blockSize; m += stride, angle += delta)
            {
                var wr = Cos(angle);
                var wi = Sin(angle);
                for (var i = m; i < sampleCount; i += step)
                {
                    var left = offset + i;
                    var right = left + blockSize;
                    var tr = Reduce((wr * data[right]) - (wi * data[right + 1]), 12);
                    var ti = Reduce((wr * data[right + 1]) + (wi * data[right]), 12);
                    data[right] = data[left] - tr;
                    data[right + 1] = data[left + 1] - ti;
                    data[left] += tr;
                    data[left + 1] += ti;
                }
            }
        }
    }

    private static void Shuffle(int[] data, int offset, int sizeBits, int strideBits)
    {
        var size = 1 << sizeBits;
        var stride = 1 << strideBits;
        for (var index = stride; index < size - stride; index += stride)
        {
            var reversed = BitReverse(index >> strideBits, sizeBits - strideBits) << strideBits;
            if (index > reversed)
            {
                (data[offset + index], data[offset + reversed]) = (data[offset + reversed], data[offset + index]);
                (data[offset + index + 1], data[offset + reversed + 1]) = (data[offset + reversed + 1], data[offset + index + 1]);
            }
        }
    }

    private static int BitReverse(int value, int bits)
    {
        var reversed = 0;
        for (var bit = 0; bit < bits; bit++)
        {
            reversed = (reversed << 1) | ((value >> bit) & 1);
        }

        return reversed;
    }

    private static int Sin(int angle)
    {
        var normalized = angle & ((1 << SinBits) - 1);
        var magnitude = SinTable[normalized & ((1 << (SinBits - 1)) - 1)];
        return ((normalized & (1 << (SinBits - 1))) != 0) ? -magnitude : magnitude;
    }

    private static int Cos(int angle)
    {
        return Sin(angle + (1 << (SinBits - 2)));
    }

    private static int Reduce(int value, int shift)
    {
        return (value + (1 << (shift - 1))) >> shift;
    }

    private static Complex32 Reduce(Complex32 value, int shift)
    {
        var adder = 1 << (shift - 1);
        return new(
            (value.Real + adder) >> shift,
            (value.Imaginary + adder) >> shift);
    }

    private static int DivideRounded(int numerator, int denominator)
    {
        ArgumentOutOfRangeException.ThrowIfZero(denominator);

        if ((numerator >= 0) == (denominator > 0))
        {
            return (Math.Abs(numerator) + (Math.Abs(denominator) >> 1)) / Math.Abs(denominator);
        }

        return -((Math.Abs(numerator) + (Math.Abs(denominator) >> 1)) / Math.Abs(denominator));
    }

    private static int OctAbs(Complex32 value)
    {
        var real = Math.Abs(value.Real);
        var imaginary = Math.Abs(value.Imaginary);
        var max = Math.Max(real, imaginary);
        return Math.Max(max, Reduce((real + imaginary) * 181, 8));
    }

    private sealed class FftImage(byte[] buffer, int offset, int width, int size, bool invertOnRead)
    {
        public int Width => width;

        public int Read(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= size)
            {
                return 0;
            }

            var value = buffer[offset + x + y];
            return invertOnRead ? unchecked((byte)~value) : value;
        }

        public void Write(int x, int y, byte value)
        {
            if (x < 0 || x >= width || y < 0 || y >= size)
            {
                return;
            }

            buffer[offset + x + y] = value;
        }

        public void AddValue(int x, int y, byte value)
        {
            if (x < 0 || x >= width || y < 0 || y >= size)
            {
                return;
            }

            var index = offset + x + y;
            buffer[index] = unchecked((byte)(buffer[index] + value));
        }

        public void Inverse()
        {
            for (var index = 0; index < size; index++)
            {
                buffer[offset + index] = unchecked((byte)~buffer[offset + index]);
            }
        }
    }

    private sealed class Envelope(int size, int spacing, bool left, bool right)
    {
        private readonly int floor = Math.Min(size + 1 - spacing, spacing);

        public int this[int index] =>
            Math.Min(
                Math.Min(left ? floor : index + 1, right ? floor : size - index),
                floor);

        public int Norm => size + 1 - spacing;
    }

    private readonly record struct Complex32(int Real, int Imaginary)
    {
        public static readonly Complex32 Zero = new(0, 0);

        public Complex32 Conjugate()
        {
            return new(Real, -Imaginary);
        }

        public static Complex32 operator +(Complex32 left, Complex32 right)
        {
            return new(left.Real + right.Real, left.Imaginary + right.Imaginary);
        }

        public static Complex32 operator -(Complex32 left, Complex32 right)
        {
            return new(left.Real - right.Real, left.Imaginary - right.Imaginary);
        }

        public static Complex32 operator *(Complex32 left, Complex32 right)
        {
            return new(
                (left.Real * right.Real) - (left.Imaginary * right.Imaginary),
                (left.Real * right.Imaginary) + (left.Imaginary * right.Real));
        }

        public static Complex32 operator *(Complex32 value, int scalar)
        {
            return new(value.Real * scalar, value.Imaginary * scalar);
        }

        public static Complex32 operator <<(Complex32 value, int shift)
        {
            return new(value.Real << shift, value.Imaginary << shift);
        }
    }
}
