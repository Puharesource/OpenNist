# How-to: Use OpenNist from TypeScript

This guide focuses on consuming the browser-facing OpenNist surface from TypeScript.

It covers two patterns:

- direct runtime calls on the main thread
- running OpenNist inside a dedicated worker

## Install the package

Use the browser-facing package surface:

```bash
npm install opennist-wasm
```

## Direct runtime integration

Import the runtime entry points directly:

```ts
import {
  assessNfiq,
  decodeWsq,
  encodeWsq,
  getVersion,
  inspectWsq
} from "opennist-wasm/opennist.interop.js"
```

The browser-facing runtime accepts these binary source types directly:

- `Uint8Array`
- `ArrayBuffer` and typed-array views
- `File` or `Blob`
- `Response`
- `ReadableStream<Uint8Array>`
- `AsyncIterable<Uint8Array>`

### Inspect and decode WSQ

```ts
const info = await inspectWsq(file)
const decoded = await decodeWsq(file)

console.log(await getVersion())
console.log(info.width, info.height)
console.log(decoded.width, decoded.height)
```

If you are starting from `fetch(...)`, pass the response directly. Clone it if you need to read it twice:

```ts
const response = await fetch("/samples/fingerprint.wsq")
const info = await inspectWsq(response.clone())
const decoded = await decodeWsq(response)
```

### Score NFIQ 2 directly in the runtime

```ts
const assessment = await assessNfiq(
  decoded.pixels,
  decoded.width,
  decoded.height,
  decoded.pixelsPerInch ?? 500
)

console.log(assessment.score)
```

### Encode WSQ from raw grayscale pixels

```ts filename=opennist.worker.ts
const wsqBytes = await encodeWsq(rawPixels, width, height, 500, 2.25)
```

Use the direct runtime pattern when:

- your operations are infrequent
- you are already on a lightweight page
- you want the smallest integration layer

## Worker integration

For larger files or repeated work, put OpenNist behind a worker.

### Worker module

```ts filename=opennist.worker.ts
import { assessNfiq, decodeWsq, inspectWsq } from "opennist-wasm/opennist.interop.js"

self.addEventListener("message", async (event) => {
  const request = event.data

  switch (request.type) {
    case "inspectWsq": {
      const info = await inspectWsq(request.wsqSource)
      self.postMessage({ id: request.id, type: "success", payload: info })
      break
    }

    case "decodeWsqAndAssessNfiq": {
      const decoded = await decodeWsq(request.wsqSource)
      const assessment = await assessNfiq(
        decoded.pixels,
        decoded.width,
        decoded.height,
        decoded.pixelsPerInch ?? 500
      )

      self.postMessage({
        id: request.id,
        type: "success",
        payload: { decoded, assessment }
      })
      break
    }
  }
})
```

### Main-thread caller

```ts
const worker = new Worker(new URL("./opennist.worker.ts", import.meta.url), {
  type: "module"
})

worker.postMessage({
  id: 1,
  type: "decodeWsqAndAssessNfiq",
  wsqSource: file
})
```

Use the worker pattern when:

- you want WASM startup off the main thread
- you are decoding or scoring larger images
- you want a clean async boundary around the runtime

## Direct runtime vs worker

Choose direct runtime when:

- you want fewer moving parts
- you are integrating into a small app or demo

Choose a worker when:

- responsiveness matters
- you expect repeated codec or NFIQ operations
- you want a clean request/response boundary around the WASM runtime

## Relationship to the OpenNist website

The OpenNist website uses the worker pattern internally through:

- `opennist-wasm.worker.ts`
- `opennist-wasm.ts`

Those files are a good reference if you want a fuller request/response wrapper around the lower-level runtime exports.
