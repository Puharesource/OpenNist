namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2BlockFeatureSupport
{
    public static byte[] CreateSegmentationMask(
        Nfiq2FingerprintImage fingerprintImage,
        int blockSize,
        double threshold)
    {
        ArgumentNullException.ThrowIfNull(fingerprintImage);

        if (blockSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blockSize), blockSize, "Block size must be positive.");
        }

        var normalized = NormalizeImage(fingerprintImage.Pixels.Span);
        var stdDevImage = new double[normalized.Length];

        for (var row = 0; row < fingerprintImage.Height; row += blockSize)
        {
            var blockHeight = Math.Min(blockSize, fingerprintImage.Height - row);
            for (var column = 0; column < fingerprintImage.Width; column += blockSize)
            {
                var blockWidth = Math.Min(blockSize, fingerprintImage.Width - column);
                ComputeBlockMeanAndStdDev(
                    normalized,
                    fingerprintImage.Width,
                    row,
                    column,
                    blockWidth,
                    blockHeight,
                    out _,
                    out var stdDev);

                for (var y = 0; y < blockHeight; y++)
                {
                    var rowOffset = (row + y) * fingerprintImage.Width;
                    for (var x = 0; x < blockWidth; x++)
                    {
                        stdDevImage[rowOffset + column + x] = stdDev;
                    }
                }
            }
        }

        var mask = new byte[stdDevImage.Length];
        for (var index = 0; index < stdDevImage.Length; index++)
        {
            mask[index] = stdDevImage[index] > threshold
                ? byte.MaxValue
                : byte.MinValue;
        }

        return mask;
    }

    public static bool AreAllNonZero(
        ReadOnlySpan<byte> image,
        int imageWidth,
        int row,
        int column,
        int blockWidth,
        int blockHeight)
    {
        for (var y = 0; y < blockHeight; y++)
        {
            var rowOffset = (row + y) * imageWidth;
            for (var x = 0; x < blockWidth; x++)
            {
                if (image[rowOffset + column + x] == 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static double ComputeRidgeOrientation(
        ReadOnlySpan<byte> image,
        int imageWidth,
        int row,
        int column,
        int blockWidth,
        int blockHeight)
    {
        var gradientX = Nfiq2FeatureMath.ComputeNumericalGradientX(image, imageWidth, row, column, blockWidth, blockHeight);
        var gradientY = Nfiq2FeatureMath.ComputeNumericalGradientY(image, imageWidth, row, column, blockWidth, blockHeight);

        double a = 0.0;
        double b = 0.0;
        double c = 0.0;

        for (var index = 0; index < gradientX.Length; index++)
        {
            var gx = gradientX[index];
            var gy = gradientY[index];
            a += gx * gx;
            b += gy * gy;
            c += gx * gy;
        }

        var pixelCount = blockWidth * blockHeight;
        a /= pixelCount;
        b /= pixelCount;
        c /= pixelCount;

        var delta = a - b;
        var denominator = (c * c) + (delta * delta) + double.Epsilon;
        var sin2Theta = c / denominator;
        var cos2Theta = delta / denominator;
        return Math.Atan2(sin2Theta, cos2Theta) / 2.0;
    }

    private static double[] NormalizeImage(ReadOnlySpan<byte> pixels)
    {
        double sum = 0.0;
        foreach (var pixel in pixels)
        {
            sum += pixel;
        }

        var mean = sum / pixels.Length;

        double varianceSum = 0.0;
        foreach (var pixel in pixels)
        {
            var delta = pixel - mean;
            varianceSum += delta * delta;
        }

        var stdDev = Math.Sqrt(varianceSum / pixels.Length);
        if (Math.Abs(stdDev) < double.Epsilon)
        {
            return new double[pixels.Length];
        }

        var normalized = new double[pixels.Length];
        for (var index = 0; index < pixels.Length; index++)
        {
            normalized[index] = (pixels[index] - mean) / stdDev;
        }

        return normalized;
    }

    private static void ComputeBlockMeanAndStdDev(
        ReadOnlySpan<double> image,
        int imageWidth,
        int row,
        int column,
        int blockWidth,
        int blockHeight,
        out double mean,
        out double stdDev)
    {
        double sum = 0.0;
        var count = blockWidth * blockHeight;
        for (var y = 0; y < blockHeight; y++)
        {
            var rowOffset = (row + y) * imageWidth;
            for (var x = 0; x < blockWidth; x++)
            {
                sum += image[rowOffset + column + x];
            }
        }

        mean = sum / count;

        double varianceSum = 0.0;
        for (var y = 0; y < blockHeight; y++)
        {
            var rowOffset = (row + y) * imageWidth;
            for (var x = 0; x < blockWidth; x++)
            {
                var delta = image[rowOffset + column + x] - mean;
                varianceSum += delta * delta;
            }
        }

        stdDev = Math.Sqrt(varianceSum / count);
    }
}
