namespace OpenNist.Tests;

[Category("Unit: Scaffolding - Package Graph")]
internal sealed class PackageScaffoldingTests
{
    [Test]
    [DisplayName("should expose the expected package identifiers from the public package markers")]
    public async Task PackageMarkersExposeExpectedPackageIds()
    {
        var corePackageId = OpenNist.Core.PackageInfo.PackageId;
        var nistPackageId = OpenNist.Nist.PackageInfo.PackageId;
        var wsqPackageId = OpenNist.Wsq.PackageInfo.PackageId;
        var jp2000PackageId = OpenNist.Jp2000.PackageInfo.PackageId;
        var nfiqPackageId = OpenNist.Nfiq.PackageInfo.PackageId;

        await Assert.That(corePackageId).IsEqualTo("OpenNist.Core");
        await Assert.That(nistPackageId).IsEqualTo("OpenNist.Nist");
        await Assert.That(wsqPackageId).IsEqualTo("OpenNist.Wsq");
        await Assert.That(jp2000PackageId).IsEqualTo("OpenNist.Jp2000");
        await Assert.That(nfiqPackageId).IsEqualTo("OpenNist.Nfiq");
    }

    [Test]
    [DisplayName("should expose package internals to the shared test assembly")]
    public async Task PackageInternalsAreVisibleToTheTestAssembly()
    {
        var corePackageId = OpenNist.Core.InternalVisibilityProbe.PackageId;
        var nistPackageId = OpenNist.Nist.InternalVisibilityProbe.PackageId;
        var wsqPackageId = OpenNist.Wsq.InternalVisibilityProbe.PackageId;
        var jp2000PackageId = OpenNist.Jp2000.InternalVisibilityProbe.PackageId;
        var nfiqPackageId = OpenNist.Nfiq.InternalVisibilityProbe.PackageId;

        await Assert.That(corePackageId).IsEqualTo("OpenNist.Core");
        await Assert.That(nistPackageId).IsEqualTo("OpenNist.Nist");
        await Assert.That(wsqPackageId).IsEqualTo("OpenNist.Wsq");
        await Assert.That(jp2000PackageId).IsEqualTo("OpenNist.Jp2000");
        await Assert.That(nfiqPackageId).IsEqualTo("OpenNist.Nfiq");
    }
}
