namespace OpenNist.Wasm;

using System.Text.Json;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using OpenNist.Nfiq;
using OpenNist.Nist;
using OpenNist.Wsq;

[SupportedOSPlatform("browser")]
internal static partial class OpenNistWasmExports
{
    private static readonly WsqCodec Codec = new();
    private static readonly Nfiq2Algorithm Nfiq2 = new();

    [JSExport]
    public static string GetVersion()
    {
        return typeof(OpenNistWasmExports).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    [JSExport]
    public static byte[] EncodeWsq(
        byte[] rawPixels,
        int width,
        int height,
        int pixelsPerInch,
        double bitRate)
    {
        ArgumentNullException.ThrowIfNull(rawPixels);
        using var rawImageStream = new MemoryStream(rawPixels, writable: false);
        using var wsqStream = new MemoryStream();

        Codec.EncodeAsync(
            rawImageStream,
            wsqStream,
            new WsqRawImageDescription(width, height, BitsPerPixel: 8, PixelsPerInch: pixelsPerInch),
            new WsqEncodeOptions(bitRate),
            CancellationToken.None).AsTask().GetAwaiter().GetResult();

        return wsqStream.ToArray();
    }

    [JSExport]
    public static string InspectWsq(byte[] wsqBytes)
    {
        ArgumentNullException.ThrowIfNull(wsqBytes);

        using var metadataStream = new MemoryStream(wsqBytes, writable: false);
        var fileInfo = Codec.InspectAsync(metadataStream, CancellationToken.None).AsTask().GetAwaiter().GetResult();

        return JsonSerializer.Serialize(fileInfo, OpenNistWasmJsonContext.Default.WsqFileInfo);
    }

    [JSExport]
    public static byte[] DecodeWsq(byte[] wsqBytes)
    {
        ArgumentNullException.ThrowIfNull(wsqBytes);

        using var wsqStream = new MemoryStream(wsqBytes, writable: false);
        using var rawImageStream = new MemoryStream();

        Codec.DecodeAsync(wsqStream, rawImageStream, CancellationToken.None).AsTask().GetAwaiter().GetResult();

        return rawImageStream.ToArray();
    }

    [JSExport]
    public static string AssessNfiq(
        byte[] rawPixels,
        int width,
        int height,
        int pixelsPerInch)
    {
        ArgumentNullException.ThrowIfNull(rawPixels);

        var assessment = Nfiq2.AnalyzeAsync(
            rawPixels,
            new Nfiq2RawImageDescription(width, height, BitsPerPixel: 8, PixelsPerInch: pixelsPerInch),
            cancellationToken: CancellationToken.None).AsTask().GetAwaiter().GetResult();

        var browserAssessment = OpenNistNfiqAssessmentResult.FromAssessment(assessment);
        return JsonSerializer.Serialize(browserAssessment, OpenNistWasmJsonContext.Default.OpenNistNfiqAssessmentResult);
    }

    [JSExport]
    public static string InspectNist(byte[] nistBytes)
    {
        ArgumentNullException.ThrowIfNull(nistBytes);

        var file = NistDecoder.Decode(nistBytes);
        var browserFile = OpenNistNistFileResult.FromFile(file);
        return JsonSerializer.Serialize(browserFile, OpenNistWasmJsonContext.Default.OpenNistNistFileResult);
    }

    [JSExport]
    public static byte[] EncodeNist(string fileJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileJson);

        var file = JsonSerializer.Deserialize(fileJson, OpenNistWasmJsonContext.Default.OpenNistNistFileInput) ??
            throw new InvalidOperationException("Encoded NIST payload could not be deserialized.");

        return NistEncoder.Encode(file.ToFile());
    }
}
