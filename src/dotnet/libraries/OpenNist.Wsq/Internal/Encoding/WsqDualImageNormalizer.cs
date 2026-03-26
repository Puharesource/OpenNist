namespace OpenNist.Wsq.Internal.Encoding;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Major Code Smell",
    "S1244:Floating point numbers should not be tested for equality",
    Justification = "The WSQ normalizers intentionally preserve the reference implementation's exact zero-scale behavior.")]
internal static class WsqDualImageNormalizer
{
    public static WsqDualNormalizedImage Normalize(ReadOnlySpan<byte> rawPixels)
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

        var shiftDouble = (double)sum / rawPixels.Length;
        var lowerDistanceDouble = shiftDouble - minimumPixelValue;
        var upperDistanceDouble = maximumPixelValue - shiftDouble;
        var scaleDouble = Math.Max(lowerDistanceDouble, upperDistanceDouble) / 128.0;

        var shiftFloat = (float)shiftDouble;
        var lowerDistanceFloat = shiftFloat - minimumPixelValue;
        var upperDistanceFloat = maximumPixelValue - shiftFloat;
        var scaleFloat = Math.Max(lowerDistanceFloat, upperDistanceFloat) / 128.0f;

        var floatPixels = new float[rawPixels.Length];
        var doublePixels = new double[rawPixels.Length];

        if (scaleFloat == 0.0f && scaleDouble == 0.0)
        {
            return new(
                new(floatPixels, shiftFloat, scaleFloat),
                new(doublePixels, shiftDouble, scaleDouble));
        }

        if (scaleFloat == 0.0f)
        {
            for (var index = 0; index < rawPixels.Length; index++)
            {
                doublePixels[index] = (rawPixels[index] - shiftDouble) / scaleDouble;
            }

            return new(
                new(floatPixels, shiftFloat, scaleFloat),
                new(doublePixels, shiftDouble, scaleDouble));
        }

        if (scaleDouble == 0.0)
        {
            for (var index = 0; index < rawPixels.Length; index++)
            {
                floatPixels[index] = (rawPixels[index] - shiftFloat) / scaleFloat;
            }

            return new(
                new(floatPixels, shiftFloat, scaleFloat),
                new(doublePixels, shiftDouble, scaleDouble));
        }

        for (var index = 0; index < rawPixels.Length; index++)
        {
            var pixel = rawPixels[index];
            floatPixels[index] = (pixel - shiftFloat) / scaleFloat;
            doublePixels[index] = (pixel - shiftDouble) / scaleDouble;
        }

        return new(
            new(floatPixels, shiftFloat, scaleFloat),
            new(doublePixels, shiftDouble, scaleDouble));
    }
}

internal readonly record struct WsqDualNormalizedImage(
    WsqNormalizedImage FloatImage,
    WsqDoubleNormalizedImage DoubleImage);
