namespace OpenNist.Tests.Wsq.TestDiagnostics;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Encoding;
using OpenNist.Wsq.Internal.Metadata;

internal static class WsqHighPrecisionRegionTwoFollowOnSnapshotBuilder
{
    public static async Task<WsqHighPrecisionRegionTwoFollowOnSnapshot> CreateAsync(WsqEncodingReferenceCase testCase)
    {
        var blockerSnapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAsync(testCase);
        var followOnSnapshot = blockerSnapshot.ProductionWithSubbandZeroBiasFollowOnSnapshot
            ?? throw new InvalidOperationException($"{testCase.FileName} does not expose a follow-on blocker snapshot.");

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
        var quantizationArtifacts = WsqHighPrecisionQuantizer.CreateQuantizationArtifacts(
            decomposedPixels,
            quantizationTree,
            testCase.RawImage.Width,
            testCase.BitRate);
        var floatVariances = WsqHighPrecisionVarianceCalculator.Compute(
            floatDecomposedPixels,
            quantizationTree,
            testCase.RawImage.Width);
        var doubleSinglePrecisionAccumulationVariances = WsqHighPrecisionVarianceCalculator.ComputeWithSinglePrecisionAccumulation(
            decomposedPixels,
            quantizationTree,
            testCase.RawImage.Width);

        var currentTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
            quantizationArtifacts.Variances,
            testCase.BitRate,
            new(UseSinglePrecisionScaleFactor: true));
        var sigmaAndInitialTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
            quantizationArtifacts.Variances,
            testCase.BitRate,
            new(UseSinglePrecisionSigma: true, UseSinglePrecisionInitialQuantizationBins: true));
        var literalSigmaAndInitialTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
            quantizationArtifacts.Variances,
            testCase.BitRate,
            new(UseSinglePrecisionSigma: true, UseLiteralSinglePrecisionInitialQuantizationBins: true));
        var sigmaInitialAndScaleTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
            quantizationArtifacts.Variances,
            testCase.BitRate,
            new(
                UseSinglePrecisionSigma: true,
                UseSinglePrecisionInitialQuantizationBins: true,
                UseSinglePrecisionScaleFactor: true));
        var literalSigmaInitialAndScaleTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
            quantizationArtifacts.Variances,
            testCase.BitRate,
            new(
                UseSinglePrecisionSigma: true,
                UseLiteralSinglePrecisionInitialQuantizationBins: true,
                UseSinglePrecisionScaleFactor: true));
        var productTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
            quantizationArtifacts.Variances,
            testCase.BitRate,
            new(UseSinglePrecisionProduct: true));
        var productAndScaleTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
            quantizationArtifacts.Variances,
            testCase.BitRate,
            new(UseSinglePrecisionProduct: true, UseSinglePrecisionScaleFactor: true));
        var allSinglePrecisionTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
            quantizationArtifacts.Variances,
            testCase.BitRate,
            new(
                UseSinglePrecisionSigma: true,
                UseSinglePrecisionInitialQuantizationBins: true,
                UseSinglePrecisionProduct: true,
                UseSinglePrecisionScaleFactor: true));

        var nbisAnalysis = await WsqNbisOracleReader.ReadAnalysisAsync(testCase);
        var targetSubbandIndex = followOnSnapshot.MismatchLocation.SubbandIndex;
        var targetSubbandNbisVarianceSpliced = quantizationArtifacts.Variances.ToArray();
        targetSubbandNbisVarianceSpliced[targetSubbandIndex] = nbisAnalysis.Variances[targetSubbandIndex];
        var targetSubbandNbisVarianceTrace =
            WsqHighPrecisionQuantizer.CreateQuantizationTrace(targetSubbandNbisVarianceSpliced, testCase.BitRate);
        var fullRegionTwoNbisVarianceSpliced = quantizationArtifacts.Variances.ToArray();
        for (var subbandIndex = WsqConstants.StartSizeRegion2; subbandIndex < WsqConstants.StartSizeRegion3; subbandIndex++)
        {
            fullRegionTwoNbisVarianceSpliced[subbandIndex] = nbisAnalysis.Variances[subbandIndex];
        }

        var fullRegionTwoNbisVarianceTrace =
            WsqHighPrecisionQuantizer.CreateQuantizationTrace(fullRegionTwoNbisVarianceSpliced, testCase.BitRate);
        var floatVarianceSpliced = quantizationArtifacts.Variances.ToArray();
        floatVarianceSpliced[targetSubbandIndex] = floatVariances[targetSubbandIndex];
        var floatVarianceTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(floatVarianceSpliced, testCase.BitRate);
        var singlePrecisionAccumulationVarianceSpliced = quantizationArtifacts.Variances.ToArray();
        singlePrecisionAccumulationVarianceSpliced[targetSubbandIndex] =
            doubleSinglePrecisionAccumulationVariances[targetSubbandIndex];
        var singlePrecisionAccumulationVarianceTrace =
            WsqHighPrecisionQuantizer.CreateQuantizationTrace(singlePrecisionAccumulationVarianceSpliced, testCase.BitRate);
        var currentDelta = Math.Abs(currentTrace.QuantizationBins[targetSubbandIndex] - followOnSnapshot.NbisQuantizationBin);
        var bestSingleSubbandNbisVarianceSplicedIndex = targetSubbandIndex;
        var bestSingleSubbandNbisVarianceSplicedQuantizationBin = currentTrace.QuantizationBins[targetSubbandIndex];
        var bestSingleSubbandDelta = currentDelta;
        var bestNonTargetSubbandNbisVarianceSplicedIndex = -1;
        var bestNonTargetSubbandNbisVarianceSplicedQuantizationBin = currentTrace.QuantizationBins[targetSubbandIndex];
        var bestNonTargetSubbandDelta = double.PositiveInfinity;
        var improvingNonTargetSubbandCount = 0;

        for (var subbandIndex = WsqConstants.StartSizeRegion2; subbandIndex < WsqConstants.StartSizeRegion3; subbandIndex++)
        {
            var singleSubbandNbisVarianceSpliced = quantizationArtifacts.Variances.ToArray();
            singleSubbandNbisVarianceSpliced[subbandIndex] = nbisAnalysis.Variances[subbandIndex];
            var singleSubbandTrace =
                WsqHighPrecisionQuantizer.CreateQuantizationTrace(singleSubbandNbisVarianceSpliced, testCase.BitRate);
            var singleSubbandQuantizationBin = singleSubbandTrace.QuantizationBins[targetSubbandIndex];
            var singleSubbandDelta = Math.Abs(singleSubbandQuantizationBin - followOnSnapshot.NbisQuantizationBin);

            if (singleSubbandDelta < bestSingleSubbandDelta)
            {
                bestSingleSubbandNbisVarianceSplicedIndex = subbandIndex;
                bestSingleSubbandNbisVarianceSplicedQuantizationBin = singleSubbandQuantizationBin;
                bestSingleSubbandDelta = singleSubbandDelta;
            }

            if (subbandIndex == targetSubbandIndex)
            {
                continue;
            }

            if (singleSubbandDelta < currentDelta)
            {
                improvingNonTargetSubbandCount++;
            }

            if (singleSubbandDelta < bestNonTargetSubbandDelta)
            {
                bestNonTargetSubbandNbisVarianceSplicedIndex = subbandIndex;
                bestNonTargetSubbandNbisVarianceSplicedQuantizationBin = singleSubbandQuantizationBin;
                bestNonTargetSubbandDelta = singleSubbandDelta;
            }
        }

        return new(
            testCase.FileName,
            targetSubbandIndex,
            followOnSnapshot.MismatchLocation,
            ThresholdQuantizationBinForNbisBucket: followOnSnapshot.ThresholdQuantizationBinForNbisBucket,
            ThresholdQuantizationBinForReferenceBucket: followOnSnapshot.ThresholdQuantizationBinForReferenceBucket,
            CurrentInitialQuantizationBin: currentTrace.InitialQuantizationBins[targetSubbandIndex],
            SigmaAndInitialInitialQuantizationBin: sigmaAndInitialTrace.InitialQuantizationBins[targetSubbandIndex],
            LiteralSigmaAndInitialInitialQuantizationBin:
                literalSigmaAndInitialTrace.InitialQuantizationBins[targetSubbandIndex],
            SigmaAndInitialInitialWithCurrentScaleQuantizationBin:
                sigmaAndInitialTrace.InitialQuantizationBins[targetSubbandIndex] / currentTrace.QuantizationScale,
            LiteralSigmaAndInitialInitialWithCurrentScaleQuantizationBin:
                literalSigmaAndInitialTrace.InitialQuantizationBins[targetSubbandIndex] / currentTrace.QuantizationScale,
            CurrentInitialWithSigmaAndInitialScaleQuantizationBin:
                currentTrace.InitialQuantizationBins[targetSubbandIndex] / sigmaInitialAndScaleTrace.QuantizationScale,
            CurrentQuantizationBin: currentTrace.QuantizationBins[targetSubbandIndex],
            SigmaAndInitialQuantizationBin: sigmaAndInitialTrace.QuantizationBins[targetSubbandIndex],
            LiteralSigmaAndInitialQuantizationBin: literalSigmaAndInitialTrace.QuantizationBins[targetSubbandIndex],
            SigmaInitialAndScaleQuantizationBin: sigmaInitialAndScaleTrace.QuantizationBins[targetSubbandIndex],
            LiteralSigmaInitialAndScaleQuantizationBin:
                literalSigmaInitialAndScaleTrace.QuantizationBins[targetSubbandIndex],
            ProductQuantizationBin: productTrace.QuantizationBins[targetSubbandIndex],
            ProductAndScaleQuantizationBin: productAndScaleTrace.QuantizationBins[targetSubbandIndex],
            AllSinglePrecisionQuantizationBin: allSinglePrecisionTrace.QuantizationBins[targetSubbandIndex],
            TargetSubbandNbisVarianceSplicedQuantizationBin:
                targetSubbandNbisVarianceTrace.QuantizationBins[targetSubbandIndex],
            FullRegionTwoNbisVarianceSplicedQuantizationBin:
                fullRegionTwoNbisVarianceTrace.QuantizationBins[targetSubbandIndex],
            BestSingleSubbandNbisVarianceSplicedIndex: bestSingleSubbandNbisVarianceSplicedIndex,
            BestSingleSubbandNbisVarianceSplicedQuantizationBin:
                bestSingleSubbandNbisVarianceSplicedQuantizationBin,
            BestNonTargetSubbandNbisVarianceSplicedIndex: bestNonTargetSubbandNbisVarianceSplicedIndex,
            BestNonTargetSubbandNbisVarianceSplicedQuantizationBin:
                bestNonTargetSubbandNbisVarianceSplicedQuantizationBin,
            ImprovingNonTargetSubbandCount: improvingNonTargetSubbandCount,
            FullRegionTwoNbisInitialQuantizationBin:
                fullRegionTwoNbisVarianceTrace.InitialQuantizationBins[targetSubbandIndex],
            CurrentInitialWithFullRegionTwoNbisScaleQuantizationBin:
                currentTrace.InitialQuantizationBins[targetSubbandIndex] / fullRegionTwoNbisVarianceTrace.QuantizationScale,
            FullRegionTwoNbisInitialWithCurrentScaleQuantizationBin:
                fullRegionTwoNbisVarianceTrace.InitialQuantizationBins[targetSubbandIndex] / currentTrace.QuantizationScale,
            FloatVarianceSplicedQuantizationBin: floatVarianceTrace.QuantizationBins[targetSubbandIndex],
            SinglePrecisionAccumulationVarianceSplicedQuantizationBin:
                singlePrecisionAccumulationVarianceTrace.QuantizationBins[targetSubbandIndex],
            NbisQuantizationBin: followOnSnapshot.NbisQuantizationBin,
            ReferenceQuantizationBin: followOnSnapshot.ReferenceQuantizationBin,
            CurrentQuantizationScale: currentTrace.QuantizationScale,
            SigmaAndInitialQuantizationScale: sigmaAndInitialTrace.QuantizationScale,
            SigmaInitialAndScaleQuantizationScale: sigmaInitialAndScaleTrace.QuantizationScale,
            ProductQuantizationScale: productTrace.QuantizationScale,
            ProductAndScaleQuantizationScale: productAndScaleTrace.QuantizationScale,
            AllSinglePrecisionQuantizationScale: allSinglePrecisionTrace.QuantizationScale,
            FullRegionTwoNbisVarianceSplicedQuantizationScale:
                fullRegionTwoNbisVarianceTrace.QuantizationScale,
            FloatVarianceSplicedQuantizationScale: floatVarianceTrace.QuantizationScale,
            SinglePrecisionAccumulationVarianceSplicedQuantizationScale:
                singlePrecisionAccumulationVarianceTrace.QuantizationScale,
            CurrentVariance: quantizationArtifacts.Variances[targetSubbandIndex],
            FloatVariance: floatVariances[targetSubbandIndex],
            DoubleSinglePrecisionAccumulationVariance: doubleSinglePrecisionAccumulationVariances[targetSubbandIndex],
            NbisVariance: nbisAnalysis.Variances[targetSubbandIndex]);
    }
}

internal readonly record struct WsqHighPrecisionRegionTwoFollowOnSnapshot(
    string FileName,
    int TargetSubbandIndex,
    WsqCoefficientLocation TargetLocation,
    double ThresholdQuantizationBinForNbisBucket,
    double ThresholdQuantizationBinForReferenceBucket,
    double CurrentInitialQuantizationBin,
    double SigmaAndInitialInitialQuantizationBin,
    double LiteralSigmaAndInitialInitialQuantizationBin,
    double SigmaAndInitialInitialWithCurrentScaleQuantizationBin,
    double LiteralSigmaAndInitialInitialWithCurrentScaleQuantizationBin,
    double CurrentInitialWithSigmaAndInitialScaleQuantizationBin,
    double CurrentQuantizationBin,
    double SigmaAndInitialQuantizationBin,
    double LiteralSigmaAndInitialQuantizationBin,
    double SigmaInitialAndScaleQuantizationBin,
    double LiteralSigmaInitialAndScaleQuantizationBin,
    double ProductQuantizationBin,
    double ProductAndScaleQuantizationBin,
    double AllSinglePrecisionQuantizationBin,
    double TargetSubbandNbisVarianceSplicedQuantizationBin,
    double FullRegionTwoNbisVarianceSplicedQuantizationBin,
    int BestSingleSubbandNbisVarianceSplicedIndex,
    double BestSingleSubbandNbisVarianceSplicedQuantizationBin,
    int BestNonTargetSubbandNbisVarianceSplicedIndex,
    double BestNonTargetSubbandNbisVarianceSplicedQuantizationBin,
    int ImprovingNonTargetSubbandCount,
    double FullRegionTwoNbisInitialQuantizationBin,
    double CurrentInitialWithFullRegionTwoNbisScaleQuantizationBin,
    double FullRegionTwoNbisInitialWithCurrentScaleQuantizationBin,
    double FloatVarianceSplicedQuantizationBin,
    double SinglePrecisionAccumulationVarianceSplicedQuantizationBin,
    double NbisQuantizationBin,
    double ReferenceQuantizationBin,
    double CurrentQuantizationScale,
    double SigmaAndInitialQuantizationScale,
    double SigmaInitialAndScaleQuantizationScale,
    double ProductQuantizationScale,
    double ProductAndScaleQuantizationScale,
    double AllSinglePrecisionQuantizationScale,
    double FullRegionTwoNbisVarianceSplicedQuantizationScale,
    double FloatVarianceSplicedQuantizationScale,
    double SinglePrecisionAccumulationVarianceSplicedQuantizationScale,
    double CurrentVariance,
    double FloatVariance,
    double DoubleSinglePrecisionAccumulationVariance,
    double NbisVariance);
