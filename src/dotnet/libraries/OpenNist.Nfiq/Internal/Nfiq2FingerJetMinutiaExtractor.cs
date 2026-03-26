namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2FingerJetMinutiaExtractor
{
    private const int s_invalid = 255;
    private const int s_invalidBlockValue = -1;
    private const int s_orientationFilterSize = 13;
    private const int s_startRowOffset = 3;
    private const int s_max2D5FastXOffset = 4;
    private const int s_max2D5FastYOffset = 4;
    private const int s_smmeThreshold = 1328;
    private const int s_angleSweepStep = 4;
    private const int s_angleSweepStart = -2;
    private const int s_angleSweepEnd = 2;
    private const int s_typeThreshold = 105;

    public static IReadOnlyList<Nfiq2FingerJetRawMinutia> ExtractRaw(
        byte[] phasemap,
        int width,
        int capacity = byte.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(phasemap);
        return ExtractRaw(phasemap.AsSpan(), width, capacity);
    }

    public static IReadOnlyList<Nfiq2FingerJetRawMinutia> ExtractRaw(
        ReadOnlySpan<byte> phasemap,
        int width,
        int capacity = byte.MaxValue)
    {
        return ExtractRawCore(phasemap, width, capacity);
    }

    public static Nfiq2FingerJetMinutiaExtractionTrace Trace(
        ReadOnlySpan<byte> phasemap,
        int width,
        int capacity = byte.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        if (phasemap.IsEmpty || phasemap.Length % width != 0)
        {
            throw new ArgumentException("Phasemap length must be a non-zero multiple of width.", nameof(phasemap));
        }

        var size = phasemap.Length;
        var height = size / width;
        var widthHalf = width / 2;
        var endIndex = size - width;
        var directionMap = new byte[size];
        var candidateMap = new byte[size];

        var candidates = new List<Nfiq2FingerJetRawMinutia>(Math.Min(capacity, 128));
        var cxx = new Convolution3X3(width, t0: 2, t1: 1, normBits: 5);
        var cxy = new Convolution3X3(width, t0: 2, t1: 1, normBits: 5);
        var cyy = new Convolution3X3(width, t0: 2, t1: 1, normBits: 5);
        var maxima = new Max2D5Fast(width);
        var candidateDelay = new PackedBoolDelay((width * (s_orientationFilterSize - s_max2D5FastYOffset)) - s_max2D5FastXOffset, initialValue: false);
        var directionAccumulator = new DirectionAccumulator(widthHalf, s_orientationFilterSize);
        var orientationSums = new Nfiq2FingerJetComplex[widthHalf];
        var direction = new byte[widthHalf];

        for (var y = 0; y < height + s_orientationFilterSize; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pIndex = (s_startRowOffset * width) + (y * width) + x;
                var outside = pIndex >= endIndex;
                var gx = 0;
                var gy = 0;
                if (!outside)
                {
                    outside = phasemap[pIndex + 1] == s_invalid
                        || phasemap[pIndex - 3] == s_invalid
                        || phasemap[pIndex + width] == s_invalid
                        || phasemap[pIndex - (3 * width)] == s_invalid;
                    gx = phasemap[pIndex + 1] - phasemap[pIndex - 1];
                    gy = phasemap[pIndex + width] - phasemap[pIndex - width];
                }

                var gxx = cxx.Next(gx * gx);
                var gxy = cxy.Next(gx * gy);
                var gyy = cyy.Next(gy * gy);

                var e1b = (gxx + gyy) > (2 * s_smmeThreshold);
                var b2 = (s_smmeThreshold - gxx) * (s_smmeThreshold - gyy) - (gxy * gxy);
                var e = e1b && b2 > 0;

                if ((x & 1) == 0 && (y & 1) == 0)
                {
                    var orientation = Nfiq2FingerJetOrientationSupport.OctSign(new(gxx - gyy, 2 * gxy));
                    var index = x / 2;
                    var delayed = directionAccumulator.NextVertical(orientation);
                    orientationSums[index] = new(
                        orientationSums[index].Real + orientation.Real - delayed.Real,
                        orientationSums[index].Imaginary + orientation.Imaginary - delayed.Imaginary);
                }

                var blockValue = s_invalidBlockValue;
                if (!outside)
                {
                    blockValue = e ? b2 : 0;
                }

                var candidate = candidateDelay.Next(maxima.Next(blockValue, x, y));
                var xp = x - Convolution3X3.XOffset;
                var yp = y + s_startRowOffset - Convolution3X3.YOffset - s_orientationFilterSize;
                candidate = candidate
                    && Nfiq2FingerJetMinutiaExtractionSupport.IsInFootprint(xp, yp, width, size, phasemap);
                var candidateTraceIndex = (y * width) + x;
                if ((uint)candidateTraceIndex < (uint)candidateMap.Length)
                {
                    candidateMap[candidateTraceIndex] = candidate ? (byte)1 : (byte)0;
                    directionMap[candidateTraceIndex] = direction[x / 2];
                }

                if (candidate)
                {
                    var bestConfidence = 0;
                    var confirmed = false;
                    var type = false;
                    byte angle = 0;

                    for (var delta = s_angleSweepStart; delta <= s_angleSweepEnd; delta += s_angleSweepStep)
                    {
                        var currentAngle = unchecked((byte)(direction[x / 2] + delta));
                        var result = Nfiq2FingerJetBifFilterSupport.Evaluate(
                            phasemap,
                            width,
                            x: xp,
                            y: yp,
                            c: Nfiq2FingerJetMath.Cos(currentAngle),
                            s: Nfiq2FingerJetMath.Sin(currentAngle));
                        if (!result.Confirmed)
                        {
                            continue;
                        }

                        confirmed = true;
                        if (result.Confidence > bestConfidence)
                        {
                            bestConfidence = result.Confidence;
                            angle = currentAngle;
                            type = result.Type;
                            if (result.Rotate180)
                            {
                                angle = unchecked((byte)(angle + 128));
                            }
                        }
                    }

                    if (confirmed)
                    {
                        if (!Nfiq2FingerJetMinutiaExtractionSupport.TryAdjustAngle(ref angle, xp, yp, phasemap, width, size, relative: false))
                        {
                            Nfiq2FingerJetMinutiaExtractionSupport.TryAdjustAngle(ref angle, xp, yp, phasemap, width, size, relative: true);
                        }

                        var minutiaType = 0;
                        if (bestConfidence > s_typeThreshold)
                        {
                            minutiaType = type ? 1 : 2;
                        }

                        candidates.Add(new(
                            xp,
                            yp,
                            angle,
                            bestConfidence,
                            minutiaType));
                    }
                }
            }

            if ((y & 1) == 0)
            {
                directionAccumulator.FillNextDirectionRow(orientationSums, direction);
            }
        }

        return new(
            Nfiq2FingerJetMinutiaRanking.SelectTopByConfidence(candidates, capacity),
            directionMap,
            candidateMap);
    }

    public static Nfiq2FingerJetMinutiaExtractionTrace Trace(
        byte[] phasemap,
        int width,
        int capacity = byte.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(phasemap);
        return Trace(phasemap.AsSpan(), width, capacity);
    }

    public static Nfiq2FingerJetDetailedExtractionTrace TraceDetailed(
        byte[] phasemap,
        int width,
        int capacity = byte.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(phasemap);
        return TraceDetailed(phasemap.AsSpan(), width, capacity);
    }

    public static Nfiq2FingerJetDetailedExtractionTrace TraceDetailed(
        ReadOnlySpan<byte> phasemap,
        int width,
        int capacity = byte.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        if (phasemap.IsEmpty || phasemap.Length % width != 0)
        {
            throw new ArgumentException("Phasemap length must be a non-zero multiple of width.", nameof(phasemap));
        }

        var size = phasemap.Length;
        var height = size / width;
        var widthHalf = width / 2;
        var endIndex = size - width;
        var directionMap = new byte[size];
        var candidateMap = new byte[size];

        var candidates = new List<Nfiq2FingerJetRawMinutia>(Math.Min(capacity, 128));
        var debugEntries = new List<Nfiq2FingerJetMinutiaDebugEntry>(Math.Min(capacity, 128));
        var cxx = new Convolution3X3(width, t0: 2, t1: 1, normBits: 5);
        var cxy = new Convolution3X3(width, t0: 2, t1: 1, normBits: 5);
        var cyy = new Convolution3X3(width, t0: 2, t1: 1, normBits: 5);
        var maxima = new Max2D5Fast(width);
        var candidateDelay = new PackedBoolDelay((width * (s_orientationFilterSize - s_max2D5FastYOffset)) - s_max2D5FastXOffset, initialValue: false);
        var directionAccumulator = new DirectionAccumulator(widthHalf, s_orientationFilterSize);
        var orientationSums = new Nfiq2FingerJetComplex[widthHalf];
        var direction = new byte[widthHalf];

        for (var y = 0; y < height + s_orientationFilterSize; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pIndex = (s_startRowOffset * width) + (y * width) + x;
                var outside = pIndex >= endIndex;
                var gx = 0;
                var gy = 0;
                if (!outside)
                {
                    outside = phasemap[pIndex + 1] == s_invalid
                        || phasemap[pIndex - 3] == s_invalid
                        || phasemap[pIndex + width] == s_invalid
                        || phasemap[pIndex - (3 * width)] == s_invalid;
                    gx = phasemap[pIndex + 1] - phasemap[pIndex - 1];
                    gy = phasemap[pIndex + width] - phasemap[pIndex - width];
                }

                var gxx = cxx.Next(gx * gx);
                var gxy = cxy.Next(gx * gy);
                var gyy = cyy.Next(gy * gy);

                var e1b = (gxx + gyy) > (2 * s_smmeThreshold);
                var b2 = (s_smmeThreshold - gxx) * (s_smmeThreshold - gyy) - (gxy * gxy);
                var e = e1b && b2 > 0;

                if ((x & 1) == 0 && (y & 1) == 0)
                {
                    var orientation = Nfiq2FingerJetOrientationSupport.OctSign(new(gxx - gyy, 2 * gxy));
                    var index = x / 2;
                    var delayed = directionAccumulator.NextVertical(orientation);
                    orientationSums[index] = new(
                        orientationSums[index].Real + orientation.Real - delayed.Real,
                        orientationSums[index].Imaginary + orientation.Imaginary - delayed.Imaginary);
                }

                var blockValue = s_invalidBlockValue;
                if (!outside)
                {
                    blockValue = e ? b2 : 0;
                }

                var candidate = candidateDelay.Next(maxima.Next(blockValue, x, y));
                var xp = x - Convolution3X3.XOffset;
                var yp = y + s_startRowOffset - Convolution3X3.YOffset - s_orientationFilterSize;
                candidate = candidate
                    && Nfiq2FingerJetMinutiaExtractionSupport.IsInFootprint(xp, yp, width, size, phasemap);
                var candidateTraceIndex = (y * width) + x;
                if ((uint)candidateTraceIndex < (uint)candidateMap.Length)
                {
                    candidateMap[candidateTraceIndex] = candidate ? (byte)1 : (byte)0;
                    directionMap[candidateTraceIndex] = direction[x / 2];
                }

                if (candidate)
                {
                    var bestConfidence = 0;
                    var confirmed = false;
                    var type = false;
                    byte angle = 0;

                    for (var delta = s_angleSweepStart; delta <= s_angleSweepEnd; delta += s_angleSweepStep)
                    {
                        var currentAngle = unchecked((byte)(direction[x / 2] + delta));
                        var result = Nfiq2FingerJetBifFilterSupport.Evaluate(
                            phasemap,
                            width,
                            x: xp,
                            y: yp,
                            c: Nfiq2FingerJetMath.Cos(currentAngle),
                            s: Nfiq2FingerJetMath.Sin(currentAngle));
                        if (!result.Confirmed)
                        {
                            continue;
                        }

                        confirmed = true;
                        if (result.Confidence > bestConfidence)
                        {
                            bestConfidence = result.Confidence;
                            angle = currentAngle;
                            type = result.Type;
                            if (result.Rotate180)
                            {
                                angle = unchecked((byte)(angle + 128));
                            }
                        }
                    }

                    if (confirmed)
                    {
                        var candidateAngle = angle;
                        var adjustedAbsolute = Nfiq2FingerJetMinutiaExtractionSupport.TryAdjustAngle(ref angle, xp, yp, phasemap, width, size, relative: false);
                        var adjustedRelative = false;
                        if (!adjustedAbsolute)
                        {
                            adjustedRelative = Nfiq2FingerJetMinutiaExtractionSupport.TryAdjustAngle(ref angle, xp, yp, phasemap, width, size, relative: true);
                        }

                        var minutiaType = 0;
                        if (bestConfidence > s_typeThreshold)
                        {
                            minutiaType = type ? 1 : 2;
                        }

                        candidates.Add(new(
                            xp,
                            yp,
                            angle,
                            bestConfidence,
                            minutiaType));
                        debugEntries.Add(new(
                            xp,
                            yp,
                            candidateAngle,
                            angle,
                            bestConfidence,
                            minutiaType,
                            adjustedAbsolute,
                            adjustedRelative));
                    }
                }
            }

            if ((y & 1) == 0)
            {
                directionAccumulator.FillNextDirectionRow(orientationSums, direction);
            }
        }

        var ranked = Nfiq2FingerJetMinutiaRanking.SelectTopByConfidence(candidates, capacity);
        var rankedDebug = debugEntries
            .OrderByDescending(static entry => entry.Confidence)
            .ThenBy(static entry => entry.Y)
            .ThenBy(static entry => entry.X)
            .ThenBy(static entry => entry.FinalAngle)
            .Take(capacity)
            .ToArray();

        return new(ranked, directionMap, candidateMap, rankedDebug);
    }

    private static IReadOnlyList<Nfiq2FingerJetRawMinutia> ExtractRawCore(
        ReadOnlySpan<byte> phasemap,
        int width,
        int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        if (phasemap.IsEmpty || phasemap.Length % width != 0)
        {
            throw new ArgumentException("Phasemap length must be a non-zero multiple of width.", nameof(phasemap));
        }

        var size = phasemap.Length;
        var height = size / width;
        var widthHalf = width / 2;
        var endIndex = size - width;

        var candidates = new List<Nfiq2FingerJetRawMinutia>(Math.Min(capacity, 128));
        var cxx = new Convolution3X3(width, t0: 2, t1: 1, normBits: 5);
        var cxy = new Convolution3X3(width, t0: 2, t1: 1, normBits: 5);
        var cyy = new Convolution3X3(width, t0: 2, t1: 1, normBits: 5);
        var maxima = new Max2D5Fast(width);
        var candidateDelay = new PackedBoolDelay((width * (s_orientationFilterSize - s_max2D5FastYOffset)) - s_max2D5FastXOffset, initialValue: false);
        var directionAccumulator = new DirectionAccumulator(widthHalf, s_orientationFilterSize);
        var orientationSums = new Nfiq2FingerJetComplex[widthHalf];
        var direction = new byte[widthHalf];

        for (var y = 0; y < height + s_orientationFilterSize; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pIndex = (s_startRowOffset * width) + (y * width) + x;
                var outside = pIndex >= endIndex;
                var gx = 0;
                var gy = 0;
                if (!outside)
                {
                    outside = phasemap[pIndex + 1] == s_invalid
                        || phasemap[pIndex - 3] == s_invalid
                        || phasemap[pIndex + width] == s_invalid
                        || phasemap[pIndex - (3 * width)] == s_invalid;
                    gx = phasemap[pIndex + 1] - phasemap[pIndex - 1];
                    gy = phasemap[pIndex + width] - phasemap[pIndex - width];
                }

                var gxx = cxx.Next(gx * gx);
                var gxy = cxy.Next(gx * gy);
                var gyy = cyy.Next(gy * gy);

                var e1b = (gxx + gyy) > (2 * s_smmeThreshold);
                var b2 = (s_smmeThreshold - gxx) * (s_smmeThreshold - gyy) - (gxy * gxy);
                var e = e1b && b2 > 0;

                if ((x & 1) == 0 && (y & 1) == 0)
                {
                    var orientation = Nfiq2FingerJetOrientationSupport.OctSign(new(gxx - gyy, 2 * gxy));
                    var index = x / 2;
                    var delayed = directionAccumulator.NextVertical(orientation);
                    orientationSums[index] = new(
                        orientationSums[index].Real + orientation.Real - delayed.Real,
                        orientationSums[index].Imaginary + orientation.Imaginary - delayed.Imaginary);
                }

                var blockValue = s_invalidBlockValue;
                if (!outside)
                {
                    blockValue = e ? b2 : 0;
                }

                var candidate = candidateDelay.Next(maxima.Next(blockValue, x, y));
                var xp = x - Convolution3X3.XOffset;
                var yp = y + s_startRowOffset - Convolution3X3.YOffset - s_orientationFilterSize;
                candidate = candidate
                    && Nfiq2FingerJetMinutiaExtractionSupport.IsInFootprint(xp, yp, width, size, phasemap);

                if (candidate)
                {
                    var bestConfidence = 0;
                    var confirmed = false;
                    var type = false;
                    byte angle = 0;

                    for (var delta = s_angleSweepStart; delta <= s_angleSweepEnd; delta += s_angleSweepStep)
                    {
                        var currentAngle = unchecked((byte)(direction[x / 2] + delta));
                        var result = Nfiq2FingerJetBifFilterSupport.Evaluate(
                            phasemap,
                            width,
                            x: xp,
                            y: yp,
                            c: Nfiq2FingerJetMath.Cos(currentAngle),
                            s: Nfiq2FingerJetMath.Sin(currentAngle));
                        if (!result.Confirmed)
                        {
                            continue;
                        }

                        confirmed = true;
                        if (result.Confidence > bestConfidence)
                        {
                            bestConfidence = result.Confidence;
                            angle = currentAngle;
                            type = result.Type;
                            if (result.Rotate180)
                            {
                                angle = unchecked((byte)(angle + 128));
                            }
                        }
                    }

                    if (confirmed)
                    {
                        if (!Nfiq2FingerJetMinutiaExtractionSupport.TryAdjustAngle(ref angle, xp, yp, phasemap, width, size, relative: false))
                        {
                            Nfiq2FingerJetMinutiaExtractionSupport.TryAdjustAngle(ref angle, xp, yp, phasemap, width, size, relative: true);
                        }

                        var minutiaType = 0;
                        if (bestConfidence > s_typeThreshold)
                        {
                            minutiaType = type ? 1 : 2;
                        }

                        candidates.Add(new(
                            xp,
                            yp,
                            angle,
                            bestConfidence,
                            minutiaType));
                    }
                }
            }

            if ((y & 1) == 0)
            {
                directionAccumulator.FillNextDirectionRow(orientationSums, direction);
            }
        }

        return Nfiq2FingerJetMinutiaRanking.SelectTopByConfidence(candidates, capacity);
    }

    private sealed class Convolution3X3
    {
        public const int XOffset = 1;
        public const int YOffset = 1;

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

    private sealed class Max2D5Fast
    {
        private readonly int[][] _buffer;
        private readonly bool[][] _maxima;
        public Max2D5Fast(int width)
        {
            _buffer = new int[7][];
            _maxima = new bool[7][];
            for (var row = 0; row < _buffer.Length; row++)
            {
                _buffer[row] = GC.AllocateUninitializedArray<int>(width);
                Array.Fill(_buffer[row], -1);
                _maxima[row] = new bool[width + s_max2D5FastXOffset];
            }
        }

        public bool Next(int value, int x, int y)
        {
            var row = y % 7;
            _buffer[row][x] = value;
            if (x == 0)
            {
                Array.Clear(_maxima[Mod7(y - 1)], 0, _maxima[row].Length);
            }

            if (y >= 6 && (y % 3) == 0 && x >= 6 && (x % 3) == 0)
            {
                FindMaxInBlock(y - 4, x - 4);
            }

            if (!_maxima[row][x])
            {
                return false;
            }

            _maxima[row][x] = false;
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
                    if (i2 >= i && i2 <= i + 2 && j2 >= j && j2 <= j + 2)
                    {
                        continue;
                    }

                    var current = _buffer[Mod7(i2)][j2];
                    if (current < 0 || current > maxValue)
                    {
                        return;
                    }
                }
            }

            _maxima[(maxI + s_max2D5FastYOffset) % 7][maxJ + s_max2D5FastXOffset] = true;
        }

        private static int Mod7(int value)
        {
            var result = value % 7;
            return result < 0 ? result + 7 : result;
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
            var byteSize = Math.Max((delayLength + 7) / 8, 1);
            _buffer = new byte[byteSize];
            _initMask = delayLength == 0
                ? (byte)1
                : (byte)(1 << ((-delayLength) & 7));
            _mask = _initMask;

            if (initialValue)
            {
                Array.Fill(_buffer, byte.MaxValue);
            }
        }

        public bool Next(bool value)
        {
            var output = (_buffer[_pointer] & _mask) != 0;
            if (value)
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

    private sealed class DirectionAccumulator
    {
        private readonly Nfiq2FingerJetComplex[] _verticalBuffer;
        private readonly int _widthHalf;
        private readonly int _filterSize;
        private readonly int _filterHalf;
        private int _verticalIndex;

        public DirectionAccumulator(int widthHalf, int filterSize)
        {
            _widthHalf = widthHalf;
            _filterSize = filterSize;
            _filterHalf = filterSize / 2;
            _verticalBuffer = new Nfiq2FingerJetComplex[filterSize * widthHalf];
        }

        public Nfiq2FingerJetComplex NextVertical(Nfiq2FingerJetComplex value)
        {
            var output = _verticalBuffer[_verticalIndex];
            _verticalBuffer[_verticalIndex] = value;
            _verticalIndex++;
            if (_verticalIndex >= _verticalBuffer.Length)
            {
                _verticalIndex = 0;
            }

            return output;
        }

        public void FillNextDirectionRow(ReadOnlySpan<Nfiq2FingerJetComplex> verticalSums, Span<byte> destination)
        {
            var horizontalReal = 0;
            var horizontalImaginary = 0;
            for (var x = 0; x < _widthHalf + _filterHalf; x++)
            {
                if (x < _widthHalf)
                {
                    horizontalReal += verticalSums[x].Real;
                    horizontalImaginary += verticalSums[x].Imaginary;
                }

                if (x >= _filterSize)
                {
                    horizontalReal -= verticalSums[x - _filterSize].Real;
                    horizontalImaginary -= verticalSums[x - _filterSize].Imaginary;
                }

                if (x >= _filterHalf)
                {
                    destination[x - _filterHalf] = (byte)(Nfiq2FingerJetMath.Atan2IntMath(horizontalReal, horizontalImaginary) / 2);
                }
            }
        }
    }
}
