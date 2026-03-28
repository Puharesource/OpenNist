namespace OpenNist.Tests.Wsq.TestDataReaders;

using System.Globalization;
using OpenNist.Wsq;
using OpenNist.Wsq.Model;

internal static class WsqNistComMetadataReader
{
    public static WsqNistComMetadata Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var values = File
            .ReadLines(path)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Select(static line => line.Split(' ', 2, StringSplitOptions.TrimEntries))
            .Where(static parts => parts.Length == 2)
            .ToDictionary(static parts => parts[0], static parts => parts[1], StringComparer.Ordinal);

        return new(
            Width: ParseRequiredInt(values, "PIX_WIDTH"),
            Height: ParseRequiredInt(values, "PIX_HEIGHT"),
            BitsPerPixel: ParseRequiredInt(values, "PIX_DEPTH"),
            PixelsPerInch: ParseOptionalInt(values, "PPI"),
            ColorSpace: ParseRequiredString(values, "COLORSPACE"));
    }

    private static int ParseRequiredInt(Dictionary<string, string> values, string key)
    {
        return int.Parse(ParseRequiredString(values, key), CultureInfo.InvariantCulture);
    }

    private static int? ParseOptionalInt(Dictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var value))
        {
            return null;
        }

        var parsedValue = int.Parse(value, CultureInfo.InvariantCulture);
        return parsedValue > 0 ? parsedValue : null;
    }

    private static string ParseRequiredString(Dictionary<string, string> values, string key)
    {
        if (values.TryGetValue(key, out var value))
        {
            return value;
        }

        throw new InvalidOperationException($"The NISTCOM sidecar is missing the required '{key}' field.");
    }
}

internal readonly record struct WsqNistComMetadata(
    int Width,
    int Height,
    int BitsPerPixel,
    int? PixelsPerInch,
    string ColorSpace)
{
    public WsqRawImageDescription ToRawImageDescription(int defaultPixelsPerInch = 500)
    {
        return new(
            Width,
            Height,
            BitsPerPixel,
            PixelsPerInch ?? defaultPixelsPerInch);
    }
}
