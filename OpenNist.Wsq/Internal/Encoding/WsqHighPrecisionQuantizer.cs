namespace OpenNist.Wsq.Internal.Encoding;

using OpenNist.Wsq.Internal.Decoding;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Major Code Smell",
    "S1244:Floating point numbers should not be tested for equality",
    Justification = "The WSQ encoder must mirror the reference implementation's exact zero checks for quantization bins.")]
internal static class WsqHighPrecisionQuantizer
{
    private static readonly float[] s_subbandWeights = CreateSubbandWeights();

    public static WsqQuantizationResult Quantize(
        double[] waveletData,
        WsqWaveletNode[] waveletTree,
        WsqQuantizationNode[] quantizationTree,
        int width,
        int height,
        double bitRate)
    {
        return Quantize(
            waveletData,
            waveletData,
            waveletTree,
            quantizationTree,
            width,
            height,
            bitRate);
    }

    public static WsqQuantizationResult Quantize(
        ReadOnlySpan<float> waveletData,
        WsqWaveletNode[] waveletTree,
        WsqQuantizationNode[] quantizationTree,
        int width,
        int height,
        double bitRate)
    {
        ArgumentNullException.ThrowIfNull(waveletTree);
        ArgumentNullException.ThrowIfNull(quantizationTree);

        var quantizationArtifacts = CreateQuantizationArtifacts(waveletData, quantizationTree, width, bitRate);
        var quantizationTable = WsqQuantizationTableFactory.Create(
            quantizationArtifacts.QuantizationBins,
            quantizationArtifacts.ZeroBins);
        var quantizedCoefficients = WsqCoefficientQuantizer.Quantize(
            waveletData,
            quantizationTree,
            width,
            quantizationArtifacts.QuantizationBins,
            quantizationArtifacts.ZeroBins);
        var blockSizes = WsqQuantizationDecoder.ComputeBlockSizes(quantizationTable, waveletTree, quantizationTree);

        return new(quantizationTable, quantizedCoefficients, blockSizes);
    }

    public static WsqQuantizationResult Quantize(
        ReadOnlySpan<double> varianceWaveletData,
        ReadOnlySpan<double> coefficientWaveletData,
        WsqWaveletNode[] waveletTree,
        WsqQuantizationNode[] quantizationTree,
        int width,
        int height,
        double bitRate)
    {
        ArgumentNullException.ThrowIfNull(waveletTree);
        ArgumentNullException.ThrowIfNull(quantizationTree);

        var quantizationArtifacts = CreateQuantizationArtifacts(varianceWaveletData, quantizationTree, width, bitRate);
        var quantizationTable = WsqQuantizationTableFactory.Create(
            quantizationArtifacts.QuantizationBins,
            quantizationArtifacts.ZeroBins);
        var quantizedCoefficients = WsqCoefficientQuantizer.Quantize(
            coefficientWaveletData,
            quantizationTree,
            width,
            quantizationArtifacts.QuantizationBins,
            quantizationArtifacts.ZeroBins);
        var blockSizes = WsqQuantizationDecoder.ComputeBlockSizes(quantizationTable, waveletTree, quantizationTree);

        return new(quantizationTable, quantizedCoefficients, blockSizes);
    }

    internal static WsqHighPrecisionQuantizationArtifacts CreateQuantizationArtifacts(
        ReadOnlySpan<double> waveletData,
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
        int width,
        double bitRate)
    {
        var variances = WsqHighPrecisionVarianceCalculator.Compute(waveletData, quantizationTree, width);
        var quantizationBins = new double[WsqConstants.MaxSubbands];
        var zeroBins = new double[WsqConstants.MaxSubbands];
        ComputeQuantizationBins(variances, bitRate, quantizationBins, zeroBins);
        return new(variances, quantizationBins, zeroBins);
    }

    internal static WsqHighPrecisionQuantizationArtifacts CreateQuantizationArtifacts(
        ReadOnlySpan<float> waveletData,
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
        int width,
        double bitRate)
    {
        var variances = WsqHighPrecisionVarianceCalculator.Compute(waveletData, quantizationTree, width);
        var quantizationBins = new double[WsqConstants.MaxSubbands];
        var zeroBins = new double[WsqConstants.MaxSubbands];
        ComputeQuantizationBins(variances, bitRate, quantizationBins, zeroBins, useSinglePrecisionScaleFactor: true);
        return new(variances, quantizationBins, zeroBins);
    }

    private static void ComputeQuantizationBins(
        ReadOnlySpan<double> variances,
        double bitRate,
        Span<double> quantizationBins,
        Span<double> zeroBins,
        bool useSinglePrecisionScaleFactor = false)
    {
        var reciprocalSubbandAreas = new double[WsqConstants.NumberOfSubbands];
        var sigma = new double[WsqConstants.NumberOfSubbands];
        var workingQuantizationBins = new double[WsqConstants.NumberOfSubbands];
        var initialSubbands = new int[WsqConstants.NumberOfSubbands];
        var workingSubbands = new int[WsqConstants.NumberOfSubbands];
        var positiveBitRateFlags = new bool[WsqConstants.NumberOfSubbands];

        SetReciprocalSubbandAreas(reciprocalSubbandAreas);

        var initialSubbandCount = 0;
        for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
        {
            if (variances[subband] < WsqConstants.VarianceThreshold)
            {
                quantizationBins[subband] = 0.0;
                zeroBins[subband] = 0.0;
                workingQuantizationBins[subband] = 0.0f;
                continue;
            }

            var variance = variances[subband];
            sigma[subband] = Math.Sqrt(variance);
            workingQuantizationBins[subband] = subband < WsqConstants.StartSizeRegion2
                ? 1.0
                : 10.0 / (s_subbandWeights[subband] * Math.Log(variance));
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
            var reciprocalAreaSum = 0.0;
            for (var index = 0; index < activeSubbandCount; index++)
            {
                reciprocalAreaSum += reciprocalSubbandAreas[activeSubbands[index]];
            }

            var product = 1.0;
            for (var index = 0; index < activeSubbandCount; index++)
            {
                var subband = activeSubbands[index];
                product *= Math.Pow(
                    sigma[subband] / workingQuantizationBins[subband],
                    reciprocalSubbandAreas[subband]);
            }

            var quantizationScale = (Math.Pow(2.0, (bitRate / reciprocalAreaSum) - 1.0) / 2.5)
                / Math.Pow(product, 1.0 / reciprocalAreaSum);
            if (useSinglePrecisionScaleFactor)
            {
                quantizationScale = (float)quantizationScale;
            }
            var nonPositiveBitRateCount = 0;

            Array.Clear(positiveBitRateFlags);
            for (var index = 0; index < activeSubbandCount; index++)
            {
                var subband = activeSubbands[index];
                if ((workingQuantizationBins[subband] / quantizationScale) >= (5.0 * sigma[subband]))
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
                    var quantizationBin = positiveBitRateFlags[subband]
                        ? workingQuantizationBins[subband] / quantizationScale
                        : 0.0;
                    var zeroBin = 1.2 * quantizationBin;
                    quantizationBins[subband] = quantizationBin;
                    zeroBins[subband] = zeroBin;
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

    private static void SetReciprocalSubbandAreas(Span<double> reciprocalSubbandAreas)
    {
        const double firstRegionReciprocalArea = 1.0 / 1024.0;
        const double secondRegionReciprocalArea = 1.0 / 256.0;
        const double thirdRegionReciprocalArea = 1.0 / 16.0;

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
