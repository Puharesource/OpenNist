namespace OpenNist.Wasm;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using OpenNist.Wsq;

public static class OpenNistWasmExports
{
    private static readonly WsqCodec Codec = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    [JSInvokable("openNist_getVersion")]
    public static string GetVersion()
    {
        return typeof(OpenNistWasmExports).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    [JSInvokable("openNist_encodeWsq")]
    public static async Task<string> EncodeWsqAsync(
        string rawPixelsBase64,
        int width,
        int height,
        int pixelsPerInch,
        double bitRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPixelsBase64);

        var rawPixels = Convert.FromBase64String(rawPixelsBase64);
        using var rawImageStream = new MemoryStream(rawPixels, writable: false);
        using var wsqStream = new MemoryStream();

        await Codec.EncodeAsync(
            rawImageStream,
            wsqStream,
            new WsqRawImageDescription(width, height, BitsPerPixel: 8, PixelsPerInch: pixelsPerInch),
            new WsqEncodeOptions(bitRate),
            CancellationToken.None).ConfigureAwait(false);

        return Convert.ToBase64String(wsqStream.ToArray());
    }

    [JSInvokable("openNist_decodeWsq")]
    public static async Task<string> DecodeWsqAsync(string wsqBase64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wsqBase64);

        var wsqBytes = Convert.FromBase64String(wsqBase64);
        using var wsqStream = new MemoryStream(wsqBytes, writable: false);
        using var metadataStream = new MemoryStream(wsqBytes, writable: false);
        using var rawImageStream = new MemoryStream();

        var fileInfo = await Codec.InspectAsync(metadataStream, CancellationToken.None).ConfigureAwait(false);
        var description = await Codec.DecodeAsync(wsqStream, rawImageStream, CancellationToken.None).ConfigureAwait(false);

        return JsonSerializer.Serialize(new WsqDecodeResult(
            description.Width,
            description.Height,
            description.BitsPerPixel,
            description.PixelsPerInch,
            Convert.ToBase64String(rawImageStream.ToArray()),
            fileInfo), JsonOptions);
    }

    private sealed record WsqDecodeResult(
        int Width,
        int Height,
        int BitsPerPixel,
        int PixelsPerInch,
        string RawPixelsBase64,
        WsqFileInfo FileInfo);
}
