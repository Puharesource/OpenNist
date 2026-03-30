# OpenNist Web App

Browser workbench for viewing, inspecting, and converting WSQ files.

Toolchain:

- `Vite 8`, which uses `Rolldown`
- `@vitejs/plugin-react`, which uses `Oxc`
- `Oxlint` for linting
- `Bun` for package management and scripts

Useful commands:

```bash
bun run dev
bun run lint
bun run build
bun run build:cloudflare
bun run preview:cloudflare
bun run deploy:cloudflare
```

Cloudflare Pages notes:

- `bun run build` refreshes the committed OpenNist WASM interop assets with `dotnet publish` before running the Vite build.
- `bun run build:cloudflare` skips the .NET publish step and builds from the already committed assets in `public/`.
- `ffmpeg.wasm` is resolved from `VITE_FFMPEG_CORE_URL` and `VITE_FFMPEG_WASM_URL` when set, and otherwise falls back to jsDelivr for `@ffmpeg/core@0.12.10`.
- `wrangler.jsonc` configures the site for Cloudflare Workers static asset hosting with SPA fallback handling.
