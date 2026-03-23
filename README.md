# OpenNist

OpenNist is a .NET library suite for working with biometric and NIST-related formats, standards, and quality tooling.

The repository is still early, but it is no longer scaffolding-only. The current WSQ work now includes a managed parser, a managed decode path backed by the official NIST reference codestream corpus and NBIS-generated decoder reconstructions, and a working managed encode path that accepts raw grayscale input streams and emits WSQ codestreams with generated Huffman tables and NISTCOM metadata. The managed decoder now matches the local NBIS-generated raw reference reconstructions byte-for-byte across the official public decoder corpus. On the encoder side, the managed implementation now satisfies the published NIST public-corpus checks for file size, frame-header values, quantization-bin widths, and quantized coefficient-bin tolerances when encoded with the reference software implementation number used by the included corpus, and it now also matches local `NBIS Release 5.0.0` encoder output byte-for-byte across all 80 public encoder cases. Exact encoder-parity investigation has also shown that the published NIST reference corpus and local `NBIS Release 5.0.0` are not identical exact targets across all encoder cases: local `NBIS Release 5.0.0 cwsq` runs are deterministic, but they still do not reproduce the bundled public reference codestreams byte-for-byte. So the repo now uses exact local `NBIS Release 5.0.0` codestream parity as its primary internal exactness target while keeping the published NIST threshold checks as the certification-oriented acceptance floor. Exact published reference-bin and reference-codestream parity remain separate diagnostic goals, and formal FBI/NIST certification is still outside the scope of the current repository state.

OpenNist is licensed under the Apache License 2.0. See [`LICENSE`](LICENSE).

## Planned packages

- `OpenNist.Core`: shared primitives, abstractions, and common helpers used across the suite.
- `OpenNist.Nist`: support for ANSI/NIST and related interchange structures.
- `OpenNist.Wsq`: WSQ-specific types and helpers.
- `OpenNist.Jp2000`: JPEG 2000-related functionality needed by biometric workflows.
- `OpenNist.Nfiq`: NFIQ-related integration points and utilities.

## Repository layout

```text
/src
  /design-system
  /dotnet
    /apps
    /interop
    /libraries
      /OpenNist.Core
      /OpenNist.Nist
      /OpenNist.Wsq
      /OpenNist.Jp2000
      /OpenNist.Nfiq
  /web
/tests
  /OpenNist.Tests
/tools
/docs
OpenNist.slnx
```

## Development

### Prerequisites

- .NET SDK 10.0.103 or compatible .NET 10 SDK feature band

### Git conventions

- Commit messages follow the Conventional Commits format: `<type>(optional-scope): <summary>`.
- The repository includes a versioned `commit-msg` hook in `.githooks/` and a commit template in `.gitmessage`.
- Contributor setup notes are in [`CONTRIBUTING.md`](CONTRIBUTING.md).

### Restore

```bash
dotnet restore OpenNist.slnx
```

### Build

```bash
dotnet build OpenNist.slnx
```

### Test

```bash
dotnet test --solution OpenNist.slnx
```

## Packaging notes

- Shared SDK, analyzer, and package metadata settings are centralized in `Directory.Build.props`.
- Shared package versions are centralized in `Directory.Packages.props`.
- Shared package license metadata uses the SPDX expression `Apache-2.0`.
- Each non-test project is configured to pack cleanly as its own NuGet package.
- `OpenNist.Tests` verifies the baseline project graph and internal visibility wiring used by the package projects.

## Documentation

Additional notes live in [`docs/`](docs/), starting with the package overview in [`docs/package-overview.md`](docs/package-overview.md).

Shared UI design guidance for future MAUI and web apps is documented in [`docs/design-system.md`](docs/design-system.md), with the source design assets under [`src/design-system/`](src/design-system/).

A minimal MAUI WSQ viewer now lives under [`src/dotnet/apps/OpenNist.Viewer.Maui/`](src/dotnet/apps/OpenNist.Viewer.Maui/). It is intentionally kept out of the main solution until the local machine has the .NET MAUI workload installed.

WSQ-specific implementation notes live in [`docs/wsq.md`](docs/wsq.md).

The NBIS-oracle debugging approach for the remaining WSQ encoder blocker cases is documented in [`docs/wsq-nbis-stage-oracle.md`](docs/wsq-nbis-stage-oracle.md).

The direct local `cwsq`-vs-bundled-reference comparison report is documented in [`docs/wsq-nbis-corpus-comparison.md`](docs/wsq-nbis-corpus-comparison.md).
