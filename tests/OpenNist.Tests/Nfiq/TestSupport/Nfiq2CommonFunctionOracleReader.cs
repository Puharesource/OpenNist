namespace OpenNist.Tests.Nfiq.TestSupport;

using System.Diagnostics;
using System.Globalization;

internal static class Nfiq2CommonFunctionOracleReader
{
    private static readonly Lazy<string> s_oraclePath = new(BuildOracle);

    public static Nfiq2BlockGridOracleResult ReadBlockGrid(string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = s_oraclePath.Value,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Nfiq2TestPaths.NfiqDiagnosticToolDirectory,
            ArgumentList = { "block-grid", imagePath },
        }) ?? throw new InvalidOperationException("Failed to start native NFIQ common-function oracle.");

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Native NFIQ common-function oracle failed with exit code {process.ExitCode}: {standardError}");
        }

        return ParseBlockGrid(standardOutput);
    }

    public static Nfiq2RotatedBlockOracleResult ReadRotatedBlock(string imagePath, int row, int column)
    {
        var output = RunOracle("rotated-block", imagePath, row, column);
        return ParseRotatedBlock(output);
    }

    public static Nfiq2RidgeStructureOracleResult ReadRidgeStructure(string imagePath, int row, int column)
    {
        var output = RunOracle("ridge-structure", imagePath, row, column);
        return ParseRidgeStructure(output);
    }

    public static Nfiq2FrequencyDomainOracleResult ReadFrequencyDomainBlock(string imagePath, int row, int column)
    {
        var output = RunOracle("fda-block", imagePath, row, column);
        return ParseFrequencyDomainBlock(output);
    }

    private static string BuildOracle()
    {
        Directory.CreateDirectory(Nfiq2TestPaths.NfiqDiagnosticToolDirectory);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/bash",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Nfiq2TestPaths.NfiqDiagnosticToolDirectory,
            ArgumentList = { Nfiq2TestPaths.NfiqCommonOracleBuildScriptPath },
        }) ?? throw new InvalidOperationException("Failed to start native NFIQ common-function oracle build.");

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to build native NFIQ common-function oracle with exit code {process.ExitCode}: {standardError}");
        }

        if (!File.Exists(Nfiq2TestPaths.NfiqCommonOraclePath))
        {
            throw new InvalidOperationException(
                $"Native NFIQ common-function oracle was not produced. Output: {standardOutput}");
        }

        return Nfiq2TestPaths.NfiqCommonOraclePath;
    }

    private static string RunOracle(string command, string imagePath, int row, int column)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = s_oraclePath.Value,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Nfiq2TestPaths.NfiqDiagnosticToolDirectory,
            ArgumentList =
            {
                command,
                imagePath,
                row.ToString(CultureInfo.InvariantCulture),
                column.ToString(CultureInfo.InvariantCulture),
            },
        }) ?? throw new InvalidOperationException("Failed to start native NFIQ common-function oracle.");

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Native NFIQ common-function oracle failed with exit code {process.ExitCode}: {standardError}");
        }

        return standardOutput;
    }

    private static Nfiq2BlockGridOracleResult ParseBlockGrid(string text)
    {
        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
        {
            throw new InvalidOperationException("Native NFIQ common-function oracle returned no output.");
        }

        var sizeTokens = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (sizeTokens.Length != 3 || !string.Equals(sizeTokens[0], "size", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected size header from native NFIQ common-function oracle: {lines[0]}");
        }

        var width = int.Parse(sizeTokens[1], CultureInfo.InvariantCulture);
        var height = int.Parse(sizeTokens[2], CultureInfo.InvariantCulture);
        var blocks = new List<Nfiq2BlockOracleEntry>();

        foreach (var line in lines.Skip(1))
        {
            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length != 5 || !string.Equals(tokens[0], "block", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unexpected block line from native NFIQ common-function oracle: {line}");
            }

            blocks.Add(new(
                int.Parse(tokens[1], CultureInfo.InvariantCulture),
                int.Parse(tokens[2], CultureInfo.InvariantCulture),
                tokens[3] == "1",
                double.Parse(tokens[4], CultureInfo.InvariantCulture)));
        }

        return new(width, height, blocks);
    }

    private static Nfiq2RotatedBlockOracleResult ParseRotatedBlock(string text)
    {
        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length != 4)
        {
            throw new InvalidOperationException($"Unexpected rotated-block response from native NFIQ common-function oracle: {text}");
        }

        ParseSizeHeader(lines[0], out var width, out var height);
        var orientation = ParseSingleDoubleLine(lines[1], "orientation");
        var sourcePixels = ParseByteVectorLine(lines[2], "source");
        var pixels = ParseByteVectorLine(lines[3], "data");
        return new(width, height, orientation, sourcePixels, pixels);
    }

    private static Nfiq2RidgeStructureOracleResult ParseRidgeStructure(string text)
    {
        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length != 7)
        {
            throw new InvalidOperationException($"Unexpected ridge-structure response from native NFIQ common-function oracle: {text}");
        }

        ParseSizeHeader(lines[0], out var width, out var height);
        var orientation = ParseSingleDoubleLine(lines[1], "orientation");
        var pixels = ParseByteVectorLine(lines[2], "data");
        var trendLine = ParseDoubleVectorLine(lines[3], "dt");
        var ridgeValleyPattern = ParseByteVectorLine(lines[4], "ridval");
        var ridgeValleyUniformityRatios = ParseDoubleVectorLine(lines[5], "rvu");
        var localClarity = ParseSingleDoubleLine(lines[6], "lcs");
        return new(width, height, orientation, pixels, trendLine, ridgeValleyPattern, ridgeValleyUniformityRatios, localClarity);
    }

    private static Nfiq2FrequencyDomainOracleResult ParseFrequencyDomainBlock(string text)
    {
        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length != 4)
        {
            throw new InvalidOperationException($"Unexpected fda-block response from native NFIQ common-function oracle: {text}");
        }

        ParseSizeHeader(lines[0], out var width, out var height);
        var orientation = ParseSingleDoubleLine(lines[1], "orientation");
        var pixels = ParseByteVectorLine(lines[2], "data");
        var frequencyDomainAnalysis = ParseSingleDoubleLine(lines[3], "fda");
        return new(width, height, orientation, pixels, frequencyDomainAnalysis);
    }

    private static void ParseSizeHeader(string line, out int width, out int height)
    {
        var sizeTokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (sizeTokens.Length != 3 || !string.Equals(sizeTokens[0], "size", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected size header from native NFIQ common-function oracle: {line}");
        }

        width = int.Parse(sizeTokens[1], CultureInfo.InvariantCulture);
        height = int.Parse(sizeTokens[2], CultureInfo.InvariantCulture);
    }

    private static double ParseSingleDoubleLine(string line, string prefix)
    {
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length != 2 || !string.Equals(tokens[0], prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected '{prefix}' line from native NFIQ common-function oracle: {line}");
        }

        return double.Parse(tokens[1], CultureInfo.InvariantCulture);
    }

    private static byte[] ParseByteVectorLine(string line, string prefix)
    {
        if (!line.StartsWith(prefix + ' ', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected '{prefix}' line from native NFIQ common-function oracle: {line}");
        }

        return line[(prefix.Length + 1)..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static token => byte.Parse(token, CultureInfo.InvariantCulture))
            .ToArray();
    }

    private static double[] ParseDoubleVectorLine(string line, string prefix)
    {
        if (!line.StartsWith(prefix + ' ', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected '{prefix}' line from native NFIQ common-function oracle: {line}");
        }

        var content = line[(prefix.Length + 1)..];
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        return content
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static token => double.Parse(token, CultureInfo.InvariantCulture))
            .ToArray();
    }
}

internal sealed record Nfiq2BlockGridOracleResult(
    int Width,
    int Height,
    IReadOnlyList<Nfiq2BlockOracleEntry> Blocks);

internal sealed record Nfiq2BlockOracleEntry(
    int Row,
    int Column,
    bool AllNonZero,
    double Orientation);

internal sealed record Nfiq2RotatedBlockOracleResult(
    int Width,
    int Height,
    double Orientation,
    IReadOnlyList<byte> SourcePixels,
    IReadOnlyList<byte> Pixels);

internal sealed record Nfiq2RidgeStructureOracleResult(
    int Width,
    int Height,
    double Orientation,
    IReadOnlyList<byte> Pixels,
    IReadOnlyList<double> TrendLine,
    IReadOnlyList<byte> RidgeValleyPattern,
    IReadOnlyList<double> RidgeValleyUniformityRatios,
    double LocalClarity);

internal sealed record Nfiq2FrequencyDomainOracleResult(
    int Width,
    int Height,
    double Orientation,
    IReadOnlyList<byte> Pixels,
    double FrequencyDomainAnalysis);
