namespace OpenNist.Tests.Nist.TestSupport;

internal static class NistTestPaths
{
    public static string TestDataRootPath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "Nist");

    public static string Reference2007RootPath =>
        Path.Combine(TestDataRootPath, "ansi_nist_2007_reference");
}
