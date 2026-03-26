namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2FingerJetBifFilterSupport
{
    private const byte s_phasemapFiller = 127;
    private const int s_imageFractionBits = 7;
    private const int s_imageScale = 1 << s_imageFractionBits;
    private const int s_filterSize = 17;
    private const int s_responseGridSize = 3;
    private const int s_scaleCount = 5;
    private const int s_patchWidth = s_filterSize + s_responseGridSize - 1;
    private const int s_patchCenter = s_patchWidth / 2;
    private const int s_responseCount = s_responseGridSize * s_responseGridSize * s_scaleCount;

    private static ReadOnlySpan<int> ConfidenceWeights =>
    [
        16, 16, 16, 16, 16, 16, 16, 16, 16, 15, 15, 15, 15, 15, 15, 15, 15, 15,
        14, 14, 14, 14, 14, 14, 14, 14, 14, 13, 13, 13, 13, 13, 13, 13, 13, 13,
        12, 12, 12, 12, 12, 12, 12, 12, 12,
    ];

    private static readonly int[] s_cv1Kernel = CreateSymmetricKernel([50, 48, 41, 32, 23, 14, 8, 4, 2]);
    private static readonly int[] s_cv2Kernel = CreateAntiSymmetricKernel([48, 82, 96, 90, 72, 50, 30, 16]);

    private static readonly int[][] s_horizontalCv1Kernels =
    [
        CreateSymmetricKernel([0, 55, -53, -61, 74, 3, -28, 7, 3]),
        CreateSymmetricKernel([0, 42, 0, -83, 3, 63, -1, -25, 1]),
        CreateSymmetricKernel([0, 29, 25, -50, -58, 22, 51, 5, -24]),
        CreateSymmetricKernel([-2, 16, 28, -14, -60, -41, 18, 41, 13]),
        CreateSymmetricKernel([0, 13, 28, 13, -27, -50, -29, 14, 38]),
    ];

    private static readonly int[][] s_horizontalCv2Kernels =
    [
        CreateSymmetricKernel([52, -14, -32, 22, 6, -9, 1, 1, 0]),
        CreateSymmetricKernel([44, 0, -36, 1, 19, 0, -7, 0, 2]),
        CreateSymmetricKernel([37, 9, -28, -18, 12, 14, -1, -6, -1]),
        CreateSymmetricKernel([31, 13, -17, -24, -5, 13, 11, 0, -5]),
        CreateSymmetricKernel([26, 15, -7, -21, -16, 0, 11, 10, 3]),
    ];

    public static byte SampleImage(ReadOnlySpan<byte> image, int width, int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        if (image.IsEmpty || image.Length % width != 0)
        {
            throw new ArgumentException("Image length must be a non-zero multiple of width.", nameof(image));
        }

        return SampleImageCore(image, width, image.Length / width, x, y);
    }

    public static Nfiq2FingerJetBifFilterResult Evaluate(
        ReadOnlySpan<byte> phasemap,
        int width,
        int x,
        int y,
        sbyte c,
        sbyte s,
        int threshold = 80,
        int ratio = 102)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        if (phasemap.IsEmpty || phasemap.Length % width != 0)
        {
            throw new ArgumentException("Phasemap length must be a non-zero multiple of width.", nameof(phasemap));
        }

        return EvaluateCore(phasemap, width, phasemap.Length / width, x, y, c, s, threshold, ratio);
    }

    private static byte SampleImageCore(ReadOnlySpan<byte> image, int width, int height, int x, int y)
    {
        var wx1 = x & (s_imageScale - 1);
        var wy1 = y & (s_imageScale - 1);
        var wx0 = s_imageScale - wx1;
        var wy0 = s_imageScale - wy1;
        var x0 = x >> s_imageFractionBits;
        var y0 = y >> s_imageFractionBits;
        if (x0 < 0 || y0 < 0 || y0 > height - 1 || x0 > width - 1)
        {
            return s_phasemapFiller;
        }

        var index = x0 + (y0 * width);
        if ((uint)(index + width + 1) >= (uint)image.Length)
        {
            return s_phasemapFiller;
        }

        var value =
            (image[index] * wx0 * wy0) +
            (image[index + 1] * wx1 * wy0) +
            (image[index + width] * wx0 * wy1) +
            (image[index + width + 1] * wx1 * wy1);
        return (byte)Reduce(value, s_imageFractionBits * 2);
    }

    private static Nfiq2FingerJetBifFilterResult EvaluateCore(
        ReadOnlySpan<byte> phasemap,
        int width,
        int height,
        int x,
        int y,
        sbyte c,
        sbyte s,
        int threshold,
        int ratio)
    {
        Span<byte> patch = stackalloc byte[s_patchWidth * s_patchWidth];
        RotatePatch(phasemap, width, height, x, y, c, s, patch);

        Span<int> response = stackalloc int[s_responseCount * 2];
        ApplySeparableConvolutions(patch, s_cv1Kernel, s_horizontalCv1Kernels, response[..s_responseCount]);
        ApplySeparableConvolutions(patch, s_cv2Kernel, s_horizontalCv2Kernels, response[s_responseCount..]);

        return EvaluateButterfly(response, c, s, threshold, ratio);
    }

    private static void RotatePatch(
        ReadOnlySpan<byte> phasemap,
        int width,
        int height,
        int x,
        int y,
        sbyte c,
        sbyte s,
        Span<byte> patch)
    {
        var fx = x << s_imageFractionBits;
        var fy = y << s_imageFractionBits;
        fx -= s_patchCenter * (c - s);
        fy -= s_patchCenter * (s + c);

        for (var row = 0; row < s_patchWidth; row++)
        {
            var rowX = fx - (row * s);
            var rowY = fy + (row * c);
            var sampleX = rowX;
            var sampleY = rowY;
            var patchRowIndex = row * s_patchWidth;
            for (var column = 0; column < s_patchWidth; column++)
            {
                patch[patchRowIndex + column] = SampleImageCore(
                    phasemap,
                    width,
                    height,
                    sampleX,
                    sampleY);
                sampleX += c;
                sampleY += s;
            }
        }
    }

    private static void ApplySeparableConvolutions(ReadOnlySpan<byte> patch, int[] verticalKernel, int[][] horizontalKernels, Span<int> destination)
    {
        Span<int> intermediate = stackalloc int[s_patchWidth * s_responseGridSize];
        var intermediateIndex = 0;
        for (var rowOffset = 0; rowOffset < s_responseGridSize; rowOffset++)
        {
            for (var column = 0; column < s_patchWidth; column++, intermediateIndex++)
            {
                intermediate[intermediateIndex] = ConvolveVertical(patch, column + (rowOffset * s_patchWidth), verticalKernel);
            }
        }

        var destinationIndex = 0;
        for (var row = 0; row < s_responseGridSize; row++)
        {
            var rowBase = row * s_patchWidth;
            for (var column = 0; column < s_responseGridSize; column++)
            {
                var columnBase = rowBase + column;
                for (var kernelIndex = 0; kernelIndex < horizontalKernels.Length; kernelIndex++)
                {
                    destination[destinationIndex++] = ConvolveHorizontal(
                        intermediate,
                        columnBase,
                        horizontalKernels[kernelIndex]);
                }
            }
        }
    }

    private static int ConvolveVertical(ReadOnlySpan<byte> patch, int startIndex, int[] kernel)
    {
        var sum = 0;
        var patchIndex = startIndex;
        for (var row = 0; row < s_filterSize; row++)
        {
            sum += patch[patchIndex] * kernel[row];
            patchIndex += s_patchWidth;
        }

        return sum;
    }

    private static int ConvolveHorizontal(ReadOnlySpan<int> intermediate, int startIndex, int[] kernel)
    {
        var sum = 0;
        var intermediateIndex = startIndex;
        for (var column = 0; column < s_filterSize; column++)
        {
            sum += intermediate[intermediateIndex++] * kernel[column];
        }

        return sum;
    }

    private static Nfiq2FingerJetBifFilterResult EvaluateButterfly(
        ReadOnlySpan<int> response,
        sbyte c,
        sbyte s,
        int threshold,
        int ratio)
    {
        var bestConfidence = 0;
        var mirroredConfidence = 0;
        var bestIndex = 0;
        var bestValue = 0;
        for (var index = 0; index < s_responseCount; index++)
        {
            var sum = response[index] + response[index + s_responseCount];
            var diff = response[index] - response[index + s_responseCount];

            var weightedSum = Reduce(Math.Abs(sum) * ConfidenceWeights[index], 4);
            var weightedDiff = Reduce(Math.Abs(diff) * ConfidenceWeights[index], 4);
            if (weightedSum > bestConfidence)
            {
                bestConfidence = weightedSum;
                mirroredConfidence = weightedDiff;
                bestIndex = index;
                bestValue = sum;
            }

            if (weightedDiff > bestConfidence)
            {
                bestConfidence = weightedDiff;
                mirroredConfidence = weightedSum;
                bestIndex = index + s_responseCount;
                bestValue = diff;
            }
        }

        var confidence = Reduce(bestConfidence, 17);
        var mirrored = Reduce(mirroredConfidence, 17);
        if (confidence < threshold || (confidence * ratio) < (256 * mirrored))
        {
            return new(false, false, false, 0, 0, 0, confidence);
        }

        var rotate180 = bestIndex >= s_responseCount;
        var localIndex = bestIndex % s_responseCount;
        var dy = (localIndex / (s_responseGridSize * s_scaleCount)) - 1;
        localIndex %= s_responseGridSize * s_scaleCount;
        var dx = (localIndex / s_scaleCount) - 1;
        var period = (localIndex % s_scaleCount) + 5;
        var type = bestValue > 0;
        var xOffset = (dx * c) - (dy * s);
        var yOffset = (dx * s) + (dy * c);
        return new(true, type, rotate180, xOffset, yOffset, period, confidence);
    }

    private static int Reduce(int value, int bits)
    {
        return (value + (1 << (bits - 1))) >> bits;
    }

    private static int[] CreateSymmetricKernel(ReadOnlySpan<int> halfKernel)
    {
        var kernel = new int[s_filterSize];
        var center = s_filterSize / 2;
        kernel[center] = halfKernel[0];
        for (var index = 1; index < halfKernel.Length; index++)
        {
            kernel[center - index] = halfKernel[index];
            kernel[center + index] = halfKernel[index];
        }

        return kernel;
    }

    private static int[] CreateAntiSymmetricKernel(ReadOnlySpan<int> halfKernel)
    {
        var kernel = new int[s_filterSize];
        var center = s_filterSize / 2;
        kernel[center] = 0;
        for (var index = 0; index < halfKernel.Length; index++)
        {
            kernel[center - index - 1] = -halfKernel[index];
            kernel[center + index + 1] = halfKernel[index];
        }

        return kernel;
    }
}

internal readonly record struct Nfiq2FingerJetBifFilterResult(
    bool Confirmed,
    bool Type,
    bool Rotate180,
    int XOffset,
    int YOffset,
    int Period,
    int Confidence);
