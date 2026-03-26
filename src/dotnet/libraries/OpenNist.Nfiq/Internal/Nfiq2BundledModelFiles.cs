namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2BundledModelFiles
{
    private const string s_modelInfoResourceName = "OpenNist.Nfiq.Assets.Nfiq2.nist_plain_tir-ink.txt";
    private const string s_modelYamlResourceName = "OpenNist.Nfiq.Assets.Nfiq2.nist_plain_tir-ink.yaml";
    private const string s_virtualModelInfoPath = "Assets/Nfiq2/nist_plain_tir-ink.txt";

    public static bool TryLoad(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Nfiq2ModelInfo? modelInfo,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? yaml)
    {
        var assembly = typeof(Nfiq2BundledModelFiles).Assembly;
        using var modelInfoStream = assembly.GetManifestResourceStream(s_modelInfoResourceName);
        using var yamlStream = assembly.GetManifestResourceStream(s_modelYamlResourceName);
        if (modelInfoStream is null || yamlStream is null)
        {
            modelInfo = null;
            yaml = null;
            return false;
        }

        using var modelInfoReader = new StreamReader(modelInfoStream);
        using var yamlReader = new StreamReader(yamlStream);
        var modelInfoContent = modelInfoReader.ReadToEnd();
        yaml = yamlReader.ReadToEnd();
        modelInfo = Nfiq2ModelInfo.Parse(modelInfoContent, s_virtualModelInfoPath);
        return true;
    }
}
