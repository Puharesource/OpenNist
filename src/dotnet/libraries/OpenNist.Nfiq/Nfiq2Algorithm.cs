namespace OpenNist.Nfiq;

using System.Diagnostics;
using System.Globalization;
using System.Text;
using OpenNist.Nfiq.Internal;
using JetBrains.Annotations;

/// <summary>
/// Default NFIQ 2 implementation that wraps the official NIST CLI.
/// </summary>
[PublicAPI]
public sealed class Nfiq2Algorithm : INfiq2Algorithm
{
    private const string PgmFileName = "fingerprint.pgm";
    private static readonly Nfiq2AnalysisOptions s_defaultOptions = new(
        IncludeMappedQualityMeasures: true,
        Force: true,
        ThreadCount: null);

    private readonly Nfiq2Installation installation;

    /// <summary>
    /// Initializes a new instance of the <see cref="Nfiq2Algorithm"/> class using the default installation.
    /// </summary>
    public Nfiq2Algorithm()
        : this(Nfiq2Installation.FindDefault())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Nfiq2Algorithm"/> class.
    /// </summary>
    /// <param name="installation">The NFIQ 2 installation to use.</param>
    public Nfiq2Algorithm(Nfiq2Installation installation)
    {
        this.installation = installation ?? throw new ArgumentNullException(nameof(installation));
    }

    /// <inheritdoc />
    public async ValueTask<Nfiq2AssessmentResult> AnalyzeFileAsync(
        string fingerprintPath,
        Nfiq2AnalysisOptions options = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprintPath);
        options = NormalizeOptions(options);

        var report = await AnalyzeFilesAsync([fingerprintPath], options, cancellationToken).ConfigureAwait(false);
        if (report.Results.Count != 1)
        {
            throw new Nfiq2Exception(
                $"Expected exactly one NFIQ 2 result for '{fingerprintPath}', but received {report.Results.Count}.");
        }

        return report.Results[0];
    }

    /// <inheritdoc />
    public async ValueTask<Nfiq2AssessmentResult> AnalyzeAsync(
        ReadOnlyMemory<byte> rawPixels,
        Nfiq2RawImageDescription rawImage,
        Nfiq2AnalysisOptions options = default,
        CancellationToken cancellationToken = default)
    {
        options = NormalizeOptions(options);
        ValidateRawImage(rawPixels, rawImage);

        var tempDirectoryPath = Path.Combine(Path.GetTempPath(), "OpenNist.Nfiq", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectoryPath);
        try
        {
            var tempImagePath = Path.Combine(tempDirectoryPath, PgmFileName);
            await WritePortableGrayMapAsync(tempImagePath, rawPixels, rawImage, cancellationToken).ConfigureAwait(false);
            return await AnalyzeFileAsync(tempImagePath, options, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (Directory.Exists(tempDirectoryPath))
            {
                Directory.Delete(tempDirectoryPath, recursive: true);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<Nfiq2CsvReport> AnalyzeFilesAsync(
        IEnumerable<string> fingerprintPaths,
        Nfiq2AnalysisOptions options = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fingerprintPaths);
        options = NormalizeOptions(options);

        var normalizedPaths = fingerprintPaths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToArray();

        if (normalizedPaths.Length == 0)
        {
            throw new ArgumentException("At least one fingerprint path must be provided.", nameof(fingerprintPaths));
        }

        var csv = await RunCliAsync(normalizedPaths, options, cancellationToken).ConfigureAwait(false);
        return Nfiq2CsvReportParser.Parse(csv);
    }

    private async ValueTask<string> RunCliAsync(
        IReadOnlyList<string> fingerprintPaths,
        Nfiq2AnalysisOptions options,
        CancellationToken cancellationToken)
    {
        ValidateOptions(options);

        var startInfo = new ProcessStartInfo
        {
            FileName = installation.ExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = installation.RootPath,
        };

        if (options.Force)
        {
            startInfo.ArgumentList.Add("-F");
        }

        startInfo.ArgumentList.Add("-a");
        startInfo.ArgumentList.Add("-v");

        if (options.IncludeMappedQualityMeasures)
        {
            startInfo.ArgumentList.Add("-b");
        }

        startInfo.ArgumentList.Add("-m");
        startInfo.ArgumentList.Add(installation.ModelInfoPath);

        foreach (var fingerprintPath in fingerprintPaths)
        {
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(fingerprintPath);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new Nfiq2Exception("Failed to start the official NFIQ 2 CLI.");
            }
        }
        catch (Exception ex)
        {
            throw new Nfiq2Exception(
                $"Failed to start the official NFIQ 2 CLI at '{installation.ExecutablePath}'.",
                ex);
        }

        try
        {
            using var cancellationRegistration = cancellationToken.Register(static state =>
            {
                var runningProcess = (Process)state!;
                try
                {
                    if (!runningProcess.HasExited)
                    {
                        runningProcess.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                    // The process exited between the state check and Kill().
                }
            }, process);

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw CreateCliFailure(process.ExitCode, stderr, stdout);
            }

            if (string.IsNullOrWhiteSpace(stdout))
            {
                throw new Nfiq2Exception(
                    $"The official NFIQ 2 CLI returned no CSV output. stderr: {stderr}");
            }

            return stdout;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Nfiq2Exception)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new Nfiq2Exception("The official NFIQ 2 CLI failed unexpectedly.", ex);
        }
    }

    private static Nfiq2Exception CreateCliFailure(int exitCode, string stderr, string stdout)
    {
        var messageBuilder = new StringBuilder();
        messageBuilder.Append("The official NFIQ 2 CLI exited with code ");
        messageBuilder.Append(exitCode.ToString(CultureInfo.InvariantCulture));
        messageBuilder.Append('.');

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            messageBuilder.Append(' ');
            messageBuilder.Append("stderr: ");
            messageBuilder.Append(stderr.Trim());
        }

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            messageBuilder.Append(' ');
            messageBuilder.Append("stdout: ");
            messageBuilder.Append(stdout.Trim());
        }

        return new(messageBuilder.ToString());
    }

    private static async ValueTask WritePortableGrayMapAsync(
        string path,
        ReadOnlyMemory<byte> rawPixels,
        Nfiq2RawImageDescription rawImage,
        CancellationToken cancellationToken)
    {
#pragma warning disable CA2007 // FileStream async disposal does not expose a useful ConfigureAwait pattern here.
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
#pragma warning restore CA2007
        var header = $"P5\n{rawImage.Width} {rawImage.Height}\n255\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(rawPixels, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateRawImage(ReadOnlyMemory<byte> rawPixels, Nfiq2RawImageDescription rawImage)
    {
        if (rawImage.Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rawImage), rawImage.Width, "Image width must be positive.");
        }

        if (rawImage.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rawImage), rawImage.Height, "Image height must be positive.");
        }

        if (rawImage.BitsPerPixel != 8)
        {
            throw new NotSupportedException(
                $"NFIQ 2 only supports 8-bit grayscale input, but received {rawImage.BitsPerPixel} bits per pixel.");
        }

        if (rawImage.PixelsPerInch != 500)
        {
            throw new NotSupportedException(
                $"In-memory NFIQ 2 analysis currently requires 500 PPI input, but received {rawImage.PixelsPerInch} PPI.");
        }

        var expectedLength = checked(rawImage.Width * rawImage.Height);
        if (rawPixels.Length != expectedLength)
        {
            throw new ArgumentException(
                $"The supplied raw pixel buffer length ({rawPixels.Length}) does not match the declared image area ({expectedLength}).",
                nameof(rawPixels));
        }
    }

    private static void ValidateOptions(Nfiq2AnalysisOptions options)
    {
        if (options.ThreadCount is int threadCount && threadCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), threadCount, "Thread count must be positive.");
        }
    }

    private static Nfiq2AnalysisOptions NormalizeOptions(Nfiq2AnalysisOptions options)
    {
        return options == default
            ? s_defaultOptions
            : options;
    }
}
