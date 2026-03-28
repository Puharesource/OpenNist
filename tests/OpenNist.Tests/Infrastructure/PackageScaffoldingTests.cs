namespace OpenNist.Tests.Infrastructure;

using OpenNist.Nist.Metadata;

[Category("Unit: Scaffolding - Package Graph")]
internal sealed class PackageScaffoldingTests
{
    [Test]
    [DisplayName("should expose the expected package identifiers from the public package markers")]
    public async Task PackageMarkersExposeExpectedPackageIds()
    {
        var nistPackageId = PackageInfo.PackageId;
        var wsqPackageId = OpenNist.Wsq.Metadata.PackageInfo.PackageId;
        var nfiqPackageId = OpenNist.Nfiq.Metadata.PackageInfo.PackageId;

        await Assert.That(nistPackageId).IsEqualTo("OpenNist.Nist");
        await Assert.That(wsqPackageId).IsEqualTo("OpenNist.Wsq");
        await Assert.That(nfiqPackageId).IsEqualTo("OpenNist.Nfiq");
    }

    [Test]
    [DisplayName("should expose package internals to the shared test assembly")]
    public async Task PackageInternalsAreVisibleToTheTestAssembly()
    {
        var nistPackageId = InternalVisibilityProbe.s_packageId;
        var wsqPackageId = OpenNist.Wsq.Metadata.InternalVisibilityProbe.s_packageId;
        var nfiqPackageId = OpenNist.Nfiq.Metadata.InternalVisibilityProbe.s_packageId;

        await Assert.That(nistPackageId).IsEqualTo("OpenNist.Nist");
        await Assert.That(wsqPackageId).IsEqualTo("OpenNist.Wsq");
        await Assert.That(nfiqPackageId).IsEqualTo("OpenNist.Nfiq");
    }
}
