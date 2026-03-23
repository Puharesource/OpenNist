namespace OpenNist.Wsq.Internal.Encoding;

using OpenNist.Wsq.Internal.Decoding;

internal static class WsqVarianceCalculator
{
    public static float[] Compute(
        ReadOnlySpan<float> waveletData,
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
        int width)
    {
        var variances = new float[WsqConstants.MaxSubbands];
        var varianceSum = 0.0f;

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

    private static float ComputeSubbandVariance(
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
            startY += (9 * node.Height) / 32;
            regionWidth = (3 * node.Width) / 4;
            regionHeight = (7 * node.Height) / 16;
        }

        var fp = (startY * width) + startX;
        var ssq = 0.0f;
        var sumPix = 0.0f;

        for (var row = 0; row < regionHeight; row++, fp += width - regionWidth)
        {
            for (var column = 0; column < regionWidth; column++)
            {
                var pixel = waveletData[fp];
                sumPix += pixel;
                ssq = MathF.FusedMultiplyAdd(pixel, pixel, ssq);
                fp++;
            }
        }

        var sampleCount = regionWidth * regionHeight;
        var sum2 = (sumPix * sumPix) / sampleCount;
        return (float)((ssq - sum2) / (sampleCount - 1.0));
    }
}
