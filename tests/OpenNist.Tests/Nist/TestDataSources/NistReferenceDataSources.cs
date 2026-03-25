namespace OpenNist.Tests.Nist.TestDataSources;

using OpenNist.Tests.Nist.TestSupport;

internal static class NistReferenceDataSources
{
    private static readonly string[] s_referenceFilePaths = EnumerateReferenceFilePathsCore().ToArray();

    public static IEnumerable<TestDataRow<string>> ReferenceFiles()
    {
        return s_referenceFilePaths.Select(static path =>
            new TestDataRow<string>(
                path,
                DisplayName: $"should handle official NIST fixture {Path.GetFileName(path)}"));
    }

    public static IEnumerable<string> EnumerateReferenceFilePaths()
    {
        return s_referenceFilePaths;
    }

    private static IEnumerable<string> EnumerateReferenceFilePathsCore()
    {
        return Directory.EnumerateFiles(NistTestPaths.Reference2007RootPath, "*.*", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(NistTestPaths.TestDataRootPath, "*.nist", SearchOption.TopDirectoryOnly))
            .Where(static path =>
                path.EndsWith(".an2", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".nist", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.Ordinal);
    }
}
