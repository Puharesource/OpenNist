using FluentAssertions;

namespace OpenNist.Tests;

/// <summary>
/// Verifies the baseline scaffolding for the OpenNist package graph.
/// </summary>
public sealed class PackageScaffoldingTests
{
    /// <summary>
    /// Ensures the public package markers expose the expected package identifiers.
    /// </summary>
    [Fact]
    public void PackageMarkersExposeExpectedPackageIds()
    {
        OpenNist.Core.PackageInfo.PackageId.Should().Be("OpenNist.Core");
        OpenNist.Nist.PackageInfo.PackageId.Should().Be("OpenNist.Nist");
        OpenNist.Wsq.PackageInfo.PackageId.Should().Be("OpenNist.Wsq");
        OpenNist.Jp2000.PackageInfo.PackageId.Should().Be("OpenNist.Jp2000");
        OpenNist.Nfiq.PackageInfo.PackageId.Should().Be("OpenNist.Nfiq");
    }

    /// <summary>
    /// Ensures internals from each package assembly are visible to the shared test project.
    /// </summary>
    [Fact]
    public void PackageInternalsAreVisibleToTheTestAssembly()
    {
        OpenNist.Core.InternalVisibilityProbe.PackageId.Should().Be("OpenNist.Core");
        OpenNist.Nist.InternalVisibilityProbe.PackageId.Should().Be("OpenNist.Nist");
        OpenNist.Wsq.InternalVisibilityProbe.PackageId.Should().Be("OpenNist.Wsq");
        OpenNist.Jp2000.InternalVisibilityProbe.PackageId.Should().Be("OpenNist.Jp2000");
        OpenNist.Nfiq.InternalVisibilityProbe.PackageId.Should().Be("OpenNist.Nfiq");
    }
}
