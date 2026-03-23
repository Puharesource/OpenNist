namespace OpenNist.Tests.Wsq.TestDiagnostics;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Encoding;

internal static class WsqHighPrecisionQbinSynthesisSnapshotBuilder
{
    public static async Task<WsqHighPrecisionQbinSynthesisSnapshot> CreateAsync(WsqEncodingReferenceCase testCase)
    {
        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath);

        WsqWaveletTreeBuilder.Build(
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            out var waveletTree,
            out var quantizationTree);

        var normalizedImage = WsqDoubleImageNormalizer.Normalize(rawBytes);
        var decomposedPixels = WsqDoubleDecomposition.Decompose(
            normalizedImage.Pixels,
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            waveletTree,
            WsqReferenceTables.CreateStandardTransformTable());
        var floatNormalizedImage = WsqFloatImageNormalizer.Normalize(rawBytes);
        var floatDecomposedPixels = WsqDecomposition.Decompose(
            floatNormalizedImage.Pixels,
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            waveletTree,
            WsqReferenceTables.CreateStandardTransformTable());
        var currentArtifacts = WsqHighPrecisionQuantizer.CreateQuantizationArtifacts(
            decomposedPixels,
            quantizationTree,
            testCase.RawImage.Width,
            testCase.BitRate);
        var currentTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
            currentArtifacts.Variances,
            testCase.BitRate,
            new(UseSinglePrecisionScaleFactor: true));
        var currentDoubleTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(currentArtifacts.Variances, testCase.BitRate);
        var currentSinglePrecisionScaleTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
            currentArtifacts.Variances,
            testCase.BitRate,
            new(UseSinglePrecisionScaleFactor: true));
        var currentSinglePrecisionProductTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
            currentArtifacts.Variances,
            testCase.BitRate,
            new(UseSinglePrecisionProduct: true));
        var currentSinglePrecisionReciprocalAreaTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
            currentArtifacts.Variances,
            testCase.BitRate,
            new(UseSinglePrecisionReciprocalAreaSum: true));
        var currentSinglePrecisionProductAndScaleTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
            currentArtifacts.Variances,
            testCase.BitRate,
            new(UseSinglePrecisionProduct: true, UseSinglePrecisionScaleFactor: true));
        var currentSinglePrecisionSigmaAndInitialBinTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
            currentArtifacts.Variances,
            testCase.BitRate,
            new(UseSinglePrecisionSigma: true, UseSinglePrecisionInitialQuantizationBins: true));
        var currentSinglePrecisionSigmaInitialAndScaleTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
            currentArtifacts.Variances,
            testCase.BitRate,
            new(
                UseSinglePrecisionSigma: true,
                UseSinglePrecisionInitialQuantizationBins: true,
                UseSinglePrecisionScaleFactor: true));

        var nbisAnalysis = await WsqNbisOracleReader.ReadAnalysisAsync(testCase);
        var subbandZeroNbisVariances = currentArtifacts.Variances.ToArray();
        subbandZeroNbisVariances[0] = nbisAnalysis.Variances[0];

        var subbandZeroNbisTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(subbandZeroNbisVariances, testCase.BitRate);
        var firstFourNbisVariances = currentArtifacts.Variances.ToArray();
        for (var subbandIndex = 0; subbandIndex < 4; subbandIndex++)
        {
            firstFourNbisVariances[subbandIndex] = nbisAnalysis.Variances[subbandIndex];
        }

        var firstFourNbisTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(firstFourNbisVariances, testCase.BitRate);
        var floatVariances = WsqHighPrecisionVarianceCalculator.Compute(
            floatDecomposedPixels,
            quantizationTree,
            testCase.RawImage.Width);
        var firstFourFloatVariances = currentArtifacts.Variances.ToArray();
        for (var subbandIndex = 0; subbandIndex < 4; subbandIndex++)
        {
            firstFourFloatVariances[subbandIndex] = floatVariances[subbandIndex];
        }

        var firstFourFloatTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(firstFourFloatVariances, testCase.BitRate);
        var doubleSinglePrecisionAccumulationVariances = WsqHighPrecisionVarianceCalculator.ComputeWithSinglePrecisionAccumulation(
            decomposedPixels,
            quantizationTree,
            testCase.RawImage.Width);
        var firstFourDoubleSinglePrecisionAccumulationVariances = currentArtifacts.Variances.ToArray();
        for (var subbandIndex = 0; subbandIndex < 4; subbandIndex++)
        {
            firstFourDoubleSinglePrecisionAccumulationVariances[subbandIndex] =
                doubleSinglePrecisionAccumulationVariances[subbandIndex];
        }

        var firstFourDoubleSinglePrecisionAccumulationTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
            firstFourDoubleSinglePrecisionAccumulationVariances,
            testCase.BitRate);
        var nbisTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(nbisAnalysis.Variances, testCase.BitRate);

        return new(
            testCase.FileName,
            CurrentVariance: currentArtifacts.Variances[0],
            NbisVariance: nbisAnalysis.Variances[0],
            CurrentInitialQuantizationBin: currentTrace.InitialQuantizationBins[0],
            CurrentQuantizationBin: currentArtifacts.QuantizationBins[0],
            CurrentQuantizationScale: currentTrace.QuantizationScale,
            CurrentIterationCount: currentTrace.IterationCount,
            CurrentFinalActiveSubbandCount: currentTrace.FinalActiveSubbands.Length,
            CurrentReciprocalAreaSum: currentTrace.ReciprocalAreaSum,
            CurrentProduct: currentTrace.Product,
            CurrentDoubleTraceQuantizationBin: currentDoubleTrace.QuantizationBins[0],
            CurrentDoubleTraceQuantizationScale: currentDoubleTrace.QuantizationScale,
            CurrentDoubleTraceReciprocalAreaSum: currentDoubleTrace.ReciprocalAreaSum,
            CurrentDoubleTraceProduct: currentDoubleTrace.Product,
            CurrentSinglePrecisionScaleQuantizationBin: currentSinglePrecisionScaleTrace.QuantizationBins[0],
            CurrentSinglePrecisionScaleQuantizationScale: currentSinglePrecisionScaleTrace.QuantizationScale,
            CurrentSinglePrecisionScaleIterationCount: currentSinglePrecisionScaleTrace.IterationCount,
            CurrentSinglePrecisionScaleFinalActiveSubbandCount: currentSinglePrecisionScaleTrace.FinalActiveSubbands.Length,
            CurrentSinglePrecisionProductQuantizationBin: currentSinglePrecisionProductTrace.QuantizationBins[0],
            CurrentSinglePrecisionProductQuantizationScale: currentSinglePrecisionProductTrace.QuantizationScale,
            CurrentSinglePrecisionProductReciprocalAreaSum: currentSinglePrecisionProductTrace.ReciprocalAreaSum,
            CurrentSinglePrecisionProductValue: currentSinglePrecisionProductTrace.Product,
            CurrentSinglePrecisionReciprocalAreaQuantizationBin: currentSinglePrecisionReciprocalAreaTrace.QuantizationBins[0],
            CurrentSinglePrecisionReciprocalAreaQuantizationScale: currentSinglePrecisionReciprocalAreaTrace.QuantizationScale,
            CurrentSinglePrecisionReciprocalAreaSum: currentSinglePrecisionReciprocalAreaTrace.ReciprocalAreaSum,
            CurrentSinglePrecisionReciprocalAreaProduct: currentSinglePrecisionReciprocalAreaTrace.Product,
            CurrentSinglePrecisionProductAndScaleQuantizationBin: currentSinglePrecisionProductAndScaleTrace.QuantizationBins[0],
            CurrentSinglePrecisionProductAndScaleQuantizationScale: currentSinglePrecisionProductAndScaleTrace.QuantizationScale,
            CurrentSinglePrecisionSigmaAndInitialBinQuantizationBin: currentSinglePrecisionSigmaAndInitialBinTrace.QuantizationBins[0],
            CurrentSinglePrecisionSigmaAndInitialBinQuantizationScale: currentSinglePrecisionSigmaAndInitialBinTrace.QuantizationScale,
            CurrentSinglePrecisionSigmaInitialAndScaleQuantizationBin: currentSinglePrecisionSigmaInitialAndScaleTrace.QuantizationBins[0],
            CurrentSinglePrecisionSigmaInitialAndScaleQuantizationScale: currentSinglePrecisionSigmaInitialAndScaleTrace.QuantizationScale,
            SubbandZeroNbisQuantizationBin: subbandZeroNbisTrace.QuantizationBins[0],
            SubbandZeroNbisQuantizationScale: subbandZeroNbisTrace.QuantizationScale,
            SubbandZeroNbisIterationCount: subbandZeroNbisTrace.IterationCount,
            SubbandZeroNbisFinalActiveSubbandCount: subbandZeroNbisTrace.FinalActiveSubbands.Length,
            FirstFourNbisQuantizationBin: firstFourNbisTrace.QuantizationBins[0],
            FirstFourNbisQuantizationScale: firstFourNbisTrace.QuantizationScale,
            FirstFourNbisIterationCount: firstFourNbisTrace.IterationCount,
            FirstFourNbisFinalActiveSubbandCount: firstFourNbisTrace.FinalActiveSubbands.Length,
            FirstFourFloatQuantizationBin: firstFourFloatTrace.QuantizationBins[0],
            FirstFourFloatQuantizationScale: firstFourFloatTrace.QuantizationScale,
            FirstFourFloatIterationCount: firstFourFloatTrace.IterationCount,
            FirstFourFloatFinalActiveSubbandCount: firstFourFloatTrace.FinalActiveSubbands.Length,
            FirstFourDoubleSinglePrecisionAccumulationQuantizationBin: firstFourDoubleSinglePrecisionAccumulationTrace.QuantizationBins[0],
            FirstFourDoubleSinglePrecisionAccumulationQuantizationScale: firstFourDoubleSinglePrecisionAccumulationTrace.QuantizationScale,
            FirstFourDoubleSinglePrecisionAccumulationIterationCount: firstFourDoubleSinglePrecisionAccumulationTrace.IterationCount,
            FirstFourDoubleSinglePrecisionAccumulationFinalActiveSubbandCount: firstFourDoubleSinglePrecisionAccumulationTrace.FinalActiveSubbands.Length,
            NbisQuantizationBin: nbisAnalysis.QuantizationBins[0],
            NbisTraceQuantizationBin: nbisTrace.QuantizationBins[0],
            NbisQuantizationScale: nbisTrace.QuantizationScale,
            NbisIterationCount: nbisTrace.IterationCount,
            NbisFinalActiveSubbandCount: nbisTrace.FinalActiveSubbands.Length);
    }
}

internal sealed record WsqHighPrecisionQbinSynthesisSnapshot(
    string FileName,
    double CurrentVariance,
    double NbisVariance,
    double CurrentInitialQuantizationBin,
    double CurrentQuantizationBin,
    double CurrentQuantizationScale,
    int CurrentIterationCount,
    int CurrentFinalActiveSubbandCount,
    double CurrentReciprocalAreaSum,
    double CurrentProduct,
    double CurrentDoubleTraceQuantizationBin,
    double CurrentDoubleTraceQuantizationScale,
    double CurrentDoubleTraceReciprocalAreaSum,
    double CurrentDoubleTraceProduct,
    double CurrentSinglePrecisionScaleQuantizationBin,
    double CurrentSinglePrecisionScaleQuantizationScale,
    int CurrentSinglePrecisionScaleIterationCount,
    int CurrentSinglePrecisionScaleFinalActiveSubbandCount,
    double CurrentSinglePrecisionProductQuantizationBin,
    double CurrentSinglePrecisionProductQuantizationScale,
    double CurrentSinglePrecisionProductReciprocalAreaSum,
    double CurrentSinglePrecisionProductValue,
    double CurrentSinglePrecisionReciprocalAreaQuantizationBin,
    double CurrentSinglePrecisionReciprocalAreaQuantizationScale,
    double CurrentSinglePrecisionReciprocalAreaSum,
    double CurrentSinglePrecisionReciprocalAreaProduct,
    double CurrentSinglePrecisionProductAndScaleQuantizationBin,
    double CurrentSinglePrecisionProductAndScaleQuantizationScale,
    double CurrentSinglePrecisionSigmaAndInitialBinQuantizationBin,
    double CurrentSinglePrecisionSigmaAndInitialBinQuantizationScale,
    double CurrentSinglePrecisionSigmaInitialAndScaleQuantizationBin,
    double CurrentSinglePrecisionSigmaInitialAndScaleQuantizationScale,
    double SubbandZeroNbisQuantizationBin,
    double SubbandZeroNbisQuantizationScale,
    int SubbandZeroNbisIterationCount,
    int SubbandZeroNbisFinalActiveSubbandCount,
    double FirstFourNbisQuantizationBin,
    double FirstFourNbisQuantizationScale,
    int FirstFourNbisIterationCount,
    int FirstFourNbisFinalActiveSubbandCount,
    double FirstFourFloatQuantizationBin,
    double FirstFourFloatQuantizationScale,
    int FirstFourFloatIterationCount,
    int FirstFourFloatFinalActiveSubbandCount,
    double FirstFourDoubleSinglePrecisionAccumulationQuantizationBin,
    double FirstFourDoubleSinglePrecisionAccumulationQuantizationScale,
    int FirstFourDoubleSinglePrecisionAccumulationIterationCount,
    int FirstFourDoubleSinglePrecisionAccumulationFinalActiveSubbandCount,
    double NbisQuantizationBin,
    double NbisTraceQuantizationBin,
    double NbisQuantizationScale,
    int NbisIterationCount,
    int NbisFinalActiveSubbandCount);
