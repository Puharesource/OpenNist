# OpenNist

OpenNist is an Apache-2.0 biometric toolkit for .NET and WebAssembly. The repository currently focuses on modular libraries for transaction parsing, WSQ, NFIQ 2, and shared primitives:

- `OpenNist.Primitives` for shared low-level result, error, validation, and documentation-link types
- `OpenNist.Nist` for ANSI/NIST-style transaction files
- `OpenNist.Wsq` for WSQ inspection, decode, and encode workflows
- `OpenNist.Nfiq` for NFIQ 2 scoring

The repo also includes:

- `OpenNist.Wasm` for browser interop
- a React-based web app for NIST inspection, image codec workflows, and NFIQ review
- benchmark coverage for NIST, WSQ, and NFIQ hot paths

## Documentation

Start here:

- [Documentation hub](docs/README.md)
- [Changelog](CHANGELOG.md)
- [Quickstart](docs/quickstart.md)
- [Package reference](docs/reference/package-reference.md)
- [Troubleshooting](docs/troubleshooting.md)
- [Glossary](docs/glossary.md)

## Repository layout

```text
/src
  /design-system
  /dotnet
    /interop
      /OpenNist.Wasm
    /libraries
      /OpenNist.Primitives
      /OpenNist.Nfiq
      /OpenNist.Nist
      /OpenNist.Wsq
  /web
    /open-nist-site
/tests
  /OpenNist.Tests
/tools
  /OpenNist.Benchmarks
/docs
OpenNist.slnx
```

## Prerequisites

- .NET SDK `10.0.103` or a compatible .NET 10 feature band
- [Bun](https://bun.sh) for the web app workspace

The pinned SDK is defined in [`global.json`](global.json).

## Build

Restore and build the .NET solution:

```bash
dotnet restore OpenNist.slnx
dotnet build OpenNist.slnx
```

Run the test suite:

```bash
dotnet test --project tests/OpenNist.Tests/OpenNist.Tests.csproj
```

Run the web app locally:

```bash
cd src/web/open-nist-site
bun install
bun run dev
```

## Current scope

### `OpenNist.Primitives`

- provide shared low-level result, error, validation, exception, and documentation-link primitives
- support the higher-level OpenNist libraries without turning `Common` into a catch-all package

### `OpenNist.Nist`

- decode ANSI/NIST-style fielded and mixed binary transactions
- preserve opaque binary records for byte-exact round-tripping
- encode the in-memory object model back to transaction bytes

### `OpenNist.Wsq`

- inspect WSQ metadata
- decode WSQ into raw 8-bit grayscale pixels
- encode raw 8-bit grayscale pixels into WSQ
- benchmark and regression-test exactness against the checked-in corpus

### `OpenNist.Nfiq`

- score 500 PPI 8-bit grayscale fingerprint images
- expose mapped quality measures
- run in both .NET and WASM flows

## Development notes

- Commit messages use Conventional Commits.
- Local contributor setup is documented in [CONTRIBUTING.md](CONTRIBUTING.md).
- Shared package metadata is centralized in `Directory.Build.props` and `Directory.Packages.props`.

## License

OpenNist is licensed under the Apache License 2.0. See [LICENSE](LICENSE).
