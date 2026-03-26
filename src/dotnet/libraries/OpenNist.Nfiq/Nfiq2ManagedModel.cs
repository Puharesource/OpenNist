namespace OpenNist.Nfiq;

using JetBrains.Annotations;
using OpenNist.Nfiq.Internal;

/// <summary>
/// Managed NFIQ 2 model loader and scorer for already-computed native quality measures.
/// </summary>
[PublicAPI]
public sealed class Nfiq2ManagedModel
{
    private static readonly Lazy<Nfiq2ManagedModel> s_defaultModel =
        new(LoadDefaultCore, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly Nfiq2ModelInfo _modelInfo;
    private readonly Nfiq2RandomForestModel _randomForestModel;

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
        _modelInfo = modelInfo ?? throw new ArgumentNullException(nameof(modelInfo));
        _randomForestModel = randomForestModel ?? throw new ArgumentNullException(nameof(randomForestModel));
    }

    /// <summary>
    /// Gets the human-readable model name.
    /// </summary>
    public string? Name => _modelInfo.Name;

    /// <summary>
    /// Gets the model version.
    /// </summary>
    public string? Version => _modelInfo.Version;

    /// <summary>
    /// Gets the validated random forest parameter hash.
    /// </summary>
    public string ParameterHash => _randomForestModel.ParameterHash;

    /// <summary>
    /// Loads the default installed NFIQ 2 model.
    /// </summary>
    /// <returns>The loaded managed model.</returns>
    public static Nfiq2ManagedModel LoadDefault()
    {
        return s_defaultModel.Value;
    }

    private static Nfiq2ManagedModel LoadDefaultCore()
    {
        if (Nfiq2BundledModelFiles.TryLoad(out var bundledModelInfo, out var bundledYaml))
        {
            // Bundled model assets ship with the assembly, so we can trust the declared model-info hash
            // without recomputing MD5 in browser WASM environments where that algorithm may be unavailable.
            return new(bundledModelInfo, Nfiq2RandomForestModel.Parse(bundledYaml, bundledModelInfo.ModelHash));
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
        return _randomForestModel.Evaluate(nativeQualityMeasures);
    }

    /// <summary>
    /// Computes the unified NFIQ 2 quality score from non-null native quality measures.
    /// </summary>
    /// <param name="nativeQualityMeasures">The native quality measures required by the trained model.</param>
    /// <returns>The unified NFIQ 2 quality score.</returns>
    public int ComputeUnifiedQualityScore(IReadOnlyDictionary<string, double> nativeQualityMeasures)
    {
        ArgumentNullException.ThrowIfNull(nativeQualityMeasures);
        return _randomForestModel.Evaluate(nativeQualityMeasures);
    }
}
