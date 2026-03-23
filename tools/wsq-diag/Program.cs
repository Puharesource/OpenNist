using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using OpenNist.Wsq;
using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Encoding;

const string RepoRoot = "/Users/pmtar/Development/Projects/OpenNist";

if (args.Length > 0 && string.Equals(args[0], "--nbis-report", StringComparison.Ordinal))
{
    await ReportNbisParityAsync();
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--nbis-report-float-high-rate", StringComparison.Ordinal))
{
    await ReportNbisParityAsync(useFloatHighRatePath: true);
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--nbis-report-double-source", StringComparison.Ordinal))
{
    await ReportNbisParityAsync(useDoubleSourcePath: true);
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--nbis-report-float-low-rate", StringComparison.Ordinal))
{
    await ReportNbisParityAsync(useFloatLowRatePath: true);
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--nbis-codestream-report", StringComparison.Ordinal))
{
    await ReportNbisCodestreamParityAsync();
    return;
}

if (args.Length > 2 && string.Equals(args[0], "--nbis-codestream-diff", StringComparison.Ordinal))
{
    await ReportNbisCodestreamDiffAsync(args[1], double.Parse(args[2], CultureInfo.InvariantCulture));
    return;
}

if (args.Length > 3 && string.Equals(args[0], "--nbis-bin-at", StringComparison.Ordinal))
{
    await ReportNbisBinAtAsync(args[1], double.Parse(args[2], CultureInfo.InvariantCulture), int.Parse(args[3], CultureInfo.InvariantCulture));
    return;
}

if (args.Length > 2 && string.Equals(args[0], "--nbis-stage-at", StringComparison.Ordinal))
{
    await ReportNbisStageAtAsync(args[1], double.Parse(args[2], CultureInfo.InvariantCulture));
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--nist-report", StringComparison.Ordinal))
{
    await ReportNistParityAsync();
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--nist-guard-report", StringComparison.Ordinal))
{
    await ReportNistGuardCasesAsync();
    return;
}

var fileName = args.Length > 0 ? args[0] : "cmp00001.raw";
var bitRate = args.Length > 1 ? double.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 2.25;
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
var normalizedImage = WsqFloatImageNormalizer.Normalize(rawBytes);
var decomposedWaveletData = WsqDecomposition.Decompose(
    normalizedImage.Pixels.ToArray(),
    rawImage.Width,
    rawImage.Height,
    waveletTree,
    WsqReferenceTables.StandardTransformTable);
var literalDecomposedWaveletData = DecomposeLiterally(
    normalizedImage.Pixels.ToArray(),
    rawImage.Width,
    rawImage.Height,
    waveletTree,
    WsqReferenceTables.StandardTransformTable);

var coefficientsUsingReferenceBins = QuantizeWithTable(
    decomposedWaveletData,
    quantizationTree,
    rawImage.Width,
    reference.QuantizationTable);
var literalCoefficientsUsingReferenceBins = QuantizeWithTable(
    literalDecomposedWaveletData,
    quantizationTree,
    rawImage.Width,
    reference.QuantizationTable);

var coefficientsUsingAnalysisBins = QuantizeWithTable(
    decomposedWaveletData,
    quantizationTree,
    rawImage.Width,
    analysis.QuantizationTable);
var literalCoefficientsUsingAnalysisBins = QuantizeWithTable(
    literalDecomposedWaveletData,
    quantizationTree,
    rawImage.Width,
    analysis.QuantizationTable);
var doubleNormalizedImage = WsqDoubleImageNormalizer.Normalize(rawBytes);
var highRateDecomposedWaveletData = WsqDoubleDecomposition.Decompose(
    doubleNormalizedImage.Pixels,
    rawImage.Width,
    rawImage.Height,
    waveletTree,
    WsqReferenceTables.CreateStandardTransformTable());
var rawHighPrecisionArtifacts = WsqHighPrecisionQuantizer.CreateQuantizationArtifacts(
    highRateDecomposedWaveletData,
    quantizationTree,
    rawImage.Width,
    bitRate);
var currentHighPrecisionTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
    rawHighPrecisionArtifacts.Variances,
    bitRate,
    new(UseSinglePrecisionScaleFactor: true));
var singlePrecisionSigmaInitialTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
    rawHighPrecisionArtifacts.Variances,
    bitRate,
    new(UseSinglePrecisionSigma: true, UseSinglePrecisionInitialQuantizationBins: true));
var literalSinglePrecisionSigmaInitialTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
    rawHighPrecisionArtifacts.Variances,
    bitRate,
    new(UseSinglePrecisionSigma: true, UseLiteralSinglePrecisionInitialQuantizationBins: true));
var singlePrecisionSigmaInitialAndScaleTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
    rawHighPrecisionArtifacts.Variances,
    bitRate,
    new(
        UseSinglePrecisionSigma: true,
        UseSinglePrecisionInitialQuantizationBins: true,
        UseSinglePrecisionScaleFactor: true));
var literalSinglePrecisionSigmaInitialAndScaleTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
    rawHighPrecisionArtifacts.Variances,
    bitRate,
    new(
        UseSinglePrecisionSigma: true,
        UseLiteralSinglePrecisionInitialQuantizationBins: true,
        UseSinglePrecisionScaleFactor: true));
var singlePrecisionProductTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
    rawHighPrecisionArtifacts.Variances,
    bitRate,
    new(UseSinglePrecisionProduct: true));
var singlePrecisionProductAndScaleTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
    rawHighPrecisionArtifacts.Variances,
    bitRate,
    new(UseSinglePrecisionProduct: true, UseSinglePrecisionScaleFactor: true));
var allSinglePrecisionTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
    rawHighPrecisionArtifacts.Variances,
    bitRate,
    new(
        UseSinglePrecisionSigma: true,
        UseSinglePrecisionInitialQuantizationBins: true,
        UseSinglePrecisionProduct: true,
        UseSinglePrecisionScaleFactor: true));
var floatVarianceSpliced = rawHighPrecisionArtifacts.Variances.ToArray();
var doubleSinglePrecisionAccumulationVarianceSpliced = rawHighPrecisionArtifacts.Variances.ToArray();
var floatHighPrecisionArtifacts = WsqHighPrecisionQuantizer.CreateQuantizationArtifacts(
    decomposedWaveletData,
    quantizationTree,
    rawImage.Width,
    bitRate);
var floatHighPrecisionVariances = WsqHighPrecisionVarianceCalculator.Compute(
    decomposedWaveletData,
    quantizationTree,
    rawImage.Width);
var doubleSinglePrecisionAccumulationVariances = WsqHighPrecisionVarianceCalculator.ComputeWithSinglePrecisionAccumulation(
    highRateDecomposedWaveletData,
    quantizationTree,
    rawImage.Width);
var highRateWaveletDataAsFloat = Array.ConvertAll(highRateDecomposedWaveletData, static value => (float)value);
var castFloatHighPrecisionArtifacts = WsqHighPrecisionQuantizer.CreateQuantizationArtifacts(
    highRateWaveletDataAsFloat,
    quantizationTree,
    rawImage.Width,
    bitRate);
var highRateCoefficientsUsingFloatHighPrecisionBins = WsqCoefficientQuantizer.Quantize(
    highRateDecomposedWaveletData,
    quantizationTree,
    rawImage.Width,
    floatHighPrecisionArtifacts.QuantizationBins,
    floatHighPrecisionArtifacts.ZeroBins);
var highRateCoefficientsUsingCastFloatHighPrecisionBins = WsqCoefficientQuantizer.Quantize(
    highRateDecomposedWaveletData,
    quantizationTree,
    rawImage.Width,
    castFloatHighPrecisionArtifacts.QuantizationBins,
    castFloatHighPrecisionArtifacts.ZeroBins);
var floatCoefficientsUsingFloatHighPrecisionBins = WsqCoefficientQuantizer.Quantize(
    decomposedWaveletData,
    quantizationTree,
    rawImage.Width,
    floatHighPrecisionArtifacts.QuantizationBins,
    floatHighPrecisionArtifacts.ZeroBins);
var subbandZeroNbisQuantizationBins = rawHighPrecisionArtifacts.QuantizationBins.ToArray();
var subbandZeroNbisZeroBins = rawHighPrecisionArtifacts.ZeroBins.ToArray();
subbandZeroNbisQuantizationBins[0] = nbisAnalysis.QuantizationBins[0];
subbandZeroNbisZeroBins[0] = nbisAnalysis.ZeroBins[0];
var highRateCoefficientsUsingSubbandZeroNbisBins = WsqCoefficientQuantizer.Quantize(
    highRateDecomposedWaveletData,
    quantizationTree,
    rawImage.Width,
    subbandZeroNbisQuantizationBins,
    subbandZeroNbisZeroBins);
var subbandZeroBiasedQuantizationBins = rawHighPrecisionArtifacts.QuantizationBins.ToArray();
if (subbandZeroBiasedQuantizationBins[0] > 0.0)
{
    subbandZeroBiasedQuantizationBins[0] *= 0.999996;
}

var highRateCoefficientsUsingSubbandZeroBias = WsqCoefficientQuantizer.Quantize(
    highRateDecomposedWaveletData,
    quantizationTree,
    rawImage.Width,
    subbandZeroBiasedQuantizationBins,
    rawHighPrecisionArtifacts.ZeroBins);
var nbisWaveletData = await ReadNbisWaveletDataAsync(rawPath, dimensions.Width, dimensions.Height);
var firstMismatchIndex = FindFirstMismatchIndex(analysis.QuantizedCoefficients, reference.QuantizedCoefficients);
var refBinMismatchIndex = FindFirstMismatchIndex(coefficientsUsingReferenceBins, reference.QuantizedCoefficients);
var analysisBinMismatchIndex = FindFirstMismatchIndex(coefficientsUsingAnalysisBins, reference.QuantizedCoefficients);
var floatHighPrecisionBinMismatchIndex = FindFirstMismatchIndex(floatCoefficientsUsingFloatHighPrecisionBins, reference.QuantizedCoefficients);
var highRateFloatHighPrecisionBinMismatchIndex = FindFirstMismatchIndex(highRateCoefficientsUsingFloatHighPrecisionBins, reference.QuantizedCoefficients);
var highRateCastFloatHighPrecisionBinMismatchIndex = FindFirstMismatchIndex(highRateCoefficientsUsingCastFloatHighPrecisionBins, reference.QuantizedCoefficients);
var subbandZeroNbisMismatchIndex = FindFirstMismatchIndex(highRateCoefficientsUsingSubbandZeroNbisBins, reference.QuantizedCoefficients);
var subbandZeroBiasMismatchIndex = FindFirstMismatchIndex(highRateCoefficientsUsingSubbandZeroBias, reference.QuantizedCoefficients);

Console.WriteLine($"{fileName} @ {bitRate:0.##} bpp");
Console.WriteLine($"analysis mismatch index      : {FindFirstMismatchIndex(analysis.QuantizedCoefficients, reference.QuantizedCoefficients)}");
Console.WriteLine($"re-quantize with ref bins    : {refBinMismatchIndex}");
Console.WriteLine($"re-quantize with analysis bins: {analysisBinMismatchIndex}");
Console.WriteLine($"literal decompose + ref bins : {FindFirstMismatchIndex(literalCoefficientsUsingReferenceBins, reference.QuantizedCoefficients)}");
Console.WriteLine($"literal decompose + analysis bins: {FindFirstMismatchIndex(literalCoefficientsUsingAnalysisBins, reference.QuantizedCoefficients)}");
Console.WriteLine($"high-rate source + float HP bins: {highRateFloatHighPrecisionBinMismatchIndex}");
Console.WriteLine($"high-rate source + cast-float high-rate bins: {highRateCastFloatHighPrecisionBinMismatchIndex}");
Console.WriteLine($"float source + float HP bins : {floatHighPrecisionBinMismatchIndex}");
Console.WriteLine($"high-rate source + subband0 NBIS bins: {subbandZeroNbisMismatchIndex}");
Console.WriteLine($"high-rate source + subband0 bias : {subbandZeroBiasMismatchIndex}");
Console.WriteLine($"qbin first diff              : {FindFirstBinDifference(analysis.QuantizationTable.QuantizationBins, reference.QuantizationTable.QuantizationBins)}");
Console.WriteLine($"zbin first diff              : {FindFirstBinDifference(analysis.QuantizationTable.ZeroBins, reference.QuantizationTable.ZeroBins)}");
Console.WriteLine($"managed vs NBIS qbin diff    : {FindFirstFloatBinDifference(analysis.QuantizationTable.QuantizationBins, nbisAnalysis.QuantizationBins)}");
Console.WriteLine($"managed vs NBIS zbin diff    : {FindFirstFloatBinDifference(analysis.QuantizationTable.ZeroBins, nbisAnalysis.ZeroBins)}");
Console.WriteLine($"managed vs NBIS coeff mismatch: {FindFirstMismatchIndex(analysis.QuantizedCoefficients, nbisAnalysis.QuantizedCoefficients)}");
if (firstMismatchIndex >= 0)
{
    var firstMismatchLocation = FindCoefficientLocation(analysis.QuantizationTable.QuantizationBins, quantizationTree, firstMismatchIndex);
    var firstMismatchWaveletIndex = (firstMismatchLocation.ImageY * rawImage.Width) + firstMismatchLocation.ImageX;

    Console.WriteLine(
        $"first mismatch location      : subband={firstMismatchLocation.SubbandIndex}, row={firstMismatchLocation.Row}, column={firstMismatchLocation.Column}, x={firstMismatchLocation.ImageX}, y={firstMismatchLocation.ImageY}");
    Console.WriteLine(
        $"first mismatch source        : float={decomposedWaveletData[firstMismatchWaveletIndex]:G17}, literal={literalDecomposedWaveletData[firstMismatchWaveletIndex]:G17}, highRate={highRateDecomposedWaveletData[firstMismatchWaveletIndex]:G17}, nbis={ReadNbisWaveletAtIndex(rawPath, dimensions.Width, dimensions.Height, firstMismatchWaveletIndex):G17}");
    Console.WriteLine(
        $"first mismatch bins          : rawQ={rawHighPrecisionArtifacts.QuantizationBins[firstMismatchLocation.SubbandIndex]:G17}, rawHalfZ={rawHighPrecisionArtifacts.ZeroBins[firstMismatchLocation.SubbandIndex] / 2.0:G17}, serQ={analysis.QuantizationTable.QuantizationBins[firstMismatchLocation.SubbandIndex]:G17}, serHalfZ={analysis.QuantizationTable.ZeroBins[firstMismatchLocation.SubbandIndex] / 2.0:G17}, refQ={reference.QuantizationTable.QuantizationBins[firstMismatchLocation.SubbandIndex]:G17}, refHalfZ={reference.QuantizationTable.ZeroBins[firstMismatchLocation.SubbandIndex] / 2.0:G17}, nbisQ={nbisAnalysis.QuantizationBins[firstMismatchLocation.SubbandIndex]:G17}, nbisHalfZ={nbisAnalysis.ZeroBins[firstMismatchLocation.SubbandIndex] / 2.0:G17}");
    Console.WriteLine(
        $"first mismatch coeffs        : managed={analysis.QuantizedCoefficients[firstMismatchIndex]}, ref={reference.QuantizedCoefficients[firstMismatchIndex]}, nbis={nbisAnalysis.QuantizedCoefficients[firstMismatchIndex]}");
}
else
{
    Console.WriteLine("first mismatch location      : none");
}

PrintVariantMismatchLocation("ref-bin mismatch location     ", refBinMismatchIndex, analysis.QuantizationTable.QuantizationBins, quantizationTree);
PrintVariantMismatchLocation("analysis-bin mismatch location", analysisBinMismatchIndex, analysis.QuantizationTable.QuantizationBins, quantizationTree);
PrintVariantMismatchLocation("float HP mismatch location    ", floatHighPrecisionBinMismatchIndex, analysis.QuantizationTable.QuantizationBins, quantizationTree);
PrintVariantMismatchLocation("hi-rate+floatHP mismatch loc  ", highRateFloatHighPrecisionBinMismatchIndex, analysis.QuantizationTable.QuantizationBins, quantizationTree);
PrintVariantMismatchLocation("hi-rate+castFloatHP mismatch  ", highRateCastFloatHighPrecisionBinMismatchIndex, analysis.QuantizationTable.QuantizationBins, quantizationTree);
PrintVariantMismatchLocation("subband0 NBIS mismatch loc    ", subbandZeroNbisMismatchIndex, analysis.QuantizationTable.QuantizationBins, quantizationTree);
PrintVariantMismatchLocation("subband0 bias mismatch loc    ", subbandZeroBiasMismatchIndex, analysis.QuantizationTable.QuantizationBins, quantizationTree);
PrintVariantMismatchDetails(
    "subband0 bias mismatch detail ",
    subbandZeroBiasMismatchIndex,
    highRateCoefficientsUsingSubbandZeroBias,
    reference.QuantizedCoefficients,
    nbisAnalysis.QuantizedCoefficients,
    subbandZeroBiasedQuantizationBins,
    rawHighPrecisionArtifacts.ZeroBins,
    decomposedWaveletData,
    highRateDecomposedWaveletData,
    nbisWaveletData,
    nbisAnalysis.QuantizationBins,
    nbisAnalysis.ZeroBins,
    reference.QuantizationTable,
    quantizationTree,
    rawImage.Width);

if (subbandZeroBiasMismatchIndex >= 0)
{
    var followOnLocation = FindCoefficientLocation(reference.QuantizationTable.QuantizationBins, quantizationTree, subbandZeroBiasMismatchIndex);
    var subband = followOnLocation.SubbandIndex;
    floatVarianceSpliced[subband] = floatHighPrecisionVariances[subband];
    doubleSinglePrecisionAccumulationVarianceSpliced[subband] = doubleSinglePrecisionAccumulationVariances[subband];
    var floatVarianceSplicedTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(floatVarianceSpliced, bitRate);
    var doubleSinglePrecisionAccumulationVarianceSplicedTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(doubleSinglePrecisionAccumulationVarianceSpliced, bitRate);
    Console.WriteLine(
        $"follow-on qbin variants      : subband={subband}, current={currentHighPrecisionTrace.QuantizationBins[subband]:G17}, sigmaInit={singlePrecisionSigmaInitialTrace.QuantizationBins[subband]:G17}, literalSigmaInit={literalSinglePrecisionSigmaInitialTrace.QuantizationBins[subband]:G17}, sigmaInitScale={singlePrecisionSigmaInitialAndScaleTrace.QuantizationBins[subband]:G17}, literalSigmaInitScale={literalSinglePrecisionSigmaInitialAndScaleTrace.QuantizationBins[subband]:G17}, product={singlePrecisionProductTrace.QuantizationBins[subband]:G17}, productScale={singlePrecisionProductAndScaleTrace.QuantizationBins[subband]:G17}, allFloat={allSinglePrecisionTrace.QuantizationBins[subband]:G17}, floatVarSplice={floatVarianceSplicedTrace.QuantizationBins[subband]:G17}, doubleFloatAccumVarSplice={doubleSinglePrecisionAccumulationVarianceSplicedTrace.QuantizationBins[subband]:G17}, nbis={nbisAnalysis.QuantizationBins[subband]:G17}, ref={reference.QuantizationTable.QuantizationBins[subband]:G17}");
    Console.WriteLine(
        $"follow-on scale variants     : current={currentHighPrecisionTrace.QuantizationScale:G17}, sigmaInit={singlePrecisionSigmaInitialTrace.QuantizationScale:G17}, sigmaInitScale={singlePrecisionSigmaInitialAndScaleTrace.QuantizationScale:G17}, product={singlePrecisionProductTrace.QuantizationScale:G17}, productScale={singlePrecisionProductAndScaleTrace.QuantizationScale:G17}, allFloat={allSinglePrecisionTrace.QuantizationScale:G17}");
    Console.WriteLine(
        $"follow-on init hybrids       : currentInit={currentHighPrecisionTrace.InitialQuantizationBins[subband]:G17}, sigmaInit={singlePrecisionSigmaInitialTrace.InitialQuantizationBins[subband]:G17}, literalSigmaInit={literalSinglePrecisionSigmaInitialTrace.InitialQuantizationBins[subband]:G17}, sigmaInit/currentScale={singlePrecisionSigmaInitialTrace.InitialQuantizationBins[subband] / currentHighPrecisionTrace.QuantizationScale:G17}, literalSigmaInit/currentScale={literalSinglePrecisionSigmaInitialTrace.InitialQuantizationBins[subband] / currentHighPrecisionTrace.QuantizationScale:G17}, currentInit/sigmaScale={currentHighPrecisionTrace.InitialQuantizationBins[subband] / singlePrecisionSigmaInitialAndScaleTrace.QuantizationScale:G17}");
    Console.WriteLine(
        $"follow-on variance variants  : subband={subband}, current={rawHighPrecisionArtifacts.Variances[subband]:G17}, float={floatHighPrecisionVariances[subband]:G17}, doubleFloatAccum={doubleSinglePrecisionAccumulationVariances[subband]:G17}");
}

static async Task ReportNbisParityAsync(
    bool useFloatHighRatePath = false,
    bool useDoubleSourcePath = false,
    bool useFloatLowRatePath = false)
{
    var dimensionsByFileName = LoadDimensions().ToDictionary(static dimensions => dimensions.FileName, StringComparer.Ordinal);
    var exactCases = new List<string>();
    var mismatchCases = new List<string>();

    foreach (var fileName in dimensionsByFileName.Keys.OrderBy(static value => value, StringComparer.Ordinal))
    {
        foreach (var bitRate in new[] { 0.75, 2.25 })
        {
            var dimensions = dimensionsByFileName[fileName];
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
            var rawBytes = await File.ReadAllBytesAsync(rawPath).ConfigureAwait(false);
            var rawImage = new WsqRawImageDescription(dimensions.Width, dimensions.Height, 8, 500);
            var analysis = useDoubleSourcePath
                ? AnalyzeWithDoubleSourcePath(rawBytes, rawImage, bitRate)
                : useFloatLowRatePath
                ? AnalyzeWithFloatLowRatePath(rawBytes, rawImage, bitRate)
                : useFloatHighRatePath
                ? AnalyzeWithFloatHighRatePath(rawBytes, rawImage, bitRate)
                : WsqEncoderAnalysisPipeline.Analyze(rawBytes, rawImage, new WsqEncodeOptions(bitRate));
            var nbisAnalysis = await ReadNbisAnalysisAsync(rawPath, dimensions.Width, dimensions.Height, bitRate).ConfigureAwait(false);

            WsqWaveletTreeBuilder.Build(dimensions.Width, dimensions.Height, out var waveletTree, out var quantizationTree);
            var nbisQuantizationTable = WsqQuantizationTableFactory.Create(
                nbisAnalysis.QuantizationBins,
                nbisAnalysis.ZeroBins);
            var nbisBlockSizes = WsqQuantizationDecoder.ComputeBlockSizes(nbisQuantizationTable, waveletTree, quantizationTree);
            var caseLabel = $"{fileName} @ {bitRate:0.##}";

            if (!analysis.BlockSizes.SequenceEqual(nbisBlockSizes))
            {
                mismatchCases.Add($"{caseLabel} (block sizes [{string.Join(", ", analysis.BlockSizes)}] vs NBIS [{string.Join(", ", nbisBlockSizes)}])");
                continue;
            }

            var mismatchIndex = FindFirstMismatchIndex(analysis.QuantizedCoefficients, nbisAnalysis.QuantizedCoefficients);
            if (mismatchIndex < 0)
            {
                exactCases.Add(caseLabel);
                continue;
            }

            var coefficientLocation = FindCoefficientLocation(analysis.QuantizationTable.QuantizationBins, quantizationTree, mismatchIndex);
            mismatchCases.Add(
                $"{caseLabel} (coeff {mismatchIndex}: actual={analysis.QuantizedCoefficients[mismatchIndex]}, nbis={nbisAnalysis.QuantizedCoefficients[mismatchIndex]}, "
                + $"loc=subband {coefficientLocation.SubbandIndex}, row {coefficientLocation.Row}, col {coefficientLocation.Column}, "
                + $"qbin={FindFirstFloatBinDifference(analysis.QuantizationTable.QuantizationBins, nbisAnalysis.QuantizationBins)}, "
                + $"zbin={FindFirstFloatBinDifference(analysis.QuantizationTable.ZeroBins, nbisAnalysis.ZeroBins)})");
        }
    }

    Console.WriteLine(useDoubleSourcePath
        ? $"Exact NBIS parity cases (double source): {exactCases.Count}"
        : useFloatLowRatePath
        ? $"Exact NBIS parity cases (float low-rate): {exactCases.Count}"
        : useFloatHighRatePath
        ? $"Exact NBIS parity cases (float high-rate): {exactCases.Count}"
        : $"Exact NBIS parity cases: {exactCases.Count}");
    foreach (var exactCase in exactCases)
    {
        Console.WriteLine($"EXACT {exactCase}");
    }

    Console.WriteLine(useDoubleSourcePath
        ? $"NBIS mismatch cases (double source): {mismatchCases.Count}"
        : useFloatLowRatePath
        ? $"NBIS mismatch cases (float low-rate): {mismatchCases.Count}"
        : useFloatHighRatePath
        ? $"NBIS mismatch cases (float high-rate): {mismatchCases.Count}"
        : $"NBIS mismatch cases: {mismatchCases.Count}");
    foreach (var mismatchCase in mismatchCases)
    {
        Console.WriteLine($"MISMATCH {mismatchCase}");
    }
}

static async Task ReportNbisCodestreamParityAsync()
{
    var dimensionsByFileName = LoadDimensions().ToDictionary(static dimensions => dimensions.FileName, StringComparer.Ordinal);
    var exactCases = new List<string>();
    var sameSizeCases = new List<string>();
    var mismatchCases = new List<string>();

    foreach (var fileName in dimensionsByFileName.Keys.OrderBy(static value => value, StringComparer.Ordinal))
    {
        foreach (var bitRate in new[] { 0.75, 2.25 })
        {
            var dimensions = dimensionsByFileName[fileName];
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
            var rawBytes = await File.ReadAllBytesAsync(rawPath).ConfigureAwait(false);
            var rawImage = new WsqRawImageDescription(dimensions.Width, dimensions.Height, 8, 500);
            var managedBytes = await EncodeManagedAsync(rawBytes, rawImage, bitRate).ConfigureAwait(false);
            var nbisBytes = await EncodeNbisAsync(rawPath, dimensions.Width, dimensions.Height, bitRate).ConfigureAwait(false);
            var caseLabel = $"{fileName} @ {bitRate:0.##}";

            if (managedBytes.AsSpan().SequenceEqual(nbisBytes))
            {
                exactCases.Add(caseLabel);
                continue;
            }

            if (managedBytes.Length == nbisBytes.Length)
            {
                sameSizeCases.Add(caseLabel);
            }

            mismatchCases.Add($"{caseLabel} (managed={managedBytes.Length}, nbis={nbisBytes.Length})");
        }
    }

    Console.WriteLine($"Exact NBIS codestream parity cases: {exactCases.Count}");
    foreach (var exactCase in exactCases)
    {
        Console.WriteLine($"EXACT {exactCase}");
    }

    Console.WriteLine($"Same-size NBIS codestream cases: {sameSizeCases.Count}");
    foreach (var sameSizeCase in sameSizeCases)
    {
        Console.WriteLine($"SAME_SIZE {sameSizeCase}");
    }

    Console.WriteLine($"NBIS codestream mismatch cases: {mismatchCases.Count}");
    foreach (var mismatchCase in mismatchCases)
    {
        Console.WriteLine($"MISMATCH {mismatchCase}");
    }
}

static async Task ReportNbisCodestreamDiffAsync(string fileName, double bitRate)
{
    var dimensions = LoadDimensions().Single(dimensions => string.Equals(dimensions.FileName, fileName, StringComparison.Ordinal));
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
    var rawBytes = await File.ReadAllBytesAsync(rawPath).ConfigureAwait(false);
    var rawImage = new WsqRawImageDescription(dimensions.Width, dimensions.Height, 8, 500);
    var managedBytes = await EncodeManagedAsync(rawBytes, rawImage, bitRate).ConfigureAwait(false);
    var nbisBytes = await EncodeNbisAsync(rawPath, dimensions.Width, dimensions.Height, bitRate).ConfigureAwait(false);
    var firstDiffIndex = FindFirstByteMismatchIndex(managedBytes, nbisBytes);

    Console.WriteLine($"{fileName} @ {bitRate:0.##}");
    Console.WriteLine($"managed length: {managedBytes.Length}");
    Console.WriteLine($"nbis length   : {nbisBytes.Length}");
    Console.WriteLine($"first diff    : {firstDiffIndex}");
    if (firstDiffIndex >= 0)
    {
        Console.WriteLine($"managed loc   : {DescribeByteOffset(managedBytes, firstDiffIndex)}");
        Console.WriteLine($"nbis loc      : {DescribeByteOffset(nbisBytes, firstDiffIndex)}");
        Console.WriteLine($"managed byte  : {managedBytes[firstDiffIndex]}");
        Console.WriteLine($"nbis byte     : {nbisBytes[firstDiffIndex]}");
        Console.WriteLine($"managed slice : {FormatByteWindow(managedBytes, firstDiffIndex)}");
        Console.WriteLine($"nbis slice    : {FormatByteWindow(nbisBytes, firstDiffIndex)}");
    }
}

static string DescribeByteOffset(byte[] bytes, int offset)
{
    if (offset < 0 || offset >= bytes.Length)
    {
        return "out-of-range";
    }

    foreach (var segment in EnumerateSegments(bytes))
    {
        if (offset < segment.StartOffset || offset >= segment.EndOffset)
        {
            continue;
        }

        if (segment.PayloadOffset < 0 || offset < segment.PayloadOffset)
        {
            return $"{segment.Marker} header byte +{offset - segment.StartOffset}";
        }

        var payloadIndex = offset - segment.PayloadOffset;
        if (segment.Marker == WsqMarker.DefineQuantizationTable)
        {
            if (payloadIndex == 0)
            {
                return "DQT bin-center scale";
            }

            if (payloadIndex is 1 or 2)
            {
                return $"DQT bin-center raw byte {payloadIndex}";
            }

            var subbandPayloadIndex = payloadIndex - 3;
            var subband = subbandPayloadIndex / 6;
            var fieldOffset = subbandPayloadIndex % 6;

            var field = fieldOffset switch
            {
                0 => "qbin scale",
                1 => "qbin raw msb",
                2 => "qbin raw lsb",
                3 => "zbin scale",
                4 => "zbin raw msb",
                5 => "zbin raw lsb",
                _ => "unknown"
            };

            return $"DQT subband {subband} {field}";
        }

        if (segment.Marker == WsqMarker.StartOfFrame)
        {
            return $"SOF payload byte {payloadIndex}";
        }

        if (segment.Marker == WsqMarker.DefineTransformTable)
        {
            return $"DTT payload byte {payloadIndex}";
        }

        if (segment.Marker == WsqMarker.DefineHuffmanTable)
        {
            return $"DHT payload byte {payloadIndex}";
        }

        if (segment.Marker == WsqMarker.Comment)
        {
            return $"COM payload byte {payloadIndex}";
        }

        if (segment.Marker == WsqMarker.StartOfBlock)
        {
            return payloadIndex == 0
                ? "SOB table id"
                : $"SOB payload byte {payloadIndex}";
        }

        return $"{segment.Marker} payload byte {payloadIndex}";
    }

    return "entropy-coded data";
}

static async Task ReportNbisBinAtAsync(string fileName, double bitRate, int subband)
{
    var dimensions = LoadDimensions().Single(dimensions => string.Equals(dimensions.FileName, fileName, StringComparison.Ordinal));
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

    var rawBytes = await File.ReadAllBytesAsync(rawPath);
    var rawImage = new WsqRawImageDescription(dimensions.Width, dimensions.Height, 8, 500);
    var analysis = WsqEncoderAnalysisPipeline.Analyze(rawBytes, rawImage, new WsqEncodeOptions(bitRate));
    var nbisAnalysis = await ReadNbisAnalysisAsync(rawPath, dimensions.Width, dimensions.Height, bitRate);
    var nbisWaveletData = await ReadNbisWaveletDataAsync(rawPath, dimensions.Width, dimensions.Height);
    WsqWaveletTreeBuilder.Build(rawImage.Width, rawImage.Height, out var waveletTree, out var quantizationTree);
    var normalizedImage = WsqFloatImageNormalizer.Normalize(rawBytes);
    var decomposedWaveletData = WsqDecomposition.Decompose(
        normalizedImage.Pixels.ToArray(),
        rawImage.Width,
        rawImage.Height,
        waveletTree,
        WsqReferenceTables.StandardTransformTable);
    var managedArtifacts = WsqQuantizer.CreateQuantizationArtifacts(
        decomposedWaveletData,
        quantizationTree,
        rawImage.Width,
        (float)bitRate);
    var doubleNormalizedImage = WsqDoubleImageNormalizer.Normalize(rawBytes);
    var doubleDecomposedWaveletData = WsqDoubleDecomposition.Decompose(
        doubleNormalizedImage.Pixels,
        rawImage.Width,
        rawImage.Height,
        waveletTree,
        WsqReferenceTables.CreateStandardTransformTable());
    var highPrecisionArtifacts = WsqHighPrecisionQuantizer.CreateQuantizationArtifacts(
        doubleDecomposedWaveletData,
        quantizationTree,
        rawImage.Width,
        bitRate);
    var doubleArtifacts = WsqQuantizer.CreateQuantizationArtifacts(
        Array.ConvertAll(doubleDecomposedWaveletData, static value => (float)value),
        quantizationTree,
        rawImage.Width,
        (float)bitRate);
    var literalDecomposedWaveletData = DecomposeLiterally(
        normalizedImage.Pixels.ToArray(),
        rawImage.Width,
        rawImage.Height,
        waveletTree,
        WsqReferenceTables.StandardTransformTable);
    var managedVariances = WsqVarianceCalculator.Compute(
        decomposedWaveletData,
        quantizationTree,
        rawImage.Width);
    var literalVariances = WsqVarianceCalculator.Compute(
        literalDecomposedWaveletData,
        quantizationTree,
        rawImage.Width);
    var managedOnNbisWaveletVariances = WsqVarianceCalculator.Compute(
        nbisWaveletData,
        quantizationTree,
        rawImage.Width);
    var managedOnNbisWaveletVarianceTraces = ComputeVarianceTraces(
        nbisWaveletData,
        quantizationTree,
        rawImage.Width,
        managedOnNbisWaveletVariances);
    var managedTrace = WsqQuantizer.CreateQuantizationTrace(managedVariances, (float)bitRate);
    var literalTrace = WsqQuantizer.CreateQuantizationTrace(literalVariances, (float)bitRate);
    var managedOnNbisWaveletTrace = WsqQuantizer.CreateQuantizationTrace(managedOnNbisWaveletVariances, (float)bitRate);
    var nbisVariances = Array.ConvertAll(nbisAnalysis.Variances, static value => (float)value);
    var nbisTrace = WsqQuantizer.CreateQuantizationTrace(nbisVariances, (float)bitRate);
    var legacyNbisTrace = CreateLowRateQuantizationTrace(nbisVariances, (float)bitRate);
    var managedOnNbisVariancesRawQbin = managedOnNbisWaveletTrace.QuantizationBins[subband];
    var managedOnNbisVariancesRawZbin = managedOnNbisWaveletTrace.ZeroBins[subband];
    var nbisVariancesRawQbin = nbisTrace.QuantizationBins[subband];
    var nbisVariancesRawZbin = nbisTrace.ZeroBins[subband];

    var managedRawQbin = managedArtifacts.QuantizationBins[subband];
    var managedRawZbin = managedArtifacts.ZeroBins[subband];
    var highPrecisionRawQbin = highPrecisionArtifacts.QuantizationBins[subband];
    var highPrecisionRawZbin = highPrecisionArtifacts.ZeroBins[subband];
    var managedQbin = analysis.QuantizationTable.QuantizationBins[subband];
    var managedZbin = analysis.QuantizationTable.ZeroBins[subband];
    var nbisQbin = nbisAnalysis.QuantizationBins[subband];
    var nbisZbin = nbisAnalysis.ZeroBins[subband];

    var managedRawQbinRaw = WsqScaledValueCodec.ScaleToUInt16(managedRawQbin);
    var managedRawZbinRaw = WsqScaledValueCodec.ScaleToUInt16(managedRawZbin);
    var highPrecisionRawQbinRaw = WsqScaledValueCodec.ScaleToUInt16((float)highPrecisionRawQbin);
    var highPrecisionRawZbinRaw = WsqScaledValueCodec.ScaleToUInt16((float)highPrecisionRawZbin);
    var managedOnNbisWaveletRawQbinRaw = WsqScaledValueCodec.ScaleToUInt16(managedOnNbisVariancesRawQbin);
    var managedOnNbisWaveletRawZbinRaw = WsqScaledValueCodec.ScaleToUInt16(managedOnNbisVariancesRawZbin);
    var nbisVariancesRawQbinRaw = WsqScaledValueCodec.ScaleToUInt16(nbisVariancesRawQbin);
    var nbisVariancesRawZbinRaw = WsqScaledValueCodec.ScaleToUInt16(nbisVariancesRawZbin);
    var doubleRawQbinRaw = WsqScaledValueCodec.ScaleToUInt16(doubleArtifacts.QuantizationBins[subband]);
    var doubleRawZbinRaw = WsqScaledValueCodec.ScaleToUInt16(doubleArtifacts.ZeroBins[subband]);
    var managedQbinRaw = WsqScaledValueCodec.ScaleToUInt16((float)managedQbin);
    var managedZbinRaw = WsqScaledValueCodec.ScaleToUInt16((float)managedZbin);
    var nbisQbinRaw = WsqScaledValueCodec.ScaleToUInt16((float)nbisQbin);
    var nbisZbinRaw = WsqScaledValueCodec.ScaleToUInt16((float)nbisZbin);

    Console.WriteLine($"{fileName} @ {bitRate:0.##} subband {subband}");
    Console.WriteLine($"managed shift    : {normalizedImage.Shift:G17}");
    Console.WriteLine($"nbis shift       : {nbisAnalysis.Shift:G17}");
    Console.WriteLine($"managed scale    : {normalizedImage.Scale:G17}");
    Console.WriteLine($"nbis scale       : {nbisAnalysis.Scale:G17}");
    Console.WriteLine($"managed variance : {managedVariances[subband]:G17}");
    Console.WriteLine($"literal variance : {literalVariances[subband]:G17}");
    Console.WriteLine($"managed on nbis wavelet variance : {managedOnNbisWaveletVariances[subband]:G17}");
    Console.WriteLine($"nbis variance    : {nbisVariances[subband]:G17}");
    var firstVarianceDiff = FindFirstVarianceDifference(managedOnNbisWaveletVariances, nbisVariances);
    if (firstVarianceDiff < 0)
    {
        Console.WriteLine("first variance diff index : none");
    }
    else
    {
        Console.WriteLine(
            $"first variance diff index : {firstVarianceDiff} actual={managedOnNbisWaveletVariances[firstVarianceDiff]:G17} expected={nbisVariances[firstVarianceDiff]:G17}");
    }
    var managedNode = ToQuantizationTreeNode(quantizationTree[subband]);
    var nbisNode = nbisAnalysis.QuantizationTree[subband];
    Console.WriteLine(
        $"managed qtree[{subband}] : x={managedNode.X} y={managedNode.Y} w={managedNode.Width} h={managedNode.Height}");
    Console.WriteLine(
        $"nbis qtree[{subband}]    : x={nbisNode.X} y={nbisNode.Y} w={nbisNode.Width} h={nbisNode.Height}");
    var (managedCropX, managedCropY, managedCropWidth, managedCropHeight) = GetVarianceRegion(managedNode, useCroppedRegion: true);
    var (nbisCropX, nbisCropY, nbisCropWidth, nbisCropHeight) = GetVarianceRegion(nbisNode, useCroppedRegion: true);
    Console.WriteLine(
        $"managed crop[{subband}]  : x={managedCropX} y={managedCropY} w={managedCropWidth} h={managedCropHeight}");
    Console.WriteLine(
        $"nbis crop[{subband}]     : x={nbisCropX} y={nbisCropY} w={nbisCropWidth} h={nbisCropHeight}");
    PrintVarianceTrace($"managed vartrace[{subband}]", managedOnNbisWaveletVarianceTraces[subband]);
    PrintVarianceTrace($"nbis vartrace[{subband}]   ", nbisAnalysis.VarianceTraces[subband]);
    if (firstVarianceDiff >= 0)
    {
        var managedVarianceNode = ToQuantizationTreeNode(quantizationTree[firstVarianceDiff]);
        var nbisVarianceNode = nbisAnalysis.QuantizationTree[firstVarianceDiff];
        var managedVarianceCrop = GetVarianceRegion(managedVarianceNode, useCroppedRegion: true);
        var nbisVarianceCrop = GetVarianceRegion(nbisVarianceNode, useCroppedRegion: true);
        Console.WriteLine(
            $"managed qtree[{firstVarianceDiff}] : x={managedVarianceNode.X} y={managedVarianceNode.Y} w={managedVarianceNode.Width} h={managedVarianceNode.Height}");
        Console.WriteLine(
            $"nbis qtree[{firstVarianceDiff}]    : x={nbisVarianceNode.X} y={nbisVarianceNode.Y} w={nbisVarianceNode.Width} h={nbisVarianceNode.Height}");
        Console.WriteLine(
            $"managed crop[{firstVarianceDiff}]  : x={managedVarianceCrop.X} y={managedVarianceCrop.Y} w={managedVarianceCrop.Width} h={managedVarianceCrop.Height}");
        Console.WriteLine(
            $"nbis crop[{firstVarianceDiff}]     : x={nbisVarianceCrop.X} y={nbisVarianceCrop.Y} w={nbisVarianceCrop.Width} h={nbisVarianceCrop.Height}");
        PrintVarianceTrace($"managed vartrace[{firstVarianceDiff}]", managedOnNbisWaveletVarianceTraces[firstVarianceDiff]);
        PrintVarianceTrace($"nbis vartrace[{firstVarianceDiff}]   ", nbisAnalysis.VarianceTraces[firstVarianceDiff]);
    }
    Console.WriteLine($"managed init q'  : {managedTrace.InitialQuantizationBins[subband]:G17}");
    Console.WriteLine($"literal init q'  : {literalTrace.InitialQuantizationBins[subband]:G17}");
    Console.WriteLine($"managed on nbis wavelet init q'  : {managedOnNbisWaveletTrace.InitialQuantizationBins[subband]:G17}");
    Console.WriteLine($"nbis init q'     : {nbisTrace.InitialQuantizationBins[subband]:G17}");
    var firstInitialQDiff = FindFirstInitialQuantizationBinDifference(nbisTrace.InitialQuantizationBins, nbisAnalysis.InitialQuantizationBins);
    Console.WriteLine($"first init q' diff index : {firstInitialQDiff}");
    Console.WriteLine($"managed scale q  : {managedTrace.QuantizationScale:G17}");
    Console.WriteLine($"literal scale q  : {literalTrace.QuantizationScale:G17}");
    Console.WriteLine($"managed on nbis wavelet scale q  : {managedOnNbisWaveletTrace.QuantizationScale:G17}");
    Console.WriteLine($"nbis scale q     : {nbisAnalysis.QuantizationScale:G17}");
    Console.WriteLine($"managed S/P      : S={managedTrace.ReciprocalAreaSum:G17}, P={managedTrace.Product:G17}, iters={managedTrace.IterationCount}");
    Console.WriteLine($"literal S/P      : S={literalTrace.ReciprocalAreaSum:G17}, P={literalTrace.Product:G17}, iters={literalTrace.IterationCount}");
    Console.WriteLine($"managed on nbis wavelet S/P      : S={managedOnNbisWaveletTrace.ReciprocalAreaSum:G17}, P={managedOnNbisWaveletTrace.Product:G17}, iters={managedOnNbisWaveletTrace.IterationCount}");
    Console.WriteLine($"nbis variance S/P: S={nbisAnalysis.ReciprocalAreaSum:G17}, P={nbisAnalysis.Product:G17}, q={nbisAnalysis.QuantizationScale:G17}");
    Console.WriteLine($"legacy nbis q    : {legacyNbisTrace.QuantizationScale:G17}");
    Console.WriteLine($"managed final K  : [{string.Join(", ", managedTrace.FinalActiveSubbands)}]");
    Console.WriteLine($"nbis variance K  : [{string.Join(", ", nbisAnalysis.FinalActiveSubbands)}]");
    foreach (var step in CreateManagedProductSteps(nbisTrace.Sigma, nbisTrace.InitialQuantizationBins, nbisTrace.FinalActiveSubbands))
    {
        Console.WriteLine(step);
    }
    Console.WriteLine($"managed raw qbin : {managedRawQbin:G17} -> scale={managedRawQbinRaw.Scale}, raw={managedRawQbinRaw.RawValue}");
    Console.WriteLine($"managed raw zbin : {managedRawZbin:G17} -> scale={managedRawZbinRaw.Scale}, raw={managedRawZbinRaw.RawValue}");
    Console.WriteLine($"high-precision raw qbin : {highPrecisionRawQbin:G17} -> scale={highPrecisionRawQbinRaw.Scale}, raw={highPrecisionRawQbinRaw.RawValue}");
    Console.WriteLine($"high-precision raw zbin : {highPrecisionRawZbin:G17} -> scale={highPrecisionRawZbinRaw.Scale}, raw={highPrecisionRawZbinRaw.RawValue}");
    Console.WriteLine($"managed on nbis wavelet raw qbin : {managedOnNbisVariancesRawQbin:G17} -> scale={managedOnNbisWaveletRawQbinRaw.Scale}, raw={managedOnNbisWaveletRawQbinRaw.RawValue}");
    Console.WriteLine($"managed on nbis wavelet raw zbin : {managedOnNbisVariancesRawZbin:G17} -> scale={managedOnNbisWaveletRawZbinRaw.Scale}, raw={managedOnNbisWaveletRawZbinRaw.RawValue}");
    Console.WriteLine($"nbis variance raw qbin : {nbisVariancesRawQbin:G17} -> scale={nbisVariancesRawQbinRaw.Scale}, raw={nbisVariancesRawQbinRaw.RawValue}");
    Console.WriteLine($"nbis variance raw zbin : {nbisVariancesRawZbin:G17} -> scale={nbisVariancesRawZbinRaw.Scale}, raw={nbisVariancesRawZbinRaw.RawValue}");
    Console.WriteLine($"double raw qbin  : {doubleArtifacts.QuantizationBins[subband]:G17} -> scale={doubleRawQbinRaw.Scale}, raw={doubleRawQbinRaw.RawValue}");
    Console.WriteLine($"double raw zbin  : {doubleArtifacts.ZeroBins[subband]:G17} -> scale={doubleRawZbinRaw.Scale}, raw={doubleRawZbinRaw.RawValue}");
    Console.WriteLine($"managed qbin : {managedQbin:G17} -> scale={managedQbinRaw.Scale}, raw={managedQbinRaw.RawValue}");
    Console.WriteLine($"nbis qbin    : {nbisQbin:G17} -> scale={nbisQbinRaw.Scale}, raw={nbisQbinRaw.RawValue}");
    Console.WriteLine($"managed zbin : {managedZbin:G17} -> scale={managedZbinRaw.Scale}, raw={managedZbinRaw.RawValue}");
    Console.WriteLine($"nbis zbin    : {nbisZbin:G17} -> scale={nbisZbinRaw.Scale}, raw={nbisZbinRaw.RawValue}");
}

static async Task ReportNbisStageAtAsync(string fileName, double bitRate)
{
    var dimensions = LoadDimensions().Single(dimensions => string.Equals(dimensions.FileName, fileName, StringComparison.Ordinal));
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

    var rawBytes = await File.ReadAllBytesAsync(rawPath).ConfigureAwait(false);
    var transformTable = WsqReferenceTables.CreateStandardTransformTable();
    WsqWaveletTreeBuilder.Build(dimensions.Width, dimensions.Height, out var waveletTree, out _);

    var normalizedImage = WsqFloatImageNormalizer.Normalize(rawBytes);
    var steps = WsqDecomposition.Trace(
        normalizedImage.Pixels,
        dimensions.Width,
        dimensions.Height,
        waveletTree,
        transformTable);

    Console.WriteLine($"{fileName} @ {bitRate:0.##}");
    for (var nodeIndex = 0; nodeIndex < steps.Length; nodeIndex++)
    {
        var managedStep = steps[nodeIndex];
        var nbisRowPassData = await ReadNbisWaveletStageDataAsync(rawPath, dimensions.Width, dimensions.Height, nodeIndex, stopAfterRowPass: true).ConfigureAwait(false);
        var rowPassDifference = FindFirstFloatDifference(managedStep.RowPassData, nbisRowPassData);
        if (rowPassDifference != -1)
        {
            Console.WriteLine($"first step divergence: node={nodeIndex}, pass=row, index={rowPassDifference}");
            Console.WriteLine($"managed value         : {managedStep.RowPassData[rowPassDifference]:G17}");
            Console.WriteLine($"nbis value            : {nbisRowPassData[rowPassDifference]:G17}");
            return;
        }

        var nbisWaveletData = await ReadNbisWaveletStageDataAsync(rawPath, dimensions.Width, dimensions.Height, nodeIndex).ConfigureAwait(false);
        var columnPassDifference = FindFirstFloatDifference(managedStep.WaveletDataAfterColumnPass, nbisWaveletData);
        if (columnPassDifference != -1)
        {
            Console.WriteLine($"first step divergence: node={nodeIndex}, pass=column, index={columnPassDifference}");
            Console.WriteLine($"managed value         : {managedStep.WaveletDataAfterColumnPass[columnPassDifference]:G17}");
            Console.WriteLine($"nbis value            : {nbisWaveletData[columnPassDifference]:G17}");
            return;
        }
    }

    Console.WriteLine("first step divergence: none");
}

static LowRateQuantizationTrace CreateLowRateQuantizationTrace(ReadOnlySpan<float> variances, float bitRate)
{
    var subbandWeights = CreateLowRateSubbandWeights();
    var reciprocalAreas = new float[WsqConstants.NumberOfSubbands];
    var sigma = new float[WsqConstants.NumberOfSubbands];
    var initialQuantizationBins = new float[WsqConstants.NumberOfSubbands];
    var quantizationBins = new float[WsqConstants.NumberOfSubbands];
    var initialSubbands = new int[WsqConstants.NumberOfSubbands];
    var workingSubbands = new int[WsqConstants.NumberOfSubbands];
    var positiveBitRateFlags = new bool[WsqConstants.NumberOfSubbands];

    for (var index = 0; index < WsqConstants.StartSizeRegion2; index++)
    {
        reciprocalAreas[index] = 1.0f / 1024.0f;
    }

    for (var index = WsqConstants.StartSizeRegion2; index < WsqConstants.StartSizeRegion3; index++)
    {
        reciprocalAreas[index] = 1.0f / 256.0f;
    }

    for (var index = WsqConstants.StartSizeRegion3; index < WsqConstants.NumberOfSubbands; index++)
    {
        reciprocalAreas[index] = 1.0f / 16.0f;
    }

    var initialSubbandCount = 0;
    for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
    {
        if (variances[subband] < WsqConstants.VarianceThreshold)
        {
            continue;
        }

        sigma[subband] = (float)Math.Sqrt(variances[subband]);
        initialQuantizationBins[subband] = subband < WsqConstants.StartSizeRegion2
            ? 1.0f
            : 10.0f / (subbandWeights[subband] * (float)Math.Log(variances[subband]));
        quantizationBins[subband] = initialQuantizationBins[subband];
        initialSubbands[initialSubbandCount] = subband;
        workingSubbands[initialSubbandCount++] = subband;
    }

    Span<int> activeSubbands = workingSubbands;
    var activeSubbandCount = initialSubbandCount;
    var quantizationScale = 0.0f;

    while (initialSubbandCount > 0)
    {
        var reciprocalAreaSum = 0.0f;
        for (var index = 0; index < activeSubbandCount; index++)
        {
            reciprocalAreaSum += reciprocalAreas[activeSubbands[index]];
        }

        var product = 1.0f;
        for (var index = 0; index < activeSubbandCount; index++)
        {
            var subband = activeSubbands[index];
            product *= (float)Math.Pow(
                sigma[subband] / quantizationBins[subband],
                reciprocalAreas[subband]);
        }

        quantizationScale = (float)((Math.Pow(2.0, ((bitRate / reciprocalAreaSum) - 1.0)) / 2.5)
            / Math.Pow(product, (1.0 / reciprocalAreaSum)));

        Array.Clear(positiveBitRateFlags);
        var nonPositiveCount = 0;
        for (var index = 0; index < activeSubbandCount; index++)
        {
            var subband = activeSubbands[index];
            if ((quantizationBins[subband] / quantizationScale) >= (5.0f * sigma[subband]))
            {
                positiveBitRateFlags[subband] = true;
                nonPositiveCount++;
            }
        }

        if (nonPositiveCount == 0)
        {
            break;
        }

        var nextActiveCount = 0;
        for (var index = 0; index < activeSubbandCount; index++)
        {
            var subband = activeSubbands[index];
            if (!positiveBitRateFlags[subband])
            {
                workingSubbands[nextActiveCount++] = subband;
            }
        }

        activeSubbands = workingSubbands;
        activeSubbandCount = nextActiveCount;
    }

    for (var index = 0; index < initialSubbandCount; index++)
    {
        positiveBitRateFlags[initialSubbands[index]] = true;
    }

    for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
    {
        quantizationBins[subband] = positiveBitRateFlags[subband]
            ? quantizationBins[subband] / quantizationScale
            : 0.0f;
    }

    return new(initialQuantizationBins, quantizationBins, quantizationScale);
}

static float[] CreateLowRateSubbandWeights()
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

static IEnumerable<WsqSegmentInfo> EnumerateSegments(byte[] bytes)
{
    var offset = 0;
    while (offset + 1 < bytes.Length)
    {
        var marker = (WsqMarker)((bytes[offset] << 8) | bytes[offset + 1]);
        if (!marker.IsValid())
        {
            yield break;
        }

        if (marker is WsqMarker.StartOfImage or WsqMarker.EndOfImage)
        {
            yield return new(marker, offset, offset + 2, -1, offset + 2);
            offset += 2;
            if (marker == WsqMarker.EndOfImage)
            {
                yield break;
            }

            continue;
        }

        if (offset + 3 >= bytes.Length)
        {
            yield break;
        }

        var segmentLength = (bytes[offset + 2] << 8) | bytes[offset + 3];
        var payloadOffset = offset + 4;
        var endOffset = offset + 2 + segmentLength;
        yield return new(marker, offset, offset + 4, payloadOffset, endOffset);

        if (marker == WsqMarker.StartOfBlock)
        {
            var nextOffset = payloadOffset + 1;
            while (nextOffset + 1 < bytes.Length)
            {
                if (bytes[nextOffset] == 0xFF && bytes[nextOffset + 1] is not 0x00 and not >= 0xD0 and <= 0xD7)
                {
                    break;
                }

                nextOffset++;
            }

            offset = nextOffset;
            continue;
        }

        offset = endOffset;
    }
}

static WsqEncoderAnalysisResult AnalyzeWithFloatLowRatePath(
    ReadOnlySpan<byte> rawBytes,
    WsqRawImageDescription rawImage,
    double bitRate)
{
    if (bitRate >= 2.0)
    {
        return WsqEncoderAnalysisPipeline.Analyze(rawBytes, rawImage, new WsqEncodeOptions(bitRate));
    }

    var transformTable = WsqReferenceTables.CreateStandardTransformTable();
    WsqWaveletTreeBuilder.Build(rawImage.Width, rawImage.Height, out var waveletTree, out var quantizationTree);

    var normalizedImage = WsqFloatImageNormalizer.Normalize(rawBytes);
    var decomposedPixels = WsqDecomposition.Decompose(
        normalizedImage.Pixels,
        rawImage.Width,
        rawImage.Height,
        waveletTree,
        transformTable);

    var quantizationResult = WsqQuantizer.Quantize(
        decomposedPixels,
        waveletTree,
        quantizationTree,
        rawImage.Width,
        rawImage.Height,
        (float)bitRate);

    var frameHeader = new WsqFrameHeader(
        Black: 0,
        White: 255,
        Height: checked((ushort)rawImage.Height),
        Width: checked((ushort)rawImage.Width),
        Shift: normalizedImage.Shift,
        Scale: normalizedImage.Scale,
        WsqEncoder: 0,
        SoftwareImplementationNumber: 0);

    return new(
        frameHeader,
        transformTable,
        quantizationResult.QuantizationTable,
        quantizationResult.QuantizedCoefficients,
        quantizationResult.BlockSizes);
}

static async Task<byte[]> EncodeManagedAsync(
    ReadOnlyMemory<byte> rawBytes,
    WsqRawImageDescription rawImage,
    double bitRate)
{
    var codec = new WsqCodec();
    await using var rawStream = new MemoryStream(rawBytes.ToArray(), writable: false);
    await using var outputStream = new MemoryStream();
    await codec.EncodeAsync(rawStream, outputStream, rawImage, new(bitRate)).ConfigureAwait(false);
    return outputStream.ToArray();
}

static async Task<byte[]> EncodeNbisAsync(
    string rawPath,
    int width,
    int height,
    double bitRate)
{
    var toolPath = "/tmp/nbis_v5_0_0/Rel_5.0.0/imgtools/bin/cwsq";
    var tempDirectory = Directory.CreateTempSubdirectory("opennist-nbis-cwsq-");
    var tempRawPath = Path.Combine(tempDirectory.FullName, Path.GetFileName(rawPath));
    var tempWsqPath = Path.Combine(tempDirectory.FullName, Path.ChangeExtension(Path.GetFileName(rawPath), ".wsq"));

    try
    {
        File.Copy(rawPath, tempRawPath, overwrite: true);

        var startInfo = new ProcessStartInfo
        {
            FileName = toolPath,
            WorkingDirectory = tempDirectory.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(bitRate.ToString("0.##", CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("wsq");
        startInfo.ArgumentList.Add(Path.GetFileName(tempRawPath));
        startInfo.ArgumentList.Add("-r");
        startInfo.ArgumentList.Add($"{width.ToString(CultureInfo.InvariantCulture)},{height.ToString(CultureInfo.InvariantCulture)},8,500");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start NBIS cwsq.");
        var standardOutput = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var standardError = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"NBIS cwsq failed with exit code {process.ExitCode}: {standardOutput}{standardError}");
        }

        return await File.ReadAllBytesAsync(tempWsqPath).ConfigureAwait(false);
    }
    finally
    {
        try
        {
            tempDirectory.Delete(recursive: true);
        }
        catch
        {
            // Ignore temp cleanup failures in local diagnostics.
        }
    }
}

static int FindFirstByteMismatchIndex(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
{
    var minimumLength = Math.Min(left.Length, right.Length);
    for (var index = 0; index < minimumLength; index++)
    {
        if (left[index] != right[index])
        {
            return index;
        }
    }

    return left.Length == right.Length ? -1 : minimumLength;
}

static string FormatByteWindow(ReadOnlySpan<byte> bytes, int centerIndex)
{
    var start = Math.Max(0, centerIndex - 8);
    var length = Math.Min(16, bytes.Length - start);
    return string.Join(" ", bytes.Slice(start, length).ToArray().Select(static value => value.ToString("X2", CultureInfo.InvariantCulture)));
}

static async Task ReportNistParityAsync()
{
    var dimensionsByFileName = LoadDimensions().ToDictionary(static dimensions => dimensions.FileName, StringComparer.Ordinal);
    var exactCases = new List<string>();
    var mismatchCases = new List<string>();

    foreach (var fileName in dimensionsByFileName.Keys.OrderBy(static value => value, StringComparer.Ordinal))
    {
        var dimensions = dimensionsByFileName[fileName];
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
        var rawBytes = await File.ReadAllBytesAsync(rawPath).ConfigureAwait(false);
        var rawImage = new WsqRawImageDescription(dimensions.Width, dimensions.Height, 8, 500);
        const double bitRate = 2.25;
        var analysis = WsqEncoderAnalysisPipeline.Analyze(rawBytes, rawImage, new WsqEncodeOptions(bitRate));
        var referencePath = Path.Combine(
            RepoRoot,
            "OpenNist.Tests",
            "TestData",
            "Wsq",
            "NistReferenceImages",
            "V2_0",
            "ReferenceWsq",
            "BitRate225",
            Path.ChangeExtension(fileName, ".wsq"));
        var reference = await ReadReferenceCoefficientsAsync(referencePath).ConfigureAwait(false);

        WsqWaveletTreeBuilder.Build(dimensions.Width, dimensions.Height, out _, out var quantizationTree);
        var caseLabel = $"{fileName} @ {bitRate:0.##}";
        var mismatchIndex = FindFirstMismatchIndex(analysis.QuantizedCoefficients, reference.QuantizedCoefficients);
        if (mismatchIndex < 0)
        {
            exactCases.Add(caseLabel);
            continue;
        }

        var coefficientLocation = FindCoefficientLocation(analysis.QuantizationTable.QuantizationBins, quantizationTree, mismatchIndex);
        mismatchCases.Add(
            $"{caseLabel} (coeff {mismatchIndex}: actual={analysis.QuantizedCoefficients[mismatchIndex]}, ref={reference.QuantizedCoefficients[mismatchIndex]}, "
            + $"loc=subband {coefficientLocation.SubbandIndex}, row {coefficientLocation.Row}, col {coefficientLocation.Column})");
    }

    Console.WriteLine($"Exact NIST parity cases (2.25): {exactCases.Count}");
    foreach (var exactCase in exactCases)
    {
        Console.WriteLine($"EXACT {exactCase}");
    }

    Console.WriteLine($"NIST mismatch cases (2.25): {mismatchCases.Count}");
    foreach (var mismatchCase in mismatchCases)
    {
        Console.WriteLine($"MISMATCH {mismatchCase}");
    }
}

static async Task ReportNistGuardCasesAsync()
{
    var dimensionsByFileName = LoadDimensions().ToDictionary(static dimensions => dimensions.FileName, StringComparer.Ordinal);

    foreach (var fileName in dimensionsByFileName.Keys.OrderBy(static value => value, StringComparer.Ordinal))
    {
        var dimensions = dimensionsByFileName[fileName];
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
        var rawBytes = await File.ReadAllBytesAsync(rawPath).ConfigureAwait(false);
        var rawImage = new WsqRawImageDescription(dimensions.Width, dimensions.Height, 8, 500);
        const double bitRate = 2.25;

        var analysis = WsqEncoderAnalysisPipeline.Analyze(rawBytes, rawImage, new WsqEncodeOptions(bitRate));
        var referencePath = Path.Combine(
            RepoRoot,
            "OpenNist.Tests",
            "TestData",
            "Wsq",
            "NistReferenceImages",
            "V2_0",
            "ReferenceWsq",
            "BitRate225",
            Path.ChangeExtension(fileName, ".wsq"));
        var reference = await ReadReferenceCoefficientsAsync(referencePath).ConfigureAwait(false);

        var currentMismatchIndex = FindFirstMismatchIndex(analysis.QuantizedCoefficients, reference.QuantizedCoefficients);
        if (currentMismatchIndex >= 0)
        {
            continue;
        }

        WsqWaveletTreeBuilder.Build(rawImage.Width, rawImage.Height, out var waveletTree, out var quantizationTree);

        var normalizedImage = WsqDoubleImageNormalizer.Normalize(rawBytes);
        var doubleDecomposedPixels = WsqDoubleDecomposition.Decompose(
            normalizedImage.Pixels,
            rawImage.Width,
            rawImage.Height,
            waveletTree,
            WsqReferenceTables.CreateStandardTransformTable());
        var productAndScaleTrace = WsqHighPrecisionQuantizer.CreateQuantizationTrace(
            WsqHighPrecisionVarianceCalculator.Compute(doubleDecomposedPixels, quantizationTree, rawImage.Width),
            bitRate,
            new(UseSinglePrecisionProduct: true, UseSinglePrecisionScaleFactor: true));
        var productAndScaleCoefficients = WsqCoefficientQuantizer.Quantize(
            doubleDecomposedPixels,
            quantizationTree,
            rawImage.Width,
            productAndScaleTrace.QuantizationBins,
            productAndScaleTrace.ZeroBins);
        var productMismatchIndex = FindFirstMismatchIndex(productAndScaleCoefficients, reference.QuantizedCoefficients);

        var highPrecisionArtifacts = WsqEncoderAnalysisPipeline.AnalyzeHighPrecisionArtifacts(
            rawBytes,
            rawImage,
            WsqReferenceTables.CreateStandardTransformTable(),
            waveletTree);
        var rawQuantizationArtifacts = WsqHighPrecisionQuantizer.CreateQuantizationArtifacts(
            highPrecisionArtifacts.DoubleDecomposedPixels,
            quantizationTree,
            rawImage.Width,
            bitRate);
        var overriddenQuantizationBins = rawQuantizationArtifacts.QuantizationBins.ToArray();
        var overriddenZeroBins = rawQuantizationArtifacts.ZeroBins.ToArray();
        overriddenQuantizationBins[0] = analysis.QuantizationTable.QuantizationBins[0];
        overriddenZeroBins[0] = analysis.QuantizationTable.ZeroBins[0];
        var serializedOverrideCoefficients = WsqCoefficientQuantizer.Quantize(
            highPrecisionArtifacts.DoubleDecomposedPixels,
            quantizationTree,
            rawImage.Width,
            overriddenQuantizationBins,
            overriddenZeroBins);
        var serializedOverrideMismatchIndex = FindFirstMismatchIndex(serializedOverrideCoefficients, reference.QuantizedCoefficients);

        Console.WriteLine(
            $"{fileName}: productScale={productMismatchIndex}, serializedSubband0={serializedOverrideMismatchIndex}");
    }
}

static WsqEncoderAnalysisResult AnalyzeWithFloatHighRatePath(
    ReadOnlySpan<byte> rawBytes,
    WsqRawImageDescription rawImage,
    double bitRate)
{
    var transformTable = WsqReferenceTables.CreateStandardTransformTable();
    WsqWaveletTreeBuilder.Build(rawImage.Width, rawImage.Height, out var waveletTree, out var quantizationTree);

    if (bitRate >= 2.0)
    {
        var normalizedImage = WsqFloatImageNormalizer.Normalize(rawBytes);
        var decomposedPixels = WsqDecomposition.Decompose(
            normalizedImage.Pixels,
            rawImage.Width,
            rawImage.Height,
            waveletTree,
            transformTable);

        var quantizationResult = WsqHighPrecisionQuantizer.Quantize(
            decomposedPixels,
            waveletTree,
            quantizationTree,
            rawImage.Width,
            rawImage.Height,
            bitRate);

        var frameHeader = new WsqFrameHeader(
            Black: 0,
            White: 255,
            Height: checked((ushort)rawImage.Height),
            Width: checked((ushort)rawImage.Width),
            Shift: normalizedImage.Shift,
            Scale: normalizedImage.Scale,
            WsqEncoder: 0,
            SoftwareImplementationNumber: 0);

        return new(
            frameHeader,
            transformTable,
            quantizationResult.QuantizationTable,
            quantizationResult.QuantizedCoefficients,
            quantizationResult.BlockSizes);
    }

    return WsqEncoderAnalysisPipeline.Analyze(rawBytes, rawImage, new WsqEncodeOptions(bitRate));
}

static WsqEncoderAnalysisResult AnalyzeWithDoubleSourcePath(
    ReadOnlySpan<byte> rawBytes,
    WsqRawImageDescription rawImage,
    double bitRate)
{
    var transformTable = WsqReferenceTables.CreateStandardTransformTable();
    WsqWaveletTreeBuilder.Build(rawImage.Width, rawImage.Height, out var waveletTree, out var quantizationTree);

    var normalizedImage = WsqFloatImageNormalizer.Normalize(rawBytes);
    var doubleNormalizedImage = WsqDoubleImageNormalizer.Normalize(rawBytes);
    var decomposedPixels = WsqDoubleDecomposition.Decompose(
        doubleNormalizedImage.Pixels,
        rawImage.Width,
        rawImage.Height,
        waveletTree,
        transformTable);

    var quantizationArtifacts = WsqHighPrecisionQuantizer.CreateQuantizationArtifacts(
        normalizedImage.Pixels.ToArray() is var floatPixels
            ? WsqDecomposition.Decompose(floatPixels, rawImage.Width, rawImage.Height, waveletTree, transformTable)
            : throw new InvalidOperationException(),
        quantizationTree,
        rawImage.Width,
        bitRate);
    var quantizationTable = WsqQuantizationTableFactory.Create(
        quantizationArtifacts.QuantizationBins,
        quantizationArtifacts.ZeroBins);
    var quantizedCoefficients = WsqCoefficientQuantizer.Quantize(
        decomposedPixels,
        quantizationTree,
        rawImage.Width,
        quantizationArtifacts.QuantizationBins,
        quantizationArtifacts.ZeroBins);
    var blockSizes = WsqQuantizationDecoder.ComputeBlockSizes(quantizationTable, waveletTree, quantizationTree);

    return new(
        new WsqFrameHeader(
            Black: 0,
            White: 255,
            Height: checked((ushort)rawImage.Height),
            Width: checked((ushort)rawImage.Width),
            Shift: normalizedImage.Shift,
            Scale: normalizedImage.Scale,
            WsqEncoder: 0,
            SoftwareImplementationNumber: 0),
        transformTable,
        quantizationTable,
        quantizedCoefficients,
        blockSizes);
}

static float[] DecomposeLiterally(
    float[] waveletData,
    int width,
    int height,
    WsqWaveletNode[] waveletTree,
    WsqTransformTable transformTable)
{
    var workingWaveletData = waveletData.ToArray();
    var temporaryBuffer = new float[waveletData.Length];

    for (var nodeIndex = 0; nodeIndex < waveletTree.Length; nodeIndex++)
    {
        var node = waveletTree[nodeIndex];
        var baseOffset = node.Y * width + node.X;

        GetLetsLiterally(
            temporaryBuffer,
            0,
            workingWaveletData,
            baseOffset,
            node.Height,
            node.Width,
            width,
            1,
            transformTable.HighPassFilterCoefficients.ToArray(),
            transformTable.LowPassFilterCoefficients.ToArray(),
            node.InvertRows);

        GetLetsLiterally(
            workingWaveletData,
            baseOffset,
            temporaryBuffer,
            0,
            node.Width,
            node.Height,
            1,
            width,
            transformTable.HighPassFilterCoefficients.ToArray(),
            transformTable.LowPassFilterCoefficients.ToArray(),
            node.InvertColumns);
    }

    return workingWaveletData;
}

static void PrintVariantMismatchLocation(
    string label,
    int mismatchIndex,
    IReadOnlyList<double> quantizationBins,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree)
{
    if (mismatchIndex < 0)
    {
        Console.WriteLine($"{label}: none");
        return;
    }

    var location = FindCoefficientLocation(quantizationBins, quantizationTree, mismatchIndex);
    Console.WriteLine(
        $"{label}: subband={location.SubbandIndex}, row={location.Row}, column={location.Column}, x={location.ImageX}, y={location.ImageY}");
}

static void PrintVariantMismatchDetails(
    string label,
    int mismatchIndex,
    short[] actualCoefficients,
    short[] expectedCoefficients,
    short[] nbisCoefficients,
    IReadOnlyList<double> quantizationBins,
    IReadOnlyList<double> zeroBins,
    float[] floatWaveletData,
    double[] highRateWaveletData,
    float[] nbisWaveletData,
    double[] nbisQuantizationBins,
    double[] nbisZeroBins,
    WsqQuantizationTable referenceQuantizationTable,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width)
{
    if (mismatchIndex < 0)
    {
        Console.WriteLine($"{label}: none");
        return;
    }

    var location = FindCoefficientLocation(referenceQuantizationTable.QuantizationBins, quantizationTree, mismatchIndex);
    var waveletIndex = (location.ImageY * width) + location.ImageX;
    var variantHalfZeroBin = zeroBins[location.SubbandIndex] / 2.0;
    var nbisHalfZeroBin = nbisZeroBins[location.SubbandIndex] / 2.0;
    var referenceHalfZeroBin = referenceQuantizationTable.ZeroBins[location.SubbandIndex] / 2.0;

    Console.WriteLine(
        $"{label}: subband={location.SubbandIndex}, row={location.Row}, column={location.Column}, x={location.ImageX}, y={location.ImageY}");
    Console.WriteLine(
        $"{label} source          : float={floatWaveletData[waveletIndex]:G17}, highRate={highRateWaveletData[waveletIndex]:G17}, nbis={nbisWaveletData[waveletIndex]:G17}");
    Console.WriteLine(
        $"{label} bins            : varQ={quantizationBins[location.SubbandIndex]:G17}, varHalfZ={variantHalfZeroBin:G17}, nbisQ={nbisQuantizationBins[location.SubbandIndex]:G17}, nbisHalfZ={nbisHalfZeroBin:G17}, refQ={referenceQuantizationTable.QuantizationBins[location.SubbandIndex]:G17}, refHalfZ={referenceHalfZeroBin:G17}");
    Console.WriteLine(
        $"{label} coeffs          : actual={actualCoefficients[mismatchIndex]}, ref={expectedCoefficients[mismatchIndex]}, nbis={nbisCoefficients[mismatchIndex]}");
}

static void GetLetsLiterally(
    float[] destination,
    int destinationBaseOffset,
    float[] source,
    int sourceBaseOffset,
    int lineCount,
    int lineLength,
    int linePitch,
    int sampleStride,
    float[] highPassFilter,
    float[] lowPassFilter,
    bool invertSubbands)
{
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
            highPassWriteIndex = destinationBaseOffset + lineIndex * linePitch;
            lowPassWriteIndex = highPassWriteIndex + highPassSampleCount * sampleStride;
        }
        else
        {
            lowPassWriteIndex = destinationBaseOffset + lineIndex * linePitch;
            highPassWriteIndex = lowPassWriteIndex + lowPassSampleCount * sampleStride;
        }

        var firstSourceIndex = sourceBaseOffset + lineIndex * linePitch;
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
            destination[lowPassWriteIndex] = source[currentLowPassSourceIndex] * lowPassFilter[0];

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
                destination[lowPassWriteIndex] += source[currentLowPassSourceIndex] * lowPassFilter[filterIndex];
            }

            lowPassWriteIndex += sampleStride;

            var currentHighPassSourceStride = highPassSourceStride;
            var currentHighPassSourceIndex = highPassSourceIndex;
            var currentHighPassLeftEdge = highPassLeftEdge;
            var currentHighPassRightEdge = highPassRightEdge;
            destination[highPassWriteIndex] = source[currentHighPassSourceIndex] * highPassFilter[0];

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
                destination[highPassWriteIndex] += source[currentHighPassSourceIndex] * highPassFilter[filterIndex];
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
            destination[lowPassWriteIndex] = source[currentLowPassSourceIndex] * lowPassFilter[0];

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
                destination[lowPassWriteIndex] += source[currentLowPassSourceIndex] * lowPassFilter[filterIndex];
            }
        }
    }
}

static async Task<WsqReferenceQuantizedCoefficients> ReadReferenceCoefficientsAsync(string referencePath)
{
    await using var referenceStream = File.OpenRead(referencePath);
    var container = await WsqContainerReader.ReadAsync(referenceStream);
    WsqWaveletTreeBuilder.Build(
        container.FrameHeader.Width,
        container.FrameHeader.Height,
        out var waveletTree,
        out var quantizationTree);

    var quantizedCoefficients = WsqHuffmanDecoder.DecodeQuantizedCoefficients(container, waveletTree, quantizationTree);
    var blockSizes = WsqQuantizationDecoder.ComputeBlockSizes(container.QuantizationTable, waveletTree, quantizationTree);
    return new(container.QuantizationTable, quantizedCoefficients, blockSizes);
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
    var standardOutput = await process.StandardOutput.ReadToEndAsync();
    var standardError = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"nbis_dump failed with exit code {process.ExitCode}: {standardError}");
    }

    double shift = 0.0;
    double scale = 0.0;
    var variances = new double[WsqConstants.NumberOfSubbands];
    var quantizationBins = new double[WsqConstants.NumberOfSubbands];
    var zeroBins = new double[WsqConstants.NumberOfSubbands];
    var initialQuantizationBins = new double[WsqConstants.NumberOfSubbands];
    var quantizationTree = new QuantizationTreeNode[WsqConstants.NumberOfSubbands];
    var varianceTraces = new VarianceTrace[WsqConstants.NumberOfSubbands];
    var finalActiveSubbands = new List<int>();
    var quantizedCoefficients = new List<short>();
    double reciprocalAreaSum = 0.0;
    double product = 0.0;
    double quantizationScale = 0.0;

    foreach (var line in standardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (line.StartsWith("trace.S=", StringComparison.Ordinal))
        {
            reciprocalAreaSum = double.Parse(line["trace.S=".Length..], CultureInfo.InvariantCulture);
            continue;
        }

        if (line.StartsWith("trace.P=", StringComparison.Ordinal))
        {
            product = double.Parse(line["trace.P=".Length..], CultureInfo.InvariantCulture);
            continue;
        }

        if (line.StartsWith("trace.q=", StringComparison.Ordinal))
        {
            quantizationScale = double.Parse(line["trace.q=".Length..], CultureInfo.InvariantCulture);
            continue;
        }

        if (line.StartsWith("trace.qprime[", StringComparison.Ordinal))
        {
            var subbandIndex = ParseIndexedTokenIndex(line, "trace.qprime");
            initialQuantizationBins[subbandIndex] = ParseIndexedTokenValue(line);
            continue;
        }

        if (line.StartsWith("trace.active[", StringComparison.Ordinal))
        {
            finalActiveSubbands.Add(int.Parse(line[(line.IndexOf('=') + 1)..], CultureInfo.InvariantCulture));
            continue;
        }

        if (line.StartsWith("shift=", StringComparison.Ordinal))
        {
            shift = double.Parse(line["shift=".Length..], CultureInfo.InvariantCulture);
            continue;
        }

        if (line.StartsWith("qtree[", StringComparison.Ordinal))
        {
            var subbandIndex = ParseIndexedTokenIndex(line, "qtree");
            var payload = line[(line.IndexOf('=', StringComparison.Ordinal) + 1)..];
            var tokens = payload.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            quantizationTree[subbandIndex] = new(
                int.Parse(tokens[0]["x:".Length..], CultureInfo.InvariantCulture),
                int.Parse(tokens[1]["y:".Length..], CultureInfo.InvariantCulture),
                int.Parse(tokens[2]["lenx:".Length..], CultureInfo.InvariantCulture),
                int.Parse(tokens[3]["leny:".Length..], CultureInfo.InvariantCulture));
            continue;
        }

        if (line.StartsWith("vartrace[", StringComparison.Ordinal))
        {
            var subbandIndex = ParseIndexedTokenIndex(line, "vartrace");
            var payload = line[(line.IndexOf('=', StringComparison.Ordinal) + 1)..];
            var tokens = payload.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            varianceTraces[subbandIndex] = new(
                UseCroppedRegion: ParseTraceInt(tokens[0], "cropped:") != 0,
                StartX: ParseTraceInt(tokens[1], "startx:"),
                StartY: ParseTraceInt(tokens[2], "starty:"),
                Width: ParseTraceInt(tokens[3], "lenx:"),
                Height: ParseTraceInt(tokens[4], "leny:"),
                SkipX: ParseTraceInt(tokens[5], "skipx:"),
                SkipY: ParseTraceInt(tokens[6], "skipy:"),
                SampleCount: ParseTraceInt(tokens[7], "samples:"),
                Sum: ParseTraceDouble(tokens[8], "sum:"),
                SumSquares: ParseTraceDouble(tokens[9], "ssq:"),
                Sum2: ParseTraceDouble(tokens[10], "sum2:"),
                Variance: ParseTraceDouble(tokens[11], "variance:"));
            continue;
        }

        if (line.StartsWith("scale=", StringComparison.Ordinal))
        {
            scale = double.Parse(line["scale=".Length..], CultureInfo.InvariantCulture);
            continue;
        }

        if (line.StartsWith("var[", StringComparison.Ordinal))
        {
            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var varianceToken = tokens[0];
            var subbandIndex = ParseIndexedTokenIndex(varianceToken, "var");
            variances[subbandIndex] = ParseIndexedTokenValue(varianceToken);
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

    return new(
        shift,
        scale,
        variances,
        quantizationBins,
        zeroBins,
        quantizationTree,
        varianceTraces,
        quantizedCoefficients.ToArray(),
        initialQuantizationBins,
        finalActiveSubbands.ToArray(),
        reciprocalAreaSum,
        product,
        quantizationScale);
}

static float ReadNbisWaveletAtIndex(string rawPath, int width, int height, int waveletIndex)
{
    var waveletData = ReadNbisWaveletData(rawPath, width, height);
    return waveletData[waveletIndex];
}

static async Task<float[]> ReadNbisWaveletDataAsync(string rawPath, int width, int height)
{
    return await ReadNbisWaveletStageDataAsync(rawPath, width, height, 19, stopAfterRowPass: false).ConfigureAwait(false);
}

static async Task<float[]> ReadNbisWaveletStageDataAsync(string rawPath, int width, int height, int stopNode, bool stopAfterRowPass = false)
{
    return await Task.Run(() => ReadNbisWaveletStageData(rawPath, width, height, stopNode, stopAfterRowPass)).ConfigureAwait(false);
}

static float[] ReadNbisWaveletData(string rawPath, int width, int height)
{
    return ReadNbisWaveletStageData(rawPath, width, height, 19, stopAfterRowPass: false);
}

static float[] ReadNbisWaveletStageData(string rawPath, int width, int height, int stopNode, bool stopAfterRowPass)
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
    if (stopAfterRowPass)
    {
        startInfo.ArgumentList.Add("row");
    }

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Failed to start nbis_wavelet_dump.");
    using var outputStream = new MemoryStream();
    process.StandardOutput.BaseStream.CopyTo(outputStream);
    var standardError = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"nbis_wavelet_dump failed with exit code {process.ExitCode}: {standardError}");
    }

    var bytes = outputStream.ToArray();
    var waveletData = new float[bytes.Length / sizeof(float)];
    Buffer.BlockCopy(bytes, 0, waveletData, 0, bytes.Length);
    return waveletData;
}

static int FindFirstFloatDifference(ReadOnlySpan<float> actualValues, ReadOnlySpan<float> expectedValues)
{
    var length = Math.Min(actualValues.Length, expectedValues.Length);
    for (var index = 0; index < length; index++)
    {
        if (BitConverter.SingleToInt32Bits(actualValues[index]) == BitConverter.SingleToInt32Bits(expectedValues[index]))
        {
            continue;
        }

        return index;
    }

    return actualValues.Length == expectedValues.Length ? -1 : length;
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

static short[] QuantizeWithTable(
    ReadOnlySpan<float> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    WsqQuantizationTable quantizationTable)
{
    var quantizationBins = quantizationTable.QuantizationBins;
    var zeroBins = quantizationTable.ZeroBins;
    var quantizedCoefficients = new short[waveletData.Length];
    var coefficientIndex = 0;

    for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
    {
        if (quantizationBins[subband].CompareTo(0.0) == 0)
        {
            continue;
        }

        var node = quantizationTree[subband];
        var quantizationBin = (float)quantizationBins[subband];
        var halfZeroBin = (float)(zeroBins[subband] / 2.0);
        var rowStart = node.Y * width + node.X;

        for (var row = 0; row < node.Height; row++)
        {
            var pixelIndex = rowStart + row * width;

            for (var column = 0; column < node.Width; column++)
            {
                var coefficient = waveletData[pixelIndex + column];

                if (-halfZeroBin <= coefficient && coefficient <= halfZeroBin)
                {
                    quantizedCoefficients[coefficientIndex++] = 0;
                }
                else if (coefficient > 0.0f)
                {
                    quantizedCoefficients[coefficientIndex++] = checked((short)(((coefficient - halfZeroBin) / quantizationBin) + 1.0f));
                }
                else
                {
                    quantizedCoefficients[coefficientIndex++] = checked((short)(((coefficient + halfZeroBin) / quantizationBin) - 1.0f));
                }
            }
        }
    }

    Array.Resize(ref quantizedCoefficients, coefficientIndex);
    return quantizedCoefficients;
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

static CoefficientLocation FindCoefficientLocation(
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
        return new(subbandIndex, row, column, node.X + column, node.Y + row);
    }

    throw new InvalidOperationException($"Unable to map quantized coefficient index {coefficientIndex}.");
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
    for (var index = 0; index < actualBins.Count; index++)
    {
        var actual = actualBins[index];
        var expected = expectedBins[index];

        if (Math.Abs(actual - expected) <= 0.0)
        {
            continue;
        }

        return $"index {index}: actual={actual:G17}, expected={expected:G17}";
    }

    return "none";
}

static int FindFirstInitialQuantizationBinDifference(ReadOnlySpan<float> actualBins, ReadOnlySpan<double> expectedBins)
{
    var limit = Math.Min(actualBins.Length, expectedBins.Length);
    for (var index = 0; index < limit; index++)
    {
        if ((double)actualBins[index] == expectedBins[index])
        {
            continue;
        }

        return index;
    }

    return actualBins.Length == expectedBins.Length ? -1 : limit;
}

static int FindFirstVarianceDifference(ReadOnlySpan<float> actualVariances, ReadOnlySpan<float> expectedVariances)
{
    var limit = Math.Min(actualVariances.Length, expectedVariances.Length);
    for (var index = 0; index < limit; index++)
    {
        if (actualVariances[index] == expectedVariances[index])
        {
            continue;
        }

        return index;
    }

    return -1;
}

static IEnumerable<string> CreateManagedProductSteps(
    float[] sigma,
    float[] initialQuantizationBins,
    int[] activeSubbands)
{
    var product = 1.0f;
    for (var index = 0; index < activeSubbands.Length; index++)
    {
        var subband = activeSubbands[index];
        var reciprocalArea = subband < WsqConstants.StartSizeRegion2
            ? (float)(1.0 / 1024.0)
            : subband < WsqConstants.StartSizeRegion3
                ? (float)(1.0 / 256.0)
                : (float)(1.0 / 16.0);
        var ratio = sigma[subband] / initialQuantizationBins[subband];
        var factor = Math.Pow(ratio, reciprocalArea);
        product = (float)(product * factor);
        yield return $"managed.step[{index}]=subband:{subband} ratio:{ratio:G17} factor:{factor:G17} product:{product:G17}";
    }
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

static (int X, int Y, int Width, int Height) GetVarianceRegion(QuantizationTreeNode node, bool useCroppedRegion)
{
    if (!useCroppedRegion)
    {
        return (node.X, node.Y, node.Width, node.Height);
    }

    return (
        node.X + (node.Width / 8),
        node.Y + ((9 * node.Height) / 32),
        (3 * node.Width) / 4,
        (7 * node.Height) / 16);
}

static QuantizationTreeNode ToQuantizationTreeNode(WsqQuantizationNode node)
{
    return new(node.X, node.Y, node.Width, node.Height);
}

static VarianceTrace[] ComputeVarianceTraces(
    ReadOnlySpan<float> waveletData,
    ReadOnlySpan<WsqQuantizationNode> quantizationTree,
    int width,
    ReadOnlySpan<float> variances)
{
    var useCroppedRegion = false;
    var initialVarianceSum = 0.0f;
    for (var subband = 0; subband < WsqConstants.StartSizeRegion2; subband++)
    {
        initialVarianceSum += variances[subband];
    }

    if (initialVarianceSum >= 20000.0f)
    {
        useCroppedRegion = true;
    }

    var traces = new VarianceTrace[WsqConstants.NumberOfSubbands];
    for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
    {
        traces[subband] = ComputeVarianceTrace(waveletData, quantizationTree[subband], width, useCroppedRegion);
    }

    return traces;
}

static VarianceTrace ComputeVarianceTrace(
    ReadOnlySpan<float> waveletData,
    WsqQuantizationNode node,
    int width,
    bool useCroppedRegion)
{
    var startX = node.X;
    var startY = node.Y;
    var regionWidth = node.Width;
    var regionHeight = node.Height;
    var skipX = 0;
    var skipY = 0;

    if (useCroppedRegion)
    {
        skipX = node.Width / 8;
        skipY = (9 * node.Height) / 32;
        startX += skipX;
        startY += skipY;
        regionWidth = (3 * node.Width) / 4;
        regionHeight = (7 * node.Height) / 16;
    }

    var fp = (startY * width) + startX;
    var sum = 0.0f;
    var sumSquares = 0.0f;

    for (var row = 0; row < regionHeight; row++, fp += width - regionWidth)
    {
        for (var column = 0; column < regionWidth; column++)
        {
            var pixel = waveletData[fp];
            sum += pixel;
            sumSquares += pixel * pixel;
            fp++;
        }
    }

    var sampleCount = regionWidth * regionHeight;
    var sum2 = (sum * sum) / sampleCount;
    var variance = (sumSquares - sum2) / (sampleCount - 1.0f);

    return new(
        useCroppedRegion,
        startX,
        startY,
        regionWidth,
        regionHeight,
        skipX,
        skipY,
        sampleCount,
        sum,
        sumSquares,
        sum2,
        variance);
}

static void PrintVarianceTrace(string label, VarianceTrace trace)
{
    Console.WriteLine(
        $"{label} : cropped={(trace.UseCroppedRegion ? 1 : 0)} startx={trace.StartX} starty={trace.StartY} lenx={trace.Width} leny={trace.Height} skipx={trace.SkipX} skipy={trace.SkipY} samples={trace.SampleCount} sum={trace.Sum:G17} ssq={trace.SumSquares:G17} sum2={trace.Sum2:G17} variance={trace.Variance:G17}");
}

static int ParseTraceInt(string token, string prefix)
{
    return int.Parse(token[prefix.Length..], CultureInfo.InvariantCulture);
}

static double ParseTraceDouble(string token, string prefix)
{
    return double.Parse(token[prefix.Length..], CultureInfo.InvariantCulture);
}

internal sealed record RawImageDimensions(string FileName, int Width, int Height);

internal readonly record struct WsqReferenceQuantizedCoefficients(
    WsqQuantizationTable QuantizationTable,
    short[] QuantizedCoefficients,
    int[] BlockSizes);

internal readonly record struct NbisAnalysisDump(
    double Shift,
    double Scale,
    double[] Variances,
    double[] QuantizationBins,
    double[] ZeroBins,
    QuantizationTreeNode[] QuantizationTree,
    VarianceTrace[] VarianceTraces,
    short[] QuantizedCoefficients,
    double[] InitialQuantizationBins,
    int[] FinalActiveSubbands,
    double ReciprocalAreaSum,
    double Product,
    double QuantizationScale);

internal readonly record struct QuantizationTreeNode(
    int X,
    int Y,
    int Width,
    int Height);

internal readonly record struct VarianceTrace(
    bool UseCroppedRegion,
    int StartX,
    int StartY,
    int Width,
    int Height,
    int SkipX,
    int SkipY,
    int SampleCount,
    double Sum,
    double SumSquares,
    double Sum2,
    double Variance);

internal readonly record struct LowRateQuantizationTrace(
    float[] InitialQuantizationBins,
    float[] QuantizationBins,
    float QuantizationScale);

internal readonly record struct WsqSegmentInfo(
    WsqMarker Marker,
    int StartOffset,
    int HeaderEndOffset,
    int PayloadOffset,
    int EndOffset);

internal readonly record struct CoefficientLocation(
    int SubbandIndex,
    int Row,
    int Column,
    int ImageX,
    int ImageY);
