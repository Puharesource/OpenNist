namespace OpenNist.Wsq.Internal.Encoding;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Major Code Smell",
    "S1244:Floating point numbers should not be tested for equality",
    Justification = "Uniform grayscale images need an exact zero-scale guard to avoid division by zero during normalization.")]
internal static class WsqFloatImageNormalizer
{
    public static WsqNormalizedImage Normalize(ReadOnlySpan<byte> rawPixels)
    {
        var sum = 0L;
        var minimumPixelValue = byte.MaxValue;
        var maximumPixelValue = byte.MinValue;

        foreach (var pixel in rawPixels)
        {
            minimumPixelValue = Math.Min(minimumPixelValue, pixel);
            maximumPixelValue = Math.Max(maximumPixelValue, pixel);
            sum += pixel;
        }

        var shift = (float)sum / rawPixels.Length;
        var lowerDistance = shift - minimumPixelValue;
        var upperDistance = maximumPixelValue - shift;
        var scale = Math.Max(lowerDistance, upperDistance) / 128.0f;
        return Normalize(rawPixels, shift, scale);
    }

    public static WsqNormalizedImage Normalize(ReadOnlySpan<byte> rawPixels, float shift, float scale)
    {
        var normalizedPixels = new float[rawPixels.Length];

        if (scale == 0.0f)
        {
            return new(normalizedPixels, shift, scale);
        }

        for (var index = 0; index < rawPixels.Length; index++)
        {
            normalizedPixels[index] = (rawPixels[index] - shift) / scale;
        }

        return new(normalizedPixels, shift, scale);
    }
}

internal readonly record struct WsqNormalizedImage(
    float[] Pixels,
    float Shift,
    float Scale);
