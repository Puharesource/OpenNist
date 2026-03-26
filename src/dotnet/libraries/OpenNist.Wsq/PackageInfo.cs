namespace OpenNist.Wsq;

using JetBrains.Annotations;

/// <summary>
/// Describes the OpenNist.Wsq package.
/// </summary>
[PublicAPI]
public static class PackageInfo
{
    /// <summary>
    /// Gets the NuGet package identifier.
    /// </summary>
    public const string PackageId = "OpenNist.Wsq";
}

internal static class InternalVisibilityProbe
{
    internal const string s_packageId = PackageInfo.PackageId;
}
