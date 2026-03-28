namespace OpenNist.Nfiq.Configuration;

using JetBrains.Annotations;
using OpenNist.Nfiq.Errors;

/// <summary>
/// Describes the official NFIQ 2 model-info metadata file.
/// </summary>
/// <param name="Name">The human-readable model name.</param>
/// <param name="Trainer">The model trainer.</param>
/// <param name="Description">The model description.</param>
/// <param name="Version">The model version.</param>
/// <param name="ModelPath">The resolved model file path.</param>
/// <param name="ModelHash">The declared model hash.</param>
[PublicAPI]
public sealed record Nfiq2ModelInfo(
    string? Name,
    string? Trainer,
    string? Description,
    string? Version,
    string ModelPath,
    string ModelHash)
{
    private const string s_keyName = "Name";
    private const string s_keyTrainer = "Trainer";
    private const string s_keyDescription = "Description";
    private const string s_keyVersion = "Version";
    private const string s_keyPath = "Path";
    private const string s_keyHash = "Hash";

    /// <summary>
    /// Reads and parses an official NFIQ 2 model-info file.
    /// </summary>
    /// <param name="modelInfoPath">The model-info file path.</param>
    /// <returns>The parsed model-info object.</returns>
    public static Nfiq2ModelInfo FromFile(string modelInfoPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelInfoPath);

        var fullPath = Path.GetFullPath(modelInfoPath);
        var content = File.ReadAllText(fullPath);
        return Parse(content, fullPath);
    }

    /// <summary>
    /// Parses NFIQ 2 model-info content.
    /// </summary>
    /// <param name="content">The model-info file content.</param>
    /// <param name="modelInfoPath">The model-info file path used for relative-path resolution.</param>
    /// <returns>The parsed model-info object.</returns>
    public static Nfiq2ModelInfo Parse(string content, string modelInfoPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelInfoPath);

        string? name = null;
        string? trainer = null;
        string? description = null;
        string? version = null;
        string? modelPath = null;
        string? modelHash = null;

        foreach (var rawLine in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = rawLine.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0 || separatorIndex >= rawLine.Length - 1)
            {
                continue;
            }

            var key = rawLine[..separatorIndex].Trim();
            var value = rawLine[(separatorIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            switch (key)
            {
                case s_keyName:
                    name = value;
                    break;
                case s_keyTrainer:
                    trainer = value;
                    break;
                case s_keyDescription:
                    description = value;
                    break;
                case s_keyVersion:
                    version = value;
                    break;
                case s_keyPath:
                    modelPath = ResolveModelPath(modelInfoPath, value);
                    break;
                case s_keyHash:
                    modelHash = value;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(modelPath))
        {
            throw new Nfiq2Exception($"The required model information '{s_keyPath}' was not found.");
        }

        if (string.IsNullOrWhiteSpace(modelHash))
        {
            throw new Nfiq2Exception($"The required model information '{s_keyHash}' was not found.");
        }

        return new(name, trainer, description, version, modelPath, modelHash);
    }

    private static string ResolveModelPath(string modelInfoPath, string value)
    {
        if (Path.IsPathRooted(value))
        {
            return value;
        }

        var modelInfoDirectory = Path.GetDirectoryName(modelInfoPath);
        return string.IsNullOrWhiteSpace(modelInfoDirectory)
            ? Path.Combine(".", value)
            : Path.Combine(modelInfoDirectory, value);
    }
}
