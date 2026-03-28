# How-to: Decode a WSQ File

Use `WsqCodec` when you want to inspect or decode a WSQ image.

## Inspect metadata

```csharp
using OpenNist.Wsq;

await using var wsqStream = File.OpenRead("fingerprint.wsq");
var codec = new WsqCodec();
var result = await codec.TryInspectAsync(wsqStream);

if (!result.IsSuccess)
{
    Console.WriteLine(result.Error!.Code);
    Console.WriteLine(result.Error.Message);
    return;
}

var info = result.Value!;

Console.WriteLine($"{info.Width}x{info.Height}");
Console.WriteLine($"PPI: {info.PixelsPerInch}");
```

## Decode into raw grayscale bytes

```csharp
using OpenNist.Wsq;

var codec = new WsqCodec();

await using var wsqStream = File.OpenRead("fingerprint.wsq");
await using var rawStream = File.Create("fingerprint.raw");

var result = await codec.TryDecodeAsync(wsqStream, rawStream);

if (!result.IsSuccess)
{
    Console.WriteLine(result.Error!.Code);
    Console.WriteLine(result.Error.Message);
    return;
}

var rawImage = result.Value!;

Console.WriteLine($"{rawImage.Width}x{rawImage.Height}");
Console.WriteLine($"{rawImage.BitsPerPixel} bits per pixel");
```

## Encode from raw grayscale bytes

```csharp
using OpenNist.Wsq;

var codec = new WsqCodec();

await using var rawStream = File.OpenRead("fingerprint.raw");
await using var wsqStream = File.Create("fingerprint.wsq");

var result = await codec.TryEncodeAsync(
    rawStream,
    wsqStream,
    new(Width: 512, Height: 512, BitsPerPixel: 8, PixelsPerInch: 500),
    new(BitRate: 0.75));

if (!result.IsSuccess)
{
    Console.WriteLine(result.Error!.Code);

    foreach (var validationError in result.Error.ValidationErrors ?? [])
    {
        Console.WriteLine($"- {validationError.Code}: {validationError.Message}");
    }
}
```

## What the output is

The decode API writes 8-bit grayscale raster bytes to your output stream. It does not write a PNG or JPEG container for you.

If you need an image file for debugging or UI display, wrap those bytes in your own image container layer after decode.

See also: [Error codes](../reference/error-codes.md)
