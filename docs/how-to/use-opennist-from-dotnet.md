# How-to: Use OpenNist from .NET

This guide shows how to consume the .NET packages directly in your own application.

## Choose the packages you need

Install only the packages that match your workflow:

```bash
dotnet add package OpenNist.Nist
dotnet add package OpenNist.Wsq
dotnet add package OpenNist.Nfiq
```

Typical split:

- `OpenNist.Nist` for ANSI/NIST transaction parsing and encoding
- `OpenNist.Wsq` for WSQ inspection, decode, and encode
- `OpenNist.Nfiq` for NFIQ 2 scoring

`OpenNist.Primitives` provides the shared low-level error, result, validation, and documentation-link types used by those packages. Most application code should not need to install it directly.

## Decode a NIST transaction

```csharp
using OpenNist.Nist;

await using var stream = File.OpenRead("sample.nist");
var file = NistDecoder.Decode(stream);

foreach (var record in file.Records)
{
    Console.WriteLine($"Type-{record.Type}");
}
```

## Re-encode after editing

```csharp
using OpenNist.Nist;

await using var input = File.OpenRead("sample.an2");
var file = NistDecoder.Decode(input);

await using var output = File.Create("roundtrip.an2");
NistEncoder.Encode(output, file);
```

## Inspect and decode WSQ

```csharp
using OpenNist.Wsq;

var codec = new WsqCodec();

await using var wsqStream = File.OpenRead("fingerprint.wsq");
var info = await codec.InspectAsync(wsqStream);

Console.WriteLine($"{info.Width}x{info.Height}");
Console.WriteLine($"PPI: {info.PixelsPerInch}");
```

```csharp
using OpenNist.Wsq;

var codec = new WsqCodec();

await using var wsqStream = File.OpenRead("fingerprint.wsq");
await using var rawStream = File.Create("fingerprint.raw");

var rawImage = await codec.DecodeAsync(wsqStream, rawStream);
Console.WriteLine($"{rawImage.Width}x{rawImage.Height}");
```

## Score a fingerprint with NFIQ 2

```csharp
using OpenNist.Nfiq;

var algorithm = new Nfiq2Algorithm();
var result = await algorithm.AnalyzeFileAsync("fingerprint.pgm");

Console.WriteLine($"Score: {result.QualityScore}");
```

## When to stay in .NET

Prefer the .NET packages when:

- your app already runs on .NET
- you need stream-based file handling
- you want direct typed access to the object model
- you do not need browser hosting

Use `OpenNist.Wasm` instead when your execution environment is the browser and you need to call OpenNist from JavaScript or TypeScript.
