using JetBrains.Annotations;

namespace OpenNist.Core;

/// <summary>
/// Describes the OpenNist.Core package.
/// </summary>
[PublicAPI]
public static class PackageInfo
{
    /// <summary>
    /// Gets the NuGet package identifier.
    /// </summary>
    public const string PackageId = "OpenNist.Core";
}

internal static class InternalVisibilityProbe
{
    internal const string PackageId = PackageInfo.PackageId;
}
