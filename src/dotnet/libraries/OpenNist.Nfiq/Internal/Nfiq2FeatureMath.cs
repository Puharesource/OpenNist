namespace OpenNist.Nfiq.Internal;

using System.Collections.Frozen;
using System.Globalization;

internal static class Nfiq2FeatureMath
{
    private const string MeanSuffix = "Mean";
    private const string StdDevSuffix = "StdDev";

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
            gradient[x] = image[((row + 1) * imageWidth) + column + x] - image[(row * imageWidth) + column + x];
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
        features[featurePrefix + MeanSuffix] = mean;
        features[featurePrefix + StdDevSuffix] = stdDev;

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

        double sum = 0.0;
        foreach (var value in values)
        {
            sum += value;
        }

        mean = sum / values.Length;

        double varianceSum = 0.0;
        foreach (var value in values)
        {
            var delta = value - mean;
            varianceSum += delta * delta;
        }

        stdDev = Math.Sqrt(varianceSum / values.Length);
    }
}
