namespace OpenNist.Tests.Nfiq.TestSupport;

using System.Diagnostics;
using System.Globalization;
using OpenNist.Nfiq.Internal;

internal static class Nfiq2FingerJetOracleReader
{
    private static readonly Lazy<string> s_oraclePath = new(BuildOracle);

    public static Nfiq2FingerJetPreparedImageOracleResult ReadPreparedImage(string imagePath, int pixelsPerInch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelsPerInch);

        var outputPath = Path.Combine(Path.GetTempPath(), "OpenNist.Nfiq", $"{Guid.NewGuid():N}.raw");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = s_oraclePath.Value,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = Nfiq2TestPaths.NfiqDiagnosticToolDirectory,
                ArgumentList =
                {
                    "prepare-image",
                    imagePath,
                    pixelsPerInch.ToString(CultureInfo.InvariantCulture),
                    outputPath,
                },
            }) ?? throw new InvalidOperationException("Failed to start native FingerJet oracle.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Native FingerJet oracle failed with exit code {process.ExitCode}: {standardError}");
            }

            var metadata = ParseMetadata(standardOutput);
            var pixels = File.ReadAllBytes(outputPath);
            return metadata with { Pixels = pixels };
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    public static Nfiq2FingerJetPreparedImageOracleResult ReadEnhancedImage(string imagePath, int pixelsPerInch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelsPerInch);

        var outputPath = Path.Combine(Path.GetTempPath(), "OpenNist.Nfiq", $"{Guid.NewGuid():N}.raw");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = s_oraclePath.Value,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = Nfiq2TestPaths.NfiqDiagnosticToolDirectory,
                ArgumentList =
                {
                    "enhanced-image",
                    imagePath,
                    pixelsPerInch.ToString(CultureInfo.InvariantCulture),
                    outputPath,
                },
            }) ?? throw new InvalidOperationException("Failed to start native FingerJet enhancement oracle.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Native FingerJet enhancement oracle failed with exit code {process.ExitCode}: {standardError}");
            }

            var metadata = ParseMetadata(standardOutput);
            var pixels = File.ReadAllBytes(outputPath);
            return metadata with { Pixels = pixels };
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    public static Nfiq2FingerJetPhasemapOracleResult ReadPhasemap(string imagePath, int pixelsPerInch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelsPerInch);

        var outputPath = Path.Combine(Path.GetTempPath(), "OpenNist.Nfiq", $"{Guid.NewGuid():N}.raw");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = s_oraclePath.Value,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = Nfiq2TestPaths.NfiqDiagnosticToolDirectory,
                ArgumentList =
                {
                    "phasemap",
                    imagePath,
                    pixelsPerInch.ToString(CultureInfo.InvariantCulture),
                    outputPath,
                },
            }) ?? throw new InvalidOperationException("Failed to start native FingerJet phasemap oracle.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Native FingerJet phasemap oracle failed with exit code {process.ExitCode}: {standardError}");
            }

            var metadata = ParsePhasemapMetadata(standardOutput);
            var pixels = File.ReadAllBytes(outputPath);
            return metadata with { Pixels = pixels };
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    public static Nfiq2FingerJetOrientationMapOracleResult ReadOrientationMap(string imagePath, int pixelsPerInch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelsPerInch);

        var orientationPath = Path.Combine(Path.GetTempPath(), "OpenNist.Nfiq", $"{Guid.NewGuid():N}.ori");
        var footprintPath = Path.Combine(Path.GetTempPath(), "OpenNist.Nfiq", $"{Guid.NewGuid():N}.fpt");
        Directory.CreateDirectory(Path.GetDirectoryName(orientationPath)!);

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = s_oraclePath.Value,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = Nfiq2TestPaths.NfiqDiagnosticToolDirectory,
                ArgumentList =
                {
                    "orientation-map",
                    imagePath,
                    pixelsPerInch.ToString(CultureInfo.InvariantCulture),
                    orientationPath,
                    footprintPath,
                },
            }) ?? throw new InvalidOperationException("Failed to start native FingerJet orientation oracle.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Native FingerJet orientation oracle failed with exit code {process.ExitCode}: {standardError}");
            }

            var metadata = ParseMetadata(standardOutput);
            var orientationBytes = File.ReadAllBytes(orientationPath);
            var footprint = File.ReadAllBytes(footprintPath);
            if (orientationBytes.Length != metadata.OrientationMapSize * 2)
            {
                throw new InvalidOperationException("Unexpected native FingerJet orientation byte count.");
            }

            var orientation = new Nfiq2FingerJetComplex[metadata.OrientationMapSize];
            for (var index = 0; index < orientation.Length; index++)
            {
                orientation[index] = new(
                    unchecked((sbyte)orientationBytes[index * 2]),
                    unchecked((sbyte)orientationBytes[(index * 2) + 1]));
            }

            return new(
                metadata.Width,
                metadata.Height,
                metadata.PixelsPerInch,
                metadata.XOffset,
                metadata.YOffset,
                metadata.OrientationMapWidth,
                metadata.OrientationMapSize,
                orientation,
                footprint);
        }
        finally
        {
            if (File.Exists(orientationPath))
            {
                File.Delete(orientationPath);
            }

            if (File.Exists(footprintPath))
            {
                File.Delete(footprintPath);
            }
        }
    }

    public static Nfiq2FingerJetOrientationMapOracleResult ReadEnhancedOrientationMap(string imagePath, int pixelsPerInch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelsPerInch);

        var orientationPath = Path.Combine(Path.GetTempPath(), "OpenNist.Nfiq", $"{Guid.NewGuid():N}.ori");
        var footprintPath = Path.Combine(Path.GetTempPath(), "OpenNist.Nfiq", $"{Guid.NewGuid():N}.fpt");
        Directory.CreateDirectory(Path.GetDirectoryName(orientationPath)!);

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = s_oraclePath.Value,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = Nfiq2TestPaths.NfiqDiagnosticToolDirectory,
                ArgumentList =
                {
                    "enhanced-orientation-map",
                    imagePath,
                    pixelsPerInch.ToString(CultureInfo.InvariantCulture),
                    orientationPath,
                    footprintPath,
                },
            }) ?? throw new InvalidOperationException("Failed to start native enhanced FingerJet orientation oracle.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Native enhanced FingerJet orientation oracle failed with exit code {process.ExitCode}: {standardError}");
            }

            var metadata = ParseMetadata(standardOutput);
            var orientationBytes = File.ReadAllBytes(orientationPath);
            var footprint = File.ReadAllBytes(footprintPath);
            if (orientationBytes.Length != metadata.OrientationMapSize * 2)
            {
                throw new InvalidOperationException("Unexpected native enhanced FingerJet orientation byte count.");
            }

            var orientation = new Nfiq2FingerJetComplex[metadata.OrientationMapSize];
            for (var index = 0; index < orientation.Length; index++)
            {
                orientation[index] = new(
                    unchecked((sbyte)orientationBytes[index * 2]),
                    unchecked((sbyte)orientationBytes[(index * 2) + 1]));
            }

            return new(
                metadata.Width,
                metadata.Height,
                metadata.PixelsPerInch,
                metadata.XOffset,
                metadata.YOffset,
                metadata.OrientationMapWidth,
                metadata.OrientationMapSize,
                orientation,
                footprint);
        }
        finally
        {
            if (File.Exists(orientationPath))
            {
                File.Delete(orientationPath);
            }

            if (File.Exists(footprintPath))
            {
                File.Delete(footprintPath);
            }
        }
    }

    public static Nfiq2FingerJetOrientationMapOracleResult ReadRawOrientationMap(string imagePath, int pixelsPerInch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelsPerInch);

        var orientationPath = Path.Combine(Path.GetTempPath(), "OpenNist.Nfiq", $"{Guid.NewGuid():N}.ori");
        var footprintPath = Path.Combine(Path.GetTempPath(), "OpenNist.Nfiq", $"{Guid.NewGuid():N}.fpt");
        Directory.CreateDirectory(Path.GetDirectoryName(orientationPath)!);

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = s_oraclePath.Value,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = Nfiq2TestPaths.NfiqDiagnosticToolDirectory,
                ArgumentList =
                {
                    "raw-orientation-map",
                    imagePath,
                    pixelsPerInch.ToString(CultureInfo.InvariantCulture),
                    orientationPath,
                    footprintPath,
                },
            }) ?? throw new InvalidOperationException("Failed to start native raw FingerJet orientation oracle.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Native raw FingerJet orientation oracle failed with exit code {process.ExitCode}: {standardError}");
            }

            var metadata = ParseMetadata(standardOutput);
            var orientationBytes = File.ReadAllBytes(orientationPath);
            var footprint = File.ReadAllBytes(footprintPath);
            if (orientationBytes.Length != metadata.OrientationMapSize * 2)
            {
                throw new InvalidOperationException("Unexpected native raw FingerJet orientation byte count.");
            }

            var orientation = new Nfiq2FingerJetComplex[metadata.OrientationMapSize];
            for (var index = 0; index < orientation.Length; index++)
            {
                orientation[index] = new(
                    unchecked((sbyte)orientationBytes[index * 2]),
                    unchecked((sbyte)orientationBytes[(index * 2) + 1]));
            }

            return new(
                metadata.Width,
                metadata.Height,
                metadata.PixelsPerInch,
                metadata.XOffset,
                metadata.YOffset,
                metadata.OrientationMapWidth,
                metadata.OrientationMapSize,
                orientation,
                footprint);
        }
        finally
        {
            if (File.Exists(orientationPath))
            {
                File.Delete(orientationPath);
            }

            if (File.Exists(footprintPath))
            {
                File.Delete(footprintPath);
            }
        }
    }

    public static Nfiq2FingerJetOrientationMapOracleResult ReadEnhancedRawOrientationMap(string imagePath, int pixelsPerInch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelsPerInch);

        var orientationPath = Path.Combine(Path.GetTempPath(), "OpenNist.Nfiq", $"{Guid.NewGuid():N}.ori");
        var footprintPath = Path.Combine(Path.GetTempPath(), "OpenNist.Nfiq", $"{Guid.NewGuid():N}.fpt");
        Directory.CreateDirectory(Path.GetDirectoryName(orientationPath)!);

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = s_oraclePath.Value,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = Nfiq2TestPaths.NfiqDiagnosticToolDirectory,
                ArgumentList =
                {
                    "enhanced-raw-orientation-map",
                    imagePath,
                    pixelsPerInch.ToString(CultureInfo.InvariantCulture),
                    orientationPath,
                    footprintPath,
                },
            }) ?? throw new InvalidOperationException("Failed to start native enhanced raw FingerJet orientation oracle.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Native enhanced raw FingerJet orientation oracle failed with exit code {process.ExitCode}: {standardError}");
            }

            var metadata = ParseMetadata(standardOutput);
            var orientationBytes = File.ReadAllBytes(orientationPath);
            var footprint = File.ReadAllBytes(footprintPath);
            if (orientationBytes.Length != metadata.OrientationMapSize * 2)
            {
                throw new InvalidOperationException("Unexpected native enhanced raw FingerJet orientation byte count.");
            }

            var orientation = new Nfiq2FingerJetComplex[metadata.OrientationMapSize];
            for (var index = 0; index < orientation.Length; index++)
            {
                orientation[index] = new(
                    unchecked((sbyte)orientationBytes[index * 2]),
                    unchecked((sbyte)orientationBytes[(index * 2) + 1]));
            }

            return new(
                metadata.Width,
                metadata.Height,
                metadata.PixelsPerInch,
                metadata.XOffset,
                metadata.YOffset,
                metadata.OrientationMapWidth,
                metadata.OrientationMapSize,
                orientation,
                footprint);
        }
        finally
        {
            if (File.Exists(orientationPath))
            {
                File.Delete(orientationPath);
            }

            if (File.Exists(footprintPath))
            {
                File.Delete(footprintPath);
            }
        }
    }

    public static Nfiq2FingerJetRawMinutiaeOracleResult ReadRawMinutiae(string imagePath, int pixelsPerInch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelsPerInch);

        var output = RunOracle(
            "raw-minutiae",
            imagePath,
            pixelsPerInch.ToString(CultureInfo.InvariantCulture));
        return ParseRawMinutiae(output);
    }

    public static Nfiq2FingerJetComplex ReadOctSign(int real, int imaginary, int threshold)
    {
        var output = RunOracle(
            "oct-sign",
            real.ToString(CultureInfo.InvariantCulture),
            imaginary.ToString(CultureInfo.InvariantCulture),
            threshold.ToString(CultureInfo.InvariantCulture));
        return ParseComplex(output);
    }

    public static Nfiq2FingerJetComplex ReadDiv2(int real, int imaginary)
    {
        var output = RunOracle(
            "div2",
            real.ToString(CultureInfo.InvariantCulture),
            imaginary.ToString(CultureInfo.InvariantCulture));
        return ParseComplex(output);
    }

    public static byte[] ReadFillHoles(int strideX, int sizeX, int strideY, int sizeY, IReadOnlyList<byte> values)
    {
        var output = RunOracle(
            "fill-holes",
            strideX.ToString(CultureInfo.InvariantCulture),
            sizeX.ToString(CultureInfo.InvariantCulture),
            strideY.ToString(CultureInfo.InvariantCulture),
            sizeY.ToString(CultureInfo.InvariantCulture),
            string.Join(',', values));
        return ParseByteVector(output);
    }

    public static byte[] ReadBoxFilter(int boxSize, int width, int size, int threshold, IReadOnlyList<byte> values)
    {
        var output = RunOracle(
            "box-filter",
            boxSize.ToString(CultureInfo.InvariantCulture),
            width.ToString(CultureInfo.InvariantCulture),
            size.ToString(CultureInfo.InvariantCulture),
            threshold.ToString(CultureInfo.InvariantCulture),
            string.Join(',', values));
        return ParseByteVector(output);
    }

    public static IReadOnlyList<Nfiq2Minutia> ReadPostprocessedMinutiae(
        int imageResolution,
        int xOffset,
        int yOffset,
        IReadOnlyList<Nfiq2FingerJetRawMinutia> minutiae)
    {
        ArgumentNullException.ThrowIfNull(minutiae);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageResolution);

        var csv = EncodeRawMinutiaeCsv(minutiae);

        var output = RunOracle(
            "postprocess-minutiae",
            imageResolution.ToString(CultureInfo.InvariantCulture),
            xOffset.ToString(CultureInfo.InvariantCulture),
            yOffset.ToString(CultureInfo.InvariantCulture),
            csv);
        return ParseMinutiae(output);
    }

    public static IReadOnlyList<Nfiq2FingerJetRawMinutia> ReadRankedRawMinutiae(
        int capacity,
        IReadOnlyList<Nfiq2FingerJetRawMinutia> minutiae)
    {
        ArgumentNullException.ThrowIfNull(minutiae);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        var output = RunOracle(
            "rank-minutiae",
            capacity.ToString(CultureInfo.InvariantCulture),
            EncodeRawMinutiaeCsv(minutiae));
        return ParseRawMinutiaVector(output);
    }

    public static bool ReadIsInFootprint(int x, int y, int width, int size, IReadOnlyList<byte> phasemap)
    {
        ArgumentNullException.ThrowIfNull(phasemap);

        var output = RunOracle(
            "is-in-footprint",
            x.ToString(CultureInfo.InvariantCulture),
            y.ToString(CultureInfo.InvariantCulture),
            width.ToString(CultureInfo.InvariantCulture),
            size.ToString(CultureInfo.InvariantCulture),
            string.Join(',', phasemap));
        return output == "1";
    }

    public static (bool Success, byte Angle) ReadAdjustAngle(
        int x,
        int y,
        int width,
        int size,
        int angle,
        bool relative,
        IReadOnlyList<byte> phasemap)
    {
        ArgumentNullException.ThrowIfNull(phasemap);

        var output = RunOracle(
            "adjust-angle",
            x.ToString(CultureInfo.InvariantCulture),
            y.ToString(CultureInfo.InvariantCulture),
            width.ToString(CultureInfo.InvariantCulture),
            size.ToString(CultureInfo.InvariantCulture),
            angle.ToString(CultureInfo.InvariantCulture),
            relative ? "1" : "0",
            string.Join(',', phasemap));

        var tokens = output.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length != 2)
        {
            throw new InvalidOperationException($"Unexpected FingerJet adjust-angle response: {output}");
        }

        return (
            tokens[0] == "1",
            byte.Parse(tokens[1], CultureInfo.InvariantCulture));
    }

    public static bool[] ReadMax2D5Fast(int width, int size, IReadOnlyList<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var output = RunOracle(
            "max2d5fast",
            width.ToString(CultureInfo.InvariantCulture),
            size.ToString(CultureInfo.InvariantCulture),
            string.Join(',', values));

        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        return output
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static token => token == "1")
            .ToArray();
    }

    public static int[] ReadConv2D3(int width, int size, int t0, int t1, int normBits, IReadOnlyList<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var output = RunOracle(
            "conv2d3",
            width.ToString(CultureInfo.InvariantCulture),
            size.ToString(CultureInfo.InvariantCulture),
            t0.ToString(CultureInfo.InvariantCulture),
            t1.ToString(CultureInfo.InvariantCulture),
            normBits.ToString(CultureInfo.InvariantCulture),
            string.Join(',', values));

        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        return output
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static token => int.Parse(token, CultureInfo.InvariantCulture))
            .ToArray();
    }

    public static bool[] ReadBoolDelay(int delayLength, bool initialValue, IReadOnlyList<bool> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var output = RunOracle(
            "bool-delay",
            delayLength.ToString(CultureInfo.InvariantCulture),
            initialValue ? "1" : "0",
            string.Join(',', values.Select(static value => value ? "1" : "0")));

        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        return output
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static token => token == "1")
            .ToArray();
    }

    public static byte[] ReadDirectionAccumulator(
        int widthHalf,
        int rowCount,
        int filterSize,
        IReadOnlyList<Nfiq2FingerJetComplex> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var output = RunOracle(
            "direction-accumulator",
            widthHalf.ToString(CultureInfo.InvariantCulture),
            rowCount.ToString(CultureInfo.InvariantCulture),
            filterSize.ToString(CultureInfo.InvariantCulture),
            string.Join(';', values.Select(static value => $"{value.Real.ToString(CultureInfo.InvariantCulture)},{value.Imaginary.ToString(CultureInfo.InvariantCulture)}")));

        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        return output
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static token => byte.Parse(token, CultureInfo.InvariantCulture))
            .ToArray();
    }

    public static IReadOnlyList<Nfiq2FingerJetComplex> ReadSmmeOrientationSequence(int width, int size, IReadOnlyList<byte> phasemap)
    {
        ArgumentNullException.ThrowIfNull(phasemap);

        var output = RunOracle(
            "smme-orientation-sequence",
            width.ToString(CultureInfo.InvariantCulture),
            size.ToString(CultureInfo.InvariantCulture),
            string.Join(',', phasemap));

        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        return output
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static token =>
            {
                var parts = token.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length != 2)
                {
                    throw new InvalidOperationException($"Unexpected complex pair token: {token}");
                }

                return new Nfiq2FingerJetComplex(
                    sbyte.Parse(parts[0], CultureInfo.InvariantCulture),
                    sbyte.Parse(parts[1], CultureInfo.InvariantCulture));
            })
            .ToArray();
    }

    public static byte ReadBiffiltSample(int width, int size, int x, int y, IReadOnlyList<byte> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var output = RunOracle(
            "biffilt-sample",
            width.ToString(CultureInfo.InvariantCulture),
            size.ToString(CultureInfo.InvariantCulture),
            x.ToString(CultureInfo.InvariantCulture),
            y.ToString(CultureInfo.InvariantCulture),
            string.Join(',', values));

        return byte.Parse(output, CultureInfo.InvariantCulture);
    }

    public static Nfiq2FingerJetBifFilterResult ReadBiffiltEvaluate(
        int width,
        int size,
        int x,
        int y,
        sbyte c,
        sbyte s,
        IReadOnlyList<byte> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var output = RunOracle(
            "biffilt-evaluate",
            width.ToString(CultureInfo.InvariantCulture),
            size.ToString(CultureInfo.InvariantCulture),
            x.ToString(CultureInfo.InvariantCulture),
            y.ToString(CultureInfo.InvariantCulture),
            c.ToString(CultureInfo.InvariantCulture),
            s.ToString(CultureInfo.InvariantCulture),
            string.Join(',', values));

        var tokens = output.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length != 7)
        {
            throw new InvalidOperationException($"Unexpected FingerJet biffilt response: {output}");
        }

        return new(
            Confirmed: tokens[0] == "1",
            Type: tokens[1] == "1",
            Rotate180: tokens[2] == "1",
            XOffset: int.Parse(tokens[3], CultureInfo.InvariantCulture),
            YOffset: int.Parse(tokens[4], CultureInfo.InvariantCulture),
            Period: int.Parse(tokens[5], CultureInfo.InvariantCulture),
            Confidence: int.Parse(tokens[6], CultureInfo.InvariantCulture));
    }

    public static IReadOnlyList<Nfiq2FingerJetRawMinutia> ReadExtractedRawMinutiaeFromPhasemap(
        int width,
        int size,
        int capacity,
        IReadOnlyList<byte> phasemap)
    {
        ArgumentNullException.ThrowIfNull(phasemap);

        var output = RunOracle(
            "extract-minutia-raw",
            width.ToString(CultureInfo.InvariantCulture),
            size.ToString(CultureInfo.InvariantCulture),
            capacity.ToString(CultureInfo.InvariantCulture),
            string.Join(',', phasemap));
        return ParseRawMinutiaVector(output);
    }

    public static Nfiq2FingerJetMinutiaTraceOracleResult ReadExtractedMinutiaTraceFromPhasemap(
        int width,
        int size,
        int capacity,
        IReadOnlyList<byte> phasemap)
    {
        ArgumentNullException.ThrowIfNull(phasemap);

        var directionPath = Path.Combine(Path.GetTempPath(), "OpenNist.Nfiq", $"{Guid.NewGuid():N}.dir");
        var candidatePath = Path.Combine(Path.GetTempPath(), "OpenNist.Nfiq", $"{Guid.NewGuid():N}.cand");
        Directory.CreateDirectory(Path.GetDirectoryName(directionPath)!);

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = s_oraclePath.Value,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = Nfiq2TestPaths.NfiqDiagnosticToolDirectory,
                ArgumentList =
                {
                    "extract-minutia-trace",
                    width.ToString(CultureInfo.InvariantCulture),
                    size.ToString(CultureInfo.InvariantCulture),
                    capacity.ToString(CultureInfo.InvariantCulture),
                    string.Join(',', phasemap),
                    directionPath,
                    candidatePath,
                },
            }) ?? throw new InvalidOperationException("Failed to start native FingerJet extract-minutia trace oracle.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Native FingerJet extract-minutia trace oracle failed with exit code {process.ExitCode}: {standardError}");
            }

            return new(
                ParseRawMinutiaVector(standardOutput),
                File.ReadAllBytes(directionPath),
                File.ReadAllBytes(candidatePath));
        }
        finally
        {
            if (File.Exists(directionPath))
            {
                File.Delete(directionPath);
            }

            if (File.Exists(candidatePath))
            {
                File.Delete(candidatePath);
            }
        }
    }

    public static IReadOnlyList<Nfiq2FingerJetMinutiaDebugOracleEntry> ReadExtractedMinutiaDebugFromPhasemap(
        int width,
        int size,
        int capacity,
        IReadOnlyList<byte> phasemap)
    {
        ArgumentNullException.ThrowIfNull(phasemap);

        var output = RunOracle(
            "extract-minutia-debug",
            width.ToString(CultureInfo.InvariantCulture),
            size.ToString(CultureInfo.InvariantCulture),
            capacity.ToString(CultureInfo.InvariantCulture),
            string.Join(',', phasemap));

        var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0 || !lines[0].StartsWith("size ", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected FingerJet extract-minutia-debug response: {output}");
        }

        var entries = new List<Nfiq2FingerJetMinutiaDebugOracleEntry>();
        foreach (var line in lines.Skip(1))
        {
            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length != 9 || !string.Equals(tokens[0], "dbg", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unexpected FingerJet debug minutia line: {line}");
            }

            entries.Add(new(
                int.Parse(tokens[1], CultureInfo.InvariantCulture),
                int.Parse(tokens[2], CultureInfo.InvariantCulture),
                byte.Parse(tokens[3], CultureInfo.InvariantCulture),
                byte.Parse(tokens[4], CultureInfo.InvariantCulture),
                int.Parse(tokens[5], CultureInfo.InvariantCulture),
                int.Parse(tokens[6], CultureInfo.InvariantCulture),
                tokens[7] == "1",
                tokens[8] == "1"));
        }

        return entries;
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
            ArgumentList = { Nfiq2TestPaths.NfiqFingerJetOracleBuildScriptPath },
        }) ?? throw new InvalidOperationException("Failed to start native FingerJet oracle build.");

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to build native FingerJet oracle with exit code {process.ExitCode}: {standardError}");
        }

        if (!File.Exists(Nfiq2TestPaths.NfiqFingerJetOraclePath))
        {
            throw new InvalidOperationException(
                $"Native FingerJet oracle was not produced. Output: {standardOutput}");
        }

        return Nfiq2TestPaths.NfiqFingerJetOraclePath;
    }

    private static string RunOracle(string command, params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = s_oraclePath.Value,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Nfiq2TestPaths.NfiqDiagnosticToolDirectory,
        };

        startInfo.ArgumentList.Add(command);
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start native FingerJet oracle.");

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Native FingerJet oracle failed with exit code {process.ExitCode}: {standardError}");
        }

        return standardOutput.Trim();
    }

    private static Nfiq2FingerJetPreparedImageOracleResult ParseMetadata(string text)
    {
        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length != 4)
        {
            throw new InvalidOperationException($"Unexpected FingerJet oracle response: {text}");
        }

        var sizeTokens = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (sizeTokens.Length != 3 || !string.Equals(sizeTokens[0], "size", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected FingerJet size line: {lines[0]}");
        }

        var resolutionTokens = lines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (resolutionTokens.Length != 2 || !string.Equals(resolutionTokens[0], "resolution", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected FingerJet resolution line: {lines[1]}");
        }

        var offsetsTokens = lines[2].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (offsetsTokens.Length != 3 || !string.Equals(offsetsTokens[0], "offsets", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected FingerJet offsets line: {lines[2]}");
        }

        var orientationTokens = lines[3].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (orientationTokens.Length != 3 || !string.Equals(orientationTokens[0], "orientation", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected FingerJet orientation line: {lines[3]}");
        }

        return new(
            Width: int.Parse(sizeTokens[1], CultureInfo.InvariantCulture),
            Height: int.Parse(sizeTokens[2], CultureInfo.InvariantCulture),
            PixelsPerInch: int.Parse(resolutionTokens[1], CultureInfo.InvariantCulture),
            XOffset: int.Parse(offsetsTokens[1], CultureInfo.InvariantCulture),
            YOffset: int.Parse(offsetsTokens[2], CultureInfo.InvariantCulture),
            OrientationMapWidth: int.Parse(orientationTokens[1], CultureInfo.InvariantCulture),
            OrientationMapSize: int.Parse(orientationTokens[2], CultureInfo.InvariantCulture),
            Pixels: []);
    }

    private static Nfiq2FingerJetPhasemapOracleResult ParsePhasemapMetadata(string text)
    {
        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length != 3)
        {
            throw new InvalidOperationException($"Unexpected FingerJet phasemap response: {text}");
        }

        var sizeTokens = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (sizeTokens.Length != 3 || !string.Equals(sizeTokens[0], "size", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected FingerJet phasemap size line: {lines[0]}");
        }

        var resolutionTokens = lines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (resolutionTokens.Length != 2 || !string.Equals(resolutionTokens[0], "resolution", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected FingerJet phasemap resolution line: {lines[1]}");
        }

        var offsetsTokens = lines[2].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (offsetsTokens.Length != 3 || !string.Equals(offsetsTokens[0], "offsets", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected FingerJet phasemap offsets line: {lines[2]}");
        }

        return new(
            Width: int.Parse(sizeTokens[1], CultureInfo.InvariantCulture),
            Height: int.Parse(sizeTokens[2], CultureInfo.InvariantCulture),
            PixelsPerInch: int.Parse(resolutionTokens[1], CultureInfo.InvariantCulture),
            XOffset: int.Parse(offsetsTokens[1], CultureInfo.InvariantCulture),
            YOffset: int.Parse(offsetsTokens[2], CultureInfo.InvariantCulture),
            Pixels: []);
    }

    private static Nfiq2FingerJetComplex ParseComplex(string text)
    {
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length != 2)
        {
            throw new InvalidOperationException($"Unexpected FingerJet complex response: {text}");
        }

        return new(
            int.Parse(tokens[0], CultureInfo.InvariantCulture),
            int.Parse(tokens[1], CultureInfo.InvariantCulture));
    }

    private static byte[] ParseByteVector(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static token => byte.Parse(token, CultureInfo.InvariantCulture))
            .ToArray();
    }

    private static List<Nfiq2Minutia> ParseMinutiae(string text)
    {
        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0 || !lines[0].StartsWith("size ", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected FingerJet minutiae response: {text}");
        }

        var minutiae = new List<Nfiq2Minutia>();
        foreach (var line in lines.Skip(1))
        {
            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length != 6 || !string.Equals(tokens[0], "minutia", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unexpected FingerJet minutia line: {line}");
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

    private static Nfiq2FingerJetRawMinutiaeOracleResult ParseRawMinutiae(string text)
    {
        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 3)
        {
            throw new InvalidOperationException($"Unexpected FingerJet raw minutiae response: {text}");
        }

        var resolutionTokens = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (resolutionTokens.Length != 2 || !string.Equals(resolutionTokens[0], "resolution", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected FingerJet resolution line: {lines[0]}");
        }

        var offsetsTokens = lines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (offsetsTokens.Length != 3 || !string.Equals(offsetsTokens[0], "offsets", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected FingerJet offsets line: {lines[1]}");
        }

        var sizeTokens = lines[2].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (sizeTokens.Length != 2 || !string.Equals(sizeTokens[0], "size", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected FingerJet size line: {lines[2]}");
        }

        var minutiae = new List<Nfiq2FingerJetRawMinutia>();
        foreach (var line in lines.Skip(3))
        {
            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length != 6 || !string.Equals(tokens[0], "raw", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unexpected FingerJet raw minutia line: {line}");
            }

            minutiae.Add(new(
                int.Parse(tokens[1], CultureInfo.InvariantCulture),
                int.Parse(tokens[2], CultureInfo.InvariantCulture),
                int.Parse(tokens[3], CultureInfo.InvariantCulture),
                int.Parse(tokens[4], CultureInfo.InvariantCulture),
                int.Parse(tokens[5], CultureInfo.InvariantCulture)));
        }

        return new(
            ImageResolution: int.Parse(resolutionTokens[1], CultureInfo.InvariantCulture),
            XOffset: int.Parse(offsetsTokens[1], CultureInfo.InvariantCulture),
            YOffset: int.Parse(offsetsTokens[2], CultureInfo.InvariantCulture),
            Minutiae: minutiae);
    }

    private static List<Nfiq2FingerJetRawMinutia> ParseRawMinutiaVector(string text)
    {
        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0 || !lines[0].StartsWith("size ", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected FingerJet raw vector response: {text}");
        }

        var minutiae = new List<Nfiq2FingerJetRawMinutia>();
        foreach (var line in lines.Skip(1))
        {
            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length != 6 || !string.Equals(tokens[0], "raw", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unexpected FingerJet raw minutia line: {line}");
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

    private static string EncodeRawMinutiaeCsv(IReadOnlyList<Nfiq2FingerJetRawMinutia> minutiae)
    {
        return string.Join(
            ';',
            minutiae.Select(static minutia =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{minutia.X},{minutia.Y},{minutia.Angle},{minutia.Confidence},{minutia.Type}")));
    }

}

internal sealed record Nfiq2FingerJetPreparedImageOracleResult(
    int Width,
    int Height,
    int PixelsPerInch,
    int XOffset,
    int YOffset,
    int OrientationMapWidth,
    int OrientationMapSize,
    byte[] Pixels);

internal sealed record Nfiq2FingerJetPhasemapOracleResult(
    int Width,
    int Height,
    int PixelsPerInch,
    int XOffset,
    int YOffset,
    byte[] Pixels);

internal sealed record Nfiq2FingerJetOrientationMapOracleResult(
    int Width,
    int Height,
    int PixelsPerInch,
    int XOffset,
    int YOffset,
    int OrientationMapWidth,
    int OrientationMapSize,
    IReadOnlyList<Nfiq2FingerJetComplex> Orientation,
    byte[] Footprint);

internal sealed record Nfiq2FingerJetRawMinutiaeOracleResult(
    int ImageResolution,
    int XOffset,
    int YOffset,
    IReadOnlyList<Nfiq2FingerJetRawMinutia> Minutiae);

internal sealed record Nfiq2FingerJetMinutiaTraceOracleResult(
    IReadOnlyList<Nfiq2FingerJetRawMinutia> Minutiae,
    byte[] DirectionMap,
    byte[] CandidateMap);

internal sealed record Nfiq2FingerJetMinutiaDebugOracleEntry(
    int X,
    int Y,
    byte CandidateAngle,
    byte FinalAngle,
    int Confidence,
    int Type,
    bool AdjustedAbsolute,
    bool AdjustedRelative);
