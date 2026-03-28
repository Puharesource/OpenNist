namespace OpenNist.Benchmarks.Fixtures;

using System;
using System.IO;

internal static class BenchmarkPaths
{
    private const string s_solutionFileName = "OpenNist.slnx";

    public static string RepositoryRootPath { get; } = FindRepositoryRoot();

    public static string NfiqExampleImage(string fileName)
    {
        return Path.Combine(
            RepositoryRootPath,
            "tests",
            "OpenNist.Tests",
            "TestData",
            "Nfiq2",
            "Examples",
            "Images",
            fileName);
    }

    public static string NistFixture(string fileName)
    {
        return Path.Combine(
            RepositoryRootPath,
            "tests",
            "OpenNist.Tests",
            "TestData",
            "Nist",
            fileName);
    }

    public static string NistReference2007Fixture(string fileName)
    {
        return Path.Combine(
            RepositoryRootPath,
            "tests",
            "OpenNist.Tests",
            "TestData",
            "Nist",
            "ansi_nist_2007_reference",
            fileName);
    }

    public static string WsqReferenceFixture(string bitRateDirectory, string fileName)
    {
        return Path.Combine(
            RepositoryRootPath,
            "tests",
            "OpenNist.Tests",
            "TestData",
            "Wsq",
            "NistReferenceImages",
            "V2_0",
            "ReferenceWsq",
            bitRateDirectory,
            fileName);
    }

    public static string WsqRawFixture(string fileName)
    {
        return Path.Combine(
            RepositoryRootPath,
            "tests",
            "OpenNist.Tests",
            "TestData",
            "Wsq",
            "NistReferenceImages",
            "V2_0",
            "Encode",
            "Raw",
            fileName);
    }

    public static string WsqRawDimensionsMetadata()
    {
        return Path.Combine(
            RepositoryRootPath,
            "tests",
            "OpenNist.Tests",
            "TestData",
            "Wsq",
            "NistReferenceImages",
            "V2_0",
            "raw-image-dimensions.json");
    }

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
