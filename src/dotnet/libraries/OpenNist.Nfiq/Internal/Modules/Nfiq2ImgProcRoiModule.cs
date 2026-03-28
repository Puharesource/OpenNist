namespace OpenNist.Nfiq.Internal.Modules;

using System.Buffers;
using System.Collections.Frozen;
using OpenNist.Nfiq.Errors;
using OpenNist.Nfiq.Internal.Model;

internal static class Nfiq2ImgProcRoiModule
{
    private const int s_localRegionSquare = 32;
    private const int s_erosionKernelSize = 5;
    private const int s_firstGaussianKernelSize = 41;
    private const int s_secondGaussianKernelSize = 91;
    private const byte s_whitePixel = 255;
    private const byte s_blackPixel = 0;
    private const string s_regionOfInterestMean = "ImgProcROIArea_Mean";
    private static readonly double[] s_firstGaussianKernel = BuildGaussianKernel(s_firstGaussianKernelSize);
    private static readonly double[] s_secondGaussianKernel = BuildGaussianKernel(s_secondGaussianKernelSize);

    public static Nfiq2ImgProcRoiResult Compute(Nfiq2FingerprintImage fingerprintImage)
    {
        ArgumentNullException.ThrowIfNull(fingerprintImage);

        if (fingerprintImage.PixelsPerInch != Nfiq2FingerprintImage.Resolution500Ppi)
        {
            throw new Nfiq2Exception("Only 500 dpi fingerprint images are supported!");
        }

        var source = fingerprintImage.Pixels.Span;
        var eroded = Erode(source, fingerprintImage.Width, fingerprintImage.Height, s_erosionKernelSize);
        var blurred = GaussianBlur(eroded, fingerprintImage.Width, fingerprintImage.Height, s_firstGaussianKernel);
        var threshold = ThresholdOtsu(blurred);
        var blurredThreshold = GaussianBlur(threshold, fingerprintImage.Width, fingerprintImage.Height, s_secondGaussianKernel);
        var roiMask = ThresholdOtsu(blurredThreshold);

        FillWhiteHoles(roiMask, fingerprintImage.Width, fingerprintImage.Height);
        KeepLargestBlackComponent(roiMask, fingerprintImage.Width, fingerprintImage.Height);

        var roiPixels = 0U;
        var meanOfRoiPixels = 0.0;
        for (var row = 0; row < fingerprintImage.Height; row++)
        {
            var rowOffset = row * fingerprintImage.Width;
            for (var column = 0; column < fingerprintImage.Width; column++)
            {
                if (roiMask[rowOffset + column] != s_blackPixel)
                {
                    continue;
                }

                roiPixels++;
                meanOfRoiPixels += source[rowOffset + column];
            }
        }

        if (roiPixels == 0)
        {
            meanOfRoiPixels = s_whitePixel;
        }
        else
        {
            meanOfRoiPixels /= roiPixels;
        }

        var varianceSum = 0.0;
        if (roiPixels > 1)
        {
            for (var row = 0; row < fingerprintImage.Height; row++)
            {
                var rowOffset = row * fingerprintImage.Width;
                for (var column = 0; column < fingerprintImage.Width; column++)
                {
                    if (roiMask[rowOffset + column] != s_blackPixel)
                    {
                        continue;
                    }

                    var delta = source[rowOffset + column] - meanOfRoiPixels;
                    varianceSum += delta * delta;
                }
            }
        }

        var stdDevOfRoiPixels = roiPixels > 1
            ? Math.Sqrt(varianceSum / (roiPixels - 1.0))
            : 0.0;

        var roiBlocks = new List<Nfiq2RegionBlock>();
        var allBlocks = 0U;
        var completeBlocks = 0U;
        for (var row = 0; row < fingerprintImage.Height; row += s_localRegionSquare)
        {
            for (var column = 0; column < fingerprintImage.Width; column += s_localRegionSquare)
            {
                var takenWidth = Math.Min(s_localRegionSquare, fingerprintImage.Width - column);
                var takenHeight = Math.Min(s_localRegionSquare, fingerprintImage.Height - row);
                allBlocks++;
                if (takenWidth == s_localRegionSquare && takenHeight == s_localRegionSquare)
                {
                    completeBlocks++;
                }

                if (ContainsBlackPixel(roiMask, fingerprintImage.Width, row, column, takenWidth, takenHeight))
                {
                    roiBlocks.Add(new(column, row, takenWidth, takenHeight));
                }
            }
        }

        return new(
            s_localRegionSquare,
            completeBlocks,
            allBlocks,
            roiBlocks.ToArray(),
            roiPixels,
            checked((uint)(fingerprintImage.Width * fingerprintImage.Height)),
            meanOfRoiPixels,
            stdDevOfRoiPixels,
            new Dictionary<string, double>(1, StringComparer.Ordinal)
            {
                [s_regionOfInterestMean] = meanOfRoiPixels,
            }.ToFrozenDictionary(StringComparer.Ordinal));
    }

    private static byte[] Erode(ReadOnlySpan<byte> source, int width, int height, int kernelSize)
    {
        var radius = kernelSize / 2;
        var destination = new byte[source.Length];
        for (var row = 0; row < height; row++)
        {
            var rowOffset = row * width;
            for (var column = 0; column < width; column++)
            {
                var minValue = s_whitePixel;
                for (var dy = -radius; dy <= radius; dy++)
                {
                    var sampleRow = row + dy;
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        var sampleColumn = column + dx;
                        var sampleValue = IsInside(sampleRow, sampleColumn, width, height)
                            ? source[sampleRow * width + sampleColumn]
                            : s_whitePixel;
                        if (sampleValue < minValue)
                        {
                            minValue = sampleValue;
                        }
                    }
                }

                destination[rowOffset + column] = minValue;
            }
        }

        return destination;
    }

    private static byte[] GaussianBlur(ReadOnlySpan<byte> source, int width, int height, ReadOnlySpan<double> kernel)
    {
        var horizontal = new double[source.Length];
        var radius = kernel.Length / 2;

        for (var row = 0; row < height; row++)
        {
            var rowOffset = row * width;
            for (var column = 0; column < width; column++)
            {
                var sum = 0.0;
                for (var index = 0; index < kernel.Length; index++)
                {
                    var sampleColumn = Reflect101(column + index - radius, width);
                    sum += kernel[index] * source[rowOffset + sampleColumn];
                }

                horizontal[rowOffset + column] = sum;
            }
        }

        var destination = new byte[source.Length];
        for (var row = 0; row < height; row++)
        {
            var rowOffset = row * width;
            for (var column = 0; column < width; column++)
            {
                var sum = 0.0;
                for (var index = 0; index < kernel.Length; index++)
                {
                    var sampleRow = Reflect101(row + index - radius, height);
                    sum += kernel[index] * horizontal[sampleRow * width + column];
                }

                destination[rowOffset + column] = ClampToByte(sum);
            }
        }

        return destination;
    }

    private static byte[] ThresholdOtsu(ReadOnlySpan<byte> source)
    {
        Span<int> histogram = stackalloc int[256];
        foreach (var value in source)
        {
            histogram[value]++;
        }

        var threshold = ComputeOtsuThreshold(histogram, source.Length);
        var destination = new byte[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            destination[index] = source[index] > threshold ? s_whitePixel : s_blackPixel;
        }

        return destination;
    }

    private static int ComputeOtsuThreshold(ReadOnlySpan<int> histogram, int pixelCount)
    {
        long total = 0;
        for (var index = 0; index < histogram.Length; index++)
        {
            total += (long)index * histogram[index];
        }

        long backgroundWeight = 0;
        long backgroundSum = 0;
        var maxVariance = double.MinValue;
        var threshold = 0;

        for (var index = 0; index < histogram.Length; index++)
        {
            backgroundWeight += histogram[index];
            if (backgroundWeight == 0)
            {
                continue;
            }

            var foregroundWeight = pixelCount - backgroundWeight;
            if (foregroundWeight == 0)
            {
                break;
            }

            backgroundSum += (long)index * histogram[index];
            var backgroundMean = backgroundSum / (double)backgroundWeight;
            var foregroundMean = (total - backgroundSum) / (double)foregroundWeight;
            var delta = backgroundMean - foregroundMean;
            var betweenClassVariance = backgroundWeight * (double)foregroundWeight * delta * delta;

            if (betweenClassVariance > maxVariance)
            {
                maxVariance = betweenClassVariance;
                threshold = index;
            }
        }

        return threshold;
    }

    private static void FillWhiteHoles(byte[] image, int width, int height)
    {
        var visited = new bool[image.Length];
        var queue = ArrayPool<int>.Shared.Rent(image.Length);
        var queueHead = 0;
        var queueTail = 0;

        try
        {
            for (var row = 0; row < height; row++)
            {
                EnqueueBoundaryWhite(row, 0);
                EnqueueBoundaryWhite(row, width - 1);
            }

            for (var column = 0; column < width; column++)
            {
                EnqueueBoundaryWhite(0, column);
                EnqueueBoundaryWhite(height - 1, column);
            }

            while (queueHead < queueTail)
            {
                var index = queue[queueHead++];
                var row = index / width;
                var column = index % width;

                EnqueueNeighbors(row, column);
            }

            for (var index = 0; index < image.Length; index++)
            {
                if (image[index] == s_whitePixel && !visited[index])
                {
                    image[index] = s_blackPixel;
                }
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(queue);
        }

        void EnqueueBoundaryWhite(int row, int column)
        {
            if (!IsInside(row, column, width, height))
            {
                return;
            }

            var index = row * width + column;
            if (visited[index] || image[index] != s_whitePixel)
            {
                return;
            }

            visited[index] = true;
            queue[queueTail++] = index;
        }

        void Enqueue(int row, int column)
        {
            EnqueueBoundaryWhite(row, column);
        }

        void EnqueueNeighbors(int row, int column)
        {
            for (var rowOffset = -1; rowOffset <= 1; rowOffset++)
            {
                for (var columnOffset = -1; columnOffset <= 1; columnOffset++)
                {
                    if (rowOffset == 0 && columnOffset == 0)
                    {
                        continue;
                    }

                    Enqueue(row + rowOffset, column + columnOffset);
                }
            }
        }
    }

    private static void KeepLargestBlackComponent(byte[] image, int width, int height)
    {
        var visited = new bool[image.Length];
        var queue = ArrayPool<int>.Shared.Rent(image.Length);
        var componentPixels = ArrayPool<int>.Shared.Rent(image.Length);
        var largestComponent = ArrayPool<int>.Shared.Rent(image.Length);
        var maxArea = -1;
        var largestComponentCount = 0;

        try
        {
            for (var index = 0; index < image.Length; index++)
            {
                if (visited[index] || image[index] != s_blackPixel)
                {
                    continue;
                }

                var componentCount = 0;
                var queueHead = 0;
                var queueTail = 0;
                visited[index] = true;
                queue[queueTail++] = index;

                var minRow = int.MaxValue;
                var maxRow = int.MinValue;
                var minColumn = int.MaxValue;
                var maxColumn = int.MinValue;

                while (queueHead < queueTail)
                {
                    var current = queue[queueHead++];
                    componentPixels[componentCount++] = current;

                    var row = current / width;
                    var column = current % width;
                    minRow = Math.Min(minRow, row);
                    maxRow = Math.Max(maxRow, row);
                    minColumn = Math.Min(minColumn, column);
                    maxColumn = Math.Max(maxColumn, column);

                    Visit(row - 1, column, ref queueTail);
                    Visit(row + 1, column, ref queueTail);
                    Visit(row, column - 1, ref queueTail);
                    Visit(row, column + 1, ref queueTail);
                }

                var area = (maxColumn - minColumn + 1) * (maxRow - minRow + 1);
                if (area > maxArea)
                {
                    maxArea = area;
                    largestComponentCount = componentCount;
                    componentPixels.AsSpan(0, componentCount).CopyTo(largestComponent);
                }
            }

            Array.Fill(image, s_whitePixel);
            for (var index = 0; index < largestComponentCount; index++)
            {
                image[largestComponent[index]] = s_blackPixel;
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(queue);
            ArrayPool<int>.Shared.Return(componentPixels);
            ArrayPool<int>.Shared.Return(largestComponent);
        }

        void Visit(int row, int column, ref int queueTail)
        {
            if (!IsInside(row, column, width, height))
            {
                return;
            }

            var candidate = row * width + column;
            if (visited[candidate] || image[candidate] != s_blackPixel)
            {
                return;
            }

            visited[candidate] = true;
            queue[queueTail++] = candidate;
        }
    }

    private static bool ContainsBlackPixel(byte[] image, int width, int row, int column, int takenWidth, int takenHeight)
    {
        for (var y = 0; y < takenHeight; y++)
        {
            var rowOffset = (row + y) * width;
            for (var x = 0; x < takenWidth; x++)
            {
                if (image[rowOffset + column + x] == s_blackPixel)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static double[] BuildGaussianKernel(int kernelSize)
    {
        var sigma = ComputeGaussianSigma(kernelSize);
        var radius = kernelSize / 2;
        var kernel = new double[kernelSize];
        var sum = 0.0;

        for (var index = 0; index < kernelSize; index++)
        {
            var x = index - radius;
            var value = Math.Exp(-(x * x) / (2.0 * sigma * sigma));
            kernel[index] = value;
            sum += value;
        }

        for (var index = 0; index < kernel.Length; index++)
        {
            kernel[index] /= sum;
        }

        return kernel;
    }

    private static double ComputeGaussianSigma(int kernelSize)
    {
        return ((kernelSize - 1) * 0.5 - 1.0) * 0.3 + 0.8;
    }

    private static int Reflect101(int index, int length)
    {
        if (length == 1)
        {
            return 0;
        }

        while (index < 0 || index >= length)
        {
            index = index < 0 ? -index : length * 2 - index - 2;
        }

        return index;
    }

    private static bool IsInside(int row, int column, int width, int height)
    {
        return row >= 0 && row < height && column >= 0 && column < width;
    }

    private static byte ClampToByte(double value)
    {
        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return (byte)Math.Clamp(rounded, byte.MinValue, byte.MaxValue);
    }
}

internal sealed record Nfiq2ImgProcRoiResult(
    uint ChosenBlockSize,
    uint CompleteBlocks,
    uint AllBlocks,
    IReadOnlyList<Nfiq2RegionBlock> RoiBlocks,
    uint RoiPixels,
    uint ImagePixels,
    double MeanOfRoiPixels,
    double StdDevOfRoiPixels,
    IReadOnlyDictionary<string, double> Features);

internal readonly record struct Nfiq2RegionBlock(int X, int Y, int Width, int Height);
