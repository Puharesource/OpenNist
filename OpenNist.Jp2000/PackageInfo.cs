namespace OpenNist.Jp2000;

using JetBrains.Annotations;

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
