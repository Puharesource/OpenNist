namespace OpenNist.Tests.Nfiq.TestDataSources;

using OpenNist.Tests.Nfiq.TestSupport;

internal static class Nfiq2TestDataSources
{
    public static IEnumerable<TestDataRow<Nfiq2ExampleCase>> ExampleCases()
    {
        return EnumerateExampleCases().Select(static exampleCase =>
            new TestDataRow<Nfiq2ExampleCase>(
                exampleCase,
                DisplayName: $"should match the official NFIQ 2 example output for {exampleCase.Name}"));
    }

    public static IEnumerable<Nfiq2ExampleCase> EnumerateExampleCases()
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
