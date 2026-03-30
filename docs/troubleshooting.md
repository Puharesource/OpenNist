# Troubleshooting

## NFIQ 2 rejects my image

OpenNist.Nfiq currently expects:

- 8-bit grayscale pixels
- 500 PPI input

If the input is RGB, another bit depth, or a different resolution, normalize it before analysis.

## A `.nist`, `.an2`, or `.eft` file does not decode

Check whether the file is:

- a fielded ANSI/NIST-style transaction
- a mixed fielded and opaque binary transaction
- truncated or malformed

OpenNist preserves opaque binary records, but malformed length headers or invalid record layout will still fail decode.

## My browser app is using an old worker or old WASM bundle

Hard refresh the page after changes that affect:

- `OpenNist.Wasm`
- `opennist-wasm.worker.ts`
- synchronized `_framework` assets

The browser can otherwise keep stale worker or asset caches around.
