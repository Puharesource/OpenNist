namespace OpenNist.Tests.Nfiq;

using System.Globalization;
using OpenNist.Nfiq;
using OpenNist.Nfiq.Runtime;
using OpenNist.Tests.Nfiq.TestDataSources;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - Managed Model Surface")]
internal sealed class Nfiq2ManagedModelTests
{
    private static readonly Nfiq2Algorithm s_algorithm = Nfiq2TestContext.Algorithm;

    [Test]
    [DisplayName("should load the default managed NFIQ 2 model")]
    public async Task ShouldLoadTheDefaultManagedNfiq2Model()
    {
        var managedModel = Nfiq2TestContext.ManagedModel;

        await Assert.That(managedModel.ParameterHash).IsEqualTo("b4a1e7586b3be906f9770e4b77768038");
        await Assert.That(managedModel.Name).IsEqualTo("Plain TIR + Ink");
        await Assert.That(managedModel.Version).IsEqualTo("2.0.0");
    }

    [Test]
    [DisplayName("should reproduce the official NFIQ 2 score through the public managed model surface for every bundled SFinGe image")]
    [MethodDataSource(typeof(Nfiq2TestDataSources), nameof(Nfiq2TestDataSources.ExampleCases))]
    public async Task ShouldReproduceTheOfficialNfiq2ScoreThroughThePublicManagedModelSurfaceForEveryBundledSfinGeImage(
        Nfiq2ExampleCase exampleCase)
    {
        var managedModel = Nfiq2TestContext.ManagedModel;
        var official = await s_algorithm.AnalyzeFileAsync(
            exampleCase.ImagePath,
            new(IncludeMappedQualityMeasures: true, Force: true)).ConfigureAwait(false);

        var managedScore = managedModel.ComputeUnifiedQualityScore(official.NativeQualityMeasures);
        if (managedScore != official.QualityScore)
        {
            throw new InvalidOperationException(
                $"{exampleCase.Name} public managed model score diverged from the official NFIQ 2 score. "
                + $"expected={official.QualityScore.ToString(CultureInfo.InvariantCulture)}, "
                + $"actual={managedScore.ToString(CultureInfo.InvariantCulture)}.");
        }

        await Assert.That(managedScore).IsEqualTo(official.QualityScore);
    }
}
