using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using OpenNist.Wsq;
using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Encoding;

const string RepoRoot = "/Users/pmtar/Development/Projects/OpenNist";

var fileName = args.Length > 0 ? args[0] : "cmp00001.raw";
var bitRate = args.Length > 1 ? double.Parse(args[1], CultureInfo.InvariantCulture) : 2.25;
var dimensions = LoadDimensions().Single(dimensions => string.Equals(dimensions.FileName, fileName, StringComparison.Ordinal));
var rateDirectory = bitRate == 0.75 ? "BitRate075" : "BitRate225";
var rawPath = Path.Combine(
    RepoRoot,
    "OpenNist.Tests",
    "TestData",
    "Wsq",
    "NistReferenceImages",
    "V2_0",
    "Encode",
    "Raw",
    fileName);
var referencePath = Path.Combine(
    RepoRoot,
    "OpenNist.Tests",
    "TestData",
    "Wsq",
    "NistReferenceImages",
    "V2_0",
    "ReferenceWsq",
    rateDirectory,
    Path.ChangeExtension(fileName, ".wsq"));

var rawBytes = await File.ReadAllBytesAsync(rawPath);
var rawImage = new WsqRawImageDescription(dimensions.Width, dimensions.Height, 8, 500);
var analysis = WsqEncoderAnalysisPipeline.Analyze(rawBytes, rawImage, new WsqEncodeOptions(bitRate));
var reference = await ReadReferenceCoefficientsAsync(referencePath);
var nbisAnalysis = await ReadNbisAnalysisAsync(rawPath, dimensions.Width, dimensions.Height, bitRate);
WsqWaveletTreeBuilder.Build(rawImage.Width, rawImage.Height, out var waveletTree, out var quantizationTree);
var highPrecisionArtifacts = CreateHighPrecisionAnalysisArtifacts(
    rawBytes,
    rawImage,
    WsqReferenceTables.CreateStandardTransformTable(),
    waveletTree);
var highPrecisionQuantizationArtifacts = WsqHighPrecisionQuantizer.CreateQuantizationArtifacts(
    highPrecisionArtifacts.DoubleDecomposedPixels,
    quantizationTree,
    rawImage.Width,
    bitRate);
var subband0FullVarianceArtifacts = CreateSubband0FullVarianceQuantizationArtifacts(
    highPrecisionArtifacts.DoubleDecomposedPixels,
    quantizationTree,
    rawImage.Width,
    bitRate);
var normalizedImage = WsqFloatImageNormalizer.Normalize(rawBytes);
var normalizedPixels = normalizedImage.Pixels.ToArray();
var nbisNormalizedPixels = await ReadNbisWaveletDataAsync(rawPath, rawImage.Width, rawImage.Height, -1).ConfigureAwait(false);
var helperWaveletData = DecomposeWithHelper(
    normalizedImage.Pixels,
    rawImage.Width,
    rawImage.Height,
    waveletTree,
    WsqReferenceTables.StandardTransformTable);
var decomposedWaveletData = WsqDecomposition.Decompose(
    normalizedPixels,
    rawImage.Width,
    rawImage.Height,
    waveletTree,
    WsqReferenceTables.StandardTransformTable);
var nbisWaveletData = await ReadNbisWaveletDataAsync(rawPath, dimensions.Width, dimensions.Height);
var managedVariances = ComputeVariances(decomposedWaveletData, quantizationTree, rawImage.Width);
var referencePrecisionQuantizedCoefficients = QuantizeUsingReferencePrecision(
    decomposedWaveletData,
    quantizationTree,
    rawImage.Width,
    bitRate);
var referenceTableQuantizedCoefficients = QuantizeUsingProvidedQuantizationTable(
    decomposedWaveletData,
    quantizationTree,
    rawImage.Width,
    reference.QuantizationTable);
var referenceQbinQuantizedCoefficients = QuantizeUsingProvidedBins(
    decomposedWaveletData,
    quantizationTree,
    rawImage.Width,
    reference.QuantizationTable.QuantizationBins,
    analysis.QuantizationTable.ZeroBins);
var referenceZbinQuantizedCoefficients = QuantizeUsingProvidedBins(
    decomposedWaveletData,
    quantizationTree,
    rawImage.Width,
    analysis.QuantizationTable.QuantizationBins,
    reference.QuantizationTable.ZeroBins);
var roundTrippedManagedTable = RoundTripQuantizationTable(analysis.QuantizationTable);
var roundTrippedManagedTableQuantizedCoefficients = QuantizeUsingProvidedQuantizationTable(
    decomposedWaveletData,
    quantizationTree,
    rawImage.Width,
    roundTrippedManagedTable);
var doubleRoundTrippedManagedTableQuantizedCoefficients = QuantizeUsingProvidedQuantizationTableForDoubleWavelet(
    highPrecisionArtifacts.DoubleDecomposedPixels,
    quantizationTree,
    rawImage.Width,
    roundTrippedManagedTable);
var managedVarianceReferencePrecisionQuantizedCoefficients = QuantizeUsingManagedVariancesWithReferencePrecisionBins(
    decomposedWaveletData,
    quantizationTree,
    rawImage.Width,
    managedVariances,
    bitRate);
var doublePrecisionQuantizedCoefficients = QuantizeUsingDoublePrecisionEncoder(
    rawBytes,
    rawImage.Width,
    rawImage.Height,
    waveletTree,
    quantizationTree,
    WsqReferenceTables.StandardTransformTable,
    bitRate);
var referenceHeaderNormalizedQuantizedCoefficients = QuantizeUsingDoublePrecisionEncoderWithFixedNormalization(
    rawBytes,
    rawImage.Width,
    rawImage.Height,
    waveletTree,
    quantizationTree,
    WsqReferenceTables.StandardTransformTable,
    bitRate,
    reference.FrameHeader.Shift,
    reference.FrameHeader.Scale);
var floatNormalizedDoublePrecisionQuantizedCoefficients = QuantizeUsingFloatNormalizedDoublePrecisionEncoder(
    normalizedImage.Pixels,
    rawImage.Width,
    rawImage.Height,
    waveletTree,
    quantizationTree,
    WsqReferenceTables.StandardTransformTable,
    bitRate);
var hybridSubband0QuantizedCoefficients = QuantizeUsingHybridSubband0HighPrecisionEncoder(
    highPrecisionArtifacts,
    waveletTree,
    quantizationTree,
    rawImage.Width,
    rawImage.Height,
    bitRate);
var hybridSubband0ReferenceTableQuantizedCoefficients = QuantizeUsingHybridSubband0WithProvidedTable(
    highPrecisionArtifacts,
    quantizationTree,
    rawImage.Width,
    reference.QuantizationTable);
var floatCoefficientQuantizedCoefficients = QuantizeUsingFloatCoefficientQuantizer(
    highPrecisionArtifacts.FloatDecomposedPixels,
    quantizationTree,
    rawImage.Width,
    analysis.QuantizationTable);
var floatVarianceDoubleCoefficientQuantizedCoefficients = QuantizeUsingFloatVarianceWithDoubleCoefficients(
    highPrecisionArtifacts.FloatDecomposedPixels,
    highPrecisionArtifacts.DoubleDecomposedPixels,
    quantizationTree,
    rawImage.Width,
    bitRate);
var fixedHeaderRoundedTableQuantizedCoefficients = QuantizeUsingFixedNormalizationWithRoundedTable(
    rawBytes,
    rawImage.Width,
    rawImage.Height,
    waveletTree,
    quantizationTree,
    WsqReferenceTables.StandardTransformTable,
    bitRate,
    reference.FrameHeader.Shift,
    reference.FrameHeader.Scale);
var roundedCurrentHeaderQuantizedCoefficients = QuantizeUsingRoundedCurrentHeader(
    rawBytes,
    rawImage.Width,
    rawImage.Height,
    waveletTree,
    quantizationTree,
    WsqReferenceTables.StandardTransformTable,
    bitRate);
var fixedHeaderReferenceTableQuantizedCoefficients = QuantizeUsingFixedNormalizationWithProvidedTable(
    rawBytes,
    rawImage.Width,
    rawImage.Height,
    waveletTree,
    quantizationTree,
    WsqReferenceTables.StandardTransformTable,
    reference.QuantizationTable,
    reference.FrameHeader.Shift,
    reference.FrameHeader.Scale);
var floatCastCurrentBinsDoubleCoefficientQuantizedCoefficients = QuantizeUsingFloatCastBinsWithDoubleCoefficients(
    highPrecisionArtifacts.DoubleDecomposedPixels,
    quantizationTree,
    rawImage.Width,
    bitRate);
var floatArithmeticDoubleCoefficientQuantizedCoefficients = QuantizeUsingFloatArithmeticWithDoubleCoefficients(
    highPrecisionArtifacts.DoubleDecomposedPixels,
    quantizationTree,
    rawImage.Width,
    highPrecisionQuantizationArtifacts.QuantizationBins,
    highPrecisionQuantizationArtifacts.ZeroBins);
var floatScaleHighPrecisionQuantizedCoefficients = QuantizeUsingFloatScaleHighPrecisionQuantizer(
    highPrecisionArtifacts.DoubleDecomposedPixels,
    waveletTree,
    quantizationTree,
    rawImage.Width,
    rawImage.Height,
    bitRate);
var fullVarianceHighPrecisionQuantizedCoefficients = QuantizeUsingFullVarianceHighPrecisionQuantizer(
    highPrecisionArtifacts.DoubleDecomposedPixels,
    waveletTree,
    quantizationTree,
    rawImage.Width,
    rawImage.Height,
    bitRate);
var subband0FullVarianceHighPrecisionQuantizedCoefficients = QuantizeUsingSubband0FullVarianceHighPrecisionQuantizer(
    highPrecisionArtifacts.DoubleDecomposedPixels,
    waveletTree,
    quantizationTree,
    rawImage.Width,
    rawImage.Height,
    bitRate);
var subband0LowerVarianceHighPrecisionQuantizedCoefficients = QuantizeUsingSubband0LowerVarianceHighPrecisionQuantizer(
    highPrecisionArtifacts.DoubleDecomposedPixels,
    waveletTree,
    quantizationTree,
    rawImage.Width,
    rawImage.Height,
    bitRate);
var subband0LowerVarianceOverrideQuantizedCoefficients = QuantizeUsingSubband0LowerVarianceOverride(
    highPrecisionArtifacts.DoubleDecomposedPixels,
    quantizationTree,
    rawImage.Width,
    highPrecisionQuantizationArtifacts,
    subband0FullVarianceArtifacts);
var fixedShiftReferenceTableQuantizedCoefficients = QuantizeUsingFixedNormalizationWithProvidedTable(
    rawBytes,
    rawImage.Width,
    rawImage.Height,
    waveletTree,
    quantizationTree,
    WsqReferenceTables.StandardTransformTable,
    reference.QuantizationTable,
    reference.FrameHeader.Shift,
    highPrecisionArtifacts.DoubleNormalizedImage.Scale);
var fixedShiftCurrentTableQuantizedCoefficients = QuantizeUsingFixedShiftCurrentTable(
    rawBytes,
    rawImage.Width,
    rawImage.Height,
    waveletTree,
    quantizationTree,
    WsqReferenceTables.StandardTransformTable,
    bitRate,
    reference.FrameHeader.Shift,
    highPrecisionArtifacts.DoubleNormalizedImage.Scale);
var fixedShiftCurrentNormalizedImage = NormalizeWithFixedParameters(
    rawBytes,
    reference.FrameHeader.Shift,
    highPrecisionArtifacts.DoubleNormalizedImage.Scale);
var fixedShiftCurrentWaveletData = DecomposeWithReferencePrecision(
    fixedShiftCurrentNormalizedImage.Pixels,
    rawImage.Width,
    rawImage.Height,
    waveletTree,
    WsqReferenceTables.StandardTransformTable);
var fixedScaleReferenceTableQuantizedCoefficients = QuantizeUsingFixedNormalizationWithProvidedTable(
    rawBytes,
    rawImage.Width,
    rawImage.Height,
    waveletTree,
    quantizationTree,
    WsqReferenceTables.StandardTransformTable,
    reference.QuantizationTable,
    highPrecisionArtifacts.DoubleNormalizedImage.Shift,
    reference.FrameHeader.Scale);
var subband0BlendHalfArtifacts = CreateSubband0VarianceBlendQuantizationArtifacts(
    highPrecisionArtifacts.DoubleDecomposedPixels,
    quantizationTree,
    rawImage.Width,
    bitRate,
    0.5);
var subband0BlendHalfQuantizedCoefficients = WsqCoefficientQuantizer.Quantize(
    highPrecisionArtifacts.DoubleDecomposedPixels,
    quantizationTree,
    rawImage.Width,
    subband0BlendHalfArtifacts.QuantizationBins,
    subband0BlendHalfArtifacts.ZeroBins);

Console.WriteLine($"{fileName} @ {bitRate:0.##} bpp");
Console.WriteLine($"managed vs reference coeff mismatch : {FindFirstMismatchIndex(analysis.QuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"reference-precision coeff mismatch  : {FindFirstMismatchIndex(referencePrecisionQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"reference-table coeff mismatch      : {FindFirstMismatchIndex(referenceTableQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"reference-qbin coeff mismatch       : {FindFirstMismatchIndex(referenceQbinQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"reference-zbin coeff mismatch       : {FindFirstMismatchIndex(referenceZbinQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"roundtrip-table coeff mismatch      : {FindFirstMismatchIndex(roundTrippedManagedTableQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"double-roundtrip coeff mismatch     : {FindFirstMismatchIndex(doubleRoundTrippedManagedTableQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"managed-var ref-bin coeff mismatch  : {FindFirstMismatchIndex(managedVarianceReferencePrecisionQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"double-precision coeff mismatch     : {FindFirstMismatchIndex(doublePrecisionQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"reference-header coeff mismatch     : {FindFirstMismatchIndex(referenceHeaderNormalizedQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"float-norm double coeff mismatch    : {FindFirstMismatchIndex(floatNormalizedDoublePrecisionQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"hybrid-subband0 coeff mismatch      : {FindFirstMismatchIndex(hybridSubband0QuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"hybrid-subband0 table mismatch      : {FindFirstMismatchIndex(hybridSubband0ReferenceTableQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"float-quantizer coeff mismatch      : {FindFirstMismatchIndex(floatCoefficientQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"float-var double coeff mismatch     : {FindFirstMismatchIndex(floatVarianceDoubleCoefficientQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"rounded-current coeff mismatch      : {FindFirstMismatchIndex(roundedCurrentHeaderQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"fixed-header rounded coeff mismatch : {FindFirstMismatchIndex(fixedHeaderRoundedTableQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"fixed-header table coeff mismatch   : {FindFirstMismatchIndex(fixedHeaderReferenceTableQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"float-bin double coeff mismatch     : {FindFirstMismatchIndex(floatCastCurrentBinsDoubleCoefficientQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"float-arith double coeff mismatch   : {FindFirstMismatchIndex(floatArithmeticDoubleCoefficientQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"float-scale coeff mismatch          : {FindFirstMismatchIndex(floatScaleHighPrecisionQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"full-variance coeff mismatch        : {FindFirstMismatchIndex(fullVarianceHighPrecisionQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"subband0-fullvar coeff mismatch     : {FindFirstMismatchIndex(subband0FullVarianceHighPrecisionQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"subband0-lowervar coeff mismatch    : {FindFirstMismatchIndex(subband0LowerVarianceHighPrecisionQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"subband0-override coeff mismatch    : {FindFirstMismatchIndex(subband0LowerVarianceOverrideQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"fixed-shift table coeff mismatch    : {FindFirstMismatchIndex(fixedShiftReferenceTableQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"fixed-shift current coeff mismatch  : {FindFirstMismatchIndex(fixedShiftCurrentTableQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"fixed-scale table coeff mismatch    : {FindFirstMismatchIndex(fixedScaleReferenceTableQuantizedCoefficients, reference.QuantizedCoefficients)}");

if (fileName is "cmp00003.raw" or "cmp00005.raw")
{
    foreach (var blendFactor in new[] { 0.25, 0.5, 0.75 })
    {
        var blendedSubband0QuantizedCoefficients = QuantizeUsingSubband0VarianceBlend(
            highPrecisionArtifacts.DoubleDecomposedPixels,
            waveletTree,
            quantizationTree,
            rawImage.Width,
            rawImage.Height,
            bitRate,
            blendFactor);
        Console.WriteLine(
            $"subband0-blend {blendFactor:G2} coeff mismatch : {FindFirstMismatchIndex(blendedSubband0QuantizedCoefficients, reference.QuantizedCoefficients)}");
    }

    Console.WriteLine(
        $"subband0-blend 0.5 detail        : {DescribeCoefficientMismatch(subband0BlendHalfArtifacts.QuantizationBins, quantizationTree, subband0BlendHalfQuantizedCoefficients, reference.QuantizedCoefficients)}");
    Console.WriteLine(
        $"subband0-blend 0.5 source        : {DescribeDoubleCoefficientSource(subband0BlendHalfArtifacts.QuantizationBins, subband0BlendHalfArtifacts.ZeroBins, quantizationTree, highPrecisionArtifacts.DoubleDecomposedPixels, rawImage.Width, subband0BlendHalfQuantizedCoefficients, reference.QuantizedCoefficients)}");
    Console.WriteLine(
        $"subband0-blend 0.5 nbis source   : {DescribeFloatCoefficientSourceAtIndex(subband0BlendHalfArtifacts.QuantizationBins, subband0BlendHalfArtifacts.ZeroBins, quantizationTree, nbisWaveletData, rawImage.Width, FindFirstMismatchIndex(subband0BlendHalfQuantizedCoefficients, reference.QuantizedCoefficients))}");
    Console.WriteLine(
        $"subband0-blend 0.5 thresholds    : {DescribeThresholdMetrics(subband0BlendHalfArtifacts, subband0BlendHalfQuantizedCoefficients, reference)}");

    foreach (var qbinDelta in new[] { 0.00002, 0.00003, 0.00004 })
    {
        var nudgedQuantizedCoefficients = QuantizeUsingSubband0QbinNudge(
            highPrecisionArtifacts.DoubleDecomposedPixels,
            quantizationTree,
            rawImage.Width,
            subband0BlendHalfArtifacts,
            qbinDelta);
        Console.WriteLine(
            $"subband0-qbin+{qbinDelta:G5} mismatch : {FindFirstMismatchIndex(nudgedQuantizedCoefficients, reference.QuantizedCoefficients)}");
    }

    foreach (var halfZeroBinDelta in new[] { 0.00002, 0.00003, 0.00004 })
    {
        var nudgedQuantizedCoefficients = QuantizeUsingSubband0HalfZeroBinNudge(
            highPrecisionArtifacts.DoubleDecomposedPixels,
            quantizationTree,
            rawImage.Width,
            subband0BlendHalfArtifacts,
            halfZeroBinDelta);
        Console.WriteLine(
            $"subband0-halfz+{halfZeroBinDelta:G5} mismatch : {FindFirstMismatchIndex(nudgedQuantizedCoefficients, reference.QuantizedCoefficients)}");
    }

    var referenceSubband0QuantizedCoefficients = QuantizeUsingSubband0ReferenceBins(
        highPrecisionArtifacts.DoubleDecomposedPixels,
        quantizationTree,
        rawImage.Width,
        subband0BlendHalfArtifacts,
        reference.QuantizationTable.QuantizationBins[0],
        reference.QuantizationTable.ZeroBins[0]);
    Console.WriteLine(
        $"subband0-refbin mismatch        : {FindFirstMismatchIndex(referenceSubband0QuantizedCoefficients, reference.QuantizedCoefficients)}");
    foreach (var quantizationFactor in new[] { 1.00002, 1.00005, 1.0001 })
    {
        var globallyScaledQuantizedCoefficients = QuantizeUsingGlobalQuantizationFactor(
            highPrecisionArtifacts.DoubleDecomposedPixels,
            quantizationTree,
            rawImage.Width,
            subband0BlendHalfArtifacts,
            quantizationFactor);
        Console.WriteLine(
            $"global-q*{quantizationFactor:G6} mismatch : {FindFirstMismatchIndex(globallyScaledQuantizedCoefficients, reference.QuantizedCoefficients)}");
    }
    Console.WriteLine($"base scale trace                : {TraceHighPrecisionQuantizationScale(highPrecisionQuantizationArtifacts.Variances, bitRate)}");
    Console.WriteLine($"blend0.5 scale trace            : {TraceHighPrecisionQuantizationScale(subband0BlendHalfArtifacts.Variances, bitRate)}");
    Console.WriteLine($"blend0.5 ref trace              : {TraceReferencePrecisionQuantizationScale(subband0BlendHalfArtifacts.Variances, bitRate)}");
}
Console.WriteLine($"managed vs reference shift diff     : actual={analysis.FrameHeader.Shift:G17}, expected={reference.FrameHeader.Shift:G17}");
Console.WriteLine($"managed vs reference scale diff     : actual={analysis.FrameHeader.Scale:G17}, expected={reference.FrameHeader.Scale:G17}");
Console.WriteLine($"managed vs reference qbin diff      : {FindFirstBinDifference(analysis.QuantizationTable.QuantizationBins, reference.QuantizationTable.QuantizationBins)}");
Console.WriteLine($"raw vs reference qbin diff          : {FindFirstBinDifference(highPrecisionQuantizationArtifacts.QuantizationBins, reference.QuantizationTable.QuantizationBins)}");
Console.WriteLine($"first-region variance trace         : {DescribeVarianceTrace(highPrecisionQuantizationArtifacts.Variances)}");
Console.WriteLine($"subband0 cropped variance           : {highPrecisionQuantizationArtifacts.Variances[0]:G17}");
Console.WriteLine($"subband0 full variance              : {subband0FullVarianceArtifacts.Variances[0]:G17}");
Console.WriteLine($"subband0 cropped qbin               : {highPrecisionQuantizationArtifacts.QuantizationBins[0]:G17}");
Console.WriteLine($"subband0 full qbin                  : {subband0FullVarianceArtifacts.QuantizationBins[0]:G17}");
Console.WriteLine($"subband0 reference qbin             : {reference.QuantizationTable.QuantizationBins[0]:G17}");
Console.WriteLine($"override threshold metrics          : {DescribeThresholdMetrics(highPrecisionQuantizationArtifacts, subband0LowerVarianceOverrideQuantizedCoefficients, reference)}");
Console.WriteLine($"managed vs NBIS shift diff          : actual={analysis.FrameHeader.Shift:G17}, expected={nbisAnalysis.Shift:G17}");
Console.WriteLine($"managed vs NBIS scale diff          : actual={analysis.FrameHeader.Scale:G17}, expected={nbisAnalysis.Scale:G17}");
Console.WriteLine($"managed vs NBIS normalized diff     : {FindFirstWaveletDifference(normalizedImage.Pixels, nbisNormalizedPixels, rawImage.Width)}");
Console.WriteLine($"managed vs NBIS coeff mismatch      : {FindFirstMismatchIndex(analysis.QuantizedCoefficients, nbisAnalysis.QuantizedCoefficients)}");
Console.WriteLine($"managed vs NBIS qbin diff           : {FindFirstFloatBinDifference(analysis.QuantizationTable.QuantizationBins, nbisAnalysis.QuantizationBins)}");
Console.WriteLine($"raw vs NBIS qbin diff               : {FindFirstFloatBinDifference(highPrecisionQuantizationArtifacts.QuantizationBins, nbisAnalysis.QuantizationBins)}");
Console.WriteLine($"managed vs NBIS zbin diff           : {FindFirstFloatBinDifference(analysis.QuantizationTable.ZeroBins, nbisAnalysis.ZeroBins)}");
Console.WriteLine($"raw vs NBIS zbin diff               : {FindFirstFloatBinDifference(highPrecisionQuantizationArtifacts.ZeroBins, nbisAnalysis.ZeroBins)}");
Console.WriteLine($"managed vs NBIS variance diff       : {FindFirstFloatBinDifference(managedVariances, nbisAnalysis.Variances)}");
Console.WriteLine($"managed vs NBIS wavelet diff        : {FindFirstWaveletDifference(decomposedWaveletData, nbisWaveletData, rawImage.Width)}");
Console.WriteLine($"managed vs helper wavelet diff      : {FindFirstWaveletDifference(decomposedWaveletData, helperWaveletData, rawImage.Width)}");
Console.WriteLine($"managed vs fixed-shift norm diff    : {FindFirstDoubleWaveletDifference(highPrecisionArtifacts.DoubleNormalizedImage.Pixels, fixedShiftCurrentNormalizedImage.Pixels, rawImage.Width)}");
Console.WriteLine($"managed vs fixed-shift wavelet diff : {FindFirstDoubleWaveletDifference(highPrecisionArtifacts.DoubleDecomposedPixels, fixedShiftCurrentWaveletData, rawImage.Width)}");
Console.WriteLine($"managed vs NBIS first step diff     : {await FindFirstStepDifferenceAsync(rawPath, rawImage.Width, rawImage.Height, normalizedImage.Pixels, waveletTree).ConfigureAwait(false)}");
Console.WriteLine($"managed vs NBIS mismatch detail     : {DescribeCoefficientMismatch(analysis.QuantizationTable.QuantizationBins, quantizationTree, analysis.QuantizedCoefficients, nbisAnalysis.QuantizedCoefficients)}");
Console.WriteLine($"managed vs NBIS mismatch source     : {DescribeCoefficientSource(analysis.QuantizationTable.QuantizationBins, analysis.QuantizationTable.ZeroBins, quantizationTree, decomposedWaveletData, rawImage.Width, analysis.QuantizedCoefficients, nbisAnalysis.QuantizedCoefficients)}");
Console.WriteLine($"fixed-shift current mismatch detail : {DescribeCoefficientMismatch(analysis.QuantizationTable.QuantizationBins, quantizationTree, fixedShiftCurrentTableQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"fixed-shift current mismatch source : {DescribeDoubleCoefficientSource(analysis.QuantizationTable.QuantizationBins, analysis.QuantizationTable.ZeroBins, quantizationTree, fixedShiftCurrentWaveletData, rawImage.Width, fixedShiftCurrentTableQuantizedCoefficients, reference.QuantizedCoefficients)}");
PrintCrossSourceMismatchComparison(
    "managed mismatch in fixed-shift",
    FindFirstMismatchIndex(analysis.QuantizedCoefficients, reference.QuantizedCoefficients),
    analysis.QuantizationTable.QuantizationBins,
    analysis.QuantizationTable.ZeroBins,
    quantizationTree,
    highPrecisionArtifacts.DoubleNormalizedImage.Pixels,
    fixedShiftCurrentNormalizedImage.Pixels,
    highPrecisionArtifacts.DoubleDecomposedPixels,
    fixedShiftCurrentWaveletData,
    rawImage.Width,
    analysis.QuantizedCoefficients,
    fixedShiftCurrentTableQuantizedCoefficients,
    reference.QuantizedCoefficients);
PrintCrossSourceMismatchComparison(
    "fixed-shift mismatch in managed",
    FindFirstMismatchIndex(fixedShiftCurrentTableQuantizedCoefficients, reference.QuantizedCoefficients),
    analysis.QuantizationTable.QuantizationBins,
    analysis.QuantizationTable.ZeroBins,
    quantizationTree,
    highPrecisionArtifacts.DoubleNormalizedImage.Pixels,
    fixedShiftCurrentNormalizedImage.Pixels,
    highPrecisionArtifacts.DoubleDecomposedPixels,
    fixedShiftCurrentWaveletData,
    rawImage.Width,
    analysis.QuantizedCoefficients,
    fixedShiftCurrentTableQuantizedCoefficients,
    reference.QuantizedCoefficients);
Console.WriteLine($"table vs reference mismatch detail  : {DescribeCoefficientMismatch(reference.QuantizationTable.QuantizationBins, quantizationTree, referenceTableQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"table vs reference mismatch source  : {DescribeCoefficientSource(reference.QuantizationTable.QuantizationBins, reference.QuantizationTable.ZeroBins, quantizationTree, decomposedWaveletData, rawImage.Width, referenceTableQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"ref-precision mismatch detail       : {DescribeCoefficientMismatch(reference.QuantizationTable.QuantizationBins, quantizationTree, referencePrecisionQuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"ref-precision mismatch source       : {DescribeCoefficientSource(reference.QuantizationTable.QuantizationBins, reference.QuantizationTable.ZeroBins, quantizationTree, decomposedWaveletData, rawImage.Width, referencePrecisionQuantizedCoefficients, reference.QuantizedCoefficients)}");

static async Task<WsqReferenceQuantizedCoefficients> ReadReferenceCoefficientsAsync(string referencePath)
{
    await using var referenceStream = File.OpenRead(referencePath);
    var container = await WsqContainerReader.ReadAsync(referenceStream).ConfigureAwait(false);
    WsqWaveletTreeBuilder.Build(
        container.FrameHeader.Width,
        container.FrameHeader.Height,
        out var waveletTree,
        out var quantizationTree);

    var quantizedCoefficients = WsqHuffmanDecoder.DecodeQuantizedCoefficients(container, waveletTree, quantizationTree);
    return new(container.FrameHeader, container.QuantizationTable, quantizedCoefficients);
}

static async Task<NbisAnalysisDump> ReadNbisAnalysisAsync(string rawPath, int width, int height, double bitRate)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = Path.Combine(RepoRoot, "tmp", "wsq-diag", "nbis_dump"),
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    startInfo.ArgumentList.Add(rawPath);
    startInfo.ArgumentList.Add(width.ToString(CultureInfo.InvariantCulture));
    startInfo.ArgumentList.Add(height.ToString(CultureInfo.InvariantCulture));
    startInfo.ArgumentList.Add(bitRate.ToString("0.##", CultureInfo.InvariantCulture));

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Failed to start nbis_dump.");
    var standardOutput = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
    var standardError = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
    await process.WaitForExitAsync().ConfigureAwait(false);

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"nbis_dump failed with exit code {process.ExitCode}: {standardError}");
    }

    var shift = 0.0;
    var scale = 0.0;
    var quantizationBins = new double[WsqConstants.NumberOfSubbands];
    var zeroBins = new double[WsqConstants.NumberOfSubbands];
    var variances = new double[WsqConstants.NumberOfSubbands];
    var quantizedCoefficients = new List<short>();

    foreach (var line in standardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (line.StartsWith("var[", StringComparison.Ordinal))
        {
            var varianceToken = line;
            var subbandIndex = ParseIndexedTokenIndex(varianceToken, "var");
            variances[subbandIndex] = ParseIndexedTokenValue(varianceToken);
            continue;
        }

        if (line.StartsWith("shift=", StringComparison.Ordinal))
        {
            shift = double.Parse(line["shift=".Length..], CultureInfo.InvariantCulture);
            continue;
        }

        if (line.StartsWith("scale=", StringComparison.Ordinal))
        {
            scale = double.Parse(line["scale=".Length..], CultureInfo.InvariantCulture);
            continue;
        }

        if (line.StartsWith("qbin[", StringComparison.Ordinal))
        {
            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var qbinToken = tokens[0];
            var zbinToken = tokens[1];
            var subbandIndex = ParseIndexedTokenIndex(qbinToken, "qbin");
            quantizationBins[subbandIndex] = ParseIndexedTokenValue(qbinToken);
            zeroBins[subbandIndex] = ParseIndexedTokenValue(zbinToken);
            continue;
        }

        if (line.StartsWith("coeff[", StringComparison.Ordinal))
        {
            quantizedCoefficients.Add(checked((short)int.Parse(line[(line.IndexOf('=') + 1)..], CultureInfo.InvariantCulture)));
        }
    }

    return new(shift, scale, quantizationBins, zeroBins, variances, quantizedCoefficients.ToArray());
}

static async Task<float[]> ReadNbisWaveletDataAsync(string rawPath, int width, int height, int stopNode = 19)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = Path.Combine(RepoRoot, "tmp", "wsq-diag", "nbis_wavelet_dump"),
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    startInfo.ArgumentList.Add(rawPath);
    startInfo.ArgumentList.Add(width.ToString(CultureInfo.InvariantCulture));
    startInfo.ArgumentList.Add(height.ToString(CultureInfo.InvariantCulture));
    startInfo.ArgumentList.Add(stopNode.ToString(CultureInfo.InvariantCulture));

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Failed to start nbis_wavelet_dump.");
    await using var outputStream = new MemoryStream();
    await process.StandardOutput.BaseStream.CopyToAsync(outputStream).ConfigureAwait(false);
    var standardError = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
    await process.WaitForExitAsync().ConfigureAwait(false);

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"nbis_wavelet_dump failed with exit code {process.ExitCode}: {standardError}");
    }

    var bytes = outputStream.ToArray();
    var waveletData = new float[bytes.Length / sizeof(float)];
    Buffer.BlockCopy(bytes, 0, waveletData, 0, bytes.Length);
    return waveletData;
}

static async Task<float[]> ReadNbisRowPassDataAsync(string rawPath, int width, int height, int stopNode)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = Path.Combine(RepoRoot, "tmp", "wsq-diag", "nbis_wavelet_dump"),
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    startInfo.ArgumentList.Add(rawPath);
    startInfo.ArgumentList.Add(width.ToString(CultureInfo.InvariantCulture));
    startInfo.ArgumentList.Add(height.ToString(CultureInfo.InvariantCulture));
    startInfo.ArgumentList.Add(stopNode.ToString(CultureInfo.InvariantCulture));
    startInfo.ArgumentList.Add("row");

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Failed to start nbis_wavelet_dump.");
    await using var outputStream = new MemoryStream();
    await process.StandardOutput.BaseStream.CopyToAsync(outputStream).ConfigureAwait(false);
    var standardError = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
    await process.WaitForExitAsync().ConfigureAwait(false);

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"nbis_wavelet_dump failed with exit code {process.ExitCode}: {standardError}");
    }

    var bytes = outputStream.ToArray();
    var rowPassData = new float[bytes.Length / sizeof(float)];
    Buffer.BlockCopy(bytes, 0, rowPassData, 0, bytes.Length);
    return rowPassData;
}

static async Task<string> FindFirstStepDifferenceAsync(
    string rawPath,
    int width,
    int height,
    float[] normalizedPixels,
    WsqWaveletNode[] waveletTree)
{
    var workingWaveletData = normalizedPixels.ToArray();
    var lowPassFilter = WsqReferenceTables.StandardTransformTable.LowPassFilterCoefficients.ToArray();
    var highPassFilter = WsqReferenceTables.StandardTransformTable.HighPassFilterCoefficients.ToArray();
    var temporaryBuffer = new float[workingWaveletData.Length];

    for (var nodeIndex = 0; nodeIndex < waveletTree.Length; nodeIndex++)
    {
        var node = waveletTree[nodeIndex];
        var baseOffset = node.Y * width + node.X;

        Array.Clear(temporaryBuffer);
        GetLets(
            temporaryBuffer,
            0,
            workingWaveletData,
            baseOffset,
            node.Height,
            node.Width,
            width,
            1,
            highPassFilter,
            lowPassFilter,
            node.InvertRows);

        var managedRowPassData = FlattenRowPassOutput(temporaryBuffer, width, node.Width, node.Height);
        var nbisRowPassData = await ReadNbisRowPassDataAsync(rawPath, width, height, nodeIndex).ConfigureAwait(false);
        var firstRowDifference = FindFirstLinearFloatDifference(managedRowPassData, nbisRowPassData, node.Width);

        if (!string.Equals(firstRowDifference, "none", StringComparison.Ordinal))
        {
            return $"node={nodeIndex}, pass=row, x={node.X}, y={node.Y}, width={node.Width}, height={node.Height}, invertRows={node.InvertRows}, invertColumns={node.InvertColumns}, {firstRowDifference}";
        }

        GetLets(
            workingWaveletData,
            baseOffset,
            temporaryBuffer,
            0,
            node.Width,
            node.Height,
            1,
            width,
            highPassFilter,
            lowPassFilter,
            node.InvertColumns);

        var nbisWaveletData = await ReadNbisWaveletDataAsync(rawPath, width, height, nodeIndex).ConfigureAwait(false);
        var firstDifference = FindFirstWaveletDifference(workingWaveletData, nbisWaveletData, width);

        if (string.Equals(firstDifference, "none", StringComparison.Ordinal))
        {
            continue;
        }

        return $"node={nodeIndex}, pass=column, x={node.X}, y={node.Y}, width={node.Width}, height={node.Height}, invertRows={node.InvertRows}, invertColumns={node.InvertColumns}, {firstDifference}";
    }

    return "none";
}

static float[] FlattenRowPassOutput(ReadOnlySpan<float> rowPassBuffer, int imageWidth, int nodeWidth, int nodeHeight)
{
    var flattenedRowPassData = new float[nodeWidth * nodeHeight];
    var destinationIndex = 0;

    for (var row = 0; row < nodeHeight; row++)
    {
        var rowStart = row * imageWidth;
        rowPassBuffer.Slice(rowStart, nodeWidth).CopyTo(flattenedRowPassData.AsSpan(destinationIndex, nodeWidth));
        destinationIndex += nodeWidth;
    }

    return flattenedRowPassData;
}

static float[] DecomposeWithHelper(
    float[] normalizedPixels,
    int width,
    int height,
    WsqWaveletNode[] waveletTree,
    WsqTransformTable transformTable)
{
    var workingWaveletData = normalizedPixels.ToArray();
    var lowPassFilter = transformTable.LowPassFilterCoefficients.ToArray();
    var highPassFilter = transformTable.HighPassFilterCoefficients.ToArray();
    var temporaryBuffer = new float[workingWaveletData.Length];

    for (var nodeIndex = 0; nodeIndex < waveletTree.Length; nodeIndex++)
    {
        var node = waveletTree[nodeIndex];
        var baseOffset = node.Y * width + node.X;

        GetLets(
            temporaryBuffer,
            0,
            workingWaveletData,
            baseOffset,
            node.Height,
            node.Width,
            width,
            1,
            highPassFilter,
            lowPassFilter,
            node.InvertRows);

        GetLets(
            workingWaveletData,
            baseOffset,
            temporaryBuffer,
            0,
            node.Width,
            node.Height,
            1,
            width,
            highPassFilter,
            lowPassFilter,
            node.InvertColumns);
    }

    return workingWaveletData;
}

static void GetLets(
    Span<float> destination,
    int destinationBaseOffset,
    ReadOnlySpan<float> source,
    int sourceBaseOffset,
    int lineCount,
    int lineLength,
    int linePitch,
    int sampleStride,
    float[] highPassFilter,
    float[] lowPassFilter,
    bool invertSubbands)
{
    var destinationSamples = destination[destinationBaseOffset..];
    var sourceSamples = source[sourceBaseOffset..];
    var dataLengthIsOdd = lineLength % 2;
    var filterLengthIsOdd = lowPassFilter.Length % 2;
    var positiveStride = sampleStride;
    var negativeStride = -positiveStride;
    int lowPassSampleCount;
    int highPassSampleCount;

    if (dataLengthIsOdd != 0)
    {
        lowPassSampleCount = (lineLength + 1) / 2;
        highPassSampleCount = lowPassSampleCount - 1;
    }
    else
    {
        lowPassSampleCount = lineLength / 2;
        highPassSampleCount = lowPassSampleCount;
    }

    int lowPassCenterOffset;
    int highPassCenterOffset;
    int initialLowPassLeftEdge;
    int initialHighPassLeftEdge;
    var initialLowPassRightEdge = 0;
    var initialHighPassRightEdge = 0;

    if (filterLengthIsOdd != 0)
    {
        lowPassCenterOffset = (lowPassFilter.Length - 1) / 2;
        highPassCenterOffset = (highPassFilter.Length - 1) / 2 - 1;
        initialLowPassLeftEdge = 0;
        initialHighPassLeftEdge = 0;
    }
    else
    {
        lowPassCenterOffset = lowPassFilter.Length / 2 - 2;
        highPassCenterOffset = highPassFilter.Length / 2 - 2;
        initialLowPassLeftEdge = 1;
        initialHighPassLeftEdge = 1;
        initialLowPassRightEdge = 1;
        initialHighPassRightEdge = 1;

        if (lowPassCenterOffset == -1)
        {
            lowPassCenterOffset = 0;
            initialLowPassLeftEdge = 0;
        }

        if (highPassCenterOffset == -1)
        {
            highPassCenterOffset = 0;
            initialHighPassLeftEdge = 0;
        }

        for (var filterIndex = 0; filterIndex < highPassFilter.Length; filterIndex++)
        {
            highPassFilter[filterIndex] *= -1.0f;
        }
    }

    for (var lineIndex = 0; lineIndex < lineCount; lineIndex++)
    {
        int lowPassWriteIndex;
        int highPassWriteIndex;

        if (invertSubbands)
        {
            highPassWriteIndex = lineIndex * linePitch;
            lowPassWriteIndex = highPassWriteIndex + highPassSampleCount * sampleStride;
        }
        else
        {
            lowPassWriteIndex = lineIndex * linePitch;
            highPassWriteIndex = lowPassWriteIndex + lowPassSampleCount * sampleStride;
        }

        var firstSourceIndex = lineIndex * linePitch;
        var lastSourceIndex = firstSourceIndex + (lineLength - 1) * sampleStride;

        var lowPassSourceIndex = firstSourceIndex + lowPassCenterOffset * sampleStride;
        var lowPassSourceStride = negativeStride;
        var lowPassLeftEdge = initialLowPassLeftEdge;
        var lowPassRightEdge = initialLowPassRightEdge;

        var highPassSourceIndex = firstSourceIndex + highPassCenterOffset * sampleStride;
        var highPassSourceStride = negativeStride;
        var highPassLeftEdge = initialHighPassLeftEdge;
        var highPassRightEdge = initialHighPassRightEdge;

        for (var sampleIndex = 0; sampleIndex < highPassSampleCount; sampleIndex++)
        {
            var currentLowPassSourceStride = lowPassSourceStride;
            var currentLowPassSourceIndex = lowPassSourceIndex;
            var currentLowPassLeftEdge = lowPassLeftEdge;
            var currentLowPassRightEdge = lowPassRightEdge;
            destinationSamples[lowPassWriteIndex] = Multiply(currentLowPassSourceIndex, sourceSamples, lowPassFilter[0]);

            for (var filterIndex = 1; filterIndex < lowPassFilter.Length; filterIndex++)
            {
                if (currentLowPassSourceIndex == firstSourceIndex)
                {
                    if (currentLowPassLeftEdge != 0)
                    {
                        currentLowPassSourceStride = 0;
                        currentLowPassLeftEdge = 0;
                    }
                    else
                    {
                        currentLowPassSourceStride = positiveStride;
                    }
                }

                if (currentLowPassSourceIndex == lastSourceIndex)
                {
                    if (currentLowPassRightEdge != 0)
                    {
                        currentLowPassSourceStride = 0;
                        currentLowPassRightEdge = 0;
                    }
                    else
                    {
                        currentLowPassSourceStride = negativeStride;
                    }
                }

                currentLowPassSourceIndex += currentLowPassSourceStride;
                destinationSamples[lowPassWriteIndex] = AddProduct(
                    destinationSamples[lowPassWriteIndex],
                    sourceSamples[currentLowPassSourceIndex],
                    lowPassFilter[filterIndex]);
            }

            lowPassWriteIndex += sampleStride;

            var currentHighPassSourceStride = highPassSourceStride;
            var currentHighPassSourceIndex = highPassSourceIndex;
            var currentHighPassLeftEdge = highPassLeftEdge;
            var currentHighPassRightEdge = highPassRightEdge;
            destinationSamples[highPassWriteIndex] = Multiply(currentHighPassSourceIndex, sourceSamples, highPassFilter[0]);

            for (var filterIndex = 1; filterIndex < highPassFilter.Length; filterIndex++)
            {
                if (currentHighPassSourceIndex == firstSourceIndex)
                {
                    if (currentHighPassLeftEdge != 0)
                    {
                        currentHighPassSourceStride = 0;
                        currentHighPassLeftEdge = 0;
                    }
                    else
                    {
                        currentHighPassSourceStride = positiveStride;
                    }
                }

                if (currentHighPassSourceIndex == lastSourceIndex)
                {
                    if (currentHighPassRightEdge != 0)
                    {
                        currentHighPassSourceStride = 0;
                        currentHighPassRightEdge = 0;
                    }
                    else
                    {
                        currentHighPassSourceStride = negativeStride;
                    }
                }

                currentHighPassSourceIndex += currentHighPassSourceStride;
                destinationSamples[highPassWriteIndex] = AddProduct(
                    destinationSamples[highPassWriteIndex],
                    sourceSamples[currentHighPassSourceIndex],
                    highPassFilter[filterIndex]);
            }

            highPassWriteIndex += sampleStride;

            for (var advanceIndex = 0; advanceIndex < 2; advanceIndex++)
            {
                if (lowPassSourceIndex == firstSourceIndex)
                {
                    if (lowPassLeftEdge != 0)
                    {
                        lowPassSourceStride = 0;
                        lowPassLeftEdge = 0;
                    }
                    else
                    {
                        lowPassSourceStride = positiveStride;
                    }
                }

                lowPassSourceIndex += lowPassSourceStride;

                if (highPassSourceIndex == firstSourceIndex)
                {
                    if (highPassLeftEdge != 0)
                    {
                        highPassSourceStride = 0;
                        highPassLeftEdge = 0;
                    }
                    else
                    {
                        highPassSourceStride = positiveStride;
                    }
                }

                highPassSourceIndex += highPassSourceStride;
            }
        }

        if (dataLengthIsOdd != 0)
        {
            var currentLowPassSourceStride = lowPassSourceStride;
            var currentLowPassSourceIndex = lowPassSourceIndex;
            var currentLowPassLeftEdge = lowPassLeftEdge;
            var currentLowPassRightEdge = lowPassRightEdge;
            destinationSamples[lowPassWriteIndex] = Multiply(currentLowPassSourceIndex, sourceSamples, lowPassFilter[0]);

            for (var filterIndex = 1; filterIndex < lowPassFilter.Length; filterIndex++)
            {
                if (currentLowPassSourceIndex == firstSourceIndex)
                {
                    if (currentLowPassLeftEdge != 0)
                    {
                        currentLowPassSourceStride = 0;
                        currentLowPassLeftEdge = 0;
                    }
                    else
                    {
                        currentLowPassSourceStride = positiveStride;
                    }
                }

                if (currentLowPassSourceIndex == lastSourceIndex)
                {
                    if (currentLowPassRightEdge != 0)
                    {
                        currentLowPassSourceStride = 0;
                        currentLowPassRightEdge = 0;
                    }
                    else
                    {
                        currentLowPassSourceStride = negativeStride;
                    }
                }

                currentLowPassSourceIndex += currentLowPassSourceStride;
                destinationSamples[lowPassWriteIndex] = AddProduct(
                    destinationSamples[lowPassWriteIndex],
                    sourceSamples[currentLowPassSourceIndex],
                    lowPassFilter[filterIndex]);
            }

            lowPassWriteIndex += sampleStride;
        }
    }

    if (filterLengthIsOdd == 0)
    {
        for (var filterIndex = 0; filterIndex < highPassFilter.Length; filterIndex++)
        {
            highPassFilter[filterIndex] *= -1.0f;
        }
    }
}

static int ParseIndexedTokenIndex(string token, string prefix)
{
    var start = prefix.Length + 1;
    var end = token.IndexOf(']', start);
    return int.Parse(token[start..end], CultureInfo.InvariantCulture);
}

static double ParseIndexedTokenValue(string token)
{
    return double.Parse(token[(token.IndexOf('=') + 1)..], CultureInfo.InvariantCulture);
}

static int FindFirstMismatchIndex(ReadOnlySpan<short> actual, ReadOnlySpan<short> expected)
{
    if (actual.Length != expected.Length)
    {
        return Math.Min(actual.Length, expected.Length);
    }

    for (var index = 0; index < actual.Length; index++)
    {
        if (actual[index] != expected[index])
        {
            return index;
        }
    }

    return -1;
}

static string FindFirstBinDifference(IReadOnlyList<double> actualBins, IReadOnlyList<double> expectedBins)
{
    for (var index = 0; index < actualBins.Count; index++)
    {
        if (actualBins[index].CompareTo(expectedBins[index]) == 0)
        {
            continue;
        }

        return $"index {index}: actual={actualBins[index]:G17}, expected={expectedBins[index]:G17}";
    }

    return "none";
}

static string FindFirstFloatBinDifference(IReadOnlyList<double> actualBins, IReadOnlyList<double> expectedBins)
{
    var count = Math.Min(actualBins.Count, expectedBins.Count);

    for (var index = 0; index < count; index++)
    {
        var actual = actualBins[index];
        var expected = expectedBins[index];

        if (BitConverter.SingleToInt32Bits((float)actual) == BitConverter.SingleToInt32Bits((float)expected))
        {
            continue;
        }

        return $"index {index}: actual={actual:G17}, expected={expected:G17}";
    }

    return actualBins.Count == expectedBins.Count
        ? "none"
        : $"length mismatch: actual={actualBins.Count}, expected={expectedBins.Count}";
}

static string FindFirstWaveletDifference(ReadOnlySpan<float> actualWaveletData, ReadOnlySpan<float> expectedWaveletData, int imageWidth)
{
    for (var index = 0; index < actualWaveletData.Length; index++)
    {
        if (BitConverter.SingleToInt32Bits(actualWaveletData[index]) == BitConverter.SingleToInt32Bits(expectedWaveletData[index]))
        {
            continue;
        }

        var imageY = index / imageWidth;
        var imageX = index % imageWidth;
        return $"index {index}, imageX={imageX}, imageY={imageY}: actual={actualWaveletData[index]:G17}, expected={expectedWaveletData[index]:G17}";
    }

    return "none";
}

static string FindFirstDoubleWaveletDifference(ReadOnlySpan<double> actualWaveletData, ReadOnlySpan<double> expectedWaveletData, int imageWidth)
{
    for (var index = 0; index < actualWaveletData.Length; index++)
    {
        if (BitConverter.DoubleToInt64Bits(actualWaveletData[index]) == BitConverter.DoubleToInt64Bits(expectedWaveletData[index]))
        {
            continue;
        }

        var imageY = index / imageWidth;
        var imageX = index % imageWidth;
        return $"index {index}, imageX={imageX}, imageY={imageY}: actual={actualWaveletData[index]:G17}, expected={expectedWaveletData[index]:G17}";
    }

    return "none";
}

static string FindFirstLinearFloatDifference(ReadOnlySpan<float> actualValues, ReadOnlySpan<float> expectedValues, int rowWidth)
{
    for (var index = 0; index < Math.Min(actualValues.Length, expectedValues.Length); index++)
    {
        if (BitConverter.SingleToInt32Bits(actualValues[index]) == BitConverter.SingleToInt32Bits(expectedValues[index]))
        {
            continue;
        }

        var row = index / rowWidth;
        var column = index % rowWidth;
        return $"index {index}, row={row}, column={column}: actual={actualValues[index]:G17}, expected={expectedValues[index]:G17}";
    }

    return actualValues.Length == expectedValues.Length
        ? "none"
        : $"length mismatch: actual={actualValues.Length}, expected={expectedValues.Length}";
}

[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
static float Multiply(int sourceIndex, ReadOnlySpan<float> sourceSamples, float filterValue)
{
    return sourceSamples[sourceIndex] * filterValue;
}

[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
static float AddProduct(float accumulator, float sourceValue, float filterValue)
{
    return MathF.FusedMultiplyAdd(sourceValue, filterValue, accumulator);
}

static string DescribeCoefficientMismatch(
    IReadOnlyList<double> quantizationBins,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    ReadOnlySpan<short> actualCoefficients,
    ReadOnlySpan<short> expectedCoefficients)
{
    for (var index = 0; index < Math.Min(actualCoefficients.Length, expectedCoefficients.Length); index++)
    {
        if (actualCoefficients[index] == expectedCoefficients[index])
        {
            continue;
        }

        var location = FindCoefficientLocation(quantizationBins, quantizationTree, index);
        return $"index {index}: actual={actualCoefficients[index]}, expected={expectedCoefficients[index]}, {location}";
    }

    return "none";
}

static void PrintCrossSourceMismatchComparison(
    string label,
    int mismatchIndex,
    IReadOnlyList<double> quantizationBins,
    IReadOnlyList<double> zeroBins,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    ReadOnlySpan<double> managedNormalizedPixels,
    ReadOnlySpan<double> fixedShiftNormalizedPixels,
    ReadOnlySpan<double> managedWaveletData,
    ReadOnlySpan<double> fixedShiftWaveletData,
    int imageWidth,
    ReadOnlySpan<short> managedCoefficients,
    ReadOnlySpan<short> fixedShiftCoefficients,
    ReadOnlySpan<short> referenceCoefficients)
{
    if (mismatchIndex < 0)
    {
        Console.WriteLine($"{label,-31}: none");
        return;
    }

    Console.WriteLine(
        $"{label,-31}: index {mismatchIndex}, managed={managedCoefficients[mismatchIndex]}, fixedShift={fixedShiftCoefficients[mismatchIndex]}, reference={referenceCoefficients[mismatchIndex]}");
    Console.WriteLine(
        $"{label,-31} managed: {DescribeDoubleCoefficientSourceAtIndex(quantizationBins, zeroBins, quantizationTree, managedWaveletData, imageWidth, mismatchIndex)}");
    Console.WriteLine(
        $"{label,-31} fixed  : {DescribeDoubleCoefficientSourceAtIndex(quantizationBins, zeroBins, quantizationTree, fixedShiftWaveletData, imageWidth, mismatchIndex)}");
    Console.WriteLine(
        $"{label,-31} norm   : {DescribeNormalizedPairAtIndex(quantizationBins, quantizationTree, managedNormalizedPixels, fixedShiftNormalizedPixels, imageWidth, mismatchIndex)}");
}

static string DescribeDoubleCoefficientSource(
    IReadOnlyList<double> quantizationBins,
    IReadOnlyList<double> zeroBins,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    ReadOnlySpan<double> waveletData,
    int imageWidth,
    ReadOnlySpan<short> actualCoefficients,
    ReadOnlySpan<short> expectedCoefficients)
{
    for (var index = 0; index < Math.Min(actualCoefficients.Length, expectedCoefficients.Length); index++)
    {
        if (actualCoefficients[index] == expectedCoefficients[index])
        {
            continue;
        }

        var remainingCoefficientIndex = index;
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
            var imageX = node.X + column;
            var imageY = node.Y + row;
            var absoluteWaveletIndex = imageY * imageWidth + imageX;
            var coefficient = waveletData[absoluteWaveletIndex];
            var zeroThreshold = zeroBins[subbandIndex] / 2.0;
            return $"wavelet={coefficient:G17}, qbin={quantizationBins[subbandIndex]:G17}, halfZeroBin={zeroThreshold:G17}, imageX={imageX}, imageY={imageY}";
        }
    }

    return "none";
}

static string DescribeDoubleCoefficientSourceAtIndex(
    IReadOnlyList<double> quantizationBins,
    IReadOnlyList<double> zeroBins,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    ReadOnlySpan<double> waveletData,
    int imageWidth,
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
        var imageX = node.X + column;
        var imageY = node.Y + row;
        var absoluteWaveletIndex = imageY * imageWidth + imageX;
        var coefficient = waveletData[absoluteWaveletIndex];
        var zeroThreshold = zeroBins[subbandIndex] / 2.0;
        return $"wavelet={coefficient:G17}, qbin={quantizationBins[subbandIndex]:G17}, halfZeroBin={zeroThreshold:G17}, imageX={imageX}, imageY={imageY}";
    }

    return "out of range";
}

static string DescribeCoefficientSource(
    IReadOnlyList<double> quantizationBins,
    IReadOnlyList<double> zeroBins,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    ReadOnlySpan<float> waveletData,
    int imageWidth,
    ReadOnlySpan<short> actualCoefficients,
    ReadOnlySpan<short> expectedCoefficients)
{
    for (var index = 0; index < Math.Min(actualCoefficients.Length, expectedCoefficients.Length); index++)
    {
        if (actualCoefficients[index] == expectedCoefficients[index])
        {
            continue;
        }

        var remainingCoefficientIndex = index;
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
            var imageX = node.X + column;
            var imageY = node.Y + row;
            var absoluteWaveletIndex = imageY * imageWidth + imageX;
            var coefficient = waveletData[absoluteWaveletIndex];
            var zeroThreshold = zeroBins[subbandIndex] / 2.0;
            return $"wavelet={coefficient:G17}, qbin={quantizationBins[subbandIndex]:G17}, halfZeroBin={zeroThreshold:G17}, imageX={imageX}, imageY={imageY}";
        }
    }

    return "none";
}

static string DescribeCoefficientSourceAtIndex(
    IReadOnlyList<double> quantizationBins,
    IReadOnlyList<double> zeroBins,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    ReadOnlySpan<float> waveletData,
    int imageWidth,
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
        var imageX = node.X + column;
        var imageY = node.Y + row;
        var absoluteWaveletIndex = imageY * imageWidth + imageX;
        var coefficient = waveletData[absoluteWaveletIndex];
        var zeroThreshold = zeroBins[subbandIndex] / 2.0;
        return $"wavelet={coefficient:G17}, qbin={quantizationBins[subbandIndex]:G17}, halfZeroBin={zeroThreshold:G17}, imageX={imageX}, imageY={imageY}";
    }

    return "out of range";
}

static string DescribeFloatCoefficientSourceAtIndex(
    IReadOnlyList<double> quantizationBins,
    IReadOnlyList<double> zeroBins,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    ReadOnlySpan<float> waveletData,
    int imageWidth,
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
        var imageX = node.X + column;
        var imageY = node.Y + row;
        var absoluteWaveletIndex = imageY * imageWidth + imageX;
        var coefficient = waveletData[absoluteWaveletIndex];
        var zeroThreshold = zeroBins[subbandIndex] / 2.0;
        return $"wavelet={coefficient:G17}, qbin={quantizationBins[subbandIndex]:G17}, halfZeroBin={zeroThreshold:G17}, imageX={imageX}, imageY={imageY}";
    }

    return "out of range";
}

static string DescribeNormalizedPairAtIndex(
    IReadOnlyList<double> quantizationBins,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    ReadOnlySpan<double> managedNormalizedPixels,
    ReadOnlySpan<double> fixedShiftNormalizedPixels,
    int imageWidth,
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
        var imageX = node.X + column;
        var imageY = node.Y + row;
        var absolutePixelIndex = imageY * imageWidth + imageX;
        return $"managed={managedNormalizedPixels[absolutePixelIndex]:G17}, fixedShift={fixedShiftNormalizedPixels[absolutePixelIndex]:G17}, imageX={imageX}, imageY={imageY}";
    }

    return "out of range";
}

static string FindCoefficientLocation(
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
        var imageX = node.X + column;
        var imageY = node.Y + row;

        return $"subband={subbandIndex}, row={row}, column={column}, imageX={imageX}, imageY={imageY}";
    }

    return "outside the active quantized subbands";
}

static double[] ComputeVariances(
    ReadOnlySpan<float> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width)
{
    var variances = new double[WsqConstants.MaxSubbands];
    var varianceSum = 0.0f;

    for (var subband = 0; subband < WsqConstants.StartSizeRegion2; subband++)
    {
        variances[subband] = ComputeVariance(
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

static short[] QuantizeUsingReferencePrecision(
    ReadOnlySpan<float> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    double bitRate)
{
    var variances = ComputeVariancesFromFloatWaveletDataWithReferencePrecision(waveletData, quantizationTree, width);
    var quantizationBins = new double[WsqConstants.MaxSubbands];
    var zeroBins = new double[WsqConstants.MaxSubbands];
    ComputeQuantizationBinsWithReferencePrecision(variances, bitRate, quantizationBins, zeroBins);

    var quantizedCoefficients = new short[waveletData.Length];
    var coefficientIndex = 0;

    for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
    {
        if (quantizationBins[subband].CompareTo(0.0) == 0)
        {
            continue;
        }

        var node = quantizationTree[subband];
        var halfZeroBin = zeroBins[subband] / 2.0;
        var rowStart = node.Y * width + node.X;

        for (var row = 0; row < node.Height; row++)
        {
            var pixelIndex = rowStart + row * width;

            for (var column = 0; column < node.Width; column++)
            {
                var coefficient = (double)waveletData[pixelIndex + column];
                short quantizedCoefficient;

                if (-halfZeroBin <= coefficient && coefficient <= halfZeroBin)
                {
                    quantizedCoefficient = 0;
                }
                else if (coefficient > 0.0)
                {
                    quantizedCoefficient = checked((short)(((coefficient - halfZeroBin) / quantizationBins[subband]) + 1.0));
                }
                else
                {
                    quantizedCoefficient = checked((short)(((coefficient + halfZeroBin) / quantizationBins[subband]) - 1.0));
                }

                quantizedCoefficients[coefficientIndex++] = quantizedCoefficient;
            }
        }
    }

    Array.Resize(ref quantizedCoefficients, coefficientIndex);
    return quantizedCoefficients;
}

static short[] QuantizeUsingProvidedQuantizationTable(
    ReadOnlySpan<float> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    WsqQuantizationTable quantizationTable)
{
    return QuantizeUsingProvidedBins(
        waveletData,
        quantizationTree,
        width,
        quantizationTable.QuantizationBins,
        quantizationTable.ZeroBins);
}

static short[] QuantizeUsingProvidedQuantizationTableForDoubleWavelet(
    ReadOnlySpan<double> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    WsqQuantizationTable quantizationTable)
{
    return WsqCoefficientQuantizer.Quantize(
        waveletData,
        quantizationTree,
        width,
        quantizationTable.QuantizationBins.ToArray(),
        quantizationTable.ZeroBins.ToArray());
}

static short[] QuantizeUsingProvidedBins(
    ReadOnlySpan<float> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    IReadOnlyList<double> quantizationBins,
    IReadOnlyList<double> zeroBins)
{
    var quantizedCoefficients = new short[waveletData.Length];
    var coefficientIndex = 0;

    for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
    {
        if (quantizationBins[subband].CompareTo(0.0) == 0)
        {
            continue;
        }

        var node = quantizationTree[subband];
        var halfZeroBin = zeroBins[subband] / 2.0;
        var rowStart = node.Y * width + node.X;

        for (var row = 0; row < node.Height; row++)
        {
            var pixelIndex = rowStart + row * width;

            for (var column = 0; column < node.Width; column++)
            {
                var coefficient = (double)waveletData[pixelIndex + column];
                short quantizedCoefficient;

                if (-halfZeroBin <= coefficient && coefficient <= halfZeroBin)
                {
                    quantizedCoefficient = 0;
                }
                else if (coefficient > 0.0)
                {
                    quantizedCoefficient = checked((short)(((coefficient - halfZeroBin) / quantizationBins[subband]) + 1.0));
                }
                else
                {
                    quantizedCoefficient = checked((short)(((coefficient + halfZeroBin) / quantizationBins[subband]) - 1.0));
                }

                quantizedCoefficients[coefficientIndex++] = quantizedCoefficient;
            }
        }
    }

    Array.Resize(ref quantizedCoefficients, coefficientIndex);
    return quantizedCoefficients;
}

static short[] QuantizeUsingFloatArithmeticWithDoubleCoefficients(
    ReadOnlySpan<double> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    IReadOnlyList<double> quantizationBins,
    IReadOnlyList<double> zeroBins)
{
    var quantizedCoefficients = new short[waveletData.Length];
    var coefficientIndex = 0;

    for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
    {
        var quantizationBin = (float)quantizationBins[subband];
        if (quantizationBin == 0.0f)
        {
            continue;
        }

        var node = quantizationTree[subband];
        var halfZeroBin = (float)(zeroBins[subband] / 2.0);
        var rowStart = node.Y * width + node.X;

        for (var row = 0; row < node.Height; row++)
        {
            var pixelIndex = rowStart + row * width;

            for (var column = 0; column < node.Width; column++)
            {
                var coefficient = (float)waveletData[pixelIndex + column];
                short quantizedCoefficient;

                if (-halfZeroBin <= coefficient && coefficient <= halfZeroBin)
                {
                    quantizedCoefficient = 0;
                }
                else if (coefficient > 0.0f)
                {
                    quantizedCoefficient = checked((short)(((coefficient - halfZeroBin) / quantizationBin) + 1.0f));
                }
                else
                {
                    quantizedCoefficient = checked((short)(((coefficient + halfZeroBin) / quantizationBin) - 1.0f));
                }

                quantizedCoefficients[coefficientIndex++] = quantizedCoefficient;
            }
        }
    }

    Array.Resize(ref quantizedCoefficients, coefficientIndex);
    return quantizedCoefficients;
}

static short[] QuantizeUsingDoublePrecisionEncoder(
    ReadOnlySpan<byte> rawPixels,
    int width,
    int height,
    WsqWaveletNode[] waveletTree,
    WsqQuantizationNode[] quantizationTree,
    WsqTransformTable transformTable,
    double bitRate)
{
    var normalizedImage = NormalizeWithReferencePrecision(rawPixels);
    var waveletData = DecomposeWithReferencePrecision(
        normalizedImage.Pixels,
        width,
        height,
        waveletTree,
        transformTable);
    return QuantizeDoublePrecisionWaveletData(waveletData, quantizationTree, width, bitRate);
}

static short[] QuantizeUsingDoublePrecisionEncoderWithFixedNormalization(
    ReadOnlySpan<byte> rawPixels,
    int width,
    int height,
    WsqWaveletNode[] waveletTree,
    WsqQuantizationNode[] quantizationTree,
    WsqTransformTable transformTable,
    double bitRate,
    double shift,
    double scale)
{
    var normalizedImage = NormalizeWithFixedParameters(rawPixels, shift, scale);
    var waveletData = DecomposeWithReferencePrecision(
        normalizedImage.Pixels,
        width,
        height,
        waveletTree,
        transformTable);
    return QuantizeDoublePrecisionWaveletData(waveletData, quantizationTree, width, bitRate);
}

static short[] QuantizeUsingHybridSubband0HighPrecisionEncoder(
    HighPrecisionAnalysisArtifacts analysisArtifacts,
    WsqWaveletNode[] waveletTree,
    WsqQuantizationNode[] quantizationTree,
    int width,
    int height,
    double bitRate)
{
    var hybridCoefficientWaveletData = analysisArtifacts.DoubleDecomposedPixels.ToArray();
    CopySubband(
        source: analysisArtifacts.FloatDecomposedPixels,
        destination: hybridCoefficientWaveletData,
        width,
        quantizationTree[0]);

    var quantizationResult = WsqHighPrecisionQuantizer.Quantize(
        analysisArtifacts.DoubleDecomposedPixels,
        hybridCoefficientWaveletData,
        waveletTree,
        quantizationTree,
        width,
        height,
        bitRate);

    return quantizationResult.QuantizedCoefficients;
}

static short[] QuantizeUsingHybridSubband0WithProvidedTable(
    HighPrecisionAnalysisArtifacts analysisArtifacts,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    WsqQuantizationTable quantizationTable)
{
    var hybridCoefficientWaveletData = analysisArtifacts.DoubleDecomposedPixels.ToArray();
    CopySubband(
        source: analysisArtifacts.FloatDecomposedPixels,
        destination: hybridCoefficientWaveletData,
        width,
        quantizationTree[0]);

    return WsqCoefficientQuantizer.Quantize(
        hybridCoefficientWaveletData,
        quantizationTree,
        width,
        quantizationTable.QuantizationBins.ToArray(),
        quantizationTable.ZeroBins.ToArray());
}

static HighPrecisionAnalysisArtifacts CreateHighPrecisionAnalysisArtifacts(
    ReadOnlySpan<byte> rawPixels,
    WsqRawImageDescription rawImage,
    WsqTransformTable transformTable,
    WsqWaveletNode[] waveletTree)
{
    var doubleNormalizedImage = WsqDoubleImageNormalizer.Normalize(rawPixels);
    var floatNormalizedImage = WsqFloatImageNormalizer.Normalize(rawPixels);
    var doubleDecomposedPixels = WsqDoubleDecomposition.Decompose(
        doubleNormalizedImage.Pixels,
        rawImage.Width,
        rawImage.Height,
        waveletTree,
        transformTable);
    var floatDecomposedPixels = WsqDecomposition.Decompose(
        floatNormalizedImage.Pixels,
        rawImage.Width,
        rawImage.Height,
        waveletTree,
        transformTable);

    return new(doubleNormalizedImage, floatNormalizedImage.Pixels, doubleDecomposedPixels, floatDecomposedPixels);
}

static void CopySubband(
    ReadOnlySpan<float> source,
    Span<double> destination,
    int width,
    WsqQuantizationNode node)
{
    var sourceRowStart = node.Y * width + node.X;

    for (var row = 0; row < node.Height; row++)
    {
        var rowOffset = sourceRowStart + row * width;
        for (var column = 0; column < node.Width; column++)
        {
            destination[rowOffset + column] = source[rowOffset + column];
        }
    }
}

static string DescribeThresholdMetrics(
    WsqHighPrecisionQuantizationArtifacts baseArtifacts,
    ReadOnlySpan<short> candidateCoefficients,
    WsqReferenceQuantizedCoefficients reference)
{
    var maximumQuantizationDeltaPercent = FindMaximumRelativePercentDelta(
        baseArtifacts.QuantizationBins,
        reference.QuantizationTable.QuantizationBins);
    var identicalCount = 0;
    var maximumAbsoluteDifference = 0;

    for (var index = 0; index < candidateCoefficients.Length; index++)
    {
        var absoluteDifference = Math.Abs(candidateCoefficients[index] - reference.QuantizedCoefficients[index]);
        if (absoluteDifference == 0)
        {
            identicalCount++;
        }

        maximumAbsoluteDifference = Math.Max(maximumAbsoluteDifference, absoluteDifference);
    }

    var identicalPercent = (double)identicalCount / candidateCoefficients.Length * 100.0;
    return $"qbinDelta={maximumQuantizationDeltaPercent:F6}% identical={identicalPercent:F6}% maxDelta={maximumAbsoluteDifference}";
}

static double FindMaximumRelativePercentDelta(
    IReadOnlyList<double> actualBins,
    IReadOnlyList<double> expectedBins)
{
    var maximumDeltaPercent = 0.0;

    for (var index = 0; index < actualBins.Count; index++)
    {
        var expected = expectedBins[index];
        var actual = actualBins[index];

        if (expected.CompareTo(0.0) == 0)
        {
            if (actual.CompareTo(0.0) != 0)
            {
                return double.PositiveInfinity;
            }

            continue;
        }

        var deltaPercent = Math.Abs(actual - expected) / expected * 100.0;
        maximumDeltaPercent = Math.Max(maximumDeltaPercent, deltaPercent);
    }

    return maximumDeltaPercent;
}

static short[] QuantizeUsingFloatCoefficientQuantizer(
    ReadOnlySpan<float> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    WsqQuantizationTable quantizationTable)
{
    var quantizationBins = quantizationTable.QuantizationBins.Select(static value => (float)value).ToArray();
    var zeroBins = quantizationTable.ZeroBins.Select(static value => (float)value).ToArray();
    return WsqCoefficientQuantizer.Quantize(waveletData, quantizationTree, width, quantizationBins, zeroBins);
}

static short[] QuantizeUsingFloatVarianceWithDoubleCoefficients(
    ReadOnlySpan<float> varianceWaveletData,
    ReadOnlySpan<double> coefficientWaveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    double bitRate)
{
    var variances = WsqVarianceCalculator.Compute(varianceWaveletData, quantizationTree, width);
    var quantizationBins = new float[WsqConstants.MaxSubbands];
    var zeroBins = new float[WsqConstants.MaxSubbands];
    ComputeQuantizationBinsWithCurrentPrecision(variances, (float)bitRate, quantizationBins, zeroBins);

    var doubleQuantizationBins = new double[quantizationBins.Length];
    var doubleZeroBins = new double[zeroBins.Length];

    for (var index = 0; index < quantizationBins.Length; index++)
    {
        doubleQuantizationBins[index] = quantizationBins[index];
        doubleZeroBins[index] = zeroBins[index];
    }

    return WsqCoefficientQuantizer.Quantize(
        coefficientWaveletData,
        quantizationTree,
        width,
        doubleQuantizationBins,
        doubleZeroBins);
}

static short[] QuantizeUsingFixedNormalizationWithRoundedTable(
    ReadOnlySpan<byte> rawPixels,
    int width,
    int height,
    WsqWaveletNode[] waveletTree,
    WsqQuantizationNode[] quantizationTree,
    WsqTransformTable transformTable,
    double bitRate,
    double shift,
    double scale)
{
    var normalizedImage = NormalizeWithFixedParameters(rawPixels, shift, scale);
    var waveletData = DecomposeWithReferencePrecision(
        normalizedImage.Pixels,
        width,
        height,
        waveletTree,
        transformTable);
    var quantizationTable = RoundTripQuantizationTable(CreateQuantizationTableFromDoubleWaveletData(
        waveletData,
        quantizationTree,
        width,
        bitRate));
    return QuantizeUsingProvidedQuantizationTableForDoubleWavelet(waveletData, quantizationTree, width, quantizationTable);
}

static short[] QuantizeUsingFixedNormalizationWithProvidedTable(
    ReadOnlySpan<byte> rawPixels,
    int width,
    int height,
    WsqWaveletNode[] waveletTree,
    WsqQuantizationNode[] quantizationTree,
    WsqTransformTable transformTable,
    WsqQuantizationTable quantizationTable,
    double shift,
    double scale)
{
    var normalizedImage = NormalizeWithFixedParameters(rawPixels, shift, scale);
    var waveletData = DecomposeWithReferencePrecision(
        normalizedImage.Pixels,
        width,
        height,
        waveletTree,
        transformTable);
    return QuantizeUsingProvidedQuantizationTableForDoubleWavelet(waveletData, quantizationTree, width, quantizationTable);
}

static short[] QuantizeUsingRoundedCurrentHeader(
    ReadOnlySpan<byte> rawPixels,
    int width,
    int height,
    WsqWaveletNode[] waveletTree,
    WsqQuantizationNode[] quantizationTree,
    WsqTransformTable transformTable,
    double bitRate)
{
    var normalizedImage = NormalizeWithReferencePrecision(rawPixels);
    var roundedShift = WsqScaledValueCodec.RoundTripUInt16(normalizedImage.Shift);
    var roundedScale = WsqScaledValueCodec.RoundTripUInt16(normalizedImage.Scale);
    return QuantizeUsingFixedNormalizationWithRoundedTable(
        rawPixels,
        width,
        height,
        waveletTree,
        quantizationTree,
        transformTable,
        bitRate,
        roundedShift,
        roundedScale);
}

static short[] QuantizeUsingFixedShiftCurrentTable(
    ReadOnlySpan<byte> rawPixels,
    int width,
    int height,
    WsqWaveletNode[] waveletTree,
    WsqQuantizationNode[] quantizationTree,
    WsqTransformTable transformTable,
    double bitRate,
    double shift,
    double scale)
{
    var normalizedImage = NormalizeWithFixedParameters(rawPixels, shift, scale);
    var waveletData = DecomposeWithReferencePrecision(
        normalizedImage.Pixels,
        width,
        height,
        waveletTree,
        transformTable);
    return QuantizeDoublePrecisionWaveletData(waveletData, quantizationTree, width, bitRate);
}

static short[] QuantizeUsingFloatScaleHighPrecisionQuantizer(
    ReadOnlySpan<double> waveletData,
    WsqWaveletNode[] waveletTree,
    WsqQuantizationNode[] quantizationTree,
    int width,
    int height,
    double bitRate)
{
    var quantizationArtifacts = CreateFloatScaleQuantizationArtifacts(waveletData, quantizationTree, width, bitRate);
    var quantizationTable = WsqQuantizationTableFactory.Create(
        quantizationArtifacts.QuantizationBins,
        quantizationArtifacts.ZeroBins);
    var quantizedCoefficients = WsqCoefficientQuantizer.Quantize(
        waveletData,
        quantizationTree,
        width,
        quantizationArtifacts.QuantizationBins,
        quantizationArtifacts.ZeroBins);
    var _ = WsqQuantizationDecoder.ComputeBlockSizes(quantizationTable, waveletTree, quantizationTree);
    return quantizedCoefficients;
}

static short[] QuantizeUsingFullVarianceHighPrecisionQuantizer(
    ReadOnlySpan<double> waveletData,
    WsqWaveletNode[] waveletTree,
    WsqQuantizationNode[] quantizationTree,
    int width,
    int height,
    double bitRate)
{
    var quantizationArtifacts = CreateFullVarianceQuantizationArtifacts(waveletData, quantizationTree, width, bitRate);
    var quantizationTable = WsqQuantizationTableFactory.Create(
        quantizationArtifacts.QuantizationBins,
        quantizationArtifacts.ZeroBins);
    var quantizedCoefficients = WsqCoefficientQuantizer.Quantize(
        waveletData,
        quantizationTree,
        width,
        quantizationArtifacts.QuantizationBins,
        quantizationArtifacts.ZeroBins);
    var _ = WsqQuantizationDecoder.ComputeBlockSizes(quantizationTable, waveletTree, quantizationTree);
    return quantizedCoefficients;
}

static short[] QuantizeUsingSubband0FullVarianceHighPrecisionQuantizer(
    ReadOnlySpan<double> waveletData,
    WsqWaveletNode[] waveletTree,
    WsqQuantizationNode[] quantizationTree,
    int width,
    int height,
    double bitRate)
{
    var quantizationArtifacts = CreateSubband0FullVarianceQuantizationArtifacts(waveletData, quantizationTree, width, bitRate);
    var quantizationTable = WsqQuantizationTableFactory.Create(
        quantizationArtifacts.QuantizationBins,
        quantizationArtifacts.ZeroBins);
    var quantizedCoefficients = WsqCoefficientQuantizer.Quantize(
        waveletData,
        quantizationTree,
        width,
        quantizationArtifacts.QuantizationBins,
        quantizationArtifacts.ZeroBins);
    var _ = WsqQuantizationDecoder.ComputeBlockSizes(quantizationTable, waveletTree, quantizationTree);
    return quantizedCoefficients;
}

static short[] QuantizeUsingSubband0LowerVarianceHighPrecisionQuantizer(
    ReadOnlySpan<double> waveletData,
    WsqWaveletNode[] waveletTree,
    WsqQuantizationNode[] quantizationTree,
    int width,
    int height,
    double bitRate)
{
    var quantizationArtifacts = CreateSubband0LowerVarianceQuantizationArtifacts(waveletData, quantizationTree, width, bitRate);
    var quantizationTable = WsqQuantizationTableFactory.Create(
        quantizationArtifacts.QuantizationBins,
        quantizationArtifacts.ZeroBins);
    var quantizedCoefficients = WsqCoefficientQuantizer.Quantize(
        waveletData,
        quantizationTree,
        width,
        quantizationArtifacts.QuantizationBins,
        quantizationArtifacts.ZeroBins);
    var _ = WsqQuantizationDecoder.ComputeBlockSizes(quantizationTable, waveletTree, quantizationTree);
    return quantizedCoefficients;
}

static short[] QuantizeUsingSubband0LowerVarianceOverride(
    ReadOnlySpan<double> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    WsqHighPrecisionQuantizationArtifacts baseArtifacts,
    WsqHighPrecisionQuantizationArtifacts fullVarianceArtifacts)
{
    var quantizationBins = baseArtifacts.QuantizationBins.ToArray();
    var zeroBins = baseArtifacts.ZeroBins.ToArray();

    if (fullVarianceArtifacts.Variances[0] < baseArtifacts.Variances[0])
    {
        quantizationBins[0] = fullVarianceArtifacts.QuantizationBins[0];
        zeroBins[0] = fullVarianceArtifacts.ZeroBins[0];
    }

    return WsqCoefficientQuantizer.Quantize(
        waveletData,
        quantizationTree,
        width,
        quantizationBins,
        zeroBins);
}

static short[] QuantizeUsingSubband0VarianceBlend(
    ReadOnlySpan<double> waveletData,
    WsqWaveletNode[] waveletTree,
    WsqQuantizationNode[] quantizationTree,
    int width,
    int height,
    double bitRate,
    double blendFactor)
{
    var quantizationArtifacts = CreateSubband0VarianceBlendQuantizationArtifacts(
        waveletData,
        quantizationTree,
        width,
        bitRate,
        blendFactor);
    var quantizationTable = WsqQuantizationTableFactory.Create(
        quantizationArtifacts.QuantizationBins,
        quantizationArtifacts.ZeroBins);
    var quantizedCoefficients = WsqCoefficientQuantizer.Quantize(
        waveletData,
        quantizationTree,
        width,
        quantizationArtifacts.QuantizationBins,
        quantizationArtifacts.ZeroBins);
    var _ = WsqQuantizationDecoder.ComputeBlockSizes(quantizationTable, waveletTree, quantizationTree);
    return quantizedCoefficients;
}

static short[] QuantizeUsingSubband0QbinNudge(
    ReadOnlySpan<double> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    WsqHighPrecisionQuantizationArtifacts baseArtifacts,
    double qbinDelta)
{
    var quantizationBins = baseArtifacts.QuantizationBins.ToArray();
    var zeroBins = baseArtifacts.ZeroBins.ToArray();
    quantizationBins[0] += qbinDelta;
    zeroBins[0] = quantizationBins[0] * 1.2;

    return WsqCoefficientQuantizer.Quantize(
        waveletData,
        quantizationTree,
        width,
        quantizationBins,
        zeroBins);
}

static short[] QuantizeUsingSubband0HalfZeroBinNudge(
    ReadOnlySpan<double> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    WsqHighPrecisionQuantizationArtifacts baseArtifacts,
    double halfZeroBinDelta)
{
    var quantizationBins = baseArtifacts.QuantizationBins.ToArray();
    var zeroBins = baseArtifacts.ZeroBins.ToArray();
    zeroBins[0] += halfZeroBinDelta * 2.0;

    return WsqCoefficientQuantizer.Quantize(
        waveletData,
        quantizationTree,
        width,
        quantizationBins,
        zeroBins);
}

static short[] QuantizeUsingSubband0ReferenceBins(
    ReadOnlySpan<double> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    WsqHighPrecisionQuantizationArtifacts baseArtifacts,
    double referenceQuantizationBin,
    double referenceZeroBin)
{
    var quantizationBins = baseArtifacts.QuantizationBins.ToArray();
    var zeroBins = baseArtifacts.ZeroBins.ToArray();
    quantizationBins[0] = referenceQuantizationBin;
    zeroBins[0] = referenceZeroBin;

    return WsqCoefficientQuantizer.Quantize(
        waveletData,
        quantizationTree,
        width,
        quantizationBins,
        zeroBins);
}

static short[] QuantizeUsingGlobalQuantizationFactor(
    ReadOnlySpan<double> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    WsqHighPrecisionQuantizationArtifacts baseArtifacts,
    double quantizationFactor)
{
    var quantizationBins = baseArtifacts.QuantizationBins.ToArray();
    var zeroBins = baseArtifacts.ZeroBins.ToArray();

    for (var index = 0; index < WsqConstants.NumberOfSubbands; index++)
    {
        if (quantizationBins[index].CompareTo(0.0) == 0)
        {
            continue;
        }

        quantizationBins[index] *= quantizationFactor;
        zeroBins[index] = quantizationBins[index] * 1.2;
    }

    return WsqCoefficientQuantizer.Quantize(
        waveletData,
        quantizationTree,
        width,
        quantizationBins,
        zeroBins);
}

static string TraceHighPrecisionQuantizationScale(ReadOnlySpan<double> variances, double bitRate)
{
    return TraceQuantizationScaleCore(
        variances,
        bitRate,
        useReferencePrecision: false);
}

static string TraceReferencePrecisionQuantizationScale(ReadOnlySpan<double> variances, double bitRate)
{
    return TraceQuantizationScaleCore(
        variances,
        bitRate,
        useReferencePrecision: true);
}

static string TraceQuantizationScaleCore(
    ReadOnlySpan<double> variances,
    double bitRate,
    bool useReferencePrecision)
{
    var reciprocalSubbandAreas = new double[WsqConstants.NumberOfSubbands];
    var sigma = new double[WsqConstants.NumberOfSubbands];
    var quantizationBins = new double[WsqConstants.MaxSubbands];
    var zeroBins = new double[WsqConstants.MaxSubbands];
    var initialSubbands = new int[WsqConstants.NumberOfSubbands];
    var workingSubbands = new int[WsqConstants.NumberOfSubbands];
    var positiveBitRateFlags = new bool[WsqConstants.NumberOfSubbands];

    if (useReferencePrecision)
    {
        SetReferencePrecisionReciprocalSubbandAreas(reciprocalSubbandAreas);
    }
    else
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

    var initialSubbandCount = 0;
    for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
    {
        if (variances[subband] < WsqConstants.VarianceThreshold)
        {
            continue;
        }

        sigma[subband] = Math.Sqrt(variances[subband]);
        quantizationBins[subband] = subband < WsqConstants.StartSizeRegion2
            ? 1.0
            : 10.0 / ((useReferencePrecision ? GetReferencePrecisionSubbandWeight(subband) : GetSubbandWeight(subband)) * Math.Log(variances[subband]));
        initialSubbands[initialSubbandCount] = subband;
        workingSubbands[initialSubbandCount] = subband;
        initialSubbandCount++;
    }

    if (initialSubbandCount == 0)
    {
        return "none";
    }

    Span<int> activeSubbands = workingSubbands;
    var activeSubbandCount = initialSubbandCount;
    var iteration = 0;
    var summary = new System.Text.StringBuilder();

    while (true)
    {
        iteration++;
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
                sigma[subband] / quantizationBins[subband],
                reciprocalSubbandAreas[subband]);
        }

        var quantizationScale = (Math.Pow(2.0, (bitRate / reciprocalAreaSum) - 1.0) / 2.5)
            / Math.Pow(product, 1.0 / reciprocalAreaSum);
        var nonPositiveBitRateCount = 0;

        Array.Clear(positiveBitRateFlags);
        for (var index = 0; index < activeSubbandCount; index++)
        {
            var subband = activeSubbands[index];
            if ((quantizationBins[subband] / quantizationScale) >= (5.0 * sigma[subband]))
            {
                positiveBitRateFlags[subband] = true;
                nonPositiveBitRateCount++;
            }
        }

        if (summary.Length > 0)
        {
            summary.Append(" | ");
        }

        summary.Append($"i{iteration}:active={activeSubbandCount},scale={quantizationScale:G12},drop={nonPositiveBitRateCount}");

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
                    : 0.0;
                zeroBins[subband] = 1.2 * quantizationBins[subband];
            }

            summary.Append($",q0={quantizationBins[0]:G12},q1={quantizationBins[1]:G12},q5={quantizationBins[5]:G12},q24={quantizationBins[24]:G12}");
            return summary.ToString();
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

static string DescribeVarianceTrace(IReadOnlyList<double> variances)
{
    return $"v0={variances[0]:G12},v1={variances[1]:G12},v5={variances[5]:G12},v24={variances[24]:G12}";
}

static WsqHighPrecisionQuantizationArtifacts CreateFloatScaleQuantizationArtifacts(
    ReadOnlySpan<double> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    double bitRate)
{
    var variances = WsqHighPrecisionVarianceCalculator.Compute(waveletData, quantizationTree, width);
    var quantizationBins = new double[WsqConstants.MaxSubbands];
    var zeroBins = new double[WsqConstants.MaxSubbands];
    ComputeFloatScaleQuantizationBins(variances, bitRate, quantizationBins, zeroBins);
    return new(variances, quantizationBins, zeroBins);
}

static void ComputeFloatScaleQuantizationBins(
    ReadOnlySpan<double> variances,
    double bitRate,
    Span<double> quantizationBins,
    Span<double> zeroBins)
{
    var reciprocalSubbandAreas = new double[WsqConstants.NumberOfSubbands];
    var sigma = new double[WsqConstants.NumberOfSubbands];
    var initialSubbands = new int[WsqConstants.NumberOfSubbands];
    var workingSubbands = new int[WsqConstants.NumberOfSubbands];
    var positiveBitRateFlags = new bool[WsqConstants.NumberOfSubbands];

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

    var initialSubbandCount = 0;
    for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
    {
        if (variances[subband] < WsqConstants.VarianceThreshold)
        {
            quantizationBins[subband] = 0.0;
            zeroBins[subband] = 0.0;
            continue;
        }

        sigma[subband] = Math.Sqrt(variances[subband]);
        quantizationBins[subband] = subband < WsqConstants.StartSizeRegion2
            ? 1.0
            : 10.0 / (GetSubbandWeight(subband) * Math.Log(variances[subband]));
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
                sigma[subband] / quantizationBins[subband],
                reciprocalSubbandAreas[subband]);
        }

        var quantizationScale = (float)((Math.Pow(2.0, (bitRate / reciprocalAreaSum) - 1.0) / 2.5)
            / Math.Pow(product, 1.0 / reciprocalAreaSum));
        var nonPositiveBitRateCount = 0;

        Array.Clear(positiveBitRateFlags);
        for (var index = 0; index < activeSubbandCount; index++)
        {
            var subband = activeSubbands[index];
            if ((quantizationBins[subband] / quantizationScale) >= (5.0 * sigma[subband]))
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
                    : 0.0;
                zeroBins[subband] = 1.2 * quantizationBins[subband];
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

static double GetSubbandWeight(int subband)
{
    return subband switch
    {
        52 => 1.32,
        53 => 1.08,
        54 => 1.42,
        55 => 1.08,
        56 => 1.32,
        57 => 1.42,
        58 => 1.08,
        59 => 1.08,
        _ => 1.0,
    };
}

static WsqHighPrecisionQuantizationArtifacts CreateFullVarianceQuantizationArtifacts(
    ReadOnlySpan<double> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    double bitRate)
{
    var variances = ComputeFullVariances(waveletData, quantizationTree, width);
    var quantizationBins = new double[WsqConstants.MaxSubbands];
    var zeroBins = new double[WsqConstants.MaxSubbands];
    ComputeStandardHighPrecisionQuantizationBins(variances, bitRate, quantizationBins, zeroBins);
    return new(variances, quantizationBins, zeroBins);
}

static WsqHighPrecisionQuantizationArtifacts CreateSubband0FullVarianceQuantizationArtifacts(
    ReadOnlySpan<double> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    double bitRate)
{
    var variances = WsqHighPrecisionVarianceCalculator.Compute(waveletData, quantizationTree, width);
    variances[0] = ComputeSubbandVariance(
        waveletData,
        quantizationTree[0],
        width,
        useCroppedRegion: false);
    var quantizationBins = new double[WsqConstants.MaxSubbands];
    var zeroBins = new double[WsqConstants.MaxSubbands];
    ComputeStandardHighPrecisionQuantizationBins(variances, bitRate, quantizationBins, zeroBins);
    return new(variances, quantizationBins, zeroBins);
}

static WsqHighPrecisionQuantizationArtifacts CreateSubband0LowerVarianceQuantizationArtifacts(
    ReadOnlySpan<double> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    double bitRate)
{
    var variances = WsqHighPrecisionVarianceCalculator.Compute(waveletData, quantizationTree, width);
    var fullVariance = ComputeSubbandVariance(
        waveletData,
        quantizationTree[0],
        width,
        useCroppedRegion: false);
    if (fullVariance < variances[0])
    {
        variances[0] = fullVariance;
    }

    var quantizationBins = new double[WsqConstants.MaxSubbands];
    var zeroBins = new double[WsqConstants.MaxSubbands];
    ComputeStandardHighPrecisionQuantizationBins(variances, bitRate, quantizationBins, zeroBins);
    return new(variances, quantizationBins, zeroBins);
}

static WsqHighPrecisionQuantizationArtifacts CreateSubband0VarianceBlendQuantizationArtifacts(
    ReadOnlySpan<double> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    double bitRate,
    double blendFactor)
{
    var variances = WsqHighPrecisionVarianceCalculator.Compute(waveletData, quantizationTree, width);
    var fullVariance = ComputeSubbandVariance(
        waveletData,
        quantizationTree[0],
        width,
        useCroppedRegion: false);
    variances[0] = variances[0] + ((fullVariance - variances[0]) * blendFactor);

    var quantizationBins = new double[WsqConstants.MaxSubbands];
    var zeroBins = new double[WsqConstants.MaxSubbands];
    ComputeStandardHighPrecisionQuantizationBins(variances, bitRate, quantizationBins, zeroBins);
    return new(variances, quantizationBins, zeroBins);
}

static double[] ComputeFullVariances(
    ReadOnlySpan<double> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width)
{
    var variances = new double[WsqConstants.MaxSubbands];

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

static double ComputeSubbandVariance(
    ReadOnlySpan<double> waveletData,
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
    var normalizedSum = (pixelSum * pixelSum) / sampleCount;
    return (squaredSum - normalizedSum) / (sampleCount - 1.0);
}

static void ComputeStandardHighPrecisionQuantizationBins(
    ReadOnlySpan<double> variances,
    double bitRate,
    Span<double> quantizationBins,
    Span<double> zeroBins)
{
    var reciprocalSubbandAreas = new double[WsqConstants.NumberOfSubbands];
    var sigma = new double[WsqConstants.NumberOfSubbands];
    var initialSubbands = new int[WsqConstants.NumberOfSubbands];
    var workingSubbands = new int[WsqConstants.NumberOfSubbands];
    var positiveBitRateFlags = new bool[WsqConstants.NumberOfSubbands];

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

    var initialSubbandCount = 0;
    for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
    {
        if (variances[subband] < WsqConstants.VarianceThreshold)
        {
            quantizationBins[subband] = 0.0;
            zeroBins[subband] = 0.0;
            continue;
        }

        sigma[subband] = Math.Sqrt(variances[subband]);
        quantizationBins[subband] = subband < WsqConstants.StartSizeRegion2
            ? 1.0
            : 10.0 / (GetSubbandWeight(subband) * Math.Log(variances[subband]));
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
                sigma[subband] / quantizationBins[subband],
                reciprocalSubbandAreas[subband]);
        }

        var quantizationScale = (Math.Pow(2.0, (bitRate / reciprocalAreaSum) - 1.0) / 2.5)
            / Math.Pow(product, 1.0 / reciprocalAreaSum);
        var nonPositiveBitRateCount = 0;

        Array.Clear(positiveBitRateFlags);
        for (var index = 0; index < activeSubbandCount; index++)
        {
            var subband = activeSubbands[index];
            if ((quantizationBins[subband] / quantizationScale) >= (5.0 * sigma[subband]))
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
                    : 0.0;
                zeroBins[subband] = 1.2 * quantizationBins[subband];
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

static short[] QuantizeUsingFloatCastBinsWithDoubleCoefficients(
    ReadOnlySpan<double> coefficientWaveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    double bitRate)
{
    var quantizationArtifacts = CreateQuantizationArtifactsFromDoubleWaveletData(
        coefficientWaveletData,
        quantizationTree,
        width,
        bitRate);
    var floatQuantizationBins = new double[quantizationArtifacts.QuantizationBins.Length];
    var floatZeroBins = new double[quantizationArtifacts.ZeroBins.Length];

    for (var index = 0; index < quantizationArtifacts.QuantizationBins.Length; index++)
    {
        floatQuantizationBins[index] = (float)quantizationArtifacts.QuantizationBins[index];
        floatZeroBins[index] = (float)quantizationArtifacts.ZeroBins[index];
    }

    return WsqCoefficientQuantizer.Quantize(
        coefficientWaveletData,
        quantizationTree,
        width,
        floatQuantizationBins,
        floatZeroBins);
}

static HighPrecisionQuantizationArtifacts CreateQuantizationArtifactsFromDoubleWaveletData(
    ReadOnlySpan<double> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    double bitRate)
{
    var variances = ComputeVariancesWithReferencePrecision(waveletData, quantizationTree, width);
    var quantizationBins = new double[WsqConstants.MaxSubbands];
    var zeroBins = new double[WsqConstants.MaxSubbands];
    ComputeQuantizationBinsWithReferencePrecision(variances, bitRate, quantizationBins, zeroBins);
    return new(variances, quantizationBins, zeroBins);
}

static short[] QuantizeUsingFloatNormalizedDoublePrecisionEncoder(
    ReadOnlySpan<float> normalizedPixels,
    int width,
    int height,
    WsqWaveletNode[] waveletTree,
    WsqQuantizationNode[] quantizationTree,
    WsqTransformTable transformTable,
    double bitRate)
{
    var doublePrecisionPixels = new double[normalizedPixels.Length];

    for (var index = 0; index < normalizedPixels.Length; index++)
    {
        doublePrecisionPixels[index] = normalizedPixels[index];
    }

    var waveletData = DecomposeWithReferencePrecision(
        doublePrecisionPixels,
        width,
        height,
        waveletTree,
        transformTable);
    return QuantizeDoublePrecisionWaveletData(waveletData, quantizationTree, width, bitRate);
}

static short[] QuantizeUsingManagedVariancesWithReferencePrecisionBins(
    ReadOnlySpan<float> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    ReadOnlySpan<double> managedVariances,
    double bitRate)
{
    var quantizationBins = new double[WsqConstants.MaxSubbands];
    var zeroBins = new double[WsqConstants.MaxSubbands];
    ComputeQuantizationBinsWithReferencePrecision(managedVariances, bitRate, quantizationBins, zeroBins);
    return QuantizeUsingProvidedBins(waveletData, quantizationTree, width, quantizationBins, zeroBins);
}

static WsqQuantizationTable RoundTripQuantizationTable(WsqQuantizationTable quantizationTable)
{
    var quantizationBins = new double[quantizationTable.QuantizationBins.Count];
    var zeroBins = new double[quantizationTable.ZeroBins.Count];

    for (var index = 0; index < quantizationBins.Length; index++)
    {
        quantizationBins[index] = RoundTripScaledUInt16(quantizationTable.QuantizationBins[index]);
        zeroBins[index] = RoundTripScaledUInt16(quantizationTable.ZeroBins[index]);
    }

    return new(
        RoundTripScaledUInt16(quantizationTable.BinCenter),
        quantizationBins,
        zeroBins);
}

static double RoundTripScaledUInt16(double value)
{
    if (Math.Abs(value) < double.Epsilon)
    {
        return 0.0;
    }

    var scaledValue = value;
    byte scale = 0;

    while (scaledValue < ushort.MaxValue)
    {
        scale++;
        scaledValue *= 10.0;
    }

    scale--;
    var rawValue = checked((ushort)(scaledValue < 0.0 ? scaledValue / 10.0 - 0.5 : scaledValue / 10.0 + 0.5));

    var roundTrippedValue = (double)rawValue;
    while (scale > 0)
    {
        roundTrippedValue /= 10.0;
        scale--;
    }

    return roundTrippedValue;
}

static DoubleNormalizedImage NormalizeWithReferencePrecision(ReadOnlySpan<byte> rawPixels)
{
    var sum = 0L;
    var minimumPixelValue = byte.MaxValue;
    var maximumPixelValue = byte.MinValue;

    foreach (var pixel in rawPixels)
    {
        minimumPixelValue = Math.Min(minimumPixelValue, pixel);
        maximumPixelValue = Math.Max(maximumPixelValue, pixel);
        sum += pixel;
    }

    var shift = (double)sum / rawPixels.Length;
    var lowerDistance = shift - minimumPixelValue;
    var upperDistance = maximumPixelValue - shift;
    var scale = Math.Max(lowerDistance, upperDistance) / 128.0;
    var normalizedPixels = new double[rawPixels.Length];

    if (scale == 0.0)
    {
        return new(normalizedPixels, shift, scale);
    }

    for (var index = 0; index < rawPixels.Length; index++)
    {
        normalizedPixels[index] = (rawPixels[index] - shift) / scale;
    }

    return new(normalizedPixels, shift, scale);
}

static DoubleNormalizedImage NormalizeWithFixedParameters(
    ReadOnlySpan<byte> rawPixels,
    double shift,
    double scale)
{
    var normalizedPixels = new double[rawPixels.Length];

    if (scale == 0.0)
    {
        return new(normalizedPixels, shift, scale);
    }

    for (var index = 0; index < rawPixels.Length; index++)
    {
        normalizedPixels[index] = (rawPixels[index] - shift) / scale;
    }

    return new(normalizedPixels, shift, scale);
}

static double[] DecomposeWithReferencePrecision(
    double[] normalizedPixels,
    int width,
    int height,
    WsqWaveletNode[] waveletTree,
    WsqTransformTable transformTable)
{
    var workingWaveletData = normalizedPixels.ToArray();
    var lowPassFilter = new[]
    {
        0.03782845550699546,
        -0.02384946501938000,
        -0.11062440441842342,
        0.37740285561265380,
        0.85269867900940344,
        0.37740285561265380,
        -0.11062440441842342,
        -0.02384946501938000,
        0.03782845550699546,
    };
    var highPassFilter = new[]
    {
        0.06453888262893845,
        -0.04068941760955844,
        -0.41809227322221221,
        0.78848561640566439,
        -0.41809227322221221,
        -0.04068941760955844,
        0.06453888262893845,
    };
    var temporaryBuffer = new double[workingWaveletData.Length];

    for (var nodeIndex = 0; nodeIndex < waveletTree.Length; nodeIndex++)
    {
        var node = waveletTree[nodeIndex];
        var baseOffset = node.Y * width + node.X;

        GetLetsDouble(
            temporaryBuffer,
            0,
            workingWaveletData,
            baseOffset,
            node.Height,
            node.Width,
            width,
            1,
            highPassFilter,
            lowPassFilter,
            node.InvertRows);

        GetLetsDouble(
            workingWaveletData,
            baseOffset,
            temporaryBuffer,
            0,
            node.Width,
            node.Height,
            1,
            width,
            highPassFilter,
            lowPassFilter,
            node.InvertColumns);
    }

    return workingWaveletData;
}

static void GetLetsDouble(
    Span<double> destination,
    int destinationBaseOffset,
    ReadOnlySpan<double> source,
    int sourceBaseOffset,
    int lineCount,
    int lineLength,
    int linePitch,
    int sampleStride,
    double[] highPassFilter,
    double[] lowPassFilter,
    bool invertSubbands)
{
    var destinationSamples = destination[destinationBaseOffset..];
    var sourceSamples = source[sourceBaseOffset..];
    var dataLengthIsOdd = lineLength % 2;
    var filterLengthIsOdd = lowPassFilter.Length % 2;
    var positiveStride = sampleStride;
    var negativeStride = -positiveStride;
    int lowPassSampleCount;
    int highPassSampleCount;

    if (dataLengthIsOdd != 0)
    {
        lowPassSampleCount = (lineLength + 1) / 2;
        highPassSampleCount = lowPassSampleCount - 1;
    }
    else
    {
        lowPassSampleCount = lineLength / 2;
        highPassSampleCount = lowPassSampleCount;
    }

    int lowPassCenterOffset;
    int highPassCenterOffset;
    int initialLowPassLeftEdge;
    int initialHighPassLeftEdge;
    var initialLowPassRightEdge = 0;
    var initialHighPassRightEdge = 0;

    if (filterLengthIsOdd != 0)
    {
        lowPassCenterOffset = (lowPassFilter.Length - 1) / 2;
        highPassCenterOffset = (highPassFilter.Length - 1) / 2 - 1;
        initialLowPassLeftEdge = 0;
        initialHighPassLeftEdge = 0;
    }
    else
    {
        lowPassCenterOffset = lowPassFilter.Length / 2 - 2;
        highPassCenterOffset = highPassFilter.Length / 2 - 2;
        initialLowPassLeftEdge = 1;
        initialHighPassLeftEdge = 1;
        initialLowPassRightEdge = 1;
        initialHighPassRightEdge = 1;

        if (lowPassCenterOffset == -1)
        {
            lowPassCenterOffset = 0;
            initialLowPassLeftEdge = 0;
        }

        if (highPassCenterOffset == -1)
        {
            highPassCenterOffset = 0;
            initialHighPassLeftEdge = 0;
        }

        for (var filterIndex = 0; filterIndex < highPassFilter.Length; filterIndex++)
        {
            highPassFilter[filterIndex] *= -1.0;
        }
    }

    for (var lineIndex = 0; lineIndex < lineCount; lineIndex++)
    {
        int lowPassWriteIndex;
        int highPassWriteIndex;

        if (invertSubbands)
        {
            highPassWriteIndex = lineIndex * linePitch;
            lowPassWriteIndex = highPassWriteIndex + highPassSampleCount * sampleStride;
        }
        else
        {
            lowPassWriteIndex = lineIndex * linePitch;
            highPassWriteIndex = lowPassWriteIndex + lowPassSampleCount * sampleStride;
        }

        var firstSourceIndex = lineIndex * linePitch;
        var lastSourceIndex = firstSourceIndex + (lineLength - 1) * sampleStride;

        var lowPassSourceIndex = firstSourceIndex + lowPassCenterOffset * sampleStride;
        var lowPassSourceStride = negativeStride;
        var lowPassLeftEdge = initialLowPassLeftEdge;
        var lowPassRightEdge = initialLowPassRightEdge;

        var highPassSourceIndex = firstSourceIndex + highPassCenterOffset * sampleStride;
        var highPassSourceStride = negativeStride;
        var highPassLeftEdge = initialHighPassLeftEdge;
        var highPassRightEdge = initialHighPassRightEdge;

        for (var sampleIndex = 0; sampleIndex < highPassSampleCount; sampleIndex++)
        {
            var currentLowPassSourceStride = lowPassSourceStride;
            var currentLowPassSourceIndex = lowPassSourceIndex;
            var currentLowPassLeftEdge = lowPassLeftEdge;
            var currentLowPassRightEdge = lowPassRightEdge;
            destinationSamples[lowPassWriteIndex] = sourceSamples[currentLowPassSourceIndex] * lowPassFilter[0];

            for (var filterIndex = 1; filterIndex < lowPassFilter.Length; filterIndex++)
            {
                if (currentLowPassSourceIndex == firstSourceIndex)
                {
                    if (currentLowPassLeftEdge != 0)
                    {
                        currentLowPassSourceStride = 0;
                        currentLowPassLeftEdge = 0;
                    }
                    else
                    {
                        currentLowPassSourceStride = positiveStride;
                    }
                }

                if (currentLowPassSourceIndex == lastSourceIndex)
                {
                    if (currentLowPassRightEdge != 0)
                    {
                        currentLowPassSourceStride = 0;
                        currentLowPassRightEdge = 0;
                    }
                    else
                    {
                        currentLowPassSourceStride = negativeStride;
                    }
                }

                currentLowPassSourceIndex += currentLowPassSourceStride;
                destinationSamples[lowPassWriteIndex] = Math.FusedMultiplyAdd(
                    sourceSamples[currentLowPassSourceIndex],
                    lowPassFilter[filterIndex],
                    destinationSamples[lowPassWriteIndex]);
            }

            lowPassWriteIndex += sampleStride;

            var currentHighPassSourceStride = highPassSourceStride;
            var currentHighPassSourceIndex = highPassSourceIndex;
            var currentHighPassLeftEdge = highPassLeftEdge;
            var currentHighPassRightEdge = highPassRightEdge;
            destinationSamples[highPassWriteIndex] = sourceSamples[currentHighPassSourceIndex] * highPassFilter[0];

            for (var filterIndex = 1; filterIndex < highPassFilter.Length; filterIndex++)
            {
                if (currentHighPassSourceIndex == firstSourceIndex)
                {
                    if (currentHighPassLeftEdge != 0)
                    {
                        currentHighPassSourceStride = 0;
                        currentHighPassLeftEdge = 0;
                    }
                    else
                    {
                        currentHighPassSourceStride = positiveStride;
                    }
                }

                if (currentHighPassSourceIndex == lastSourceIndex)
                {
                    if (currentHighPassRightEdge != 0)
                    {
                        currentHighPassSourceStride = 0;
                        currentHighPassRightEdge = 0;
                    }
                    else
                    {
                        currentHighPassSourceStride = negativeStride;
                    }
                }

                currentHighPassSourceIndex += currentHighPassSourceStride;
                destinationSamples[highPassWriteIndex] = Math.FusedMultiplyAdd(
                    sourceSamples[currentHighPassSourceIndex],
                    highPassFilter[filterIndex],
                    destinationSamples[highPassWriteIndex]);
            }

            highPassWriteIndex += sampleStride;

            for (var advanceIndex = 0; advanceIndex < 2; advanceIndex++)
            {
                if (lowPassSourceIndex == firstSourceIndex)
                {
                    if (lowPassLeftEdge != 0)
                    {
                        lowPassSourceStride = 0;
                        lowPassLeftEdge = 0;
                    }
                    else
                    {
                        lowPassSourceStride = positiveStride;
                    }
                }

                lowPassSourceIndex += lowPassSourceStride;

                if (highPassSourceIndex == firstSourceIndex)
                {
                    if (highPassLeftEdge != 0)
                    {
                        highPassSourceStride = 0;
                        highPassLeftEdge = 0;
                    }
                    else
                    {
                        highPassSourceStride = positiveStride;
                    }
                }

                highPassSourceIndex += highPassSourceStride;
            }
        }

        if (dataLengthIsOdd != 0)
        {
            var currentLowPassSourceStride = lowPassSourceStride;
            var currentLowPassSourceIndex = lowPassSourceIndex;
            var currentLowPassLeftEdge = lowPassLeftEdge;
            var currentLowPassRightEdge = lowPassRightEdge;
            destinationSamples[lowPassWriteIndex] = sourceSamples[currentLowPassSourceIndex] * lowPassFilter[0];

            for (var filterIndex = 1; filterIndex < lowPassFilter.Length; filterIndex++)
            {
                if (currentLowPassSourceIndex == firstSourceIndex)
                {
                    if (currentLowPassLeftEdge != 0)
                    {
                        currentLowPassSourceStride = 0;
                        currentLowPassLeftEdge = 0;
                    }
                    else
                    {
                        currentLowPassSourceStride = positiveStride;
                    }
                }

                if (currentLowPassSourceIndex == lastSourceIndex)
                {
                    if (currentLowPassRightEdge != 0)
                    {
                        currentLowPassSourceStride = 0;
                        currentLowPassRightEdge = 0;
                    }
                    else
                    {
                        currentLowPassSourceStride = negativeStride;
                    }
                }

                currentLowPassSourceIndex += currentLowPassSourceStride;
                destinationSamples[lowPassWriteIndex] = Math.FusedMultiplyAdd(
                    sourceSamples[currentLowPassSourceIndex],
                    lowPassFilter[filterIndex],
                    destinationSamples[lowPassWriteIndex]);
            }
        }
    }

    if (filterLengthIsOdd == 0)
    {
        for (var filterIndex = 0; filterIndex < highPassFilter.Length; filterIndex++)
        {
            highPassFilter[filterIndex] *= -1.0;
        }
    }
}

static short[] QuantizeDoublePrecisionWaveletData(
    ReadOnlySpan<double> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    double bitRate)
{
    var variances = ComputeVariancesWithReferencePrecision(waveletData, quantizationTree, width);
    var quantizationBins = new double[WsqConstants.MaxSubbands];
    var zeroBins = new double[WsqConstants.MaxSubbands];
    ComputeQuantizationBinsWithReferencePrecision(variances, bitRate, quantizationBins, zeroBins);

    var quantizedCoefficients = new short[waveletData.Length];
    var coefficientIndex = 0;

    for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
    {
        if (quantizationBins[subband].CompareTo(0.0) == 0)
        {
            continue;
        }

        var node = quantizationTree[subband];
        var halfZeroBin = zeroBins[subband] / 2.0;
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
                else if (coefficient > 0.0)
                {
                    quantizedCoefficient = checked((short)(((coefficient - halfZeroBin) / quantizationBins[subband]) + 1.0));
                }
                else
                {
                    quantizedCoefficient = checked((short)(((coefficient + halfZeroBin) / quantizationBins[subband]) - 1.0));
                }

                quantizedCoefficients[coefficientIndex++] = quantizedCoefficient;
            }
        }
    }

    Array.Resize(ref quantizedCoefficients, coefficientIndex);
    return quantizedCoefficients;
}

static WsqQuantizationTable CreateQuantizationTableFromDoubleWaveletData(
    ReadOnlySpan<double> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    double bitRate)
{
    var variances = ComputeVariancesWithReferencePrecision(waveletData, quantizationTree, width);
    var quantizationBins = new double[WsqConstants.MaxSubbands];
    var zeroBins = new double[WsqConstants.MaxSubbands];
    ComputeQuantizationBinsWithReferencePrecision(variances, bitRate, quantizationBins, zeroBins);
    return new(44.0, quantizationBins, zeroBins);
}

static double[] ComputeVariancesFromFloatWaveletDataWithReferencePrecision(
    ReadOnlySpan<float> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width)
{
    var doublePrecisionWaveletData = new double[waveletData.Length];

    for (var index = 0; index < waveletData.Length; index++)
    {
        doublePrecisionWaveletData[index] = waveletData[index];
    }

    return ComputeVariancesWithReferencePrecision(doublePrecisionWaveletData, quantizationTree, width);
}

static double[] ComputeVariancesWithReferencePrecision(
    ReadOnlySpan<double> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width)
{
    var variances = new double[WsqConstants.MaxSubbands];
    var varianceSum = 0.0;

    for (var subband = 0; subband < WsqConstants.StartSizeRegion2; subband++)
    {
        variances[subband] = ComputeVarianceWithReferencePrecision(
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
            variances[subband] = ComputeVarianceWithReferencePrecision(
                waveletData,
                quantizationTree[subband],
                width,
                useCroppedRegion: false);
        }

        return variances;
    }

    for (var subband = WsqConstants.StartSizeRegion2; subband < WsqConstants.NumberOfSubbands; subband++)
    {
        variances[subband] = ComputeVarianceWithReferencePrecision(
            waveletData,
            quantizationTree[subband],
            width,
            useCroppedRegion: true);
    }

    return variances;
}

static double ComputeVarianceWithReferencePrecision(
    ReadOnlySpan<double> waveletData,
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
    var normalizedSum = (pixelSum * pixelSum) / sampleCount;
    return (squaredSum - normalizedSum) / (sampleCount - 1.0);
}

static void ComputeQuantizationBinsWithReferencePrecision(
    ReadOnlySpan<double> variances,
    double bitRate,
    Span<double> quantizationBins,
    Span<double> zeroBins)
{
    var reciprocalSubbandAreas = new double[WsqConstants.NumberOfSubbands];
    var sigma = new double[WsqConstants.NumberOfSubbands];
    var initialSubbands = new int[WsqConstants.NumberOfSubbands];
    var workingSubbands = new int[WsqConstants.NumberOfSubbands];
    var positiveBitRateFlags = new bool[WsqConstants.NumberOfSubbands];

    SetReferencePrecisionReciprocalSubbandAreas(reciprocalSubbandAreas);

    var initialSubbandCount = 0;
    for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
    {
        if (variances[subband] < WsqConstants.VarianceThreshold)
        {
            quantizationBins[subband] = 0.0;
            zeroBins[subband] = 0.0;
            continue;
        }

        sigma[subband] = Math.Sqrt(variances[subband]);
        quantizationBins[subband] = subband < WsqConstants.StartSizeRegion2
            ? 1.0
            : 10.0 / (GetReferencePrecisionSubbandWeight(subband) * Math.Log(variances[subband]));
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
                sigma[subband] / quantizationBins[subband],
                reciprocalSubbandAreas[subband]);
        }

        var quantizationScale = (Math.Pow(2.0, (bitRate / reciprocalAreaSum) - 1.0) / 2.5)
            / Math.Pow(product, 1.0 / reciprocalAreaSum);
        var nonPositiveBitRateCount = 0;

        Array.Clear(positiveBitRateFlags);
        for (var index = 0; index < activeSubbandCount; index++)
        {
            var subband = activeSubbands[index];
            if ((quantizationBins[subband] / quantizationScale) >= (5.0 * sigma[subband]))
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
                    : 0.0;
                zeroBins[subband] = 1.2 * quantizationBins[subband];
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

static void ComputeQuantizationBinsWithCurrentPrecision(
    ReadOnlySpan<float> variances,
    float bitRate,
    Span<float> quantizationBins,
    Span<float> zeroBins)
{
    var reciprocalSubbandAreas = new double[WsqConstants.NumberOfSubbands];
    var sigma = new double[WsqConstants.NumberOfSubbands];
    var initialSubbands = new int[WsqConstants.NumberOfSubbands];
    var workingSubbands = new int[WsqConstants.NumberOfSubbands];
    var positiveBitRateFlags = new bool[WsqConstants.NumberOfSubbands];

    SetReferencePrecisionReciprocalSubbandAreas(reciprocalSubbandAreas);

    var initialSubbandCount = 0;
    for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
    {
        if (variances[subband] < WsqConstants.VarianceThreshold)
        {
            quantizationBins[subband] = 0.0f;
            zeroBins[subband] = 0.0f;
            continue;
        }

        sigma[subband] = Math.Sqrt(variances[subband]);
        quantizationBins[subband] = subband < WsqConstants.StartSizeRegion2
            ? 1.0f
            : (float)(10.0 / (GetReferencePrecisionSubbandWeight(subband) * Math.Log(variances[subband])));
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
                sigma[subband] / quantizationBins[subband],
                reciprocalSubbandAreas[subband]);
        }

        var quantizationScale = (Math.Pow(2.0, (bitRate / reciprocalAreaSum) - 1.0) / 2.5)
            / Math.Pow(product, 1.0 / reciprocalAreaSum);
        var nonPositiveBitRateCount = 0;

        Array.Clear(positiveBitRateFlags);
        for (var index = 0; index < activeSubbandCount; index++)
        {
            var subband = activeSubbands[index];
            if ((quantizationBins[subband] / quantizationScale) >= (5.0 * sigma[subband]))
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
                    ? (float)(quantizationBins[subband] / quantizationScale)
                    : 0.0f;
                zeroBins[subband] = (float)(1.2 * quantizationBins[subband]);
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

static void SetReferencePrecisionReciprocalSubbandAreas(Span<double> reciprocalSubbandAreas)
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

static double GetReferencePrecisionSubbandWeight(int subband)
{
    return subband switch
    {
        52 => 1.32,
        53 => 1.08,
        54 => 1.42,
        55 => 1.08,
        56 => 1.32,
        57 => 1.42,
        58 => 1.08,
        59 => 1.08,
        _ => 1.0,
    };
}

static float ComputeVariance(
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

static List<RawImageDimensions> LoadDimensions()
{
    var dimensionsPath = Path.Combine(
        RepoRoot,
        "OpenNist.Tests",
        "TestData",
        "Wsq",
        "NistReferenceImages",
        "V2_0",
        "raw-image-dimensions.json");
    return JsonSerializer.Deserialize<List<RawImageDimensions>>(
        File.ReadAllText(dimensionsPath),
        new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        })
        ?? throw new InvalidOperationException("Failed to read raw image dimensions.");
}

internal sealed record RawImageDimensions(string FileName, int Width, int Height);

internal sealed record HighPrecisionAnalysisArtifacts(
    WsqDoubleNormalizedImage DoubleNormalizedImage,
    float[] FloatNormalizedPixels,
    double[] DoubleDecomposedPixels,
    float[] FloatDecomposedPixels);

internal sealed record HighPrecisionQuantizationArtifacts(
    double[] Variances,
    double[] QuantizationBins,
    double[] ZeroBins);

internal readonly record struct WsqReferenceQuantizedCoefficients(
    WsqFrameHeader FrameHeader,
    WsqQuantizationTable QuantizationTable,
    short[] QuantizedCoefficients);

internal readonly record struct DoubleNormalizedImage(
    double[] Pixels,
    double Shift,
    double Scale);

internal readonly record struct NbisAnalysisDump(
    double Shift,
    double Scale,
    double[] QuantizationBins,
    double[] ZeroBins,
    double[] Variances,
    short[] QuantizedCoefficients);
