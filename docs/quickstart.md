# Quickstart

This quickstart is focused on consuming OpenNist, not contributing to the repository.

If you are integrating OpenNist into your own application, start here and then jump to the language-specific guides.

## Choose a surface

Use the smallest surface that matches your runtime:

- `.NET`: use `OpenNist.Nist`, `OpenNist.Wsq`, and `OpenNist.Nfiq`
- browser or TypeScript: use `OpenNist.Wasm` through the browser-facing TypeScript package surface

`OpenNist.Primitives` underpins the shared error, result, validation, and documentation-link model across the .NET packages, but most consumers do not need to reference it directly.

## Quickstart for .NET

Install only the packages you need:

```bash
dotnet add package OpenNist.Nist
dotnet add package OpenNist.Wsq
dotnet add package OpenNist.Nfiq
```

### First NIST decode

```csharp
using OpenNist.Nist;

await using var stream = File.OpenRead("sample.an2");
var file = NistDecoder.Decode(stream);

Console.WriteLine($"Records: {file.Records.Count}");
Console.WriteLine($"First record type: {file.Records[0].Type}");
```

### First WSQ inspect

```csharp
using OpenNist.Wsq;

await using var wsqStream = File.OpenRead("fingerprint.wsq");
var codec = new WsqCodec();
var info = await codec.InspectAsync(wsqStream);

Console.WriteLine($"{info.Width}x{info.Height} @ {info.PixelsPerInch} PPI");
```

### First NFIQ 2 score

```csharp
using OpenNist.Nfiq;

var algorithm = new Nfiq2Algorithm();
var result = await algorithm.AnalyzeFileAsync("fingerprint.pgm");

Console.WriteLine($"NFIQ 2 score: {result.QualityScore}");
```

## Quickstart for TypeScript

Install the browser-facing package:

```bash
npm install opennist-wasm
```

### Direct runtime usage

```ts
import { decodeWsq, inspectWsq } from "opennist-wasm/opennist.interop.js"

const info = await inspectWsq(file)
const decoded = await decodeWsq(file)

console.log(info.width, info.height)
console.log(decoded.width, decoded.height)
```

### Worker usage

```ts
const worker = new Worker(new URL("./opennist.worker.ts", import.meta.url), { type: "module" })

worker.postMessage({ type: "inspectWsq", wsqSource: file })
```

Use a worker when you want to keep WASM startup and heavier file operations off the main thread.

## If you are working from source

If you want to run the repository itself rather than consume packages:

```bash
dotnet restore OpenNist.slnx
dotnet build OpenNist.slnx
dotnet test --project tests/OpenNist.Tests/OpenNist.Tests.csproj

cd src/web/open-nist-site
bun install
bun run dev
```

That starts the browser app with the NIST workspace as the default entry view.

## Next steps

- Read the [package reference](reference/package-reference.md)
- Use [.NET with OpenNist](how-to/use-opennist-from-dotnet.md)
- Use [TypeScript with OpenNist.Wasm](how-to/use-opennist-from-typescript.md)
- Check [troubleshooting](troubleshooting.md) if the browser runtime or sample files fail to load
