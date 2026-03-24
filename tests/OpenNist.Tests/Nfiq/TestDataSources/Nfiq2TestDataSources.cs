namespace OpenNist.Tests.Nfiq.TestDataSources;

using OpenNist.Tests.Nfiq.TestSupport;

internal static class Nfiq2TestDataSources
{
    private static readonly Nfiq2ExampleCase[] s_exampleCases = EnumerateExampleCasesCore().ToArray();

    public static IEnumerable<TestDataRow<Nfiq2ExampleCase>> ExampleCases()
    {
        return s_exampleCases.Select(static exampleCase =>
            new TestDataRow<Nfiq2ExampleCase>(
                exampleCase,
                DisplayName: $"should match the official NFIQ 2 example output for {exampleCase.Name}"));
    }

    public static IEnumerable<Nfiq2ExampleCase> EnumerateExampleCases()
    {
        return s_exampleCases;
    }

    private static IEnumerable<Nfiq2ExampleCase> EnumerateExampleCasesCore()
    {
        var imagesDirectoryPath = Path.Combine(Nfiq2TestPaths.TestDataRootPath, "Examples", "Images");
        var expectedDirectoryPath = Path.Combine(Nfiq2TestPaths.TestDataRootPath, "Examples", "Expected");

        foreach (var imagePath in Directory.EnumerateFiles(imagesDirectoryPath, "*.pgm").OrderBy(static path => path, StringComparer.Ordinal))
        {
            var name = Path.GetFileNameWithoutExtension(imagePath);
            var expectedOutputPath = Path.Combine(expectedDirectoryPath, $"{name}_output.txt");
            yield return new(name, imagePath, expectedOutputPath);
        }
    }
}
