using JetBrains.Annotations;

namespace OpenNist.Nist;

/// <summary>
/// Describes the OpenNist.Nist package.
/// </summary>
[PublicAPI]
public static class PackageInfo
{
    /// <summary>
    /// Gets the NuGet package identifier.
    /// </summary>
    public const string PackageId = "OpenNist.Nist";
}

internal static class InternalVisibilityProbe
{
    internal const string PackageId = PackageInfo.PackageId;
}
