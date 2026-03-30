# How-to: Deploy the Site to Cloudflare Pages

Use Cloudflare Pages to host the OpenNist website and app at `https://opennist.tarkan.dev`.

## Why Pages is the right fit

The OpenNist website is a Vite-built single-page app with static assets and client-side routing.

That makes Cloudflare Pages the simplest deployment target:

- static asset hosting
- built-in CDN
- custom domain support
- SPA route handling for `/docs/*` and `/app/*`

## Important deployment constraint

Cloudflare Pages limits individual uploaded assets to `25 MiB`.[^cf-pages-limits]

The website uses `ffmpeg.wasm` for image normalization and export workflows, and the upstream `@ffmpeg/core` WebAssembly file is larger than that limit.

Because of that, do not bundle `@ffmpeg/core` into the Cloudflare deployment itself. Instead:

- host `ffmpeg-core.js` and `ffmpeg-core.wasm` on another static host such as Cloudflare R2
- point the site at those URLs with Vite environment variables

The website already supports this through:

- `VITE_FFMPEG_CORE_URL`
- `VITE_FFMPEG_WASM_URL`

## Recommended asset layout

Host the FFmpeg runtime on a separate static origin, for example:

```text
https://static.tarkan.dev/opennist/ffmpeg/ffmpeg-core.js
https://static.tarkan.dev/opennist/ffmpeg/ffmpeg-core.wasm
```

These can live in:

- a public Cloudflare R2 bucket
- another static site
- another CDN-backed asset host

## Configure the Pages project

In Cloudflare Pages, create a project connected to the GitHub repository:

- repository: `Puharesource/OpenNist`
- production branch: your main release branch
- root directory: `src/web/open-nist-site`
- build command: `npm install -g bun && bun install && bun run build:cloudflare`
- build output directory: `dist`

Use `build:cloudflare` instead of `build` because Pages does not need to regenerate the committed OpenNist WASM assets during deployment.

## Set environment variables

In the Pages project settings, add:

```text
VITE_FFMPEG_CORE_URL=https://static.tarkan.dev/opennist/ffmpeg/ffmpeg-core.js
VITE_FFMPEG_WASM_URL=https://static.tarkan.dev/opennist/ffmpeg/ffmpeg-core.wasm
```

Set them for:

- Production
- Preview

If you do not set them, the site falls back to jsDelivr for `@ffmpeg/core`.

## Add the custom domain

In the Pages project:

1. Go to `Custom domains`
2. Add `opennist.tarkan.dev`
3. Complete the DNS setup that Cloudflare prompts you to create

Cloudflare recommends attaching the custom domain from the Pages project itself instead of only creating the DNS record manually.[^cf-custom-domains]

## Redirect the generated `pages.dev` hostname

Once the custom domain is attached, redirect the generated `*.pages.dev` hostname to `https://opennist.tarkan.dev` with a Bulk Redirect.[^cf-pages-dev-redirect]

Recommended redirect settings:

- status: `301`
- preserve query string: enabled
- subpath matching: enabled
- preserve path suffix: enabled
- include subdomains: enabled

## Client-side routing

Cloudflare Pages automatically treats projects without a top-level `404.html` as single-page applications and routes unknown paths to `/`.[^cf-pages-spa]

That means routes such as these will still work on refresh:

- `/docs`
- `/docs/error-codes`
- `/app/nist`
- `/app/nfiq`

## Local validation before deploy

From the web app directory:

```bash
bun install
bun run lint
bun run build:cloudflare
```

## What this repo already does

The website is already prepared for this deployment model:

- OpenNist WASM interop assets are committed under `public/`
- the Cloudflare build can skip `dotnet publish`
- FFmpeg runtime URLs are configurable through Vite env vars

[^cf-pages-limits]: [Cloudflare Pages limits](https://developers.cloudflare.com/pages/platform/limits/)
[^cf-custom-domains]: [Cloudflare Pages custom domains](https://developers.cloudflare.com/pages/configuration/custom-domains/)
[^cf-pages-dev-redirect]: [Redirecting `*.pages.dev` to a custom domain](https://developers.cloudflare.com/pages/how-to/redirect-to-custom-domain/)
[^cf-pages-spa]: [Cloudflare Pages SPA rendering behavior](https://developers.cloudflare.com/pages/configuration/serving-pages/)
