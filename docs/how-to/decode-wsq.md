# How-to: Decode a WSQ File

Use `WsqCodec` when you want to inspect or decode a WSQ image.

## Inspect metadata

```csharp
using OpenNist.Wsq;

await using var wsqStream = File.OpenRead("fingerprint.wsq");
var codec = new WsqCodec();
var info = await codec.InspectAsync(wsqStream);

Console.WriteLine($"{info.Width}x{info.Height}");
Console.WriteLine($"PPI: {info.PixelsPerInch}");
```

## Decode into raw grayscale bytes

```csharp
using OpenNist.Wsq;

var codec = new WsqCodec();

await using var wsqStream = File.OpenRead("fingerprint.wsq");
await using var rawStream = File.Create("fingerprint.raw");

var rawImage = await codec.DecodeAsync(wsqStream, rawStream);

Console.WriteLine($"{rawImage.Width}x{rawImage.Height}");
Console.WriteLine($"{rawImage.BitsPerPixel} bits per pixel");
```

## What the output is

The decode API writes 8-bit grayscale raster bytes to your output stream. It does not write a PNG or JPEG container for you.

If you need an image file for debugging or UI display, wrap those bytes in your own image container layer after decode.
