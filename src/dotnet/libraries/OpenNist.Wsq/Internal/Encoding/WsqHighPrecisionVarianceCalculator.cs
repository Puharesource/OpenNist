namespace OpenNist.Wsq.Internal.Encoding;

using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Metadata;

internal static class WsqHighPrecisionVarianceCalculator
{
    public static double[] ComputeWithSinglePrecisionAccumulation(
        ReadOnlySpan<double> waveletData,
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
        int width)
    {
        var variances = new double[WsqConstants.MaxSubbands];
        var varianceSum = 0.0f;

        for (var subband = 0; subband < WsqConstants.StartSizeRegion2; subband++)
        {
            variances[subband] = ComputeSubbandVariance(
                waveletData,
                quantizationTree[subband],
                width,
                useCroppedRegion: true,
                useSinglePrecisionAccumulation: true);
            varianceSum += (float)variances[subband];
        }

        if (varianceSum < 20000.0f)
        {
            for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
            {
                variances[subband] = ComputeSubbandVariance(
                    waveletData,
                    quantizationTree[subband],
                    width,
                    useCroppedRegion: false,
                    useSinglePrecisionAccumulation: true);
            }

            return variances;
        }

        for (var subband = WsqConstants.StartSizeRegion2; subband < WsqConstants.NumberOfSubbands; subband++)
        {
            variances[subband] = ComputeSubbandVariance(
                waveletData,
                quantizationTree[subband],
                width,
                useCroppedRegion: true,
                useSinglePrecisionAccumulation: true);
        }

        return variances;
    }

    public static double[] Compute(
        ReadOnlySpan<float> waveletData,
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
        int width)
    {
        var variances = new double[WsqConstants.MaxSubbands];
        var varianceSum = 0.0f;

        for (var subband = 0; subband < WsqConstants.StartSizeRegion2; subband++)
        {
            variances[subband] = ComputeSubbandVariance(
                waveletData,
                quantizationTree[subband],
                width,
                useCroppedRegion: true);
            varianceSum += (float)variances[subband];
        }

        if (varianceSum < 20000.0f)
        {
            for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
            {
                variances[subband] = ComputeSubbandVariance(
                    waveletData,
                    quantizationTree[subband],
                    width,
                    useCroppedRegion: false);
            }

            return variances;
        }

        for (var subband = WsqConstants.StartSizeRegion2; subband < WsqConstants.NumberOfSubbands; subband++)
        {
            variances[subband] = ComputeSubbandVariance(
                waveletData,
                quantizationTree[subband],
                width,
                useCroppedRegion: true);
        }

        return variances;
    }

    public static double[] Compute(
        ReadOnlySpan<double> waveletData,
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
        int width)
    {
        var variances = new double[WsqConstants.MaxSubbands];
        var varianceSum = 0.0;

        for (var subband = 0; subband < WsqConstants.StartSizeRegion2; subband++)
        {
            variances[subband] = ComputeSubbandVariance(
                waveletData,
                quantizationTree[subband],
                width,
                useCroppedRegion: true);
            varianceSum += variances[subband];
        }

        if (varianceSum < 20000.0)
        {
            for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
            {
                variances[subband] = ComputeSubbandVariance(
                    waveletData,
                    quantizationTree[subband],
                    width,
                    useCroppedRegion: false);
            }

            return variances;
        }

        for (var subband = WsqConstants.StartSizeRegion2; subband < WsqConstants.NumberOfSubbands; subband++)
        {
            variances[subband] = ComputeSubbandVariance(
                waveletData,
                quantizationTree[subband],
                width,
                useCroppedRegion: true);
        }

        return variances;
    }

    private static double ComputeSubbandVariance(
        ReadOnlySpan<double> waveletData,
        WsqQuantizationNode node,
        int width,
        bool useCroppedRegion,
        bool useSinglePrecisionAccumulation = false)
    {
        var startX = node.X;
        var startY = node.Y;
        var regionWidth = node.Width;
        var regionHeight = node.Height;

        if (useCroppedRegion)
        {
            startX += node.Width / 8;
            startY += 9 * node.Height / 32;
            regionWidth = 3 * node.Width / 4;
            regionHeight = 7 * node.Height / 16;
        }

        var rowStart = startY * width + startX;

        if (useSinglePrecisionAccumulation)
        {
            var singlePrecisionSquaredSum = 0.0f;
            var singlePrecisionPixelSum = 0.0f;

            for (var row = 0; row < regionHeight; row++)
            {
                var pixelIndex = rowStart + row * width;

                for (var column = 0; column < regionWidth; column++)
                {
                    var pixel = (float)waveletData[pixelIndex + column];
                    singlePrecisionPixelSum += pixel;
                    singlePrecisionSquaredSum += pixel * pixel;
                }
            }

            var singlePrecisionSampleCount = regionWidth * regionHeight;
            var singlePrecisionNormalizedSum = singlePrecisionPixelSum * singlePrecisionPixelSum / singlePrecisionSampleCount;
            return (singlePrecisionSquaredSum - singlePrecisionNormalizedSum) / (singlePrecisionSampleCount - 1.0f);
        }

        var squaredSum = 0.0;
        var pixelSum = 0.0;

        for (var row = 0; row < regionHeight; row++)
        {
            var pixelIndex = rowStart + row * width;

            for (var column = 0; column < regionWidth; column++)
            {
                var pixel = waveletData[pixelIndex + column];
                pixelSum += pixel;
                squaredSum += pixel * pixel;
            }
        }

        var sampleCount = regionWidth * regionHeight;
        var normalizedSum = pixelSum * pixelSum / sampleCount;
        return (squaredSum - normalizedSum) / (sampleCount - 1.0);
    }

    private static double ComputeSubbandVariance(
        ReadOnlySpan<float> waveletData,
        WsqQuantizationNode node,
        int width,
        bool useCroppedRegion)
    {
        var startX = node.X;
        var startY = node.Y;
        var regionWidth = node.Width;
        var regionHeight = node.Height;

        if (useCroppedRegion)
        {
            startX += node.Width / 8;
            startY += 9 * node.Height / 32;
            regionWidth = 3 * node.Width / 4;
            regionHeight = 7 * node.Height / 16;
        }

        var rowStart = startY * width + startX;
        var squaredSum = 0.0f;
        var pixelSum = 0.0f;

        for (var row = 0; row < regionHeight; row++)
        {
            var pixelIndex = rowStart + row * width;

            for (var column = 0; column < regionWidth; column++)
            {
                var pixel = waveletData[pixelIndex + column];
                pixelSum += pixel;
                squaredSum += pixel * pixel;
            }
        }

        var sampleCount = regionWidth * regionHeight;
        var normalizedSum = pixelSum * pixelSum / sampleCount;
        return (squaredSum - normalizedSum) / (sampleCount - 1.0f);
    }
}
