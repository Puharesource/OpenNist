namespace OpenNist.Tests.Nfiq.TestSupport;

internal sealed record Nfiq2ExampleCase(
    string Name,
    string ImagePath,
    string ExpectedOutputPath);
