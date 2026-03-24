namespace OpenNist.Nfiq;

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

/// <summary>
/// Describes an official NFIQ 2 installation layout.
/// </summary>
[PublicAPI]
public sealed record Nfiq2Installation
{
    private const string OpenNistNfiq2RootEnvironmentVariable = "OPENNIST_NFIQ2_ROOT";
    private const string Nfiq2RootEnvironmentVariable = "NFIQ2_ROOT";
    private const string ModelInfoFileName = "nist_plain_tir-ink.txt";

    /// <summary>
    /// Initializes a new instance of the <see cref="Nfiq2Installation"/> class.
    /// </summary>
    /// <param name="rootPath">The NFIQ 2 installation root path.</param>
    /// <param name="executablePath">The NFIQ 2 CLI path.</param>
    /// <param name="modelInfoPath">The default model-info path.</param>
    public Nfiq2Installation(string rootPath, string executablePath, string modelInfoPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelInfoPath);

        RootPath = rootPath;
        ExecutablePath = executablePath;
        ModelInfoPath = modelInfoPath;
    }

    /// <summary>
    /// Gets the NFIQ 2 installation root path.
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// Gets the NFIQ 2 CLI path.
    /// </summary>
    public string ExecutablePath { get; }

    /// <summary>
    /// Gets the default model-info path.
    /// </summary>
    public string ModelInfoPath { get; }

    /// <summary>
    /// Locates the default official NFIQ 2 installation.
    /// </summary>
    /// <returns>The located installation.</returns>
    /// <exception cref="Nfiq2Exception">Thrown when an installation cannot be found.</exception>
    public static Nfiq2Installation FindDefault()
    {
        if (TryFindDefault(out var installation))
        {
            return installation;
        }

        throw new Nfiq2Exception(
            "Could not locate an official NFIQ 2 installation. "
            + $"Set {OpenNistNfiq2RootEnvironmentVariable} or {Nfiq2RootEnvironmentVariable}, "
            + "or install NIST NFIQ 2 into a standard location.");
    }

    /// <summary>
    /// Attempts to locate the default official NFIQ 2 installation.
    /// </summary>
    /// <param name="installation">The located installation, if any.</param>
    /// <returns><see langword="true"/> when an installation is found; otherwise, <see langword="false"/>.</returns>
    public static bool TryFindDefault([NotNullWhen(true)] out Nfiq2Installation? installation)
    {
        foreach (var candidateRoot in EnumerateCandidateRoots())
        {
            if (TryCreate(candidateRoot, out installation))
            {
                return true;
            }
        }

        installation = null;
        return false;
    }

    /// <summary>
    /// Creates an <see cref="Nfiq2Installation"/> from a specific root path.
    /// </summary>
    /// <param name="rootPath">The installation root path.</param>
    /// <returns>The resolved installation.</returns>
    /// <exception cref="Nfiq2Exception">Thrown when the root path is not a valid installation.</exception>
    public static Nfiq2Installation FromRoot(string rootPath)
    {
        if (TryCreate(rootPath, out var installation))
        {
            return installation;
        }

        throw new Nfiq2Exception($"'{rootPath}' is not a valid official NFIQ 2 installation root.");
    }

    private static IEnumerable<string> EnumerateCandidateRoots()
    {
        yield return FromEnvironment(OpenNistNfiq2RootEnvironmentVariable);
        yield return FromEnvironment(Nfiq2RootEnvironmentVariable);

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            yield return Path.Combine(userProfile, "usr", "local", "nfiq2");
        }

        if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            yield return "/usr/local/nfiq2";
        }

        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NFIQ 2");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "NFIQ 2");
        }
    }

    private static string FromEnvironment(string variableName)
    {
        return Environment.GetEnvironmentVariable(variableName) ?? string.Empty;
    }

    private static bool TryCreate(string rootPath, [NotNullWhen(true)] out Nfiq2Installation? installation)
    {
        installation = null;
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return false;
        }

        var normalizedRootPath = Path.GetFullPath(rootPath);
        var executableFileName = OperatingSystem.IsWindows() ? "nfiq2.exe" : "nfiq2";
        var executablePath = Path.Combine(normalizedRootPath, "bin", executableFileName);
        var modelInfoPath = Path.Combine(normalizedRootPath, "share", ModelInfoFileName);

        if (!File.Exists(executablePath) || !File.Exists(modelInfoPath))
        {
            return false;
        }

        installation = new(normalizedRootPath, executablePath, modelInfoPath);
        return true;
    }
}
