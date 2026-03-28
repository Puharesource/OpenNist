namespace OpenNist.Tests.Wsq.TestDiagnostics;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Container;
using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Encoding;

internal static class WsqEncoderBlockerSnapshotBuilder
{
    private const double s_subbandZeroDiagnosticQuantizationBias = 0.999996;

    public static async Task<WsqEncoderBlockerSnapshot> CreateAsync(WsqEncodingReferenceCase testCase)
    {
        return await CreateAsync(testCase, compareWithNbis: false);
    }

    public static async Task<WsqEncoderBlockerSnapshot> CreateAgainstNbisAsync(WsqEncodingReferenceCase testCase)
    {
        return await CreateAsync(testCase, compareWithNbis: true);
    }

    private static async Task<WsqEncoderBlockerSnapshot> CreateAsync(
        WsqEncodingReferenceCase testCase,
        bool compareWithNbis)
    {
        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath);
        var analysis = WsqEncoderAnalysisPipeline.Analyze(rawBytes, testCase.RawImage, new(testCase.BitRate));
        var referenceCoefficients = await ReadReferenceCoefficientsAsync(testCase.ReferencePath);
        var nbisAnalysis = await WsqNbisOracleReader.ReadAnalysisAsync(testCase);
        var referenceQuantizationBins = referenceCoefficients.QuantizationTable.QuantizationBins.ToArray();
        var referenceZeroBins = referenceCoefficients.QuantizationTable.ZeroBins.ToArray();

        WsqWaveletTreeBuilder.Build(
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            out var waveletTree,
            out var quantizationTree);

        var mismatchIndex = FindFirstMismatchIndex(
            analysis.QuantizedCoefficients,
            compareWithNbis
                ? nbisAnalysis.QuantizedCoefficients
                : referenceCoefficients.QuantizedCoefficients);
        if (mismatchIndex < 0)
        {
            throw new InvalidOperationException(
                $"{testCase.FileName} at {testCase.BitRate:0.##} bpp does not currently diverge from the "
                + $"{(compareWithNbis ? "NBIS" : "NIST")} reference.");
        }

        var mismatchLocation = FindCoefficientLocation(
            analysis.QuantizationTable.QuantizationBins,
            quantizationTree,
            mismatchIndex);
        var waveletValueIndex = mismatchLocation.ImageY * testCase.RawImage.Width + mismatchLocation.ImageX;

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
        var rawQuantizationArtifacts = WsqHighPrecisionQuantizer.CreateQuantizationArtifacts(
            decomposedPixels,
            quantizationTree,
            testCase.RawImage.Width,
            testCase.BitRate);

        var nbisWaveletData = await WsqNbisOracleReader.ReadWaveletDataAsync(testCase);
        var floatWithRawProductionBinsQuantizedCoefficients = WsqCoefficientQuantizer.Quantize(
            floatDecomposedPixels,
            quantizationTree,
            testCase.RawImage.Width,
            rawQuantizationArtifacts.QuantizationBins,
            rawQuantizationArtifacts.ZeroBins);
        var productionWithNbisBinsQuantizedCoefficients = WsqCoefficientQuantizer.Quantize(
            decomposedPixels,
            quantizationTree,
            testCase.RawImage.Width,
            nbisAnalysis.QuantizationBins,
            nbisAnalysis.ZeroBins);
        var floatWithNbisBinsQuantizedCoefficients = WsqCoefficientQuantizer.Quantize(
            floatDecomposedPixels,
            quantizationTree,
            testCase.RawImage.Width,
            nbisAnalysis.QuantizationBins,
            nbisAnalysis.ZeroBins);
        var productionWithNbisQuantizationBinsQuantizedCoefficients = WsqCoefficientQuantizer.Quantize(
            decomposedPixels,
            quantizationTree,
            testCase.RawImage.Width,
            nbisAnalysis.QuantizationBins,
            rawQuantizationArtifacts.ZeroBins);
        var productionWithNbisZeroBinsQuantizedCoefficients = WsqCoefficientQuantizer.Quantize(
            decomposedPixels,
            quantizationTree,
            testCase.RawImage.Width,
            rawQuantizationArtifacts.QuantizationBins,
            nbisAnalysis.ZeroBins);
        var productionWithReferenceBinsQuantizedCoefficients = WsqCoefficientQuantizer.Quantize(
            decomposedPixels,
            quantizationTree,
            testCase.RawImage.Width,
            referenceQuantizationBins,
            referenceZeroBins);
        var subbandZeroBiasedQuantizationBins = rawQuantizationArtifacts.QuantizationBins.ToArray();
        if (subbandZeroBiasedQuantizationBins[0].CompareTo(0.0) > 0)
        {
            subbandZeroBiasedQuantizationBins[0] *= s_subbandZeroDiagnosticQuantizationBias;
        }

        var productionWithSubbandZeroBiasQuantizedCoefficients = WsqCoefficientQuantizer.Quantize(
            decomposedPixels,
            quantizationTree,
            testCase.RawImage.Width,
            subbandZeroBiasedQuantizationBins,
            rawQuantizationArtifacts.ZeroBins);
        var productionWithSubbandZeroBiasVariantMismatch = FindVariantMismatch(
            productionWithSubbandZeroBiasQuantizedCoefficients,
            referenceCoefficients.QuantizedCoefficients,
            referenceQuantizationBins,
            quantizationTree);

        return new(
            testCase.FileName,
            testCase.BitRate,
            mismatchIndex,
            mismatchLocation,
            FloatWaveletCoefficient: floatDecomposedPixels[waveletValueIndex],
            ProductionWaveletCoefficient: decomposedPixels[waveletValueIndex],
            NbisWaveletCoefficient: nbisWaveletData[waveletValueIndex],
            RawProductionQuantizationBin: rawQuantizationArtifacts.QuantizationBins[mismatchLocation.SubbandIndex],
            ProductionQuantizationBin: analysis.QuantizationTable.QuantizationBins[mismatchLocation.SubbandIndex],
            NbisQuantizationBin: nbisAnalysis.QuantizationBins[mismatchLocation.SubbandIndex],
            ReferenceQuantizationBin: referenceCoefficients.QuantizationTable.QuantizationBins[mismatchLocation.SubbandIndex],
            RawProductionVariance: rawQuantizationArtifacts.Variances[mismatchLocation.SubbandIndex],
            NbisVariance: nbisAnalysis.Variances[mismatchLocation.SubbandIndex],
            RawProductionHalfZeroBin: rawQuantizationArtifacts.ZeroBins[mismatchLocation.SubbandIndex] / 2.0,
            ProductionHalfZeroBin: analysis.QuantizationTable.ZeroBins[mismatchLocation.SubbandIndex] / 2.0,
            NbisHalfZeroBin: nbisAnalysis.ZeroBins[mismatchLocation.SubbandIndex] / 2.0,
            ReferenceHalfZeroBin: referenceCoefficients.QuantizationTable.ZeroBins[mismatchLocation.SubbandIndex] / 2.0,
            ProductionQuantizedCoefficient: analysis.QuantizedCoefficients[mismatchIndex],
            NbisQuantizedCoefficient: nbisAnalysis.QuantizedCoefficients[mismatchIndex],
            ReferenceQuantizedCoefficient: referenceCoefficients.QuantizedCoefficients[mismatchIndex],
            ProductionPreCastQuantizationValue: ComputePreCastQuantizationValue(
                decomposedPixels[waveletValueIndex],
                rawQuantizationArtifacts.QuantizationBins[mismatchLocation.SubbandIndex],
                rawQuantizationArtifacts.ZeroBins[mismatchLocation.SubbandIndex] / 2.0),
            ProductionFloatPreCastQuantizationValue: ComputeFloatPreCastQuantizationValue(
                decomposedPixels[waveletValueIndex],
                rawQuantizationArtifacts.QuantizationBins[mismatchLocation.SubbandIndex],
                rawQuantizationArtifacts.ZeroBins[mismatchLocation.SubbandIndex] / 2.0),
            NbisPreCastQuantizationValue: ComputePreCastQuantizationValue(
                nbisWaveletData[waveletValueIndex],
                nbisAnalysis.QuantizationBins[mismatchLocation.SubbandIndex],
                nbisAnalysis.ZeroBins[mismatchLocation.SubbandIndex] / 2.0),
            NbisFloatPreCastQuantizationValue: ComputeFloatPreCastQuantizationValue(
                nbisWaveletData[waveletValueIndex],
                nbisAnalysis.QuantizationBins[mismatchLocation.SubbandIndex],
                nbisAnalysis.ZeroBins[mismatchLocation.SubbandIndex] / 2.0),
            FloatWithRawProductionBinsQuantizedCoefficient: ComputeQuantizedCoefficient(
                floatDecomposedPixels[waveletValueIndex],
                rawQuantizationArtifacts.QuantizationBins[mismatchLocation.SubbandIndex],
                rawQuantizationArtifacts.ZeroBins[mismatchLocation.SubbandIndex] / 2.0),
            ProductionWithNbisBinsQuantizedCoefficient: ComputeQuantizedCoefficient(
                decomposedPixels[waveletValueIndex],
                nbisAnalysis.QuantizationBins[mismatchLocation.SubbandIndex],
                nbisAnalysis.ZeroBins[mismatchLocation.SubbandIndex] / 2.0),
            ProductionWithNbisBinsFloatPreCastQuantizationValue: ComputeFloatPreCastQuantizationValue(
                decomposedPixels[waveletValueIndex],
                nbisAnalysis.QuantizationBins[mismatchLocation.SubbandIndex],
                nbisAnalysis.ZeroBins[mismatchLocation.SubbandIndex] / 2.0),
            ThresholdQuantizationBinForNbisBucket: ComputeThresholdQuantizationBinForBucket(
                decomposedPixels[waveletValueIndex],
                rawQuantizationArtifacts.ZeroBins[mismatchLocation.SubbandIndex] / 2.0,
                nbisAnalysis.QuantizedCoefficients[mismatchIndex]),
            ThresholdQuantizationBinForReferenceBucket: ComputeThresholdQuantizationBinForBucket(
                decomposedPixels[waveletValueIndex],
                rawQuantizationArtifacts.ZeroBins[mismatchLocation.SubbandIndex] / 2.0,
                referenceCoefficients.QuantizedCoefficients[mismatchIndex]),
            FloatWithNbisBinsQuantizedCoefficient: ComputeQuantizedCoefficient(
                floatDecomposedPixels[waveletValueIndex],
                nbisAnalysis.QuantizationBins[mismatchLocation.SubbandIndex],
                nbisAnalysis.ZeroBins[mismatchLocation.SubbandIndex] / 2.0),
            ProductionWithNbisQuantizationBinsQuantizedCoefficient: ComputeQuantizedCoefficient(
                decomposedPixels[waveletValueIndex],
                nbisAnalysis.QuantizationBins[mismatchLocation.SubbandIndex],
                rawQuantizationArtifacts.ZeroBins[mismatchLocation.SubbandIndex] / 2.0),
            ProductionWithNbisZeroBinsQuantizedCoefficient: ComputeQuantizedCoefficient(
                decomposedPixels[waveletValueIndex],
                rawQuantizationArtifacts.QuantizationBins[mismatchLocation.SubbandIndex],
                nbisAnalysis.ZeroBins[mismatchLocation.SubbandIndex] / 2.0),
            ProductionWithSubbandZeroBiasQuantizedCoefficient: productionWithSubbandZeroBiasQuantizedCoefficients[mismatchIndex],
            FloatWithRawProductionBinsVariantMismatch: FindVariantMismatch(
                floatWithRawProductionBinsQuantizedCoefficients,
                referenceCoefficients.QuantizedCoefficients,
                referenceQuantizationBins,
                quantizationTree),
            ProductionWithNbisBinsVariantMismatch: FindVariantMismatch(
                productionWithNbisBinsQuantizedCoefficients,
                referenceCoefficients.QuantizedCoefficients,
                referenceQuantizationBins,
                quantizationTree),
            FloatWithNbisBinsVariantMismatch: FindVariantMismatch(
                floatWithNbisBinsQuantizedCoefficients,
                referenceCoefficients.QuantizedCoefficients,
                referenceQuantizationBins,
                quantizationTree),
            ProductionWithNbisQuantizationBinsVariantMismatch: FindVariantMismatch(
                productionWithNbisQuantizationBinsQuantizedCoefficients,
                referenceCoefficients.QuantizedCoefficients,
                referenceQuantizationBins,
                quantizationTree),
            ProductionWithNbisZeroBinsVariantMismatch: FindVariantMismatch(
                productionWithNbisZeroBinsQuantizedCoefficients,
                referenceCoefficients.QuantizedCoefficients,
                referenceQuantizationBins,
                quantizationTree),
            ProductionWithSubbandZeroBiasVariantMismatch: productionWithSubbandZeroBiasVariantMismatch,
            ProductionWithSubbandZeroBiasFollowOnSnapshot: CreateVariantPointSnapshot(
                productionWithSubbandZeroBiasVariantMismatch,
                productionWithSubbandZeroBiasQuantizedCoefficients,
                subbandZeroBiasedQuantizationBins,
                rawQuantizationArtifacts.ZeroBins,
                decomposedPixels,
                floatDecomposedPixels,
                nbisWaveletData,
                nbisAnalysis,
                referenceCoefficients,
                testCase.RawImage.Width),
            ProductionWithReferenceBinsVariantMismatch: FindVariantMismatch(
                productionWithReferenceBinsQuantizedCoefficients,
                referenceCoefficients.QuantizedCoefficients,
                referenceQuantizationBins,
                quantizationTree));
    }

    private static async Task<WsqReferenceQuantizedCoefficients> ReadReferenceCoefficientsAsync(string referencePath)
    {
        await using var referenceStream = File.OpenRead(referencePath);
        var container = await WsqContainerReader.ReadAsync(referenceStream);
        WsqWaveletTreeBuilder.Build(
            container.FrameHeader.Width,
            container.FrameHeader.Height,
            out var waveletTree,
            out var quantizationTree);

        var quantizedCoefficients = WsqHuffmanDecoder.DecodeQuantizedCoefficients(container, waveletTree, quantizationTree);
        return new(container.QuantizationTable, quantizedCoefficients);
    }

    private static int FindFirstMismatchIndex(ReadOnlySpan<short> actualValues, ReadOnlySpan<short> expectedValues)
    {
        for (var index = 0; index < actualValues.Length; index++)
        {
            if (actualValues[index] == expectedValues[index])
            {
                continue;
            }

            return index;
        }

        return -1;
    }

    private static double ComputePreCastQuantizationValue(
        double coefficient,
        double quantizationBin,
        double halfZeroBin)
    {
        if (-halfZeroBin <= coefficient && coefficient <= halfZeroBin)
        {
            return 0.0;
        }

        if (coefficient > 0.0)
        {
            return (coefficient - halfZeroBin) / quantizationBin + 1.0;
        }

        return (coefficient + halfZeroBin) / quantizationBin - 1.0;
    }

    private static short ComputeQuantizedCoefficient(
        double coefficient,
        double quantizationBin,
        double halfZeroBin)
    {
        var preCastValue = ComputeFloatPreCastQuantizationValue(coefficient, quantizationBin, halfZeroBin);
        return checked((short)preCastValue);
    }

    private static float ComputeFloatPreCastQuantizationValue(
        double coefficient,
        double quantizationBin,
        double halfZeroBin)
    {
        var floatCoefficient = (float)coefficient;
        var floatQuantizationBin = (float)quantizationBin;
        var floatHalfZeroBin = (float)halfZeroBin;

        if (-floatHalfZeroBin <= floatCoefficient && floatCoefficient <= floatHalfZeroBin)
        {
            return 0.0f;
        }

        if (floatCoefficient > 0.0f)
        {
            return (floatCoefficient - floatHalfZeroBin) / floatQuantizationBin + 1.0f;
        }

        return (floatCoefficient + floatHalfZeroBin) / floatQuantizationBin - 1.0f;
    }

    private static double ComputeThresholdQuantizationBinForBucket(
        double coefficient,
        double halfZeroBin,
        short targetQuantizedCoefficient)
    {
        if (targetQuantizedCoefficient == 0)
        {
            return double.PositiveInfinity;
        }

        if (targetQuantizedCoefficient > 0)
        {
            return (coefficient - halfZeroBin) / (targetQuantizedCoefficient - 1.0);
        }

        return (coefficient + halfZeroBin) / (targetQuantizedCoefficient + 1.0);
    }

    private static WsqVariantMismatchSnapshot FindVariantMismatch(
        ReadOnlySpan<short> actualValues,
        ReadOnlySpan<short> expectedValues,
        IReadOnlyList<double> referenceQuantizationBins,
        ReadOnlySpan<WsqQuantizationNode> quantizationTree)
    {
        var mismatchIndex = FindFirstMismatchIndex(actualValues, expectedValues);
        WsqCoefficientLocation? mismatchLocation = mismatchIndex >= 0
            ? FindCoefficientLocation(referenceQuantizationBins, quantizationTree, mismatchIndex)
            : null;

        return new(mismatchIndex, mismatchLocation);
    }

    private static WsqVariantQuantizationPointSnapshot? CreateVariantPointSnapshot(
        WsqVariantMismatchSnapshot mismatchSnapshot,
        ReadOnlySpan<short> variantQuantizedCoefficients,
        double[] variantQuantizationBins,
        double[] variantZeroBins,
        ReadOnlySpan<double> productionWaveletData,
        ReadOnlySpan<float> floatWaveletData,
        ReadOnlySpan<float> nbisWaveletData,
        WsqNbisAnalysisDump nbisAnalysis,
        WsqReferenceQuantizedCoefficients referenceCoefficients,
        int width)
    {
        if (mismatchSnapshot.MismatchIndex < 0 || mismatchSnapshot.MismatchLocation is null)
        {
            return null;
        }

        var location = mismatchSnapshot.MismatchLocation.Value;
        var waveletValueIndex = location.ImageY * width + location.ImageX;
        var halfZeroBin = variantZeroBins[location.SubbandIndex] / 2.0;
        var nbisHalfZeroBin = nbisAnalysis.ZeroBins[location.SubbandIndex] / 2.0;
        var referenceHalfZeroBin = referenceCoefficients.QuantizationTable.ZeroBins[location.SubbandIndex] / 2.0;

        return new(
            mismatchSnapshot.MismatchIndex,
            location,
            FloatWaveletCoefficient: floatWaveletData[waveletValueIndex],
            ProductionWaveletCoefficient: productionWaveletData[waveletValueIndex],
            NbisWaveletCoefficient: nbisWaveletData[waveletValueIndex],
            VariantQuantizationBin: variantQuantizationBins[location.SubbandIndex],
            VariantHalfZeroBin: halfZeroBin,
            NbisQuantizationBin: nbisAnalysis.QuantizationBins[location.SubbandIndex],
            NbisHalfZeroBin: nbisHalfZeroBin,
            ReferenceQuantizationBin: referenceCoefficients.QuantizationTable.QuantizationBins[location.SubbandIndex],
            ReferenceHalfZeroBin: referenceHalfZeroBin,
            VariantQuantizedCoefficient: variantQuantizedCoefficients[mismatchSnapshot.MismatchIndex],
            NbisQuantizedCoefficient: nbisAnalysis.QuantizedCoefficients[mismatchSnapshot.MismatchIndex],
            ReferenceQuantizedCoefficient: referenceCoefficients.QuantizedCoefficients[mismatchSnapshot.MismatchIndex],
            ThresholdQuantizationBinForNbisBucket: ComputeThresholdQuantizationBinForBucket(
                productionWaveletData[waveletValueIndex],
                halfZeroBin,
                nbisAnalysis.QuantizedCoefficients[mismatchSnapshot.MismatchIndex]),
            ThresholdQuantizationBinForReferenceBucket: ComputeThresholdQuantizationBinForBucket(
                productionWaveletData[waveletValueIndex],
                halfZeroBin,
                referenceCoefficients.QuantizedCoefficients[mismatchSnapshot.MismatchIndex]),
            VariantFloatPreCastQuantizationValue: ComputeFloatPreCastQuantizationValue(
                productionWaveletData[waveletValueIndex],
                variantQuantizationBins[location.SubbandIndex],
                halfZeroBin),
            NbisFloatPreCastQuantizationValue: ComputeFloatPreCastQuantizationValue(
                nbisWaveletData[waveletValueIndex],
                nbisAnalysis.QuantizationBins[location.SubbandIndex],
                nbisHalfZeroBin));
    }

    private static WsqCoefficientLocation FindCoefficientLocation(
        IReadOnlyList<double> quantizationBins,
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
        int coefficientIndex)
    {
        var remainingCoefficientIndex = coefficientIndex;

        for (var subbandIndex = 0; subbandIndex < quantizationTree.Length; subbandIndex++)
        {
            if (quantizationBins[subbandIndex].CompareTo(0.0) == 0)
            {
                continue;
            }

            var node = quantizationTree[subbandIndex];
            var subbandCoefficientCount = node.Width * node.Height;

            if (remainingCoefficientIndex >= subbandCoefficientCount)
            {
                remainingCoefficientIndex -= subbandCoefficientCount;
                continue;
            }

            var row = remainingCoefficientIndex / node.Width;
            var column = remainingCoefficientIndex % node.Width;
            return new(
                subbandIndex,
                row,
                column,
                node.X + column,
                node.Y + row);
        }

        throw new InvalidOperationException($"Unable to map quantized coefficient index {coefficientIndex} to an active WSQ subband.");
    }

    private readonly record struct WsqReferenceQuantizedCoefficients(
        WsqQuantizationTable QuantizationTable,
        short[] QuantizedCoefficients);
}

internal readonly record struct WsqEncoderBlockerSnapshot(
    string FileName,
    double BitRate,
    int MismatchIndex,
    WsqCoefficientLocation MismatchLocation,
    float FloatWaveletCoefficient,
    double ProductionWaveletCoefficient,
    double NbisWaveletCoefficient,
    double RawProductionQuantizationBin,
    double ProductionQuantizationBin,
    double NbisQuantizationBin,
    double ReferenceQuantizationBin,
    double RawProductionVariance,
    double NbisVariance,
    double RawProductionHalfZeroBin,
    double ProductionHalfZeroBin,
    double NbisHalfZeroBin,
    double ReferenceHalfZeroBin,
    short ProductionQuantizedCoefficient,
    short NbisQuantizedCoefficient,
    short ReferenceQuantizedCoefficient,
    double ProductionPreCastQuantizationValue,
    float ProductionFloatPreCastQuantizationValue,
    double NbisPreCastQuantizationValue,
    float NbisFloatPreCastQuantizationValue,
    short FloatWithRawProductionBinsQuantizedCoefficient,
    short ProductionWithNbisBinsQuantizedCoefficient,
    float ProductionWithNbisBinsFloatPreCastQuantizationValue,
    double ThresholdQuantizationBinForNbisBucket,
    double ThresholdQuantizationBinForReferenceBucket,
    short FloatWithNbisBinsQuantizedCoefficient,
    short ProductionWithNbisQuantizationBinsQuantizedCoefficient,
    short ProductionWithNbisZeroBinsQuantizedCoefficient,
    short ProductionWithSubbandZeroBiasQuantizedCoefficient,
    WsqVariantMismatchSnapshot FloatWithRawProductionBinsVariantMismatch,
    WsqVariantMismatchSnapshot ProductionWithNbisBinsVariantMismatch,
    WsqVariantMismatchSnapshot FloatWithNbisBinsVariantMismatch,
    WsqVariantMismatchSnapshot ProductionWithNbisQuantizationBinsVariantMismatch,
    WsqVariantMismatchSnapshot ProductionWithNbisZeroBinsVariantMismatch,
    WsqVariantMismatchSnapshot ProductionWithSubbandZeroBiasVariantMismatch,
    WsqVariantQuantizationPointSnapshot? ProductionWithSubbandZeroBiasFollowOnSnapshot,
    WsqVariantMismatchSnapshot ProductionWithReferenceBinsVariantMismatch);

internal readonly record struct WsqCoefficientLocation(
    int SubbandIndex,
    int Row,
    int Column,
    int ImageX,
    int ImageY);

internal readonly record struct WsqVariantMismatchSnapshot(
    int MismatchIndex,
    WsqCoefficientLocation? MismatchLocation);

internal readonly record struct WsqVariantQuantizationPointSnapshot(
    int MismatchIndex,
    WsqCoefficientLocation MismatchLocation,
    float FloatWaveletCoefficient,
    double ProductionWaveletCoefficient,
    double NbisWaveletCoefficient,
    double VariantQuantizationBin,
    double VariantHalfZeroBin,
    double NbisQuantizationBin,
    double NbisHalfZeroBin,
    double ReferenceQuantizationBin,
    double ReferenceHalfZeroBin,
    short VariantQuantizedCoefficient,
    short NbisQuantizedCoefficient,
    short ReferenceQuantizedCoefficient,
    double ThresholdQuantizationBinForNbisBucket,
    double ThresholdQuantizationBinForReferenceBucket,
    float VariantFloatPreCastQuantizationValue,
    float NbisFloatPreCastQuantizationValue);
