namespace OpenNist.Tests.Nfiq.TestSupport;

using OpenNist.Nfiq;
using OpenNist.Nfiq.Configuration;
using OpenNist.Nfiq.Internal;
using OpenNist.Nfiq.Internal.Runtime;
using OpenNist.Nfiq.Runtime;

internal static class Nfiq2TestContext
{
    private static readonly Lazy<Nfiq2Algorithm> s_algorithm = new(
        static () => new(Nfiq2TestPaths.Installation));

    private static readonly Lazy<Nfiq2ModelInfo> s_modelInfo = new(
        static () => Nfiq2ModelInfo.FromFile(Nfiq2TestPaths.Installation.ModelInfoPath));

    private static readonly Lazy<string> s_modelYaml = new(
        static () => File.ReadAllText(ModelInfo.ModelPath));

    private static readonly Lazy<Nfiq2ManagedModel> s_managedModel = new(
        static () => new(ModelInfo));

    private static readonly Lazy<Nfiq2RandomForestModel> s_randomForestModel = new(
        static () => Nfiq2RandomForestModel.FromModelInfo(ModelInfo));

    public static Nfiq2Algorithm Algorithm => s_algorithm.Value;

    public static Nfiq2ModelInfo ModelInfo => s_modelInfo.Value;

    public static string ModelYaml => s_modelYaml.Value;

    public static Nfiq2ManagedModel ManagedModel => s_managedModel.Value;

    public static Nfiq2RandomForestModel RandomForestModel => s_randomForestModel.Value;
}
