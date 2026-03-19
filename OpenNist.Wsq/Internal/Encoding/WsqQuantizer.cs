namespace OpenNist.Wsq.Internal.Encoding;

using OpenNist.Wsq.Internal.Decoding;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Major Code Smell",
    "S1244:Floating point numbers should not be tested for equality",
    Justification = "The WSQ encoder must mirror the NBIS reference implementation's exact zero checks for quantization bins.")]
internal static class WsqQuantizer
{
    private static readonly float[] s_subbandWeights = CreateSubbandWeights();

    public static WsqQuantizationResult Quantize(
        float[] waveletData,
        WsqWaveletNode[] waveletTree,
        WsqQuantizationNode[] quantizationTree,
        int width,
        int height,
        float bitRate)
    {
        ArgumentNullException.ThrowIfNull(waveletData);
        ArgumentNullException.ThrowIfNull(waveletTree);
        ArgumentNullException.ThrowIfNull(quantizationTree);

        var variances = ComputeVariances(waveletData, quantizationTree, width);
        var quantizationBins = new float[WsqConstants.MaxSubbands];
        var zeroBins = new float[WsqConstants.MaxSubbands];
        ComputeQuantizationBins(variances, bitRate, quantizationBins, zeroBins);

        var quantizedCoefficients = QuantizeSubbands(waveletData, quantizationTree, width, quantizationBins, zeroBins);
        var quantizationTable = new WsqQuantizationTable(
            BinCenter: 44.0,
            QuantizationBins: quantizationBins.Select(static value => (double)value).ToArray(),
            ZeroBins: zeroBins.Select(static value => (double)value).ToArray());
        var blockSizes = WsqQuantizationDecoder.ComputeBlockSizes(quantizationTable, waveletTree, quantizationTree);

        return new(quantizationTable, quantizedCoefficients, blockSizes);
    }

    private static float[] ComputeVariances(
        ReadOnlySpan<float> waveletData,
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
        int width)
    {
        var variances = new float[WsqConstants.MaxSubbands];
        var varianceSum = 0.0f;

        for (var subband = 0; subband < WsqConstants.StartSizeRegion2; subband++)
        {
            variances[subband] = ComputeVariance(
                waveletData,
                quantizationTree[subband],
                width,
                useCroppedRegion: true);
            varianceSum += variances[subband];
        }

        if (varianceSum < 20000.0f)
        {
            for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
            {
                variances[subband] = ComputeVariance(
                    waveletData,
                    quantizationTree[subband],
                    width,
                    useCroppedRegion: false);
            }

            return variances;
        }

        for (var subband = WsqConstants.StartSizeRegion2; subband < WsqConstants.NumberOfSubbands; subband++)
        {
            variances[subband] = ComputeVariance(
                waveletData,
                quantizationTree[subband],
                width,
                useCroppedRegion: true);
        }

        return variances;
    }

    private static float ComputeVariance(
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
        var normalizedSum = (pixelSum * pixelSum) / sampleCount;
        return (squaredSum - normalizedSum) / (sampleCount - 1.0f);
    }

    private static void ComputeQuantizationBins(
        ReadOnlySpan<float> variances,
        float bitRate,
        Span<float> quantizationBins,
        Span<float> zeroBins)
    {
        var reciprocalSubbandAreas = new float[WsqConstants.NumberOfSubbands];
        var sigma = new float[WsqConstants.NumberOfSubbands];
        var initialSubbands = new int[WsqConstants.NumberOfSubbands];
        var workingSubbands = new int[WsqConstants.NumberOfSubbands];
        var positiveBitRateFlags = new bool[WsqConstants.NumberOfSubbands];

        SetReciprocalSubbandAreas(reciprocalSubbandAreas);

        var initialSubbandCount = 0;
        for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
        {
            if (variances[subband] < WsqConstants.VarianceThreshold)
            {
                quantizationBins[subband] = 0.0f;
                zeroBins[subband] = 0.0f;
                continue;
            }

            sigma[subband] = (float)Math.Sqrt(variances[subband]);
            quantizationBins[subband] = subband < WsqConstants.StartSizeRegion2
                ? 1.0f
                : (float)(10.0 / (s_subbandWeights[subband] * (float)Math.Log(variances[subband])));
            initialSubbands[initialSubbandCount] = subband;
            workingSubbands[initialSubbandCount] = subband;
            initialSubbandCount++;
        }

        if (initialSubbandCount == 0)
        {
            return;
        }

        Span<int> activeSubbands = workingSubbands;
        var activeSubbandCount = initialSubbandCount;

        while (true)
        {
            var reciprocalAreaSum = 0.0f;
            for (var index = 0; index < activeSubbandCount; index++)
            {
                reciprocalAreaSum += reciprocalSubbandAreas[activeSubbands[index]];
            }

            var product = 1.0f;
            for (var index = 0; index < activeSubbandCount; index++)
            {
                var subband = activeSubbands[index];
                product = (float)(product * Math.Pow(
                    sigma[subband] / quantizationBins[subband],
                    reciprocalSubbandAreas[subband]));
            }

            var quantizationScale = (float)(
                (Math.Pow(2.0, (bitRate / reciprocalAreaSum) - 1.0f) / 2.5)
                / Math.Pow(product, 1.0f / reciprocalAreaSum));
            var nonPositiveBitRateCount = 0;

            Array.Clear(positiveBitRateFlags);
            for (var index = 0; index < activeSubbandCount; index++)
            {
                var subband = activeSubbands[index];
                if ((quantizationBins[subband] / quantizationScale) >= (5.0f * sigma[subband]))
                {
                    positiveBitRateFlags[subband] = true;
                    nonPositiveBitRateCount++;
                }
            }

            if (nonPositiveBitRateCount == 0)
            {
                Array.Clear(positiveBitRateFlags);
                for (var index = 0; index < initialSubbandCount; index++)
                {
                    positiveBitRateFlags[initialSubbands[index]] = true;
                }

                for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
                {
                    quantizationBins[subband] = positiveBitRateFlags[subband]
                        ? quantizationBins[subband] / quantizationScale
                        : 0.0f;
                    zeroBins[subband] = 1.2f * quantizationBins[subband];
                }

                return;
            }

            var nextActiveSubbandCount = 0;
            for (var index = 0; index < activeSubbandCount; index++)
            {
                var subband = activeSubbands[index];
                if (!positiveBitRateFlags[subband])
                {
                    workingSubbands[nextActiveSubbandCount++] = subband;
                }
            }

            activeSubbands = workingSubbands;
            activeSubbandCount = nextActiveSubbandCount;
        }
    }

    private static short[] QuantizeSubbands(
        ReadOnlySpan<float> waveletData,
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
        int width,
        ReadOnlySpan<float> quantizationBins,
        ReadOnlySpan<float> zeroBins)
    {
        var quantizedCoefficients = new short[waveletData.Length];
        var coefficientIndex = 0;

        for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
        {
            if (quantizationBins[subband] == 0.0f)
            {
                continue;
            }

            var node = quantizationTree[subband];
            var halfZeroBin = zeroBins[subband] / 2.0f;
            var rowStart = node.Y * width + node.X;

            for (var row = 0; row < node.Height; row++)
            {
                var pixelIndex = rowStart + row * width;

                for (var column = 0; column < node.Width; column++)
                {
                    var coefficient = waveletData[pixelIndex + column];
                    short quantizedCoefficient;

                    if (-halfZeroBin <= coefficient && coefficient <= halfZeroBin)
                    {
                        quantizedCoefficient = 0;
                    }
                    else if (coefficient > 0.0f)
                    {
                        quantizedCoefficient = checked((short)(((coefficient - halfZeroBin) / quantizationBins[subband]) + 1.0f));
                    }
                    else
                    {
                        quantizedCoefficient = checked((short)(((coefficient + halfZeroBin) / quantizationBins[subband]) - 1.0f));
                    }

                    quantizedCoefficients[coefficientIndex++] = quantizedCoefficient;
                }
            }
        }

        Array.Resize(ref quantizedCoefficients, coefficientIndex);
        return quantizedCoefficients;
    }

    private static void SetReciprocalSubbandAreas(Span<float> reciprocalSubbandAreas)
    {
        const float firstRegionReciprocalArea = 1.0f / 1024.0f;
        const float secondRegionReciprocalArea = 1.0f / 256.0f;
        const float thirdRegionReciprocalArea = 1.0f / 16.0f;

        for (var subband = 0; subband < WsqConstants.StartSizeRegion2; subband++)
        {
            reciprocalSubbandAreas[subband] = firstRegionReciprocalArea;
        }

        for (var subband = WsqConstants.StartSizeRegion2; subband < WsqConstants.StartSizeRegion3; subband++)
        {
            reciprocalSubbandAreas[subband] = secondRegionReciprocalArea;
        }

        for (var subband = WsqConstants.StartSizeRegion3; subband < WsqConstants.NumberOfSubbands; subband++)
        {
            reciprocalSubbandAreas[subband] = thirdRegionReciprocalArea;
        }
    }

    private static float[] CreateSubbandWeights()
    {
        var weights = new float[WsqConstants.MaxSubbands];
        Array.Fill(weights, 1.0f, 0, WsqConstants.StartSubband3);
        weights[52] = 1.32f;
        weights[53] = 1.08f;
        weights[54] = 1.42f;
        weights[55] = 1.08f;
        weights[56] = 1.32f;
        weights[57] = 1.42f;
        weights[58] = 1.08f;
        weights[59] = 1.08f;
        return weights;
    }
}

internal sealed record WsqQuantizationResult(
    WsqQuantizationTable QuantizationTable,
    short[] QuantizedCoefficients,
    int[] BlockSizes);
