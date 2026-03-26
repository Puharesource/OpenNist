namespace OpenNist.Tests;

[Category("Unit: Scaffolding - Package Graph")]
internal sealed class PackageScaffoldingTests
{
    [Test]
    [DisplayName("should expose the expected package identifiers from the public package markers")]
    public async Task PackageMarkersExposeExpectedPackageIds()
    {
        var nistPackageId = OpenNist.Nist.PackageInfo.PackageId;
        var wsqPackageId = OpenNist.Wsq.PackageInfo.PackageId;
        var nfiqPackageId = OpenNist.Nfiq.PackageInfo.PackageId;

        await Assert.That(nistPackageId).IsEqualTo("OpenNist.Nist");
        await Assert.That(wsqPackageId).IsEqualTo("OpenNist.Wsq");
        await Assert.That(nfiqPackageId).IsEqualTo("OpenNist.Nfiq");
    }

    [Test]
    [DisplayName("should expose package internals to the shared test assembly")]
    public async Task PackageInternalsAreVisibleToTheTestAssembly()
    {
        var nistPackageId = OpenNist.Nist.InternalVisibilityProbe.s_packageId;
        var wsqPackageId = OpenNist.Wsq.InternalVisibilityProbe.s_packageId;
        var nfiqPackageId = OpenNist.Nfiq.InternalVisibilityProbe.s_packageId;

        await Assert.That(nistPackageId).IsEqualTo("OpenNist.Nist");
        await Assert.That(wsqPackageId).IsEqualTo("OpenNist.Wsq");
        await Assert.That(nfiqPackageId).IsEqualTo("OpenNist.Nfiq");
    }
}
