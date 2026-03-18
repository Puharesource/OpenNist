namespace OpenNist.Tests.Wsq.TestFixtures;

using System.Text.Json;
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

    public static IReadOnlyList<WsqNistEncodeFixture> EncodeFixtures { get; } = LoadEncodeFixtures();

    private static WsqNistEncodeFixture[] LoadEncodeFixtures()
    {
        var metadataPath = Path.Combine(DatasetRoot, "raw-image-dimensions.json");

        using var stream = File.OpenRead(metadataPath);
        using var document = JsonDocument.Parse(stream);

        return document.RootElement
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
            .OrderBy(static fixture => fixture.FileName, StringComparer.Ordinal)
            .ToArray();
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
