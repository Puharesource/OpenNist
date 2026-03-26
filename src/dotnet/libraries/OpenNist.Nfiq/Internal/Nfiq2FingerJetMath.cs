namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2FingerJetMath
{
    private static ReadOnlySpan<sbyte> SinTable =>
    [
        0, 3, 6, 9, 12, 15, 19, 22, 25, 28, 31, 34, 37, 40, 43, 46,
        49, 51, 54, 57, 60, 63, 65, 68, 71, 73, 76, 78, 81, 83, 85, 88,
        90, 92, 94, 96, 98, 100, 102, 104, 106, 107, 109, 111, 112, 113, 115, 116,
        117, 118, 120, 121, 122, 122, 123, 124, 125, 125, 126, 126, 126, 127, 127, 127,
        127, 127, 127, 127, 126, 126, 126, 125, 125, 124, 123, 122, 122, 121, 120, 118,
        117, 116, 115, 113, 112, 111, 109, 107, 106, 104, 102, 100, 98, 96, 94, 92,
        90, 88, 85, 83, 81, 78, 76, 73, 71, 68, 65, 63, 60, 57, 54, 51,
        49, 46, 43, 40, 37, 34, 31, 28, 25, 22, 19, 15, 12, 9, 6, 3,
    ];

    private static ReadOnlySpan<byte> AtanTable =>
    [
        0, 0, 1, 1, 2, 2, 3, 3, 3, 4, 4, 5, 5, 5, 6, 6,
        7, 7, 8, 8, 8, 9, 9, 10, 10, 10, 11, 11, 12, 12, 12, 13,
        13, 13, 14, 14, 15, 15, 15, 16, 16, 16, 17, 17, 18, 18, 18, 19,
        19, 19, 20, 20, 20, 21, 21, 21, 22, 22, 22, 22, 23, 23, 23, 24,
        24, 24, 25, 25, 25, 25, 26, 26, 26, 26, 27, 27, 27, 28, 28, 28,
        28, 29, 29, 29, 29, 30, 30, 30, 30, 30, 31, 31, 31, 31, 32, 32,
        32,
    ];

    private static ReadOnlySpan<byte> IntMathAtanTable =>
    [
        0, 1, 1, 2, 2, 3, 3, 3, 4, 4, 5, 5, 6, 6, 6, 7,
        7, 8, 8, 8, 9, 9, 10, 10, 10, 11, 11, 12, 12, 12, 13, 13,
        13, 14, 14, 15, 15, 15, 16, 16, 16, 17, 17, 18, 18, 18, 19, 19,
        19, 20, 20, 20, 21, 21, 21, 22, 22, 22, 22, 23, 23, 23, 24, 24,
        24, 25, 25, 25, 25, 26, 26, 26, 27, 27, 27, 27, 28, 28, 28, 28,
        29, 29, 29, 29, 30, 30, 30, 30, 31, 31, 31, 31, 31, 32, 32, 32,
        32,
    ];

    public static int ScaleDown(int value, int bits)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bits);
        return (value + (1 << (bits - 1))) >> bits;
    }

    public static int MulDiv(int x, int y, int z)
    {
        return checked((int)DivideRounded((long)x * y, z));
    }

    public static byte QualityFromConfidence(byte confidence)
    {
        return (byte)Math.Min((confidence + 1) / 2, 100);
    }

    public static sbyte Sin(int angle)
    {
        var normalized = angle & 0xff;
        var magnitude = SinTable[normalized & 0x7f];
        return (sbyte)((normalized & 0x80) != 0 ? -magnitude : magnitude);
    }

    public static sbyte Cos(int angle)
    {
        return Sin((angle + 64) & 0xff);
    }

    public static byte Atan2(int c, int s)
    {
        return Atan2Core(c, s, AtanTable);
    }

    public static byte Atan2IntMath(int c, int s)
    {
        return Atan2Core(c, s, IntMathAtanTable);
    }

    private static byte Atan2Core(int c, int s, ReadOnlySpan<byte> table)
    {
        var sn = s < 0;
        if (sn)
        {
            s = -s;
        }

        var cn = c < 0;
        if (cn)
        {
            c = -c;
        }

        var cls = c < s;
        if (cls)
        {
            (c, s) = (s, c);
        }

        if (c == 0)
        {
            return 0;
        }

        var value = table[s * 96 / c];
        if (cls)
        {
            value = (byte)(0x40 - value);
        }

        if (cn)
        {
            value = (byte)(0x80 - value);
        }

        if (sn)
        {
            value = unchecked((byte)-value);
        }

        return value;
    }

    private static long DivideRounded(long x, long y)
    {
        if (y == 0)
        {
            throw new DivideByZeroException();
        }

        var sign = x >= 0 != y > 0 ? -1L : 1L;
        x = Math.Abs(x);
        y = Math.Abs(y);
        return (x + (y >> 1)) / y * sign;
    }
}
