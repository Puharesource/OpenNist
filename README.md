# OpenNist

OpenNist is a .NET library suite for working with biometric and NIST-related formats, standards, and quality tooling.

The repository is still early, but it is no longer scaffolding-only. The current WSQ work now includes a managed parser, a managed decode path backed by the official NIST reference codestream corpus and NBIS-generated decoder reconstructions, and a working managed encode path that accepts raw grayscale input streams and emits WSQ codestreams with generated Huffman tables and NISTCOM metadata. The managed decoder now matches the local NBIS-generated raw reference reconstructions byte-for-byte across the official public decoder corpus. On the encoder side, the managed implementation now satisfies the published NIST public-corpus checks for file size, frame-header values, quantization-bin widths, and quantized coefficient-bin tolerances when encoded with the reference software implementation number used by the included corpus, and the forward decomposition path now aligns with the local NBIS encoder analysis path on the current ARM64 development environment. The remaining exact encoder-parity gap is currently isolated to quantization-bin synthesis and scaled-table behavior rather than the forward transform itself. Exact published reference-bin and reference-codestream parity are still stricter separate goals, and formal FBI/NIST certification is still outside the scope of the current repository state.

OpenNist is licensed under the Apache License 2.0. See [`LICENSE`](LICENSE).

## Planned packages

- `OpenNist.Core`: shared primitives, abstractions, and common helpers used across the suite.
- `OpenNist.Nist`: support for ANSI/NIST and related interchange structures.
- `OpenNist.Wsq`: WSQ-specific types and helpers.
- `OpenNist.Jp2000`: JPEG 2000-related functionality needed by biometric workflows.
- `OpenNist.Nfiq`: NFIQ-related integration points and utilities.

## Repository layout

```text
OpenNist.slnx
OpenNist.Core/
OpenNist.Nist/
OpenNist.Wsq/
OpenNist.Jp2000/
OpenNist.Nfiq/
OpenNist.Tests/
docs/
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

WSQ-specific implementation notes live in [`docs/wsq.md`](docs/wsq.md).
