namespace OpenNist.Nfiq;

using JetBrains.Annotations;
using OpenNist.Nfiq.Internal;

/// <summary>
/// Managed NFIQ 2 model loader and scorer for already-computed native quality measures.
/// </summary>
[PublicAPI]
public sealed class Nfiq2ManagedModel
{
    private readonly Nfiq2ModelInfo modelInfo;
    private readonly Nfiq2RandomForestModel randomForestModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="Nfiq2ManagedModel"/> class from parsed model-info metadata.
    /// </summary>
    /// <param name="modelInfo">The model-info metadata describing the random forest YAML and its hash.</param>
    public Nfiq2ManagedModel(Nfiq2ModelInfo modelInfo)
        : this(modelInfo, Nfiq2RandomForestModel.FromModelInfo(modelInfo))
    {
    }

    private Nfiq2ManagedModel(Nfiq2ModelInfo modelInfo, Nfiq2RandomForestModel randomForestModel)
    {
        this.modelInfo = modelInfo ?? throw new ArgumentNullException(nameof(modelInfo));
        this.randomForestModel = randomForestModel ?? throw new ArgumentNullException(nameof(randomForestModel));
    }

    /// <summary>
    /// Gets the human-readable model name.
    /// </summary>
    public string? Name => modelInfo.Name;

    /// <summary>
    /// Gets the model version.
    /// </summary>
    public string? Version => modelInfo.Version;

    /// <summary>
    /// Gets the validated random forest parameter hash.
    /// </summary>
    public string ParameterHash => randomForestModel.ParameterHash;

    /// <summary>
    /// Loads the default installed NFIQ 2 model.
    /// </summary>
    /// <returns>The loaded managed model.</returns>
    public static Nfiq2ManagedModel LoadDefault()
    {
        if (Nfiq2BundledModelFiles.TryLoad(out var bundledModelInfo, out var bundledYaml))
        {
            var modelHash = Nfiq2RandomForestModel.CalculateMd5Hex(bundledYaml);
            if (!modelHash.Equals(bundledModelInfo.ModelHash, StringComparison.Ordinal))
            {
                throw new Nfiq2Exception(
                    $"The bundled NFIQ 2 model hash '{modelHash}' did not match the declared model-info hash '{bundledModelInfo.ModelHash}'.");
            }

            return new(bundledModelInfo, Nfiq2RandomForestModel.Parse(bundledYaml));
        }

        var installation = Nfiq2Installation.FindDefault();
        return FromModelInfoFile(installation.ModelInfoPath);
    }

    /// <summary>
    /// Loads a managed NFIQ 2 model from an official model-info file.
    /// </summary>
    /// <param name="modelInfoPath">The model-info file path.</param>
    /// <returns>The loaded managed model.</returns>
    public static Nfiq2ManagedModel FromModelInfoFile(string modelInfoPath)
    {
        return new(Nfiq2ModelInfo.FromFile(modelInfoPath));
    }

    /// <summary>
    /// Computes the unified NFIQ 2 quality score from native quality measures.
    /// </summary>
    /// <param name="nativeQualityMeasures">The native quality measures required by the trained model.</param>
    /// <returns>The unified NFIQ 2 quality score.</returns>
    public int ComputeUnifiedQualityScore(IReadOnlyDictionary<string, double?> nativeQualityMeasures)
    {
        ArgumentNullException.ThrowIfNull(nativeQualityMeasures);
        return randomForestModel.Evaluate(nativeQualityMeasures);
    }
}
