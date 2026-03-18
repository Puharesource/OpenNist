# Package Overview

## Purpose

OpenNist is structured as a small family of focused packages instead of a single large assembly. The intent is to keep dependencies conservative and let consumers reference only the capabilities they need.

## Package boundaries

### `OpenNist.Core`

Foundational shared types, abstractions, contracts, and cross-cutting helpers used by the rest of the suite.

### `OpenNist.Nist`

Types and services related to ANSI/NIST and adjacent interchange concerns.

### `OpenNist.Wsq`

WSQ-focused functionality that can evolve independently from the core package.

### `OpenNist.Jp2000`

JPEG 2000-oriented functionality that may later include parsing, metadata, or codec integration concerns.

### `OpenNist.Nfiq`

NFIQ-related adapters, quality scoring helpers, and integration surface area.

## Dependency direction

- `OpenNist.Core` is the foundation.
- `OpenNist.Nist`, `OpenNist.Wsq`, `OpenNist.Jp2000`, and `OpenNist.Nfiq` may reference `OpenNist.Core`.
- Cross-package dependencies beyond `OpenNist.Core` are intentionally avoided during scaffolding.

## Current status

The repository currently contains scaffolding only. Real protocol, image codec, or quality scoring implementations are intentionally deferred until package boundaries and public APIs are ready to harden.
