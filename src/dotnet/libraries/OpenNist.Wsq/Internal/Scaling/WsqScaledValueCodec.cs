namespace OpenNist.Wsq.Internal.Scaling;

internal static class WsqScaledValueCodec
{
    public static WsqScaledUInt16 ScaleToUInt16(float value)
    {
        if (IsExactlyZero(value))
        {
            return new(0, 0);
        }

        if (value >= ushort.MaxValue)
        {
            throw new InvalidOperationException($"WSQ scaled value is too large to be written: {value:R}.");
        }

        var scaledValue = value;
        byte scale = 0;

        while (scaledValue < ushort.MaxValue)
        {
            scale++;
            scaledValue *= 10.0f;
        }

        scale--;
        var rawValue = checked((ushort)RoundNbis(scaledValue / 10.0));
        return new(rawValue, scale);
    }

    public static WsqScaledUInt16 ScaleToUInt16(double value)
    {
        if (Math.Abs(value) < double.Epsilon)
        {
            return new(0, 0);
        }

        if (value >= ushort.MaxValue)
        {
            throw new InvalidOperationException($"WSQ scaled value is too large to be written: {value:R}.");
        }

        var scaledValue = value;
        byte scale = 0;

        while (scaledValue < ushort.MaxValue)
        {
            scale++;
            scaledValue *= 10.0;
        }

        scale--;
        var rawValue = checked((ushort)RoundNbis(scaledValue / 10.0));
        return new(rawValue, scale);
    }

    public static WsqScaledUInt32 ScaleToUInt32(double value)
    {
        var scaledValue = (float)value;

        if (IsExactlyZero(scaledValue))
        {
            return new(0, 0);
        }

        if (scaledValue >= uint.MaxValue)
        {
            throw new InvalidOperationException($"WSQ transform coefficient is too large to be written: {value:R}.");
        }

        byte scale = 0;

        while (scaledValue < uint.MaxValue)
        {
            scale++;
            scaledValue *= 10.0f;
        }

        scale--;
        var rawValue = checked((uint)RoundNbis(scaledValue / 10.0));
        return new(rawValue, scale);
    }

    public static double ScaleUInt16ToDouble(ushort rawValue, byte scale)
    {
        return ScaleUnsignedValue(rawValue, scale);
    }

    public static float ScaleUInt32ToSingle(uint rawValue, byte scale)
    {
        return ScaleUnsignedValue(rawValue, scale);
    }

    public static double RoundTripUInt16(double value)
    {
        var scaledValue = ScaleToUInt16(value);
        return ScaleUInt16ToDouble(scaledValue.RawValue, scaledValue.Scale);
    }

    private static double RoundNbis(double value)
    {
        return value < 0.0
            ? value - 0.5
            : value + 0.5;
    }

    private static bool IsExactlyZero(float value)
    {
        var bits = BitConverter.SingleToInt32Bits(value);
        return bits is 0 or unchecked((int)0x80000000);
    }

    private static float ScaleUnsignedValue(uint rawValue, byte scale)
    {
        var value = (float)rawValue;

        while (scale > 0)
        {
            value = (float)(value / 10.0);
            scale--;
        }

        return value;
    }
}

internal readonly record struct WsqScaledUInt16(
    ushort RawValue,
    byte Scale);

internal readonly record struct WsqScaledUInt32(
    uint RawValue,
    byte Scale);
