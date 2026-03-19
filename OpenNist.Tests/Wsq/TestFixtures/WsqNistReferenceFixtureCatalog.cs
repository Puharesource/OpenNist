namespace OpenNist.Tests.Wsq.TestFixtures;

using System.Text.Json;
using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Wsq;

internal static class WsqNistReferenceFixtureCatalog
{
    public static string DatasetRoot { get; } = Path.Combine(
        AppContext.BaseDirectory,
        "TestData",
        "Wsq",
        "NistReferenceImages",
        "V2_0");

    public static string EncodeRawDirectory { get; } = Path.Combine(DatasetRoot, "Encode", "Raw");

    public static string ReferenceBitRate075Directory { get; } = Path.Combine(DatasetRoot, "ReferenceWsq", "BitRate075");

    public static string ReferenceBitRate225Directory { get; } = Path.Combine(DatasetRoot, "ReferenceWsq", "BitRate225");

    public static string NonStandardFilterTapSetsDirectory { get; } = Path.Combine(DatasetRoot, "ReferenceWsq", "NonStandardFilterTapSets");

    public static string ReferenceReconstructionRawDirectory { get; } = Path.Combine(DatasetRoot, "ReferenceReconstruction", "Raw");

    public static string ReferenceReconstructionBitRate075Directory { get; } = Path.Combine(ReferenceReconstructionRawDirectory, "BitRate075");

    public static string ReferenceReconstructionBitRate225Directory { get; } = Path.Combine(ReferenceReconstructionRawDirectory, "BitRate225");

    public static string ReferenceReconstructionNonStandardFilterTapSetsDirectory { get; } = Path.Combine(
        ReferenceReconstructionRawDirectory,
        "NonStandardFilterTapSets");

    public static IReadOnlyList<WsqNistEncodeFixture> EncodeFixtures { get; } = LoadEncodeFixtures();

    public static IReadOnlyList<WsqDecodingReferenceCase> DecodeFixtures { get; } = LoadDecodeFixtures();

    public static IReadOnlyList<WsqDecodingReferenceCase> NonStandardDecodeFixtures { get; } = LoadNonStandardDecodeFixtures();

    private static WsqNistEncodeFixture[] LoadEncodeFixtures()
    {
        var metadataPath = Path.Combine(DatasetRoot, "raw-image-dimensions.json");

        using var stream = File.OpenRead(metadataPath);
        using var document = JsonDocument.Parse(stream);

        return [.. document.RootElement
            .EnumerateArray()
            .Select(static metadata => new WsqNistEncodeFixture(
                metadata.GetProperty("fileName").GetString() ?? throw new InvalidOperationException("Missing fileName."),
                new(
                    metadata.GetProperty("width").GetInt32(),
                    metadata.GetProperty("height").GetInt32()),
                Path.Combine(
                    EncodeRawDirectory,
                    metadata.GetProperty("fileName").GetString() ?? throw new InvalidOperationException("Missing fileName.")),
                Path.Combine(
                    ReferenceBitRate075Directory,
                    Path.ChangeExtension(
                        metadata.GetProperty("fileName").GetString() ?? throw new InvalidOperationException("Missing fileName."),
                        ".wsq")),
                Path.Combine(
                    ReferenceBitRate225Directory,
                    Path.ChangeExtension(
                        metadata.GetProperty("fileName").GetString() ?? throw new InvalidOperationException("Missing fileName."),
                        ".wsq"))))
            .OrderBy(static fixture => fixture.FileName, StringComparer.Ordinal)];
    }

    private static WsqDecodingReferenceCase[] LoadDecodeFixtures()
    {
        return [.. EncodeFixtures
            .SelectMany(static fixture => CreateDecodeFixtures(fixture))
            .OrderBy(static fixture => fixture.ReferenceSet, StringComparer.Ordinal)
            .ThenBy(static fixture => fixture.FileName, StringComparer.Ordinal)];
    }

    private static WsqDecodingReferenceCase[] LoadNonStandardDecodeFixtures()
    {
        return [.. Directory
            .EnumerateFiles(NonStandardFilterTapSetsDirectory, "*.wsq")
            .OrderBy(static path => Path.GetFileName(path), StringComparer.Ordinal)
            .Select(static referencePath =>
            {
                var fileName = Path.GetFileName(referencePath);
                var reconstructionRawPath = Path.Combine(
                    ReferenceReconstructionNonStandardFilterTapSetsDirectory,
                    Path.ChangeExtension(fileName, ".raw"));
                var reconstructionMetadataPath = Path.Combine(
                    ReferenceReconstructionNonStandardFilterTapSetsDirectory,
                    Path.ChangeExtension(fileName, ".ncm"));
                var metadata = WsqNistComMetadataReader.Read(reconstructionMetadataPath);

                return CreateDecodeFixture(
                    fileName,
                    referenceSet: "NonStandardFilterTapSets",
                    referencePath,
                    reconstructionRawPath,
                    reconstructionMetadataPath,
                    metadata.ToRawImageDescription());
            })];
    }

    private static WsqDecodingReferenceCase CreateDecodeFixture(
        string fileName,
        string referenceSet,
        string referencePath,
        string reconstructionRawPath,
        string reconstructionMetadataPath,
        WsqRawImageDescription rawImage)
    {
        return new(
            fileName,
            referenceSet,
            rawImage,
            referencePath,
            reconstructionRawPath,
            reconstructionMetadataPath);
    }

    private static IEnumerable<WsqDecodingReferenceCase> CreateDecodeFixtures(WsqNistEncodeFixture fixture)
    {
        yield return CreateDecodeFixture(
            fixture.FileName,
            referenceSet: "BitRate075",
            referencePath: fixture.ReferenceBitRate075Path,
            reconstructionRawPath: Path.Combine(
                ReferenceReconstructionBitRate075Directory,
                Path.ChangeExtension(fixture.FileName, ".raw")),
            reconstructionMetadataPath: Path.Combine(
                ReferenceReconstructionBitRate075Directory,
                Path.ChangeExtension(fixture.FileName, ".ncm")),
            rawImage: fixture.RawImage);

        yield return CreateDecodeFixture(
            fixture.FileName,
            referenceSet: "BitRate225",
            referencePath: fixture.ReferenceBitRate225Path,
            reconstructionRawPath: Path.Combine(
                ReferenceReconstructionBitRate225Directory,
                Path.ChangeExtension(fixture.FileName, ".raw")),
            reconstructionMetadataPath: Path.Combine(
                ReferenceReconstructionBitRate225Directory,
                Path.ChangeExtension(fixture.FileName, ".ncm")),
            rawImage: fixture.RawImage);
    }
}

internal readonly record struct WsqNistEncodeFixture(
    string FileName,
    WsqRawImageDescription RawImage,
    string RawPath,
    string ReferenceBitRate075Path,
    string ReferenceBitRate225Path);

internal readonly record struct WsqEncodingReferenceCase(
    string FileName,
    double BitRate,
    WsqRawImageDescription RawImage,
    string RawPath,
    string ReferencePath);

internal readonly record struct WsqDecodingReferenceCase(
    string FileName,
    string ReferenceSet,
    WsqRawImageDescription RawImage,
    string ReferencePath,
    string ReconstructionRawPath,
    string ReconstructionMetadataPath);
