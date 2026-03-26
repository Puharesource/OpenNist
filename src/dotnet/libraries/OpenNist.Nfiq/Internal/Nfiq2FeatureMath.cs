namespace OpenNist.Nfiq.Internal;

using System.Collections.Frozen;
using System.Globalization;

internal static class Nfiq2FeatureMath
{
    private const string s_meanSuffix = "Mean";
    private const string s_stdDevSuffix = "StdDev";

    public static void AccumulateGradientProducts(
        ReadOnlySpan<byte> image,
        int imageWidth,
        int row,
        int column,
        int blockWidth,
        int blockHeight,
        out double sumGradientXSquared,
        out double sumGradientYSquared,
        out double sumGradientXY)
    {
        sumGradientXSquared = 0.0;
        sumGradientYSquared = 0.0;
        sumGradientXY = 0.0;

        for (var y = 0; y < blockHeight; y++)
        {
            var imageRow = row + y;
            var imageRowOffset = imageRow * imageWidth;
            for (var x = 0; x < blockWidth; x++)
            {
                var imageColumn = column + x;
                var gradientX = ComputeGradientXAt(image, imageRowOffset, imageColumn, x, blockWidth);
                var gradientY = ComputeGradientYAt(image, imageWidth, imageRow, imageColumn, y, blockHeight);
                sumGradientXSquared += gradientX * gradientX;
                sumGradientYSquared += gradientY * gradientY;
                sumGradientXY += gradientX * gradientY;
            }
        }
    }

    public static double[] ComputeNumericalGradientX(
        ReadOnlySpan<byte> image,
        int imageWidth,
        int row,
        int column,
        int blockWidth,
        int blockHeight)
    {
        var gradient = new double[blockWidth * blockHeight];
        for (var y = 0; y < blockHeight; y++)
        {
            var imageRowOffset = (row + y) * imageWidth;
            var gradientRowOffset = y * blockWidth;

            if (blockWidth <= 1)
            {
                continue;
            }

            gradient[gradientRowOffset] = image[imageRowOffset + column + 1] - image[imageRowOffset + column];
            for (var x = 1; x < blockWidth - 1; x++)
            {
                gradient[gradientRowOffset + x] =
                    (image[imageRowOffset + column + x + 1] - image[imageRowOffset + column + x - 1]) / 2.0;
            }

            gradient[gradientRowOffset + blockWidth - 1] =
                image[imageRowOffset + column + blockWidth - 1] - image[imageRowOffset + column + blockWidth - 2];
        }

        return gradient;
    }

    public static double[] ComputeNumericalGradientY(
        ReadOnlySpan<byte> image,
        int imageWidth,
        int row,
        int column,
        int blockWidth,
        int blockHeight)
    {
        var gradient = new double[blockWidth * blockHeight];
        if (blockHeight <= 1)
        {
            return gradient;
        }

        for (var x = 0; x < blockWidth; x++)
        {
            gradient[x] = image[(row + 1) * imageWidth + column + x] - image[row * imageWidth + column + x];
        }

        for (var y = 1; y < blockHeight - 1; y++)
        {
            var gradientRowOffset = y * blockWidth;
            var imageRowOffset = (row + y) * imageWidth;
            for (var x = 0; x < blockWidth; x++)
            {
                gradient[gradientRowOffset + x] =
                    (image[imageRowOffset + imageWidth + column + x] - image[imageRowOffset - imageWidth + column + x]) / 2.0;
            }
        }

        var lastGradientRowOffset = (blockHeight - 1) * blockWidth;
        var lastImageRowOffset = (row + blockHeight - 1) * imageWidth;
        var previousImageRowOffset = (row + blockHeight - 2) * imageWidth;
        for (var x = 0; x < blockWidth; x++)
        {
            gradient[lastGradientRowOffset + x] =
                image[lastImageRowOffset + column + x] - image[previousImageRowOffset + column + x];
        }

        return gradient;
    }

    public static FrozenDictionary<string, double> CreateHistogramFeatures(
        string featurePrefix,
        ReadOnlySpan<double> binBoundaries,
        ReadOnlySpan<double> dataVector,
        int binCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featurePrefix);

        var expectedBoundaryCount = binCount - 1;
        if (binBoundaries.Length != expectedBoundaryCount)
        {
            throw new Nfiq2Exception(
                $"Wrong histogram bin count for {featurePrefix}. Should be {binCount} but is {binBoundaries.Length + 1}.");
        }

        var sortedValues = dataVector.ToArray();
        Array.Sort(sortedValues);

        var bins = new int[binCount];
        var currentBucket = 0;
        var currentBound = binBoundaries[currentBucket];

        foreach (var value in sortedValues)
        {
            while (!double.IsInfinity(value) && value >= currentBound)
            {
                currentBucket++;
                currentBound = currentBucket < binBoundaries.Length
                    ? binBoundaries[currentBucket]
                    : double.PositiveInfinity;
            }

            bins[currentBucket]++;
        }

        var features = new Dictionary<string, double>(binCount + 2, StringComparer.Ordinal);
        for (var index = 0; index < bins.Length; index++)
        {
            features[featurePrefix + index.ToString(CultureInfo.InvariantCulture)] = bins[index];
        }

        ComputeMeanAndStdDev(sortedValues, out var mean, out var stdDev);
        features[featurePrefix + s_meanSuffix] = mean;
        features[featurePrefix + s_stdDevSuffix] = stdDev;

        return features.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static void ComputeMeanAndStdDev(ReadOnlySpan<double> values, out double mean, out double stdDev)
    {
        if (values.IsEmpty)
        {
            mean = 0.0;
            stdDev = 0.0;
            return;
        }

        var sum = 0.0;
        foreach (var value in values)
        {
            sum += value;
        }

        mean = sum / values.Length;

        var varianceSum = 0.0;
        foreach (var value in values)
        {
            var delta = value - mean;
            varianceSum += delta * delta;
        }

        stdDev = Math.Sqrt(varianceSum / values.Length);
    }

    internal static double ComputeGradientXAt(
        ReadOnlySpan<byte> image,
        int imageRowOffset,
        int imageColumn,
        int blockColumn,
        int blockWidth)
    {
        if (blockWidth <= 1)
        {
            return 0.0;
        }

        if (blockColumn == 0)
        {
            return image[imageRowOffset + imageColumn + 1] - image[imageRowOffset + imageColumn];
        }

        if (blockColumn == blockWidth - 1)
        {
            return image[imageRowOffset + imageColumn] - image[imageRowOffset + imageColumn - 1];
        }

        return (image[imageRowOffset + imageColumn + 1] - image[imageRowOffset + imageColumn - 1]) / 2.0;
    }

    internal static double ComputeGradientYAt(
        ReadOnlySpan<byte> image,
        int imageWidth,
        int imageRow,
        int imageColumn,
        int blockRow,
        int blockHeight)
    {
        if (blockHeight <= 1)
        {
            return 0.0;
        }

        if (blockRow == 0)
        {
            return image[(imageRow + 1) * imageWidth + imageColumn] - image[imageRow * imageWidth + imageColumn];
        }

        if (blockRow == blockHeight - 1)
        {
            return image[imageRow * imageWidth + imageColumn] - image[(imageRow - 1) * imageWidth + imageColumn];
        }

        return (image[(imageRow + 1) * imageWidth + imageColumn] - image[(imageRow - 1) * imageWidth + imageColumn]) / 2.0;
    }
}
