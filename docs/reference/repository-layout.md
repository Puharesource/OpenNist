# Reference: Repository Layout

This repository is split by concern so library code, browser interop, web UI, tests, and benchmarks can evolve independently.

## Top-level structure

```text
/src
  /design-system
  /dotnet
    /interop
    /libraries
  /web
/tests
/tools
/docs
```

## Important directories

### `src/dotnet/libraries`

Contains the core .NET packages:

- `OpenNist.Nist`
- `OpenNist.Wsq`
- `OpenNist.Nfiq`

### `src/dotnet/interop/OpenNist.Wasm`

Contains the browser-facing .NET WebAssembly bridge used by the web app and other browser-hosted flows.

### `src/web/open-nist-site`

Contains the React and Vite application used for:

- NIST inspection
- image codec workflows
- NFIQ 2 review
- landing and marketing pages

### `tests/OpenNist.Tests`

Contains the regression and exactness suite, including fixture-based NIST coverage and WSQ/NFIQ verification.

### `tools/OpenNist.Benchmarks`

Contains BenchmarkDotNet suites for the main performance-sensitive paths.

### `docs`

Contains user-facing project documentation and deeper implementation notes.
