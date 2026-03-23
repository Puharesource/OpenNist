namespace OpenNist.Wsq.Internal.Encoding;

using System.Linq;
using OpenNist.Wsq.Internal.Decoding;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Major Code Smell",
    "S1244:Floating point numbers should not be tested for equality",
    Justification = "The WSQ encoder must mirror the reference implementation's exact zero checks for quantization bins.")]
internal static class WsqHighPrecisionQuantizer
{
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
        return QuantizeWithArtifacts(
            coefficientWaveletData,
            waveletTree,
            quantizationTree,
            width,
            quantizationArtifacts);
    }

    private static WsqQuantizationResult QuantizeWithArtifacts(
        ReadOnlySpan<double> coefficientWaveletData,
        WsqWaveletNode[] waveletTree,
        WsqQuantizationNode[] quantizationTree,
        int width,
        WsqHighPrecisionQuantizationArtifacts quantizationArtifacts)
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

    internal static WsqHighPrecisionQuantizationArtifacts CreateQuantizationArtifacts(
        ReadOnlySpan<double> waveletData,
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
        int width,
        double bitRate)
    {
        var variances = WsqHighPrecisionVarianceCalculator.ComputeWithSinglePrecisionAccumulation(
            waveletData,
            quantizationTree,
            width);
        var quantizationTrace = CreateNbisStyleQuantizationTrace(variances, bitRate);
        return new(variances, quantizationTrace.QuantizationBins, quantizationTrace.ZeroBins);
    }

    internal static WsqHighPrecisionQuantizationArtifacts CreateQuantizationArtifacts(
        ReadOnlySpan<float> waveletData,
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
        int width,
        double bitRate)
    {
        var variances = WsqHighPrecisionVarianceCalculator.Compute(waveletData, quantizationTree, width);
        var quantizationTrace = CreateNbisStyleQuantizationTrace(variances, bitRate);
        return new(variances, quantizationTrace.QuantizationBins, quantizationTrace.ZeroBins);
    }

    internal static WsqHighPrecisionQuantizationTrace CreateNbisStyleQuantizationTrace(
        ReadOnlySpan<double> variances,
        double bitRate)
    {
        var reciprocalSubbandAreas = new float[WsqConstants.NumberOfSubbands];
        var sigma = new float[WsqConstants.NumberOfSubbands];
        var initialQuantizationBins = new float[WsqConstants.MaxSubbands];
        var quantizationBins = new float[WsqConstants.MaxSubbands];
        var zeroBins = new float[WsqConstants.MaxSubbands];
        var initialSubbands = new int[WsqConstants.NumberOfSubbands];
        var workingSubbands = new int[WsqConstants.NumberOfSubbands];
        var nonPositiveBitRateFlags = new int[WsqConstants.NumberOfSubbands];

        WsqQuantizationParameters.SetReciprocalSubbandAreas(reciprocalSubbandAreas);

        var initialSubbandCount = 0;
        for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
        {
            quantizationBins[subband] = 0.0f;
            zeroBins[subband] = 0.0f;

            var variance = (float)variances[subband];
            if (variance < WsqConstants.VarianceThreshold)
            {
                continue;
            }

            quantizationBins[subband] = subband < WsqConstants.StartSizeRegion2
                ? 1.0f
                : 10.0f / (WsqQuantizationParameters.SubbandWeights[subband] * LogLikeNbis(variance));
            sigma[subband] = SqrtLikeNbis(variance);
            initialSubbands[initialSubbandCount] = subband;
            workingSubbands[initialSubbandCount++] = subband;
        }

        if (initialSubbandCount == 0)
        {
            return new(
                variances.ToArray(),
                Array.ConvertAll(sigma, static value => (double)value),
                Array.ConvertAll(initialQuantizationBins, static value => (double)value),
                Array.ConvertAll(quantizationBins, static value => (double)value),
                Array.ConvertAll(zeroBins, static value => (double)value),
                [],
                ReciprocalAreaSum: 0.0,
                Product: 0.0,
                QuantizationScale: 0.0,
                IterationCount: 0);
        }

        Array.Copy(quantizationBins, initialQuantizationBins, quantizationBins.Length);

        Span<int> activeSubbands = workingSubbands;
        var activeSubbandCount = initialSubbandCount;
        var reciprocalAreaSum = 0.0f;
        var product = 0.0f;
        var quantizationScale = 0.0f;
        var iterationCount = 0;

        while (true)
        {
            iterationCount++;
            reciprocalAreaSum = 0.0f;
            for (var index = 0; index < activeSubbandCount; index++)
            {
                reciprocalAreaSum += reciprocalSubbandAreas[activeSubbands[index]];
            }

            product = 1.0f;
            for (var index = 0; index < activeSubbandCount; index++)
            {
                var subband = activeSubbands[index];
                product *= PowLikeNbis(
                    sigma[subband] / quantizationBins[subband],
                    reciprocalSubbandAreas[subband]);
            }

            quantizationScale = (PowLikeNbis(2.0f, (((float)bitRate / reciprocalAreaSum) - 1.0f)) / 2.5f)
                / PowLikeNbis(product, 1.0f / reciprocalAreaSum);

            Array.Clear(nonPositiveBitRateFlags);
            var nonPositiveBitRateCount = 0;
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
                break;
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
        }

        Array.Clear(nonPositiveBitRateFlags);
        for (var index = 0; index < initialSubbandCount; index++)
        {
            nonPositiveBitRateFlags[initialSubbands[index]] = 1;
        }

        for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
        {
            if (nonPositiveBitRateFlags[subband] != 0)
            {
                quantizationBins[subband] /= quantizationScale;
            }
            else
            {
                quantizationBins[subband] = 0.0f;
            }

            zeroBins[subband] = 1.2f * quantizationBins[subband];
        }

        return new(
            variances.ToArray(),
            Array.ConvertAll(sigma, static value => (double)value),
            Array.ConvertAll(initialQuantizationBins, static value => (double)value),
            Array.ConvertAll(quantizationBins, static value => (double)value),
            Array.ConvertAll(zeroBins, static value => (double)value),
            initialSubbands[..initialSubbandCount].ToArray(),
            ReciprocalAreaSum: reciprocalAreaSum,
            Product: product,
            QuantizationScale: quantizationScale,
            IterationCount: iterationCount);
    }

    internal static WsqHighPrecisionQuantizationTrace CreateQuantizationTrace(
        ReadOnlySpan<double> variances,
        double bitRate,
        WsqHighPrecisionQuantizationTraceOptions options = default)
    {
        var reciprocalSubbandAreas = new double[WsqConstants.NumberOfSubbands];
        var sigma = new double[WsqConstants.NumberOfSubbands];
        var initialQuantizationBins = new double[WsqConstants.MaxSubbands];
        var quantizationBins = new double[WsqConstants.MaxSubbands];
        var zeroBins = new double[WsqConstants.MaxSubbands];
        var initialSubbands = new int[WsqConstants.NumberOfSubbands];
        var workingSubbands = new int[WsqConstants.NumberOfSubbands];
        var positiveBitRateFlags = new bool[WsqConstants.NumberOfSubbands];

        WsqQuantizationParameters.SetReciprocalSubbandAreas(reciprocalSubbandAreas);

        var initialSubbandCount = 0;
        for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
        {
            if (variances[subband] < WsqConstants.VarianceThreshold)
            {
                continue;
            }

            var variance = variances[subband];
            sigma[subband] = Math.Sqrt(variance);
            if (options.UseSinglePrecisionSigma)
            {
                sigma[subband] = (float)sigma[subband];
            }

            initialQuantizationBins[subband] = subband < WsqConstants.StartSizeRegion2
                ? 1.0
                : 10.0 / (WsqQuantizationParameters.SubbandWeights[subband] * Math.Log(variance));
            if (options.UseLiteralSinglePrecisionInitialQuantizationBins)
            {
                initialQuantizationBins[subband] = subband < WsqConstants.StartSizeRegion2
                    ? 1.0f
                    : 10.0f / (WsqQuantizationParameters.SubbandWeights[subband] * MathF.Log((float)variance));
            }
            else if (options.UseSinglePrecisionInitialQuantizationBins)
            {
                initialQuantizationBins[subband] = (float)initialQuantizationBins[subband];
            }

            initialSubbands[initialSubbandCount] = subband;
            workingSubbands[initialSubbandCount] = subband;
            initialSubbandCount++;
        }

        if (initialSubbandCount == 0)
        {
            return new(
                variances.ToArray(),
                sigma,
                initialQuantizationBins,
                quantizationBins,
                zeroBins,
                [],
                ReciprocalAreaSum: 0.0,
                Product: 0.0,
                QuantizationScale: 0.0,
                IterationCount: 0);
        }

        Span<int> activeSubbands = workingSubbands;
        var activeSubbandCount = initialSubbandCount;
        var quantizationScale = 0.0;
        var iterationCount = 0;

        while (true)
        {
            iterationCount++;
            var reciprocalAreaSum = 0.0;
            for (var index = 0; index < activeSubbandCount; index++)
            {
                reciprocalAreaSum += reciprocalSubbandAreas[activeSubbands[index]];
            }
            if (options.UseSinglePrecisionReciprocalAreaSum)
            {
                reciprocalAreaSum = (float)reciprocalAreaSum;
            }

            var product = 1.0;
            for (var index = 0; index < activeSubbandCount; index++)
            {
                var subband = activeSubbands[index];
                var productBase = sigma[subband] / initialQuantizationBins[subband];
                if (options.UseSinglePrecisionProduct)
                {
                    productBase = (float)productBase;
                }

                product *= Math.Pow(
                    productBase,
                    reciprocalSubbandAreas[subband]);
                if (options.UseSinglePrecisionProduct)
                {
                    product = (float)product;
                }
            }

            quantizationScale = (Math.Pow(2.0, (bitRate / reciprocalAreaSum) - 1.0) / 2.5)
                / Math.Pow(product, 1.0 / reciprocalAreaSum);
            if (options.UseSinglePrecisionScaleFactor)
            {
                quantizationScale = (float)quantizationScale;
            }
            var nonPositiveBitRateCount = 0;

            Array.Clear(positiveBitRateFlags);
            for (var index = 0; index < activeSubbandCount; index++)
            {
                var subband = activeSubbands[index];
                if ((initialQuantizationBins[subband] / quantizationScale) >= (5.0 * sigma[subband]))
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
                        ? initialQuantizationBins[subband] / quantizationScale
                        : 0.0;
                    var zeroBin = 1.2 * quantizationBin;
                    quantizationBins[subband] = quantizationBin;
                    zeroBins[subband] = zeroBin;
                }

                var finalActiveSubbands = initialSubbands
                    .Take(initialSubbandCount)
                    .Where(subband => positiveBitRateFlags[subband])
                    .ToArray();

                return new(
                    variances.ToArray(),
                    sigma,
                    initialQuantizationBins,
                    quantizationBins,
                    zeroBins,
                    finalActiveSubbands,
                    reciprocalAreaSum,
                    product,
                    quantizationScale,
                    iterationCount);
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

    private static float LogLikeNbis(float value) => (float)Math.Log(value);

    private static float PowLikeNbis(float x, float y) => (float)Math.Pow(x, y);

    private static float SqrtLikeNbis(float value) => (float)Math.Sqrt(value);
}
