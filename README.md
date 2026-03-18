# OpenNist

OpenNist is a planned .NET library suite for working with biometric and NIST-related formats, standards, and quality tooling.

The repository is currently in an early scaffolding stage. The focus at this point is solution structure, packaging conventions, test setup, and shared repository hygiene for a reusable multi-package NuGet ecosystem.

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
dotnet test OpenNist.slnx
```

## Packaging notes

- Shared SDK, analyzer, and package metadata settings are centralized in `Directory.Build.props`.
- Shared package versions are centralized in `Directory.Packages.props`.
- Shared package license metadata uses the SPDX expression `Apache-2.0`.
- Each non-test project is configured to pack cleanly as its own NuGet package.
- `OpenNist.Tests` verifies the baseline project graph and internal visibility wiring used by the package projects.

## Documentation

Additional notes live in [`docs/`](docs/), starting with the package overview in [`docs/package-overview.md`](docs/package-overview.md).
