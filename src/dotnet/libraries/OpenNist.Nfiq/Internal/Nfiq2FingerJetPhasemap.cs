namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2FingerJetPhasemap
{
    private const byte s_phasemapFiller = 127;
    private const int s_orientationScale = 4;
    private const int s_filterHalfSize = 4;

    public static byte[] Build(
        Nfiq2FingerJetPreparedImage preparedImage,
        IReadOnlyList<Nfiq2FingerJetComplex> orientation)
    {
        ArgumentNullException.ThrowIfNull(preparedImage);
        ArgumentNullException.ThrowIfNull(orientation);

        if (orientation.Count != preparedImage.OrientationMapSize)
        {
            throw new ArgumentException("Orientation map size does not match the prepared image metadata.", nameof(orientation));
        }

        var width = preparedImage.Width;
        var size = preparedImage.Pixels.Length;
        var orientationWidth = width / s_orientationScale;
        var computedLength = size - width * s_filterHalfSize * 2;
        var output = GC.AllocateUninitializedArray<byte>(size);
        output.AsSpan(computedLength).Fill(s_phasemapFiller);
        var inputLength = size + orientation.Count * 2;
        var pixelsBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(inputLength);
        CreateNativeInputLayout(preparedImage.Pixels.Span, orientation, pixelsBuffer.AsSpan(0, inputLength));

        try
        {
            var pixels = pixelsBuffer.AsSpan(0, inputLength);
            var x20 = new Conv9State(-112, -7, 48, 14, 1, symmetric: true);
            var x22 = new Conv9State(122, 78, 20, 2, 0, symmetric: true);
            var x33 = new Conv9State(122, 78, 20, 2, 0, symmetric: true);
            var x21 = new Conv9State(0, 71, 37, 6, 0, symmetric: false);
            var x30 = new Conv9State(0, -92, -12, 8, 1, symmetric: false);
            var x32 = new Conv9State(0, 52, 27, 4, 0, symmetric: false);
            var x31 = new Conv9State(-90, -23, 21, 7, 1, symmetric: true);

            var pointerIndex = (width + 1) * s_filterHalfSize;
            var outputIndex = 0;
            for (var orientationRow = orientationWidth; orientationRow < size / (s_orientationScale * s_orientationScale) - orientationWidth; orientationRow += orientationWidth)
            {
                for (var i = 0; i < s_orientationScale; i++)
                {
                    for (var orientationX = 0; orientationX < orientationWidth; orientationX++)
                    {
                        var orientationValue = orientation[orientationRow + orientationX];
                        var a10 = orientationValue.Real;
                        var a11 = orientationValue.Imaginary;

                        var a20 = SincosNorm(a10 * a10);
                        var a21 = SincosNorm(2 * a10 * a11);
                        var a22 = SincosNorm(a11 * a11);

                        var a30 = SincosNorm(a10 * a20);
                        var a31 = SincosNorm(3 * a20 * a11);
                        var a32 = SincosNorm(3 * a22 * a10);
                        var a33 = SincosNorm(a11 * a22);

                        for (var j = 0; j < s_filterHalfSize; j++, pointerIndex++, outputIndex++)
                        {
                            var v20 = x20.Next(x22.ComputeVertical(pixels, width, pointerIndex));
                            var v21 = x21.Next(x21.ComputeVertical(pixels, width, pointerIndex));
                            var v22 = x22.Next(x20.ComputeVertical(pixels, width, pointerIndex));

                            var v30 = x30.Next(x33.ComputeVertical(pixels, width, pointerIndex));
                            var v31 = x31.Next(x32.ComputeVertical(pixels, width, pointerIndex));
                            var v32 = x32.Next(x31.ComputeVertical(pixels, width, pointerIndex));
                            var v33 = x33.Next(x30.ComputeVertical(pixels, width, pointerIndex));

                            var x2 = a20 * v20 + a21 * v21 + a22 * v22;
                            var x3 = a30 * v30 + a31 * v31 + a32 * v32 + a33 * v33;
                            x2 = (x2 + 1024) >> 11;
                            x3 = (x3 + 1024) >> 11;
                            output[outputIndex] = x2 != 0
                                ? (byte)((127 - Nfiq2FingerJetOrientationSupport.OctSign(new(x2, x3)).Real) & 0xf0)
                                : s_phasemapFiller;
                        }
                    }
                }
            }

            if (outputIndex != computedLength)
            {
                throw new InvalidOperationException($"Unexpected phasemap output length. expected={computedLength}, actual={outputIndex}.");
            }

            return output;
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(pixelsBuffer, clearArray: false);
        }
    }

    private static int SincosNorm(int value)
    {
        return (value + 64) >> 7;
    }

    private static void CreateNativeInputLayout(
        ReadOnlySpan<byte> imagePixels,
        IReadOnlyList<Nfiq2FingerJetComplex> orientation,
        Span<byte> destination)
    {
        imagePixels.CopyTo(destination);
        for (var index = 0; index < orientation.Count; index++)
        {
            var offset = imagePixels.Length + index * 2;
            destination[offset] = unchecked((byte)(sbyte)orientation[index].Real);
            destination[offset + 1] = unchecked((byte)(sbyte)orientation[index].Imaginary);
        }
    }

    private sealed class Conv9State(
        int t0,
        int t1,
        int t2,
        int t3,
        int t4,
        bool symmetric)
    {
        private readonly int[] _buffer = new int[8];
        private int _index;

        public int ComputeVertical(ReadOnlySpan<byte> pixels, int width, int offset)
        {
            var v1 = Combine(pixels[offset + width], pixels[offset - width]);
            var v2 = Combine(pixels[offset + width * 2], pixels[offset - width * 2]);
            var v3 = Combine(pixels[offset + width * 3], pixels[offset - width * 3]);
            var v4 = Combine(pixels[offset + width * 4], pixels[offset - width * 4]);
            return pixels[offset] * t0 + v1 * t1 + v2 * t2 + v3 * t3 + v4 * t4;
        }

        public int Next(int value)
        {
            var v4 = Combine(value, Get(0));
            var v3 = Combine(Get(7), Get(1));
            var v2 = Combine(Get(6), Get(2));
            var v1 = Combine(Get(5), Get(3));
            var output = Get(4) * t0 + v1 * t1 + v2 * t2 + v3 * t3 + v4 * t4;
            _buffer[_index] = value;
            _index++;
            if (_index >= _buffer.Length)
            {
                _index = 0;
            }

            return output;
        }

        private int Get(int offset)
        {
            return _buffer[(_index + offset) & 7];
        }

        private int Combine(int left, int right)
        {
            return symmetric || t0 != 0 ? left + right : left - right;
        }
    }
}
