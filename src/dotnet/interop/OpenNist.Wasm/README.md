# OpenNist.Wasm

`OpenNist.Wasm` is the browser-facing .NET entry point for OpenNist.

Current scope:

- exposes WSQ encode/decode functions to JavaScript
- is intended to be hosted by a future Bun/Vite/React frontend
- enables AOT compilation for Release publishes

Current JavaScript surface from `wwwroot/opennist.interop.js`:

- `OpenNistWasm.getVersion()`
- `OpenNistWasm.readBinarySource(source)`
- `OpenNistWasm.encodeWsq(rawPixels, width, height, pixelsPerInch, bitRate)`
- `OpenNistWasm.inspectWsq(wsqSource)`
- `OpenNistWasm.decodeWsq(wsqSource)`
- `OpenNistWasm.assessNfiq(rawPixels, width, height, pixelsPerInch)`

The `source` arguments accept:

- `Uint8Array`
- `ArrayBuffer` and typed-array views
- `File` or `Blob`
- `Response`
- `ReadableStream<Uint8Array>`
- `AsyncIterable<Uint8Array>`

Example publish command:

```bash
dotnet publish src/dotnet/interop/OpenNist.Wasm/OpenNist.Wasm.csproj -c Release
```
