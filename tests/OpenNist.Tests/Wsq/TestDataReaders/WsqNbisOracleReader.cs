namespace OpenNist.Tests.Wsq.TestDataReaders;

using System.Diagnostics;
using System.Globalization;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Tests.Wsq.TestSupport;
using OpenNist.Wsq.Internal;

internal static class WsqNbisOracleReader
{
    private const string s_solutionFileName = "OpenNist.slnx";

    private static string RepositoryRoot { get; } = FindRepositoryRoot();

    private static string DiagnosticToolDirectory { get; } = Path.Combine(RepositoryRoot, "tools", "wsq-diag");

    private static string AnalysisToolPath { get; } = Path.Combine(DiagnosticToolDirectory, "nbis_dump");

    private static string WaveletToolPath { get; } = Path.Combine(DiagnosticToolDirectory, "nbis_wavelet_dump");

    private static string ScaleToolPath { get; } = Path.Combine(DiagnosticToolDirectory, "nbis_scale_dump");

    private static string EncoderToolPath { get; } = "/tmp/nbis_v5_0_0/Rel_5.0.0/imgtools/bin/cwsq";

    private static string ExpectedOracleVersion { get; } = WsqTestCaseDefinitions.s_nbis500Version;

    private static bool VersionValidated { get; set; }

    public static bool IsAvailable()
    {
        return File.Exists(AnalysisToolPath)
            && File.Exists(WaveletToolPath)
            && File.Exists(ScaleToolPath)
            && File.Exists(EncoderToolPath)
            && ValidateVersion();
    }

    public static bool TryScaleUInt16(float value, out WsqScaledUInt16 scaledValue)
    {
        scaledValue = default;

        if (!File.Exists(ScaleToolPath) || !ValidateVersion())
        {
            return false;
        }

        using var process = Process.Start(CreateStartInfo(
            ScaleToolPath,
            value.ToString("R", CultureInfo.InvariantCulture)));

        if (process is null)
        {
            return false;
        }

        var standardOutput = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            return false;
        }

        var tokens = standardOutput.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        byte? scale = null;
        ushort? raw = null;
        foreach (var token in tokens)
        {
            if (token.StartsWith("scale=", StringComparison.Ordinal))
            {
                scale = byte.Parse(token["scale=".Length..], CultureInfo.InvariantCulture);
            }
            else if (token.StartsWith("raw=", StringComparison.Ordinal))
            {
                raw = ushort.Parse(token["raw=".Length..], CultureInfo.InvariantCulture);
            }
        }

        if (!scale.HasValue || !raw.HasValue)
        {
            return false;
        }

        scaledValue = new(raw.Value, scale.Value);
        return true;
    }

    public static async Task<byte[]> ReadCodestreamAsync(WsqEncodingReferenceCase testCase)
    {
        EnsureAvailability();

        var tempDirectory = Directory.CreateTempSubdirectory("opennist-nbis-cwsq-");
        var tempRawPath = Path.Combine(tempDirectory.FullName, Path.GetFileName(testCase.RawPath));
        var tempWsqPath = Path.Combine(tempDirectory.FullName, Path.ChangeExtension(Path.GetFileName(testCase.RawPath), ".wsq"));

        try
        {
            File.Copy(testCase.RawPath, tempRawPath, overwrite: true);

            var startInfo = new ProcessStartInfo
            {
                FileName = EncoderToolPath,
                WorkingDirectory = tempDirectory.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(testCase.BitRate.ToString("0.##", CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("wsq");
            startInfo.ArgumentList.Add(Path.GetFileName(tempRawPath));
            startInfo.ArgumentList.Add("-r");
            startInfo.ArgumentList.Add($"{testCase.RawImage.Width.ToString(CultureInfo.InvariantCulture)},{testCase.RawImage.Height.ToString(CultureInfo.InvariantCulture)},8,500");

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
            catch (IOException)
            {
                // Ignore temp cleanup failures in local diagnostics.
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore temp cleanup failures in local diagnostics.
            }
        }
    }

    public static async Task<WsqNbisAnalysisDump> ReadAnalysisAsync(WsqEncodingReferenceCase testCase)
    {
        EnsureAvailability();

        var startInfo = CreateStartInfo(
            AnalysisToolPath,
            testCase.RawPath,
            testCase.RawImage.Width.ToString(CultureInfo.InvariantCulture),
            testCase.RawImage.Height.ToString(CultureInfo.InvariantCulture),
            testCase.BitRate.ToString("0.##", CultureInfo.InvariantCulture));

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
        var quantizationTree = new WsqNbisQuantizationNode[WsqConstants.NumberOfSubbands];
        var quantizedCoefficients = new List<short>();

        foreach (var line in standardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("qtree[", StringComparison.Ordinal))
            {
                ParseQuantizationTreeNode(line, quantizationTree);
                continue;
            }

            if (line.StartsWith("var[", StringComparison.Ordinal))
            {
                var subbandIndex = ParseIndexedTokenIndex(line, "var");
                variances[subbandIndex] = ParseIndexedTokenValue(line);
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
                var quantizationToken = tokens[0];
                var zeroToken = tokens[1];
                var subbandIndex = ParseIndexedTokenIndex(quantizationToken, "qbin");
                quantizationBins[subbandIndex] = ParseIndexedTokenValue(quantizationToken);
                zeroBins[subbandIndex] = ParseIndexedTokenValue(zeroToken);
                continue;
            }

            if (line.StartsWith("coeff[", StringComparison.Ordinal))
            {
                quantizedCoefficients.Add(checked((short)int.Parse(line[(line.IndexOf('=', StringComparison.Ordinal) + 1)..], CultureInfo.InvariantCulture)));
            }
        }

        return new(shift, scale, quantizationBins, zeroBins, variances, quantizationTree, quantizedCoefficients.ToArray());
    }

    public static Task<float[]> ReadNormalizedPixelsAsync(WsqEncodingReferenceCase testCase)
    {
        return ReadWaveletDataAsync(testCase, stopNode: -1);
    }

    public static async Task<float[]> ReadWaveletDataAsync(WsqEncodingReferenceCase testCase, int stopNode = 19)
    {
        EnsureAvailability();

        var startInfo = CreateStartInfo(
            WaveletToolPath,
            testCase.RawPath,
            testCase.RawImage.Width.ToString(CultureInfo.InvariantCulture),
            testCase.RawImage.Height.ToString(CultureInfo.InvariantCulture),
            stopNode.ToString(CultureInfo.InvariantCulture));

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

    public static async Task<float[]> ReadRowPassDataAsync(WsqEncodingReferenceCase testCase, int stopNode)
    {
        EnsureAvailability();

        var startInfo = CreateStartInfo(
            WaveletToolPath,
            testCase.RawPath,
            testCase.RawImage.Width.ToString(CultureInfo.InvariantCulture),
            testCase.RawImage.Height.ToString(CultureInfo.InvariantCulture),
            stopNode.ToString(CultureInfo.InvariantCulture),
            "row");

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

    private static ProcessStartInfo CreateStartInfo(string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static int ParseIndexedTokenIndex(string token, string tokenName)
    {
        var startIndex = tokenName.Length + 1;
        var endIndex = token.IndexOf(']', startIndex);
        return int.Parse(token[startIndex..endIndex], CultureInfo.InvariantCulture);
    }

    private static double ParseIndexedTokenValue(string token)
    {
        return double.Parse(token[(token.IndexOf('=', StringComparison.Ordinal) + 1)..], CultureInfo.InvariantCulture);
    }

    private static void ParseQuantizationTreeNode(string line, WsqNbisQuantizationNode[] quantizationTree)
    {
        var subbandIndex = ParseIndexedTokenIndex(line, "qtree");
        var payload = line[(line.IndexOf('=', StringComparison.Ordinal) + 1)..];
        var tokens = payload.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        quantizationTree[subbandIndex] = new(
            X: int.Parse(tokens[0]["x:".Length..], CultureInfo.InvariantCulture),
            Y: int.Parse(tokens[1]["y:".Length..], CultureInfo.InvariantCulture),
            Width: int.Parse(tokens[2]["lenx:".Length..], CultureInfo.InvariantCulture),
            Height: int.Parse(tokens[3]["leny:".Length..], CultureInfo.InvariantCulture));
    }

    private static void EnsureAvailability()
    {
        if (File.Exists(AnalysisToolPath)
            && File.Exists(WaveletToolPath)
            && File.Exists(EncoderToolPath)
            && ValidateVersion())
        {
            return;
        }

        throw new InvalidOperationException(
            $"Local NBIS diagnostic helpers were not found under '{DiagnosticToolDirectory}'. "
            + "Build or restore the NBIS 5.0.0-backed tools/wsq-diag helpers before running the NBIS stage-oracle tests.");
    }

    private static string FindRepositoryRoot()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, s_solutionFileName)))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException($"Unable to locate repository root containing {s_solutionFileName}.");
    }

    private static bool ValidateVersion()
    {
        if (VersionValidated)
        {
            return true;
        }

        var analysisVersion = ReadToolVersion(AnalysisToolPath);
        var waveletVersion = ReadToolVersion(WaveletToolPath);
        var scaleVersion = ReadToolVersion(ScaleToolPath);
        if (!string.Equals(analysisVersion, ExpectedOracleVersion, StringComparison.Ordinal)
            || !string.Equals(waveletVersion, ExpectedOracleVersion, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(scaleVersion, ExpectedOracleVersion, StringComparison.Ordinal))
        {
            return false;
        }

        VersionValidated = true;
        return true;
    }

    private static string? ReadToolVersion(string toolPath)
    {
        if (!File.Exists(toolPath))
        {
            return null;
        }

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = toolPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            ArgumentList = { "--version" },
        });

        if (process is null)
        {
            return null;
        }

        var standardOutput = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 ? standardOutput.Trim() : null;
    }
}

internal sealed record WsqNbisAnalysisDump(
    double Shift,
    double Scale,
    double[] QuantizationBins,
    double[] ZeroBins,
    double[] Variances,
    WsqNbisQuantizationNode[] QuantizationTree,
    short[] QuantizedCoefficients);

internal readonly record struct WsqNbisQuantizationNode(
    int X,
    int Y,
    int Width,
    int Height);
