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
        ReadOnlySpan<double> coefficientWaveletData,
        WsqWaveletNode[] waveletTree,
        WsqQuantizationNode[] quantizationTree,
        int width,
        int height,
        float bitRate)
    {
        ArgumentNullException.ThrowIfNull(waveletData);
        ArgumentNullException.ThrowIfNull(waveletTree);
        ArgumentNullException.ThrowIfNull(quantizationTree);

        var quantizationArtifacts = CreateQuantizationArtifacts(waveletData, quantizationTree, width, bitRate);
        return QuantizeWithArtifacts(
            coefficientWaveletData,
            waveletTree,
            quantizationTree,
            width,
            quantizationArtifacts);
    }

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

        var quantizationArtifacts = CreateQuantizationArtifacts(waveletData, quantizationTree, width, bitRate);
        return QuantizeWithArtifacts(
            waveletData,
            waveletTree,
            quantizationTree,
            width,
            quantizationArtifacts);
    }

    internal static WsqQuantizationResult QuantizeWithArtifacts(
        ReadOnlySpan<float> coefficientWaveletData,
        WsqWaveletNode[] waveletTree,
        WsqQuantizationNode[] quantizationTree,
        int width,
        WsqQuantizationArtifacts quantizationArtifacts)
    {
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

    private static WsqQuantizationResult QuantizeWithArtifacts(
        ReadOnlySpan<double> coefficientWaveletData,
        WsqWaveletNode[] waveletTree,
        WsqQuantizationNode[] quantizationTree,
        int width,
        WsqQuantizationArtifacts quantizationArtifacts)
    {
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

    internal static WsqQuantizationArtifacts CreateQuantizationArtifacts(
        ReadOnlySpan<float> waveletData,
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
        int width,
        float bitRate)
    {
        var variances = WsqVarianceCalculator.Compute(waveletData, quantizationTree, width);
        var quantizationBins = new float[WsqConstants.MaxSubbands];
        var zeroBins = new float[WsqConstants.MaxSubbands];
        ComputeQuantizationBins(variances, bitRate, quantizationBins, zeroBins);
        return new(variances, quantizationBins, zeroBins);
    }

    internal static WsqQuantizationTrace CreateQuantizationTrace(
        ReadOnlySpan<float> variances,
        float bitRate)
    {
        var quantizationBins = new float[WsqConstants.MaxSubbands];
        var zeroBins = new float[WsqConstants.MaxSubbands];
        return ComputeQuantizationBinsTrace(variances, bitRate, quantizationBins, zeroBins);
    }

    private static void ComputeQuantizationBins(
        ReadOnlySpan<float> variances,
        float bitRate,
        Span<float> quantizationBins,
        Span<float> zeroBins)
    {
        ComputeQuantizationBinsTrace(variances, bitRate, quantizationBins, zeroBins);
    }

    private static WsqQuantizationTrace ComputeQuantizationBinsTrace(
        ReadOnlySpan<float> variances,
        float bitRate,
        Span<float> quantizationBins,
        Span<float> zeroBins)
    {
        var reciprocalSubbandAreas = new float[WsqConstants.NumberOfSubbands];
        var sigma = new float[WsqConstants.NumberOfSubbands];
        var initialQuantizationBins = new float[WsqConstants.NumberOfSubbands];
        var initialSubbands = new int[WsqConstants.NumberOfSubbands];
        var workingSubbands = new int[WsqConstants.NumberOfSubbands];
        var nonPositiveBitRateFlags = new int[WsqConstants.NumberOfSubbands];
        var reciprocalAreaSum = 0.0f;
        var product = 0.0f;
        var quantizationScale = 0.0f;
        var iterationCount = 0;

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

            sigma[subband] = SqrtLikeNbis(variances[subband]);
            initialQuantizationBins[subband] = subband < WsqConstants.StartSizeRegion2
                ? 1.0f
                : 10.0f / (s_subbandWeights[subband] * (float)Math.Log(variances[subband]));
            quantizationBins[subband] = initialQuantizationBins[subband];
            initialSubbands[initialSubbandCount] = subband;
            workingSubbands[initialSubbandCount] = subband;
            initialSubbandCount++;
        }

        if (initialSubbandCount == 0)
        {
            return new(
                Variances: variances.ToArray(),
                Sigma: sigma,
                InitialQuantizationBins: initialQuantizationBins,
                QuantizationBins: quantizationBins.ToArray(),
                ZeroBins: zeroBins.ToArray(),
                FinalActiveSubbands: [],
                ReciprocalAreaSum: 0.0f,
                Product: 0.0f,
                QuantizationScale: 0.0f,
                IterationCount: 0);
        }

        Span<int> activeSubbands = workingSubbands;
        var activeSubbandCount = initialSubbandCount;

        while (true)
        {
            reciprocalAreaSum = 0.0f;
            for (var index = 0; index < activeSubbandCount; index++)
            {
                reciprocalAreaSum += reciprocalSubbandAreas[activeSubbands[index]];
            }

            product = 1.0f;
            for (var index = 0; index < activeSubbandCount; index++)
            {
                var subband = activeSubbands[index];
                product = (float)(
                    product
                    * Math.Pow(
                        sigma[subband] / quantizationBins[subband],
                        reciprocalSubbandAreas[subband]));
            }

            quantizationScale = (float)(
                (Math.Pow(2.0, ((bitRate / reciprocalAreaSum) - 1.0)) / 2.5f)
                / Math.Pow(product, 1.0 / reciprocalAreaSum));
            var nonPositiveBitRateCount = 0;

            Array.Clear(nonPositiveBitRateFlags);
            for (var index = 0; index < activeSubbandCount; index++)
            {
                var subband = activeSubbands[index];
                if ((quantizationBins[subband] / quantizationScale) >= (5.0f * sigma[subband]))
                {
                    nonPositiveBitRateFlags[subband] = 1;
                    nonPositiveBitRateCount++;
                }
            }

            if (nonPositiveBitRateCount == 0)
            {
                Array.Clear(nonPositiveBitRateFlags);
                for (var index = 0; index < initialSubbandCount; index++)
                {
                    nonPositiveBitRateFlags[initialSubbands[index]] = 1;
                }

                for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
                {
                    quantizationBins[subband] = nonPositiveBitRateFlags[subband] != 0
                        ? quantizationBins[subband] / quantizationScale
                        : 0.0f;
                    zeroBins[subband] = (float)(1.2 * quantizationBins[subband]);
                }

                return new(
                    Variances: variances.ToArray(),
                    Sigma: sigma,
                    InitialQuantizationBins: initialQuantizationBins,
                    QuantizationBins: quantizationBins.ToArray(),
                    ZeroBins: zeroBins.ToArray(),
                    FinalActiveSubbands: activeSubbands[..activeSubbandCount].ToArray(),
                    ReciprocalAreaSum: reciprocalAreaSum,
                    Product: product,
                    QuantizationScale: quantizationScale,
                    IterationCount: iterationCount);
            }

            var nextActiveSubbandCount = 0;
            for (var index = 0; index < activeSubbandCount; index++)
            {
                var subband = activeSubbands[index];
                if (nonPositiveBitRateFlags[subband] == 0)
                {
                    workingSubbands[nextActiveSubbandCount++] = subband;
                }
            }

            activeSubbands = workingSubbands;
            activeSubbandCount = nextActiveSubbandCount;
            iterationCount++;
        }
    }

    private static void SetReciprocalSubbandAreas(Span<float> reciprocalSubbandAreas)
    {
        const float firstRegionReciprocalArea = (float)(1.0 / 1024.0);
        const float secondRegionReciprocalArea = (float)(1.0 / 256.0);
        const float thirdRegionReciprocalArea = (float)(1.0 / 16.0);

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

    private static float SqrtLikeNbis(float value) => (float)Math.Sqrt(value);
}

internal sealed record WsqQuantizationResult(
    WsqQuantizationTable QuantizationTable,
    short[] QuantizedCoefficients,
    int[] BlockSizes);
