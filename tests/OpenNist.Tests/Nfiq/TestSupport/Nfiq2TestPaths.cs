namespace OpenNist.Tests.Nfiq.TestSupport;

using OpenNist.Nfiq;

internal static class Nfiq2TestPaths
{
    public static string TestDataRootPath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "Nfiq2");

    public static string StandardConformanceCsvPath =>
        Path.Combine(TestDataRootPath, "Conformance", "conformance_expected_output-v2.3.0.csv");

    public static string MappedConformanceCsvPath =>
        Path.Combine(TestDataRootPath, "Conformance", "conformance_expected_output-v2.3.0-mapped.csv");

    public static Nfiq2Installation Installation => Nfiq2Installation.FindDefault();
}
