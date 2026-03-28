# Troubleshooting

## The browser app does not reflect my latest .NET changes

If you changed `OpenNist.Wasm` or anything it publishes into the web app, resync the browser assets first:

```bash
cd src/web/open-nist-site
bun run wasm:sync:debug
```

Then hard refresh the browser tab.

## NFIQ 2 rejects my image

Managed NFIQ 2 currently expects:

- 8-bit grayscale pixels
- 500 PPI input

If the input is RGB, another bit depth, or a different resolution, normalize it before analysis.

## A `.nist`, `.an2`, or `.eft` file does not decode

Check whether the file is:

- a fielded ANSI/NIST-style transaction
- a mixed fielded and opaque binary transaction
- truncated or malformed

OpenNist preserves opaque binary records, but malformed length headers or invalid record layout will still fail decode.

## The web app is using an old worker or old WASM bundle

Hard refresh the page after changes that affect:

- `OpenNist.Wasm`
- `opennist-wasm.worker.ts`
- synchronized `_framework` assets

The browser can otherwise keep stale worker or asset caches around.

## Tests pass locally, but browser workflows still fail

The browser app adds another layer:

- worker boot
- JSON marshaling
- file input/output permissions
- browser feature support

Check the browser console and verify that the WASM sync step ran before testing.
