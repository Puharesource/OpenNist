# OpenNist.Wasm

`OpenNist.Wasm` is the browser-facing .NET entry point for OpenNist.

Current scope:

- exposes WSQ encode/decode functions to JavaScript
- is intended to be hosted by a future Bun/Vite/React frontend
- enables AOT compilation for Release publishes

Current JavaScript surface from `wwwroot/opennist.interop.js`:

- `OpenNistWasm.getVersion()`
- `OpenNistWasm.encodeWsq(rawPixelsBase64, width, height, pixelsPerInch, bitRate)`
- `OpenNistWasm.decodeWsq(wsqBase64)`

Example publish command:

```bash
dotnet publish src/dotnet/interop/OpenNist.Wasm/OpenNist.Wasm.csproj -c Release
```
