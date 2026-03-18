namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestFixtures;

[Category("Unit: WSQ - NIST Reference Fixtures")]
internal sealed class WsqNistReferenceFixtureTests
{
    [Test]
    [DisplayName("should include the full official NIST WSQ raw and reference WSQ fixture set")]
    public async Task ShouldIncludeTheFullOfficialFixtureSet()
    {
        await Assert.That(Directory.Exists(WsqNistReferenceFixtureCatalog.DatasetRoot)).IsTrue();
        await Assert.That(Directory.GetFiles(WsqNistReferenceFixtureCatalog.EncodeRawDirectory, "*.raw").Length).IsEqualTo(40);
        await Assert.That(Directory.GetFiles(WsqNistReferenceFixtureCatalog.ReferenceBitRate075Directory, "*.wsq").Length).IsEqualTo(40);
        await Assert.That(Directory.GetFiles(WsqNistReferenceFixtureCatalog.ReferenceBitRate225Directory, "*.wsq").Length).IsEqualTo(40);
        await Assert.That(Directory.GetFiles(WsqNistReferenceFixtureCatalog.NonStandardFilterTapSetsDirectory, "*.wsq").Length).IsEqualTo(6);
    }

    [Test]
    [DisplayName("should map each official raw WSQ fixture to both reference bit rate files")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeFixtures))]
    public async Task ShouldMapEveryRawFixtureToBothReferenceBitRates(WsqNistEncodeFixture fixture)
    {
        await Assert.That(File.Exists(fixture.RawPath)).IsTrue();
        await Assert.That(File.Exists(fixture.ReferenceBitRate075Path)).IsTrue();
        await Assert.That(File.Exists(fixture.ReferenceBitRate225Path)).IsTrue();
        await Assert.That(fixture.RawImage.BitsPerPixel).IsEqualTo(8);
    }
}
