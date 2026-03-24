namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2FingerJetBifFilterSupport
{
    private const byte PhasemapFiller = 127;
    private const int ImageFractionBits = 7;
    private const int ImageScale = 1 << ImageFractionBits;
    private const int FilterSize = 17;
    private const int ResponseGridSize = 3;
    private const int ScaleCount = 5;
    private const int PatchWidth = FilterSize + ResponseGridSize - 1;
    private const int PatchCenter = PatchWidth / 2;
    private const int ResponseCount = ResponseGridSize * ResponseGridSize * ScaleCount;

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

        var height = image.Length / width;
        var wx1 = x & (ImageScale - 1);
        var wy1 = y & (ImageScale - 1);
        var wx0 = ImageScale - wx1;
        var wy0 = ImageScale - wy1;
        var x0 = x >> ImageFractionBits;
        var y0 = y >> ImageFractionBits;
        if (x0 < 0 || y0 < 0 || y0 > height - 1 || x0 > width - 1)
        {
            return PhasemapFiller;
        }

        var index = x0 + (y0 * width);
        if ((uint)(index + width + 1) >= (uint)image.Length)
        {
            return PhasemapFiller;
        }

        var value =
            (image[index] * wx0 * wy0) +
            (image[index + 1] * wx1 * wy0) +
            (image[index + width] * wx0 * wy1) +
            (image[index + width + 1] * wx1 * wy1);
        return (byte)Reduce(value, ImageFractionBits * 2);
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

        Span<byte> patch = stackalloc byte[PatchWidth * PatchWidth];
        RotatePatch(phasemap, width, x, y, c, s, patch);

        Span<int> response = stackalloc int[ResponseCount * 2];
        ApplySeparableConvolutions(patch, s_cv1Kernel, s_horizontalCv1Kernels, response[..ResponseCount]);
        ApplySeparableConvolutions(patch, s_cv2Kernel, s_horizontalCv2Kernels, response[ResponseCount..]);

        return EvaluateButterfly(response, c, s, threshold, ratio);
    }

    private static void RotatePatch(ReadOnlySpan<byte> phasemap, int width, int x, int y, sbyte c, sbyte s, Span<byte> patch)
    {
        var fx = x << ImageFractionBits;
        var fy = y << ImageFractionBits;
        fx -= PatchCenter * (c - s);
        fy -= PatchCenter * (s + c);

        for (var row = 0; row < PatchWidth; row++)
        {
            var rowX = fx - (row * s);
            var rowY = fy + (row * c);
            for (var column = 0; column < PatchWidth; column++)
            {
                patch[(row * PatchWidth) + column] = SampleImage(phasemap, width, rowX + (column * c), rowY + (column * s));
            }
        }
    }

    private static void ApplySeparableConvolutions(ReadOnlySpan<byte> patch, int[] verticalKernel, int[][] horizontalKernels, Span<int> destination)
    {
        Span<int> intermediate = stackalloc int[PatchWidth * ResponseGridSize];
        for (var row = 0; row < ResponseGridSize; row++)
        {
            for (var column = 0; column < PatchWidth; column++)
            {
                intermediate[(row * PatchWidth) + column] = ConvolveVertical(patch, column, row, verticalKernel);
            }
        }

        var destinationIndex = 0;
        for (var row = 0; row < ResponseGridSize; row++)
        {
            for (var column = 0; column < ResponseGridSize; column++)
            {
                foreach (var horizontalKernel in horizontalKernels)
                {
                    destination[destinationIndex++] = ConvolveHorizontal(intermediate, row, column, horizontalKernel);
                }
            }
        }
    }

    private static int ConvolveVertical(ReadOnlySpan<byte> patch, int column, int rowOffset, int[] kernel)
    {
        var sum = 0;
        for (var row = 0; row < FilterSize; row++)
        {
            sum += patch[((rowOffset + row) * PatchWidth) + column] * kernel[row];
        }

        return sum;
    }

    private static int ConvolveHorizontal(ReadOnlySpan<int> intermediate, int row, int columnOffset, int[] kernel)
    {
        var sum = 0;
        var rowBase = row * PatchWidth;
        for (var column = 0; column < FilterSize; column++)
        {
            sum += intermediate[rowBase + columnOffset + column] * kernel[column];
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
        Span<int> combined = stackalloc int[ResponseCount * 2];
        response.CopyTo(combined);

        var bestConfidence = 0;
        var mirroredConfidence = 0;
        var bestIndex = 0;
        for (var index = 0; index < ResponseCount; index++)
        {
            var sum = combined[index] + combined[index + ResponseCount];
            var diff = combined[index] - combined[index + ResponseCount];
            combined[index] = sum;
            combined[index + ResponseCount] = diff;

            var weightedSum = Reduce(Math.Abs(sum) * ConfidenceWeights[index], 4);
            var weightedDiff = Reduce(Math.Abs(diff) * ConfidenceWeights[index], 4);
            if (weightedSum > bestConfidence)
            {
                bestConfidence = weightedSum;
                mirroredConfidence = weightedDiff;
                bestIndex = index;
            }

            if (weightedDiff > bestConfidence)
            {
                bestConfidence = weightedDiff;
                mirroredConfidence = weightedSum;
                bestIndex = index + ResponseCount;
            }
        }

        var confidence = Reduce(bestConfidence, 17);
        var mirrored = Reduce(mirroredConfidence, 17);
        if (confidence < threshold || (confidence * ratio) < (256 * mirrored))
        {
            return new(false, false, false, 0, 0, 0, confidence);
        }

        var rotate180 = bestIndex >= ResponseCount;
        var localIndex = bestIndex % ResponseCount;
        var dy = (localIndex / (ResponseGridSize * ScaleCount)) - 1;
        localIndex %= ResponseGridSize * ScaleCount;
        var dx = (localIndex / ScaleCount) - 1;
        var period = (localIndex % ScaleCount) + 5;
        var type = combined[bestIndex] > 0;
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
        var kernel = new int[FilterSize];
        var center = FilterSize / 2;
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
        var kernel = new int[FilterSize];
        var center = FilterSize / 2;
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
