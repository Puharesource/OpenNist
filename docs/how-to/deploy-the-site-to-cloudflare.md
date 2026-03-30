# How-to: Deploy the Site to Cloudflare

Use Cloudflare Workers with static assets to host the OpenNist website and app at `https://opennist.tarkan.dev`.

## Why this deployment model

Cloudflare's current product flow creates framework-based static sites such as Vite apps as Workers with static assets.

That is a good fit for OpenNist because the site is:

- a Vite-built single-page app
- client-side routed under `/docs/*` and `/app/*`
- entirely static at deploy time

The repository now includes a committed Wrangler config for that flow.

## Build behavior

Use the Cloudflare-specific build script:

```bash
bun run build:cloudflare
```

This intentionally skips `dotnet publish` and builds from the already committed OpenNist WASM assets in `public/`.

That is important because Cloudflare's build environment in this setup already has Bun and Node, but should not depend on regenerating the interop assets during deploy.

## FFmpeg runtime constraint

The site uses `ffmpeg.wasm` for image normalization and export workflows.

The upstream `ffmpeg-core.wasm` file is larger than Cloudflare's per-asset upload limit, so it should not be bundled into the Worker static asset output.

The site now resolves FFmpeg from configurable external URLs:

- `VITE_FFMPEG_CORE_URL`
- `VITE_FFMPEG_WASM_URL`

Example values:

```text
VITE_FFMPEG_CORE_URL=https://static.tarkan.dev/opennist/ffmpeg/ffmpeg-core.js
VITE_FFMPEG_WASM_URL=https://static.tarkan.dev/opennist/ffmpeg/ffmpeg-core.wasm
```

You can host those files on:

- a public Cloudflare R2 bucket
- another static origin
- another CDN-backed host

## Cloudflare project settings

In Cloudflare:

- create an application from the repository
- choose the Vite/static site flow
- set the root directory to `src/web/open-nist-site`
- set the build command to `npm install -g bun && bun install && bun run build:cloudflare`
- set the output directory to `dist`

If Cloudflare shows this as a Worker project rather than an older Pages project, that is expected.

## Repository config used by Cloudflare

The web project now includes:

- `wrangler.jsonc`
- static asset serving from `dist`
- SPA fallback handling for client-side routes

That means refreshes for these routes continue to work:

- `/docs`
- `/docs/error-codes`
- `/app/nist`
- `/app/nfiq`

## Custom domain

Attach the production domain in Cloudflare:

- `opennist.tarkan.dev`

Once attached, make it the primary public hostname for the deployment.

## Local verification

From the web app directory:

```bash
bun install
bun run lint
bun run build:cloudflare
```

Optional local Cloudflare preview:

```bash
bun run preview:cloudflare
```

Optional manual deploy:

```bash
bun run deploy:cloudflare
```
