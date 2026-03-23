namespace OpenNist.Tools.WsqClassify;

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using OpenNist.Wsq;
using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Encoding;

internal static class Program
{
    private const string RepoRoot = "/Users/pmtar/Development/Projects/OpenNist";

    public static async Task<int> Main()
    {
        var dimensionsByFileName = await LoadDimensionsByFileNameAsync().ConfigureAwait(false);
        var rawDirectory = Path.Combine(
            RepoRoot,
            "OpenNist.Tests",
            "TestData",
            "Wsq",
            "NistReferenceImages",
            "V2_0",
            "Encode",
            "Raw");
        var rawPaths = Directory.GetFiles(rawDirectory, "*.raw").OrderBy(static path => path, StringComparer.Ordinal).ToArray();
        var reports = new List<CaseReport>(rawPaths.Length * 2);

        foreach (var bitRate in new[] { 0.75, 2.25 })
        {
            foreach (var rawPath in rawPaths)
            {
                var fileName = Path.GetFileName(rawPath);
                var rawImage = dimensionsByFileName[fileName];
                var referencePath = GetReferencePath(fileName, bitRate);
                var rawBytes = await File.ReadAllBytesAsync(rawPath).ConfigureAwait(false);
                var rawImageDescription = new WsqRawImageDescription(rawImage.Width, rawImage.Height, 8, 500);

                var analysis = WsqEncoderAnalysisPipeline.Analyze(
                    rawBytes,
                    rawImageDescription,
                    new WsqEncodeOptions(bitRate));
        var reference = await ReadReferenceCoefficientsAsync(referencePath).ConfigureAwait(false);
        var nbis = await ReadNbisAnalysisAsync(rawPath, rawImage.Width, rawImage.Height, bitRate).ConfigureAwait(false);
        var strategyReport = BuildStrategyReport(rawBytes, rawImageDescription, bitRate);

                reports.Add(CreateReport(fileName, bitRate, analysis, reference, nbis, strategyReport));
            }
        }

        PrintSummary(reports);
        return 0;
    }

    private static async Task<Dictionary<string, RawImageDimensions>> LoadDimensionsByFileNameAsync()
    {
        var dimensionsPath = Path.Combine(
            RepoRoot,
            "OpenNist.Tests",
            "TestData",
            "Wsq",
            "NistReferenceImages",
            "V2_0",
            "raw-image-dimensions.json");
        await using var stream = File.OpenRead(dimensionsPath);
        var dimensions = await JsonSerializer.DeserializeAsync<RawImageDimensions[]>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Failed to deserialize RAW image dimensions.");

        return dimensions.ToDictionary(static item => item.FileName, StringComparer.Ordinal);
    }

    private static string GetReferencePath(string fileName, double bitRate)
    {
        var rateDirectory = bitRate == 0.75 ? "BitRate075" : "BitRate225";
        return Path.Combine(
            RepoRoot,
            "OpenNist.Tests",
            "TestData",
            "Wsq",
            "NistReferenceImages",
            "V2_0",
            "ReferenceWsq",
            rateDirectory,
            Path.ChangeExtension(fileName, ".wsq"));
    }

    private static async Task<WsqReferenceQuantizedCoefficients> ReadReferenceCoefficientsAsync(string referencePath)
    {
        await using var referenceStream = File.OpenRead(referencePath);
        var container = await WsqContainerReader.ReadAsync(referenceStream).ConfigureAwait(false);
        WsqWaveletTreeBuilder.Build(
            container.FrameHeader.Width,
            container.FrameHeader.Height,
            out var waveletTree,
            out var quantizationTree);

        var quantizedCoefficients = WsqHuffmanDecoder.DecodeQuantizedCoefficients(container, waveletTree, quantizationTree);
        return new(container.QuantizationTable, quantizedCoefficients);
    }

    private static async Task<NbisAnalysisDump> ReadNbisAnalysisAsync(string rawPath, int width, int height, double bitRate)
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

        var quantizationBins = new double[WsqConstants.NumberOfSubbands];
        var quantizedCoefficients = new List<short>();

        foreach (var line in standardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("qbin[", StringComparison.Ordinal))
            {
                var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var qbinToken = tokens[0];
                var subbandIndex = ParseIndexedTokenIndex(qbinToken, "qbin");
                quantizationBins[subbandIndex] = ParseIndexedTokenValue(qbinToken);
                continue;
            }

            if (line.StartsWith("coeff[", StringComparison.Ordinal))
            {
                quantizedCoefficients.Add(checked((short)int.Parse(line[(line.IndexOf('=') + 1)..], CultureInfo.InvariantCulture)));
            }
        }

        return new(quantizationBins, quantizedCoefficients.ToArray());
    }

    private static int ParseIndexedTokenIndex(string token, string prefix)
    {
        var start = prefix.Length + 1;
        var end = token.IndexOf(']', start);
        return int.Parse(token[start..end], CultureInfo.InvariantCulture);
    }

    private static double ParseIndexedTokenValue(string token)
    {
        return double.Parse(token[(token.IndexOf('=', StringComparison.Ordinal) + 1)..], CultureInfo.InvariantCulture);
    }

    private static CaseReport CreateReport(
        string fileName,
        double bitRate,
        WsqEncoderAnalysisResult analysis,
        WsqReferenceQuantizedCoefficients reference,
        NbisAnalysisDump nbis,
        StrategyReport strategyReport)
    {
        var matchesReference = analysis.QuantizedCoefficients.SequenceEqual(reference.QuantizedCoefficients);
        var matchesNbis = analysis.QuantizedCoefficients.SequenceEqual(nbis.QuantizedCoefficients);

        return new(
            fileName,
            bitRate,
            matchesReference,
            matchesNbis,
            GetFirstMismatchIndex(analysis.QuantizedCoefficients, reference.QuantizedCoefficients),
            GetFirstMismatchIndex(analysis.QuantizedCoefficients, nbis.QuantizedCoefficients),
            FindFirstBinDifference(analysis.QuantizationTable.QuantizationBins, reference.QuantizationTable.QuantizationBins),
            FindFirstBinDifference(analysis.QuantizationTable.QuantizationBins, nbis.QuantizationBins),
            GetFirstMismatchIndex(strategyReport.DoublePrecisionEncoderQuantizedCoefficients, reference.QuantizedCoefficients),
            GetFirstMismatchIndex(strategyReport.DoubleQuantizedManagedBins, reference.QuantizedCoefficients),
            GetFirstMismatchIndex(strategyReport.SmallBinRoundedQuantizedCoefficients, reference.QuantizedCoefficients),
            GetFirstMismatchIndex(strategyReport.FloatVarianceReferencePrecisionQuantizedCoefficients, reference.QuantizedCoefficients),
            GetFirstMismatchIndex(strategyReport.TableQuantizedCoefficients, reference.QuantizedCoefficients),
            GetFirstMismatchIndex(strategyReport.ReferencePrecisionQuantizedCoefficients, reference.QuantizedCoefficients),
            GetFirstMismatchIndex(strategyReport.RoundedHeaderQuantizedCoefficients, reference.QuantizedCoefficients),
            GetFirstMismatchIndex(strategyReport.DoubleRoundTrippedTableQuantizedCoefficients, reference.QuantizedCoefficients));
    }

    private static int GetFirstMismatchIndex(ReadOnlySpan<short> actual, ReadOnlySpan<short> expected)
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

    private static string FindFirstBinDifference(IReadOnlyList<double> actualBins, IReadOnlyList<double> expectedBins)
    {
        for (var index = 0; index < Math.Min(actualBins.Count, expectedBins.Count); index++)
        {
            if (actualBins[index].CompareTo(expectedBins[index]) == 0)
            {
                continue;
            }

            return $"index {index}: actual={actualBins[index]:G17}, expected={expectedBins[index]:G17}";
        }

        return actualBins.Count == expectedBins.Count
            ? "none"
            : $"length mismatch: actual={actualBins.Count}, expected={expectedBins.Count}";
    }

    private static void PrintSummary(IReadOnlyList<CaseReport> reports)
    {
        PrintRateSummary("Overall", reports);

        foreach (var bitRate in new[] { 0.75, 2.25 })
        {
            PrintRateSummary($"Bitrate {bitRate:0.##}", reports.Where(report => report.BitRate == bitRate).ToArray());
        }

        PrintStrategySummary("Alternative Strategy Summary", reports);
        foreach (var bitRate in new[] { 0.75, 2.25 })
        {
            PrintStrategySummary(
                $"Alternative Strategy Summary @ {bitRate:0.##}",
                reports.Where(report => report.BitRate == bitRate).ToArray());
        }
        PrintCategory("Matches Neither", reports.Where(static report => !report.MatchesReference && !report.MatchesNbis));
        PrintCategory("Matches NIST Only", reports.Where(static report => report.MatchesReference && !report.MatchesNbis));
        PrintCategory("Matches NBIS Only", reports.Where(static report => !report.MatchesReference && report.MatchesNbis));
    }

    private static void PrintRateSummary(string label, IReadOnlyCollection<CaseReport> reports)
    {
        var matchesBothCount = reports.Count(static report => report.MatchesReference && report.MatchesNbis);
        var matchesReferenceOnlyCount = reports.Count(static report => report.MatchesReference && !report.MatchesNbis);
        var matchesNbisOnlyCount = reports.Count(static report => !report.MatchesReference && report.MatchesNbis);
        var matchesNeitherCount = reports.Count(static report => !report.MatchesReference && !report.MatchesNbis);

        Console.WriteLine(label);
        Console.WriteLine($"  Total: {reports.Count}");
        Console.WriteLine($"  Matches both: {matchesBothCount}");
        Console.WriteLine($"  Matches NIST only: {matchesReferenceOnlyCount}");
        Console.WriteLine($"  Matches NBIS only: {matchesNbisOnlyCount}");
        Console.WriteLine($"  Matches neither: {matchesNeitherCount}");
    }

    private static void PrintCategory(string label, IEnumerable<CaseReport> reports)
    {
        Console.WriteLine(label);

        foreach (var report in reports.OrderBy(static report => report.BitRate).ThenBy(static report => report.FileName, StringComparer.Ordinal))
        {
            Console.WriteLine(
                $"  {report.FileName} @ {report.BitRate:0.##}: "
                + $"refMismatch={report.ReferenceMismatchIndex}, "
                + $"nbisMismatch={report.NbisMismatchIndex}, "
                + $"refQbin={report.ReferenceQuantizationDifference}, "
                + $"nbisQbin={report.NbisQuantizationDifference}, "
                + $"doublePrecisionMismatch={report.DoublePrecisionMismatchIndex}, "
                + $"doubleManagedBinsMismatch={report.DoubleQuantizedManagedBinsMismatchIndex}, "
                + $"floatVarRefPrecisionMismatch={report.FloatVarianceReferencePrecisionMismatchIndex}, "
                + $"tableMismatch={report.TableQuantizationMismatchIndex}, "
                + $"refPrecisionMismatch={report.ReferencePrecisionMismatchIndex}, "
                + $"roundedHeaderMismatch={report.RoundedHeaderMismatchIndex}, "
                + $"doubleRoundTripMismatch={report.DoubleRoundTrippedTableMismatchIndex}");
        }
    }

    private static void PrintStrategySummary(string label, IReadOnlyCollection<CaseReport> reports)
    {
        Console.WriteLine(label);
        Console.WriteLine($"  Current exact NIST matches: {reports.Count(static report => report.ReferenceMismatchIndex == -1)}");
        Console.WriteLine($"  Full double-precision exact NIST matches: {reports.Count(static report => report.DoublePrecisionMismatchIndex == -1)}");
        Console.WriteLine($"  Double-quantized managed-bin exact NIST matches: {reports.Count(static report => report.DoubleQuantizedManagedBinsMismatchIndex == -1)}");
        Console.WriteLine($"  Small-bin rounded exact NIST matches: {reports.Count(static report => report.SmallBinRoundedMismatchIndex == -1)}");
        Console.WriteLine($"  Float-variance reference-bin exact NIST matches: {reports.Count(static report => report.FloatVarianceReferencePrecisionMismatchIndex == -1)}");
        Console.WriteLine($"  Managed table exact NIST matches: {reports.Count(static report => report.TableQuantizationMismatchIndex == -1)}");
        Console.WriteLine($"  Reference-precision exact NIST matches: {reports.Count(static report => report.ReferencePrecisionMismatchIndex == -1)}");
        Console.WriteLine($"  Rounded-header exact NIST matches: {reports.Count(static report => report.RoundedHeaderMismatchIndex == -1)}");
        Console.WriteLine($"  Double-roundtripped-table exact NIST matches: {reports.Count(static report => report.DoubleRoundTrippedTableMismatchIndex == -1)}");
    }

    private static StrategyReport BuildStrategyReport(
        ReadOnlySpan<byte> rawBytes,
        WsqRawImageDescription rawImage,
        double bitRate)
    {
        WsqWaveletTreeBuilder.Build(rawImage.Width, rawImage.Height, out _, out var quantizationTree);
        var normalizedImage = WsqFloatImageNormalizer.Normalize(rawBytes);
        var waveletData = WsqDecomposition.Decompose(
            normalizedImage.Pixels.ToArray(),
            rawImage.Width,
            rawImage.Height,
            BuildWaveletTree(rawImage),
            WsqReferenceTables.StandardTransformTable);
        var doublePrecisionEncoderQuantizedCoefficients = QuantizeUsingDoublePrecisionEncoder(
            rawBytes,
            rawImage.Width,
            rawImage.Height,
            BuildWaveletTree(rawImage),
            quantizationTree.ToArray(),
            WsqReferenceTables.StandardTransformTable,
            bitRate);
        var doublePrecisionWaveletData = DecomposeWithReferencePrecision(
            NormalizeWithReferencePrecision(rawBytes).Pixels,
            rawImage.Width,
            rawImage.Height,
            BuildWaveletTree(rawImage),
            WsqReferenceTables.StandardTransformTable);
        var analysis = WsqEncoderAnalysisPipeline.Analyze(rawBytes, rawImage, new WsqEncodeOptions(bitRate));
        var doubleQuantizedManagedBins = QuantizeUsingCurrentFloatBinsWithDoubleQuantizer(
            waveletData,
            quantizationTree,
            rawImage.Width,
            bitRate);
        var tableQuantizedCoefficients = QuantizeUsingProvidedQuantizationTable(
            waveletData,
            quantizationTree,
            rawImage.Width,
            analysis.QuantizationTable);
        var smallBinRoundedQuantizedCoefficients = QuantizeUsingRoundedSmallBins(
            waveletData,
            quantizationTree,
            rawImage.Width,
            analysis.QuantizationTable,
            1.0);
        var floatVarianceReferencePrecisionQuantizedCoefficients = QuantizeUsingFloatVariancesWithReferencePrecisionBins(
            waveletData,
            quantizationTree,
            rawImage.Width,
            bitRate);
        var referencePrecisionQuantizedCoefficients = QuantizeUsingReferencePrecision(
            waveletData,
            quantizationTree,
            rawImage.Width,
            bitRate);
        var roundedHeaderQuantizedCoefficients = QuantizeUsingDoublePrecisionEncoderWithRoundedHeader(
            rawBytes,
            rawImage.Width,
            rawImage.Height,
            BuildWaveletTree(rawImage),
            quantizationTree.ToArray(),
            WsqReferenceTables.StandardTransformTable,
            bitRate);
        var doubleRoundTrippedTableQuantizedCoefficients = QuantizeUsingDoubleWaveletWithProvidedQuantizationTable(
            doublePrecisionWaveletData,
            quantizationTree,
            rawImage.Width,
            RoundTripQuantizationTable(analysis.QuantizationTable));

        return new(
            doublePrecisionEncoderQuantizedCoefficients,
            doubleQuantizedManagedBins,
            smallBinRoundedQuantizedCoefficients,
            floatVarianceReferencePrecisionQuantizedCoefficients,
            tableQuantizedCoefficients,
            referencePrecisionQuantizedCoefficients,
            roundedHeaderQuantizedCoefficients,
            doubleRoundTrippedTableQuantizedCoefficients);
    }

    private static WsqWaveletNode[] BuildWaveletTree(WsqRawImageDescription rawImage)
    {
        WsqWaveletTreeBuilder.Build(rawImage.Width, rawImage.Height, out var waveletTree, out _);
        return waveletTree;
    }

    private static short[] QuantizeUsingProvidedQuantizationTable(
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

    private static short[] QuantizeUsingDoubleWaveletWithProvidedQuantizationTable(
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

    private static short[] QuantizeUsingRoundedSmallBins(
        ReadOnlySpan<float> waveletData,
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
        int width,
        WsqQuantizationTable quantizationTable,
        double roundingThreshold)
    {
        var quantizationBins = new double[quantizationTable.QuantizationBins.Count];
        var zeroBins = new double[quantizationTable.ZeroBins.Count];

        for (var subband = 0; subband < quantizationBins.Length; subband++)
        {
            quantizationBins[subband] = quantizationTable.QuantizationBins[subband] <= roundingThreshold
                ? WsqScaledValueCodec.RoundTripUInt16(quantizationTable.QuantizationBins[subband])
                : quantizationTable.QuantizationBins[subband];
            zeroBins[subband] = quantizationTable.ZeroBins[subband] <= (roundingThreshold * 1.2)
                ? WsqScaledValueCodec.RoundTripUInt16(quantizationTable.ZeroBins[subband])
                : quantizationTable.ZeroBins[subband];
        }

        return QuantizeUsingProvidedBins(waveletData, quantizationTree, width, quantizationBins, zeroBins);
    }

    private static short[] QuantizeUsingProvidedBins(
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

    private static short[] QuantizeUsingProvidedBins(
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

    private static short[] QuantizeUsingReferencePrecision(
        ReadOnlySpan<float> waveletData,
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
        int width,
        double bitRate)
    {
        var variances = ComputeVariancesFromFloatWaveletDataWithReferencePrecision(waveletData, quantizationTree, width);
        var quantizationBins = new double[WsqConstants.MaxSubbands];
        var zeroBins = new double[WsqConstants.MaxSubbands];
        ComputeQuantizationBinsWithReferencePrecision(variances, bitRate, quantizationBins, zeroBins);

        return QuantizeUsingProvidedBins(waveletData, quantizationTree, width, quantizationBins, zeroBins);
    }

    private static short[] QuantizeUsingFloatVariancesWithReferencePrecisionBins(
        ReadOnlySpan<float> waveletData,
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
        int width,
        double bitRate)
    {
        var floatVariances = WsqVarianceCalculator.Compute(waveletData, quantizationTree, width);
        var referencePrecisionVariances = new double[floatVariances.Length];

        for (var index = 0; index < floatVariances.Length; index++)
        {
            referencePrecisionVariances[index] = floatVariances[index];
        }

        var quantizationBins = new double[WsqConstants.MaxSubbands];
        var zeroBins = new double[WsqConstants.MaxSubbands];
        ComputeQuantizationBinsWithReferencePrecision(referencePrecisionVariances, bitRate, quantizationBins, zeroBins);
        return QuantizeUsingProvidedBins(waveletData, quantizationTree, width, quantizationBins, zeroBins);
    }

    private static short[] QuantizeUsingCurrentFloatBinsWithDoubleQuantizer(
        ReadOnlySpan<float> waveletData,
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
        int width,
        double bitRate)
    {
        var floatVariances = WsqVarianceCalculator.Compute(waveletData, quantizationTree, width);
        var quantizationBins = new float[WsqConstants.MaxSubbands];
        var zeroBins = new float[WsqConstants.MaxSubbands];
        ComputeQuantizationBinsWithCurrentPrecision(floatVariances, (float)bitRate, quantizationBins, zeroBins);

        var doubleQuantizationBins = new double[quantizationBins.Length];
        var doubleZeroBins = new double[zeroBins.Length];

        for (var index = 0; index < quantizationBins.Length; index++)
        {
            doubleQuantizationBins[index] = quantizationBins[index];
            doubleZeroBins[index] = zeroBins[index];
        }

        return QuantizeUsingProvidedBins(waveletData, quantizationTree, width, doubleQuantizationBins, doubleZeroBins);
    }

    private static WsqQuantizationTable RoundTripQuantizationTable(WsqQuantizationTable quantizationTable)
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

    private static double RoundTripScaledUInt16(double value)
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

    private static short[] QuantizeUsingDoublePrecisionEncoder(
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

    private static short[] QuantizeUsingDoublePrecisionEncoderWithRoundedHeader(
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
        var roundedNormalizedImage = NormalizeWithFixedParameters(rawPixels, roundedShift, roundedScale);
        var waveletData = DecomposeWithReferencePrecision(
            roundedNormalizedImage.Pixels,
            width,
            height,
            waveletTree,
            transformTable);
        return QuantizeDoublePrecisionWaveletData(waveletData, quantizationTree, width, bitRate);
    }

    private static DoubleNormalizedImage NormalizeWithReferencePrecision(ReadOnlySpan<byte> rawPixels)
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

    private static DoubleNormalizedImage NormalizeWithFixedParameters(
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

    private static double[] DecomposeWithReferencePrecision(
        double[] normalizedPixels,
        int width,
        int height,
        WsqWaveletNode[] waveletTree,
        WsqTransformTable transformTable)
    {
        var workingWaveletData = normalizedPixels.ToArray();
        var lowPassFilter = transformTable.LowPassFilterCoefficients.Select(static value => (double)value).ToArray();
        var highPassFilter = transformTable.HighPassFilterCoefficients.Select(static value => (double)value).ToArray();
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

    private static void GetLetsDouble(
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

    private static short[] QuantizeDoublePrecisionWaveletData(
        ReadOnlySpan<double> waveletData,
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
        int width,
        double bitRate)
    {
        var variances = ComputeVariancesWithReferencePrecision(waveletData, quantizationTree, width);
        var quantizationBins = new double[WsqConstants.MaxSubbands];
        var zeroBins = new double[WsqConstants.MaxSubbands];
        ComputeQuantizationBinsWithReferencePrecision(variances, bitRate, quantizationBins, zeroBins);

        return QuantizeUsingProvidedBins(waveletData, quantizationTree, width, quantizationBins, zeroBins);
    }

    private static double[] ComputeVariancesFromFloatWaveletDataWithReferencePrecision(
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

    private static double[] ComputeVariancesWithReferencePrecision(
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

    private static double ComputeVarianceWithReferencePrecision(
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

    private static void ComputeQuantizationBinsWithReferencePrecision(
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

    private static void SetReferencePrecisionReciprocalSubbandAreas(Span<double> reciprocalSubbandAreas)
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

    private static void ComputeQuantizationBinsWithCurrentPrecision(
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

    private static double GetReferencePrecisionSubbandWeight(int subband)
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

    private sealed record RawImageDimensions(
        string FileName,
        int Width,
        int Height);

    private sealed record WsqReferenceQuantizedCoefficients(
        WsqQuantizationTable QuantizationTable,
        short[] QuantizedCoefficients);

    private sealed record NbisAnalysisDump(
        double[] QuantizationBins,
        short[] QuantizedCoefficients);

    private sealed record DoubleNormalizedImage(
        double[] Pixels,
        double Shift,
        double Scale);

    private sealed record CaseReport(
        string FileName,
        double BitRate,
        bool MatchesReference,
        bool MatchesNbis,
        int ReferenceMismatchIndex,
        int NbisMismatchIndex,
        string ReferenceQuantizationDifference,
        string NbisQuantizationDifference,
        int DoublePrecisionMismatchIndex,
        int DoubleQuantizedManagedBinsMismatchIndex,
        int SmallBinRoundedMismatchIndex,
        int FloatVarianceReferencePrecisionMismatchIndex,
        int TableQuantizationMismatchIndex,
        int ReferencePrecisionMismatchIndex,
        int RoundedHeaderMismatchIndex,
        int DoubleRoundTrippedTableMismatchIndex);

    private sealed record StrategyReport(
        short[] DoublePrecisionEncoderQuantizedCoefficients,
        short[] DoubleQuantizedManagedBins,
        short[] SmallBinRoundedQuantizedCoefficients,
        short[] FloatVarianceReferencePrecisionQuantizedCoefficients,
        short[] TableQuantizedCoefficients,
        short[] ReferencePrecisionQuantizedCoefficients,
        short[] RoundedHeaderQuantizedCoefficients,
        short[] DoubleRoundTrippedTableQuantizedCoefficients);
}
