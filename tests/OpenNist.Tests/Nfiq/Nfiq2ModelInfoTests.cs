namespace OpenNist.Tests.Nfiq;

using OpenNist.Nfiq;
using OpenNist.Nfiq.Configuration;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Contract: NFIQ2 - Model Info")]
internal sealed class Nfiq2ModelInfoTests
{
    [Test]
    [DisplayName("should parse the official NFIQ2 model info file")]
    public async Task ShouldParseTheOfficialNfiq2ModelInfoFile()
    {
        var modelInfo = Nfiq2ModelInfo.FromFile(Nfiq2TestPaths.Installation.ModelInfoPath);

        await Assert.That(modelInfo.Name).IsEqualTo("Plain TIR + Ink");
        await Assert.That(modelInfo.Trainer).IsEqualTo("National Institute of Standards and Technology");
        await Assert.That(modelInfo.Version).IsEqualTo("2.0.0");
        await Assert.That(modelInfo.ModelHash).IsEqualTo("b4a1e7586b3be906f9770e4b77768038");
        await Assert.That(modelInfo.ModelPath).EndsWith("nist_plain_tir-ink.yaml");
        await Assert.That(Path.IsPathRooted(modelInfo.ModelPath)).IsTrue();
    }

    [Test]
    [DisplayName("should resolve a relative model path against the model-info file path")]
    public async Task ShouldResolveARelativeModelPathAgainstTheModelInfoFilePath()
    {
        var tempDirectoryPath = Path.Combine(Path.GetTempPath(), "OpenNist.Nfiq.ModelInfo", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectoryPath);

        try
        {
            var modelInfoPath = Path.Combine(tempDirectoryPath, "model-info.txt");
            var yamlPath = Path.Combine(tempDirectoryPath, "model.yaml");
            await File.WriteAllTextAsync(yamlPath, "model: stub");

            var content = """
                Name = Demo
                Path = model.yaml
                Hash = abc123
                """;

            var parsed = Nfiq2ModelInfo.Parse(content, modelInfoPath);

            await Assert.That(parsed.ModelPath).IsEqualTo(yamlPath);
            await Assert.That(parsed.ModelHash).IsEqualTo("abc123");
        }
        finally
        {
            if (Directory.Exists(tempDirectoryPath))
            {
                Directory.Delete(tempDirectoryPath, recursive: true);
            }
        }
    }
}
