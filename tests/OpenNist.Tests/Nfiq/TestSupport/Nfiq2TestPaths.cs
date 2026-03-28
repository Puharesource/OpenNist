namespace OpenNist.Tests.Nfiq.TestSupport;

using OpenNist.Nfiq;
using OpenNist.Nfiq.Configuration;

internal static class Nfiq2TestPaths
{
    private const string s_solutionFileName = "OpenNist.slnx";
    private static readonly Nfiq2Installation s_installation = Nfiq2Installation.FindDefault();
    private static readonly string s_repositoryRootPath = FindRepositoryRoot();

    public static string TestDataRootPath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "Nfiq2");

    public static string RepositoryRootPath => s_repositoryRootPath;

    public static string StandardConformanceCsvPath =>
        Path.Combine(TestDataRootPath, "Conformance", "conformance_expected_output-v2.3.0.csv");

    public static string MappedConformanceCsvPath =>
        Path.Combine(TestDataRootPath, "Conformance", "conformance_expected_output-v2.3.0-mapped.csv");

    public static string NfiqDiagnosticToolDirectory =>
        Path.Combine(RepositoryRootPath, "tools", "nfiq-diag");

    public static string NfiqCommonOracleBuildScriptPath =>
        Path.Combine(NfiqDiagnosticToolDirectory, "build-nfiq2-common-oracle.sh");

    public static string NfiqCommonOraclePath =>
        Path.Combine(NfiqDiagnosticToolDirectory, "nfiq2_common_oracle");

    public static string NfiqMinutiaeOracleBuildScriptPath =>
        Path.Combine(NfiqDiagnosticToolDirectory, "build-nfiq2-minutiae-oracle.sh");

    public static string NfiqMinutiaeOraclePath =>
        Path.Combine(NfiqDiagnosticToolDirectory, "nfiq2_minutiae_oracle");

    public static string NfiqFingerJetOracleBuildScriptPath =>
        Path.Combine(NfiqDiagnosticToolDirectory, "build-nfiq2-fingerjet-oracle.sh");

    public static string NfiqFingerJetOraclePath =>
        Path.Combine(NfiqDiagnosticToolDirectory, "nfiq2_fingerjet_oracle");

    public static Nfiq2Installation Installation => s_installation;

    private static string FindRepositoryRoot()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, s_solutionFileName)))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException($"Unable to locate repository root containing {s_solutionFileName}.");
    }
}
