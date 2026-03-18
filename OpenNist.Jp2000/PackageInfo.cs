using JetBrains.Annotations;

namespace OpenNist.Jp2000;

/// <summary>
/// Describes the OpenNist.Jp2000 package.
/// </summary>
[PublicAPI]
public static class PackageInfo
{
    /// <summary>
    /// Gets the NuGet package identifier.
    /// </summary>
    public const string PackageId = "OpenNist.Jp2000";
}

internal static class InternalVisibilityProbe
{
    internal const string PackageId = PackageInfo.PackageId;
}
