using JetBrains.Annotations;

namespace OpenNist.Nfiq;

/// <summary>
/// Describes the OpenNist.Nfiq package.
/// </summary>
[PublicAPI]
public static class PackageInfo
{
    /// <summary>
    /// Gets the NuGet package identifier.
    /// </summary>
    public const string PackageId = "OpenNist.Nfiq";
}

internal static class InternalVisibilityProbe
{
    internal const string PackageId = PackageInfo.PackageId;
}
