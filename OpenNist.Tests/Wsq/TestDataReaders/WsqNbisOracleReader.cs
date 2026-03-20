namespace OpenNist.Tests.Wsq.TestDataReaders;

using System.Diagnostics;
using System.Globalization;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq.Internal;

internal static class WsqNbisOracleReader
{
    private static string RepositoryRoot { get; } = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));

    private static string DiagnosticToolDirectory { get; } = Path.Combine(RepositoryRoot, "tmp", "wsq-diag");

    private static string AnalysisToolPath { get; } = Path.Combine(DiagnosticToolDirectory, "nbis_dump");

    private static string WaveletToolPath { get; } = Path.Combine(DiagnosticToolDirectory, "nbis_wavelet_dump");

    public static bool IsAvailable()
    {
        return File.Exists(AnalysisToolPath) && File.Exists(WaveletToolPath);
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
        var quantizedCoefficients = new List<short>();

        foreach (var line in standardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
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

        return new(shift, scale, quantizationBins, zeroBins, variances, quantizedCoefficients.ToArray());
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

    private static void EnsureAvailability()
    {
        if (IsAvailable())
        {
            return;
        }

        throw new InvalidOperationException(
            $"Local NBIS diagnostic helpers were not found under '{DiagnosticToolDirectory}'. "
            + "Build or restore the tmp/wsq-diag tools before running the NBIS stage-oracle tests.");
    }
}

internal sealed record WsqNbisAnalysisDump(
    double Shift,
    double Scale,
    double[] QuantizationBins,
    double[] ZeroBins,
    double[] Variances,
    short[] QuantizedCoefficients);
