namespace OpenNist.Tests.Nfiq;

using System.Globalization;
using OpenNist.Nfiq;
using OpenNist.Nfiq.Errors;
using OpenNist.Nfiq.Internal;
using OpenNist.Nfiq.Internal.Runtime;
using OpenNist.Nfiq.Runtime;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - Managed Random Forest")]
internal sealed class Nfiq2RandomForestModelTests
{
    private static readonly Nfiq2Algorithm s_algorithm = Nfiq2TestContext.Algorithm;

    [Test]
    [DisplayName("should parse the official NFIQ 2 random forest model and match its declared md5 hash")]
    public async Task ShouldParseTheOfficialNfiq2RandomForestModelAndMatchItsDeclaredMd5Hash()
    {
        var modelInfo = Nfiq2TestContext.ModelInfo;
        var yaml = Nfiq2TestContext.ModelYaml;

        var model = Nfiq2RandomForestModel.Parse(yaml);
        var hash = Nfiq2RandomForestModel.CalculateMd5Hex(yaml);

        await Assert.That(model.TreeCount).IsEqualTo(100);
        await Assert.That(hash).IsEqualTo(modelInfo.ModelHash);
        await Assert.That(model.ParameterHash).IsEqualTo(modelInfo.ModelHash);
    }

    [Test]
    [DisplayName("should load the official NFIQ 2 random forest model from model-info with hash validation")]
    public async Task ShouldLoadTheOfficialNfiq2RandomForestModelFromModelInfoWithHashValidation()
    {
        var modelInfo = Nfiq2TestContext.ModelInfo;

        var model = Nfiq2TestContext.RandomForestModel;

        await Assert.That(model.TreeCount).IsEqualTo(100);
        await Assert.That(model.ParameterHash).IsEqualTo(modelInfo.ModelHash);
    }

    [Test]
    [DisplayName("should reject a model-info file whose declared hash does not match the random forest yaml")]
    public async Task ShouldRejectAModelInfoFileWhoseDeclaredHashDoesNotMatchTheRandomForestYaml()
    {
        var modelInfo = Nfiq2TestContext.ModelInfo with
        {
            ModelHash = "00000000000000000000000000000000",
        };

        await Assert.That(() => Nfiq2RandomForestModel.FromModelInfo(modelInfo))
            .Throws<Nfiq2Exception>();
    }

    [Test]
    [DisplayName("should reproduce the official NFIQ 2 score from the native feature vector for every bundled SFinGe image")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceTheOfficialNfiq2ScoreFromTheNativeFeatureVectorForEveryBundledSfinGeImage(
        Nfiq2ExampleCase exampleCase)
    {
        var model = Nfiq2TestContext.RandomForestModel;
        var nativeResult = await s_algorithm.AnalyzeFileAsync(
            exampleCase.ImagePath,
            new(IncludeMappedQualityMeasures: true, Force: true)).ConfigureAwait(false);

        var managedScore = model.Evaluate(nativeResult.NativeQualityMeasures);

        if (managedScore != nativeResult.QualityScore)
        {
            throw new InvalidOperationException(
                $"{exampleCase.Name} managed random forest score diverged from the official NFIQ 2 score. "
                + $"expected={nativeResult.QualityScore.ToString(CultureInfo.InvariantCulture)}, "
                + $"actual={managedScore.ToString(CultureInfo.InvariantCulture)}.");
        }

        await Assert.That(managedScore).IsEqualTo(nativeResult.QualityScore);
    }
}
