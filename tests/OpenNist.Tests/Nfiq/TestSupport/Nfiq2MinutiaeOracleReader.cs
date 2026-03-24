namespace OpenNist.Tests.Nfiq.TestSupport;

using System.Diagnostics;
using System.Globalization;
using OpenNist.Nfiq.Internal;

internal static class Nfiq2MinutiaeOracleReader
{
    private static readonly Lazy<string> s_oraclePath = new(BuildOracle);

    public static IReadOnlyList<Nfiq2Minutia> ReadMinutiae(string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = s_oraclePath.Value,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Nfiq2TestPaths.NfiqDiagnosticToolDirectory,
            ArgumentList = { imagePath, "minutiae" },
        }) ?? throw new InvalidOperationException("Failed to start native NFIQ minutiae oracle.");

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Native NFIQ minutiae oracle failed with exit code {process.ExitCode}: {standardError}");
        }

        return ParseMinutiae(standardOutput);
    }

    private static string BuildOracle()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/bash",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Nfiq2TestPaths.NfiqDiagnosticToolDirectory,
            ArgumentList = { Nfiq2TestPaths.NfiqMinutiaeOracleBuildScriptPath },
        }) ?? throw new InvalidOperationException("Failed to start native NFIQ minutiae oracle build.");

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to build native NFIQ minutiae oracle with exit code {process.ExitCode}: {standardError}");
        }

        if (!File.Exists(Nfiq2TestPaths.NfiqMinutiaeOraclePath))
        {
            throw new InvalidOperationException(
                $"Native NFIQ minutiae oracle was not produced. Output: {standardOutput}");
        }

        return Nfiq2TestPaths.NfiqMinutiaeOraclePath;
    }

    private static List<Nfiq2Minutia> ParseMinutiae(string text)
    {
        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0 || !lines[0].StartsWith("size ", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected minutiae response from native NFIQ oracle: {text}");
        }

        var minutiae = new List<Nfiq2Minutia>();
        foreach (var line in lines.Skip(1))
        {
            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length != 6 || !string.Equals(tokens[0], "minutia", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unexpected minutia line from native NFIQ oracle: {line}");
            }

            minutiae.Add(new(
                int.Parse(tokens[1], CultureInfo.InvariantCulture),
                int.Parse(tokens[2], CultureInfo.InvariantCulture),
                int.Parse(tokens[3], CultureInfo.InvariantCulture),
                int.Parse(tokens[4], CultureInfo.InvariantCulture),
                int.Parse(tokens[5], CultureInfo.InvariantCulture)));
        }

        return minutiae;
    }
}
