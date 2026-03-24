namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2FingerJetMinutiaExtractor
{
    private const int Invalid = 255;
    private const int InvalidBlockValue = -1;
    private const int OrientationFilterSize = 13;
    private const int StartRowOffset = 3;
    private const int Max2D5FastXOffset = 4;
    private const int Max2D5FastYOffset = 4;
    private const int SmmeThreshold = 1328;
    private const int AngleSweepStep = 4;
    private const int AngleSweepStart = -2;
    private const int AngleSweepEnd = 2;
    private const int TypeThreshold = 105;

    public static IReadOnlyList<Nfiq2FingerJetRawMinutia> ExtractRaw(
        byte[] phasemap,
        int width,
        int capacity = byte.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(phasemap);
        return Trace(phasemap.AsSpan(), width, capacity).RawMinutiae;
    }

    public static IReadOnlyList<Nfiq2FingerJetRawMinutia> ExtractRaw(
        ReadOnlySpan<byte> phasemap,
        int width,
        int capacity = byte.MaxValue)
    {
        return Trace(phasemap, width, capacity).RawMinutiae;
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
        var candidateDelay = new PackedBoolDelay((width * (OrientationFilterSize - Max2D5FastYOffset)) - Max2D5FastXOffset, initialValue: false);
        var directionAccumulator = new DirectionAccumulator(widthHalf, OrientationFilterSize);
        var orientationSums = new Nfiq2FingerJetComplex[widthHalf];
        var direction = new byte[widthHalf];

        for (var y = 0; y < height + OrientationFilterSize; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pIndex = (StartRowOffset * width) + (y * width) + x;
                var outside = pIndex >= endIndex;
                var gx = 0;
                var gy = 0;
                if (!outside)
                {
                    outside = phasemap[pIndex + 1] == Invalid
                        || phasemap[pIndex - 3] == Invalid
                        || phasemap[pIndex + width] == Invalid
                        || phasemap[pIndex - (3 * width)] == Invalid;
                    gx = phasemap[pIndex + 1] - phasemap[pIndex - 1];
                    gy = phasemap[pIndex + width] - phasemap[pIndex - width];
                }

                var gxx = cxx.Next(gx * gx);
                var gxy = cxy.Next(gx * gy);
                var gyy = cyy.Next(gy * gy);

                var e1b = (gxx + gyy) > (2 * SmmeThreshold);
                var b2 = (SmmeThreshold - gxx) * (SmmeThreshold - gyy) - (gxy * gxy);
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

                var blockValue = InvalidBlockValue;
                if (!outside)
                {
                    blockValue = e ? b2 : 0;
                }

                var candidate = candidateDelay.Next(maxima.Next(blockValue, x, y));
                var xp = x - Convolution3X3.XOffset;
                var yp = y + StartRowOffset - Convolution3X3.YOffset - OrientationFilterSize;
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

                    for (var delta = AngleSweepStart; delta <= AngleSweepEnd; delta += AngleSweepStep)
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
                        if (bestConfidence > TypeThreshold)
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
                direction = directionAccumulator.NextDirectionRow(orientationSums);
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
        var candidateDelay = new PackedBoolDelay((width * (OrientationFilterSize - Max2D5FastYOffset)) - Max2D5FastXOffset, initialValue: false);
        var directionAccumulator = new DirectionAccumulator(widthHalf, OrientationFilterSize);
        var orientationSums = new Nfiq2FingerJetComplex[widthHalf];
        var direction = new byte[widthHalf];

        for (var y = 0; y < height + OrientationFilterSize; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pIndex = (StartRowOffset * width) + (y * width) + x;
                var outside = pIndex >= endIndex;
                var gx = 0;
                var gy = 0;
                if (!outside)
                {
                    outside = phasemap[pIndex + 1] == Invalid
                        || phasemap[pIndex - 3] == Invalid
                        || phasemap[pIndex + width] == Invalid
                        || phasemap[pIndex - (3 * width)] == Invalid;
                    gx = phasemap[pIndex + 1] - phasemap[pIndex - 1];
                    gy = phasemap[pIndex + width] - phasemap[pIndex - width];
                }

                var gxx = cxx.Next(gx * gx);
                var gxy = cxy.Next(gx * gy);
                var gyy = cyy.Next(gy * gy);

                var e1b = (gxx + gyy) > (2 * SmmeThreshold);
                var b2 = (SmmeThreshold - gxx) * (SmmeThreshold - gyy) - (gxy * gxy);
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

                var blockValue = InvalidBlockValue;
                if (!outside)
                {
                    blockValue = e ? b2 : 0;
                }

                var candidate = candidateDelay.Next(maxima.Next(blockValue, x, y));
                var xp = x - Convolution3X3.XOffset;
                var yp = y + StartRowOffset - Convolution3X3.YOffset - OrientationFilterSize;
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

                    for (var delta = AngleSweepStart; delta <= AngleSweepEnd; delta += AngleSweepStep)
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
                        if (bestConfidence > TypeThreshold)
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
                direction = directionAccumulator.NextDirectionRow(orientationSums);
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

    private sealed class Convolution3X3
    {
        public const int XOffset = 1;
        public const int YOffset = 1;

        private readonly int[] verticalDelay1;
        private readonly int[] verticalDelay2;
        private readonly int t0;
        private readonly int t1;
        private readonly int normBits;
        private int verticalIndex1;
        private int verticalIndex2;
        private int horizontalDelay1;
        private int horizontalDelay2;

        public Convolution3X3(int width, int t0, int t1, int normBits)
        {
            this.t0 = t0;
            this.t1 = t1;
            this.normBits = normBits;
            verticalDelay1 = new int[width];
            verticalDelay2 = new int[width];
        }

        public int Next(int value)
        {
            var v1 = verticalDelay1[verticalIndex1];
            verticalDelay1[verticalIndex1] = value;
            verticalIndex1++;
            if (verticalIndex1 >= verticalDelay1.Length)
            {
                verticalIndex1 = 0;
            }

            var v2 = verticalDelay2[verticalIndex2];
            verticalDelay2[verticalIndex2] = v1;
            verticalIndex2++;
            if (verticalIndex2 >= verticalDelay2.Length)
            {
                verticalIndex2 = 0;
            }

            var h0 = (v1 * t0) + ((v2 + value) * t1);
            var h1 = horizontalDelay1;
            horizontalDelay1 = h0;
            var h2 = horizontalDelay2;
            horizontalDelay2 = h1;
            var output = (h1 * t0) + ((h2 + h0) * t1);
            return (output + (1 << (normBits - 1))) >> normBits;
        }
    }

    private sealed class Max2D5Fast
    {
        private readonly int[][] buffer;
        private readonly bool[][] maxima;
        public Max2D5Fast(int width)
        {
            buffer = Enumerable.Range(0, 7).Select(_ => Enumerable.Repeat(-1, width).ToArray()).ToArray();
            maxima = Enumerable.Range(0, 7).Select(_ => new bool[width + Max2D5FastXOffset]).ToArray();
        }

        public bool Next(int value, int x, int y)
        {
            var row = y % 7;
            buffer[row][x] = value;
            if (x == 0)
            {
                Array.Clear(maxima[Mod7(y - 1)], 0, maxima[row].Length);
            }

            if (y >= 6 && (y % 3) == 0 && x >= 6 && (x % 3) == 0)
            {
                FindMaxInBlock(y - 4, x - 4);
            }

            if (!maxima[row][x])
            {
                return false;
            }

            maxima[row][x] = false;
            return true;
        }

        private void FindMaxInBlock(int i, int j)
        {
            var maxI = i;
            var maxJ = j;
            var maxValue = buffer[maxI % 7][maxJ];
            for (var i2 = i; i2 <= i + 2; i2++)
            {
                for (var j2 = j; j2 <= j + 2; j2++)
                {
                    var current = buffer[i2 % 7][j2];
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

                    var current = buffer[Mod7(i2)][j2];
                    if (current < 0 || current > maxValue)
                    {
                        return;
                    }
                }
            }

            maxima[(maxI + Max2D5FastYOffset) % 7][maxJ + Max2D5FastXOffset] = true;
        }

        private static int Mod7(int value)
        {
            var result = value % 7;
            return result < 0 ? result + 7 : result;
        }
    }

    private sealed class PackedBoolDelay
    {
        private readonly byte[] buffer;
        private readonly byte initMask;
        private int pointer;
        private byte mask;

        public PackedBoolDelay(int delayLength, bool initialValue)
        {
            var byteSize = Math.Max((delayLength + 7) / 8, 1);
            buffer = new byte[byteSize];
            initMask = delayLength == 0
                ? (byte)1
                : (byte)(1 << ((-delayLength) & 7));
            mask = initMask;

            if (initialValue)
            {
                Array.Fill(buffer, byte.MaxValue);
            }
        }

        public bool Next(bool value)
        {
            var output = (buffer[pointer] & mask) != 0;
            if (value)
            {
                buffer[pointer] |= mask;
            }
            else
            {
                buffer[pointer] = (byte)(buffer[pointer] & ~mask);
            }

            mask <<= 1;
            if (mask == 0)
            {
                pointer++;
                if (pointer >= buffer.Length)
                {
                    pointer = 0;
                    mask = initMask;
                }
                else
                {
                    mask = 1;
                }
            }

            return output;
        }
    }

    private sealed class DirectionAccumulator
    {
        private readonly Nfiq2FingerJetComplex[] verticalBuffer;
        private readonly int widthHalf;
        private readonly int filterSize;
        private readonly int filterHalf;
        private int verticalIndex;

        public DirectionAccumulator(int widthHalf, int filterSize)
        {
            this.widthHalf = widthHalf;
            this.filterSize = filterSize;
            filterHalf = filterSize / 2;
            verticalBuffer = new Nfiq2FingerJetComplex[filterSize * widthHalf];
        }

        public Nfiq2FingerJetComplex NextVertical(Nfiq2FingerJetComplex value)
        {
            var output = verticalBuffer[verticalIndex];
            verticalBuffer[verticalIndex] = value;
            verticalIndex++;
            if (verticalIndex >= verticalBuffer.Length)
            {
                verticalIndex = 0;
            }

            return output;
        }

        public byte[] NextDirectionRow(ReadOnlySpan<Nfiq2FingerJetComplex> verticalSums)
        {
            var output = new byte[widthHalf];
            var horizontalReal = 0;
            var horizontalImaginary = 0;
            for (var x = 0; x < widthHalf + filterHalf; x++)
            {
                if (x < widthHalf)
                {
                    horizontalReal += verticalSums[x].Real;
                    horizontalImaginary += verticalSums[x].Imaginary;
                }

                if (x >= filterSize)
                {
                    horizontalReal -= verticalSums[x - filterSize].Real;
                    horizontalImaginary -= verticalSums[x - filterSize].Imaginary;
                }

                if (x >= filterHalf)
                {
                    output[x - filterHalf] = (byte)(Nfiq2FingerJetMath.Atan2IntMath(horizontalReal, horizontalImaginary) / 2);
                }
            }

            return output;
        }
    }
}
