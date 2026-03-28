# Reference: Package Reference

This page describes the main packages you consume, what each one is for, and when to choose it.

## `OpenNist.Nist`

Purpose:

- decode ANSI/NIST-style transaction files
- preserve mixed fielded and opaque binary records
- encode the in-memory object model back to bytes

Key types:

- `NistDecoder`
- `NistEncoder`
- `NistFile`
- `NistRecord`
- `NistField`
- `NistTag`

Use it when you need transaction structure, record walking, and round-tripping of NIST-family files from .NET.

## `OpenNist.Wsq`

Purpose:

- inspect WSQ container metadata
- decode WSQ to raw 8-bit grayscale pixels
- encode raw 8-bit grayscale pixels to WSQ

Key types:

- `WsqCodec`
- `WsqFileInfo`
- `WsqRawImageDescription`
- `WsqEncodeOptions`

Use it when you need WSQ-specific image workflows from .NET without bringing in unrelated format handling.

## `OpenNist.Nfiq`

Purpose:

- run managed NFIQ 2 analysis
- return typed assessment results and mapped quality measures
- support both .NET and browser-hosted flows

Key types:

- `Nfiq2Algorithm`
- `Nfiq2AssessmentResult`
- `Nfiq2AnalysisOptions`
- `Nfiq2RawImageDescription`

Use it when you need fingerprint quality scoring on supported grayscale inputs from .NET.

## `OpenNist.Wasm`

Purpose:

- expose browser-safe entry points over the managed libraries
- support the web app and external browser integrations

Use it when you need OpenNist functionality inside the browser from JavaScript or TypeScript.

## Typical choices

- Choose `OpenNist.Nist` when your app needs transaction parsing or round-tripping.
- Choose `OpenNist.Wsq` when your app needs WSQ inspection, decode, or encode.
- Choose `OpenNist.Nfiq` when your app needs managed NFIQ 2 scoring.
- Choose `OpenNist.Wasm` when your app runs in the browser and you want a TypeScript-facing surface over OpenNist.

See also:

- [Use OpenNist from .NET](../how-to/use-opennist-from-dotnet.md)
- [Use OpenNist from TypeScript](../how-to/use-opennist-from-typescript.md)
