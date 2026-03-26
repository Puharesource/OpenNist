namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2FingerJetMinutiaExtractionSupport
{
    private const byte s_phasemapFiller = 127;
    private const int s_max2D5FastXOffset = 4;
    private const int s_max2D5FastYOffset = 4;

    private static ReadOnlySpan<sbyte> NeighborOffsets =>
    [
        0, 1, 1, 1, 0, -1, -1, -1,
    ];

    public static bool IsInFootprint(int x, int y, int width, int size, ReadOnlySpan<byte> phasemap, int neighborhood = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        var xp = x + neighborhood;
        var yp = y + neighborhood;
        if (xp < (neighborhood * 2) || yp < (neighborhood * 2) || xp >= width)
        {
            return false;
        }

        var offset = xp + (yp * width);
        if ((uint)offset >= (uint)size)
        {
            return false;
        }

        return phasemap[offset - (2 * neighborhood)] != s_phasemapFiller
            && phasemap[offset] != s_phasemapFiller
            && phasemap[offset - ((width * 2 + 2) * neighborhood)] != s_phasemapFiller
            && phasemap[offset - (width * 2 * neighborhood)] != s_phasemapFiller;
    }

    public static bool TryAdjustAngle(
        ref byte angle,
        int x,
        int y,
        ReadOnlySpan<byte> phasemap,
        int width,
        int size,
        bool relative)
    {
        var start = new AnglePoint((short)x, (short)y, (byte)(angle >> 5), phasemap, width);
        var current = start.Next(phasemap, width, min: false, relative);
        var p2 = start.Next(phasemap, width, min: true, relative);
        var min = (255 - p2.Value) > current.Value;
        if (min)
        {
            current = p2;
        }

        var d0 = 0u;
        for (var iteration = 0; iteration < 20; iteration++)
        {
            current = current.Next(phasemap, width, min, relative);
            if (min == (current.Value >= 128))
            {
                break;
            }

            if (!IsInFootprint(current.X, current.Y, width, size, phasemap))
            {
                break;
            }

            var d = DistanceSquared(current.X - start.X, current.Y - start.Y);
            if (d <= d0)
            {
                break;
            }

            d0 = d;
            if (d > 400)
            {
                break;
            }

            if (iteration == 0)
            {
                p2 = current;
            }
        }

        if (d0 < (14 * 14))
        {
            return false;
        }

        angle = Nfiq2FingerJetMath.Atan2IntMath(-(current.Y - p2.Y), current.X - p2.X);
        return true;
    }

    public static bool[] RunMax2D5Fast(ReadOnlySpan<int> values, int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        if (values.Length == 0 || values.Length % width != 0)
        {
            throw new ArgumentException("Value count must be a non-zero multiple of width.", nameof(values));
        }

        var matcher = new Max2D5Fast(width);
        var result = new bool[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            var x = index % width;
            var y = index / width;
            result[index] = matcher.Next(values[index], x, y);
        }

        return result;
    }

    public static int[] RunConv2D3(ReadOnlySpan<int> values, int width, int t0, int t1, int normBits)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(normBits);
        if (values.IsEmpty)
        {
            return [];
        }

        var result = new int[values.Length];
        var verticalDelay1 = new RingDelay(width);
        var verticalDelay2 = new RingDelay(width);
        var horizontalDelay1 = new SingleDelay();
        var horizontalDelay2 = new SingleDelay();

        for (var index = 0; index < values.Length; index++)
        {
            var v0 = values[index];
            var v1 = verticalDelay1.Next(v0);
            var v2 = verticalDelay2.Next(v1);
            var h0 = (v1 * t0) + ((v2 + v0) * t1);
            var h1 = horizontalDelay1.Next(h0);
            var h2 = horizontalDelay2.Next(h1);
            var output = (h1 * t0) + ((h2 + h0) * t1);
            result[index] = (output + (1 << (normBits - 1))) >> normBits;
        }

        return result;
    }

    public static bool[] RunBoolDelay(ReadOnlySpan<bool> values, int delayLength, bool initialValue = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(delayLength);
        if (values.IsEmpty)
        {
            return [];
        }

        var delay = new PackedBoolDelay(delayLength, initialValue);
        var result = new bool[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            result[index] = delay.Next(values[index]);
        }

        return result;
    }

    public static byte[] RunDirectionAccumulator(
        IReadOnlyList<Nfiq2FingerJetComplex> evenRowOrientations,
        int widthHalf,
        int rowCount,
        int orientationFilterSize = 13)
    {
        ArgumentNullException.ThrowIfNull(evenRowOrientations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(widthHalf);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(orientationFilterSize);
        if (evenRowOrientations.Count != checked(widthHalf * rowCount))
        {
            throw new ArgumentException("Orientation count must equal widthHalf * rowCount.", nameof(evenRowOrientations));
        }

        var accumulator = new DirectionAccumulator(widthHalf, orientationFilterSize);
        var result = new byte[evenRowOrientations.Count];
        for (var row = 0; row < rowCount; row++)
        {
            var rowSpan = new Nfiq2FingerJetComplex[widthHalf];
            for (var x = 0; x < widthHalf; x++)
            {
                rowSpan[x] = evenRowOrientations[(row * widthHalf) + x];
            }

            var direction = accumulator.NextRow(rowSpan);
            direction.CopyTo(result, row * widthHalf);
        }

        return result;
    }

    public static Nfiq2FingerJetComplex[] RunSmmeOrientationSequence(
        ReadOnlySpan<byte> phasemap,
        int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        if (phasemap.IsEmpty || phasemap.Length % width != 0)
        {
            throw new ArgumentException("Phasemap length must be a non-zero multiple of width.", nameof(phasemap));
        }

        const int orientationFilterSize = 13;
        const int startRowOffset = 3;

        var size = phasemap.Length;
        var height = size / width;
        var output = new List<Nfiq2FingerJetComplex>((((height + orientationFilterSize) + 1) / 2) * (width / 2));

        var cxx = new Convolution3X3(width, t0: 2, t1: 1, normBits: 5);
        var cxy = new Convolution3X3(width, t0: 2, t1: 1, normBits: 5);
        var cyy = new Convolution3X3(width, t0: 2, t1: 1, normBits: 5);
        var endIndex = size - width;

        for (var y = 0; y < height + orientationFilterSize; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pIndex = (startRowOffset * width) + (y * width) + x;
                var outside = pIndex >= endIndex;
                var gx = 0;
                var gy = 0;
                if (!outside)
                {
                    gx = phasemap[pIndex + 1] - phasemap[pIndex - 1];
                    gy = phasemap[pIndex + width] - phasemap[pIndex - width];
                }

                var gxx = cxx.Next(gx * gx);
                var gxy = cxy.Next(gx * gy);
                var gyy = cyy.Next(gy * gy);

                if ((x & 1) == 0 && (y & 1) == 0)
                {
                    output.Add(Nfiq2FingerJetOrientationSupport.OctSign(new(gxx - gyy, 2 * gxy)));
                }
            }
        }

        return output.ToArray();
    }

    private static uint DistanceSquared(int x, int y)
    {
        return unchecked((uint)((x * x) + (y * y)));
    }

    private sealed class Max2D5Fast
    {
        private readonly int[][] _buffer;
        private readonly bool[][] _maxSet;
        private readonly int _width;

        public Max2D5Fast(int width)
        {
            _width = width;
            _buffer = new int[7][];
            _maxSet = new bool[7][];

            for (var y = 0; y < 7; y++)
            {
                _buffer[y] = GC.AllocateUninitializedArray<int>(width);
                Array.Fill(_buffer[y], -1);
                _maxSet[y] = new bool[width + s_max2D5FastXOffset];
            }
        }

        public bool Next(int value, int x, int y)
        {
            var yIndex = y % 7;
            _buffer[yIndex][x] = value;

            if (x == 0)
            {
                var resetRow = Mod7(y - 1);
                for (var column = 0; column < _width + s_max2D5FastXOffset; column++)
                {
                    _maxSet[resetRow][column] = false;
                }
            }

            if (y >= 6 && y % 3 == 0 && x >= 6 && x % 3 == 0)
            {
                FindMaxInBlock(y - 4, x - 4);
            }

            if (!_maxSet[yIndex][x])
            {
                return false;
            }

            _maxSet[yIndex][x] = false;
            return true;
        }

        private void FindMaxInBlock(int i, int j)
        {
            var maxI = i;
            var maxJ = j;
            var maxValue = _buffer[maxI % 7][maxJ];
            for (var i2 = i; i2 <= i + 2; i2++)
            {
                for (var j2 = j; j2 <= j + 2; j2++)
                {
                    var current = _buffer[i2 % 7][j2];
                    if (current < 0)
                    {
                        return;
                    }

                    if (current > maxValue)
                    {
                        maxI = i2;
                        maxJ = j2;
                        maxValue = current;
                    }
                }
            }

            if (maxValue <= 0)
            {
                return;
            }

            for (var i2 = maxI - 2; i2 <= maxI + 2; i2++)
            {
                for (var j2 = maxJ - 2; j2 <= maxJ + 2; j2++)
                {
                    if (i2 < i || i2 > i + 2 || j2 < j || j2 > j + 2)
                    {
                        var current = _buffer[Mod7(i2)][j2];
                        if (current < 0 || current > maxValue)
                        {
                            return;
                        }
                    }
                }
            }

            _maxSet[(maxI + s_max2D5FastYOffset) % 7][maxJ + s_max2D5FastXOffset] = true;
        }

        private static int Mod7(int value)
        {
            var result = value % 7;
            return result < 0 ? result + 7 : result;
        }
    }

    private sealed class RingDelay
    {
        private readonly int[] _buffer;
        private int _index;

        public RingDelay(int length)
        {
            _buffer = new int[length];
        }

        public int Next(int input)
        {
            var output = _buffer[_index];
            _buffer[_index] = input;
            _index++;
            if (_index >= _buffer.Length)
            {
                _index = 0;
            }

            return output;
        }
    }

    private sealed class Convolution3X3
    {
        private readonly int[] _verticalDelay1;
        private readonly int[] _verticalDelay2;
        private readonly int _t0;
        private readonly int _t1;
        private readonly int _normBits;
        private int _verticalIndex1;
        private int _verticalIndex2;
        private int _horizontalDelay1;
        private int _horizontalDelay2;

        public Convolution3X3(int width, int t0, int t1, int normBits)
        {
            _t0 = t0;
            _t1 = t1;
            _normBits = normBits;
            _verticalDelay1 = new int[width];
            _verticalDelay2 = new int[width];
        }

        public int Next(int value)
        {
            var v1 = _verticalDelay1[_verticalIndex1];
            _verticalDelay1[_verticalIndex1] = value;
            _verticalIndex1++;
            if (_verticalIndex1 >= _verticalDelay1.Length)
            {
                _verticalIndex1 = 0;
            }

            var v2 = _verticalDelay2[_verticalIndex2];
            _verticalDelay2[_verticalIndex2] = v1;
            _verticalIndex2++;
            if (_verticalIndex2 >= _verticalDelay2.Length)
            {
                _verticalIndex2 = 0;
            }

            var h0 = (v1 * _t0) + ((v2 + value) * _t1);
            var h1 = _horizontalDelay1;
            _horizontalDelay1 = h0;
            var h2 = _horizontalDelay2;
            _horizontalDelay2 = h1;
            var output = (h1 * _t0) + ((h2 + h0) * _t1);
            return (output + (1 << (_normBits - 1))) >> _normBits;
        }
    }

    private sealed class SingleDelay
    {
        private int _previous;

        public int Next(int input)
        {
            var output = _previous;
            _previous = input;
            return output;
        }
    }

    private sealed class PackedBoolDelay
    {
        private readonly byte[] _buffer;
        private readonly byte _initMask;
        private int _pointer;
        private byte _mask;

        public PackedBoolDelay(int delayLength, bool initialValue)
        {
            var byteSize = (delayLength + 7) / 8;
            _buffer = new byte[Math.Max(byteSize, 1)];
            _initMask = delayLength == 0
                ? (byte)1
                : (byte)(1 << ((-delayLength) & 7));
            _mask = _initMask;

            if (initialValue)
            {
                Array.Fill(_buffer, byte.MaxValue);
            }
        }

        public bool Next(bool input)
        {
            var output = (_buffer[_pointer] & _mask) != 0;
            if (input)
            {
                _buffer[_pointer] |= _mask;
            }
            else
            {
                _buffer[_pointer] = (byte)(_buffer[_pointer] & ~_mask);
            }

            _mask <<= 1;
            if (_mask == 0)
            {
                _pointer++;
                if (_pointer >= _buffer.Length)
                {
                    _pointer = 0;
                    _mask = _initMask;
                }
                else
                {
                    _mask = 1;
                }
            }

            return output;
        }
    }

    private sealed class ComplexRingDelay
    {
        private readonly Nfiq2FingerJetComplex[] _buffer;
        private int _index;

        public ComplexRingDelay(int length)
        {
            _buffer = new Nfiq2FingerJetComplex[length];
        }

        public Nfiq2FingerJetComplex Next(Nfiq2FingerJetComplex input)
        {
            var output = _buffer[_index];
            _buffer[_index] = input;
            _index++;
            if (_index >= _buffer.Length)
            {
                _index = 0;
            }

            return output;
        }
    }

    private sealed class DirectionAccumulator
    {
        private readonly int _widthHalf;
        private readonly int _filterSize;
        private readonly int _filterHalf;
        private readonly ComplexRingDelay _verticalDelay;
        private readonly Nfiq2FingerJetComplex[] _verticalSum;

        public DirectionAccumulator(int widthHalf, int orientationFilterSize)
        {
            _widthHalf = widthHalf;
            _filterSize = orientationFilterSize;
            _filterHalf = orientationFilterSize / 2;
            _verticalDelay = new(orientationFilterSize * widthHalf);
            _verticalSum = new Nfiq2FingerJetComplex[widthHalf];
        }

        public byte[] NextRow(Nfiq2FingerJetComplex[] row)
        {
            var direction = new byte[_widthHalf];
            for (var x = 0; x < _widthHalf; x++)
            {
                var current = row[x];
                var delayed = _verticalDelay.Next(current);
                _verticalSum[x] = new(
                    _verticalSum[x].Real + current.Real - delayed.Real,
                    _verticalSum[x].Imaginary + current.Imaginary - delayed.Imaginary);
            }

            var horizontalReal = 0;
            var horizontalImaginary = 0;
            for (var x = 0; x < _widthHalf + _filterHalf; x++)
            {
                if (x < _widthHalf)
                {
                    horizontalReal += _verticalSum[x].Real;
                    horizontalImaginary += _verticalSum[x].Imaginary;
                }

                if (x >= _filterSize)
                {
                    horizontalReal -= _verticalSum[x - _filterSize].Real;
                    horizontalImaginary -= _verticalSum[x - _filterSize].Imaginary;
                }

                if (x >= _filterHalf)
                {
                    direction[x - _filterHalf] = (byte)(Nfiq2FingerJetMath.Atan2IntMath(horizontalReal, horizontalImaginary) / 2);
                }
            }

            return direction;
        }
    }

    private readonly record struct AnglePoint(short X, short Y, byte Angle, byte Value)
    {
        public AnglePoint(short x, short y, byte angle, ReadOnlySpan<byte> phasemap, int width)
            : this(x, y, angle, phasemap[x + (y * width)])
        {
        }

        public AnglePoint Next(ReadOnlySpan<byte> phasemap, int width, bool min, bool relative)
        {
            var best = new AnglePoint(0, 0, 0, min ? byte.MaxValue : byte.MinValue);
            for (var i = -1; i <= 1; i++)
            {
                var current = new AnglePoint(
                    (short)(X + NeighborOffsets[(Angle + i) & 7]),
                    (short)(Y + NeighborOffsets[(Angle + i - 2) & 7]),
                    relative ? (byte)((Angle + i) & 0xff) : Angle,
                    phasemap,
                    width);

                if (min ? current.Value <= best.Value : current.Value >= best.Value)
                {
                    best = current;
                }
            }

            return best;
        }
    }
}
