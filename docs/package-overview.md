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

NFIQ 2 analysis, CSV parsing, and compliance helpers built around the official NIST `nfiq2` command-line tool and published conformance artifacts.

## Dependency direction

- `OpenNist.Core` is the foundation.
- `OpenNist.Nist`, `OpenNist.Wsq`, `OpenNist.Jp2000`, and `OpenNist.Nfiq` may reference `OpenNist.Core`.
- Cross-package dependencies beyond `OpenNist.Core` are intentionally avoided during scaffolding.

## Current status

The repository is still early, but implementation has started.

- `OpenNist.Wsq` now contains a managed WSQ parser, quantized coefficient decoder, inverse wavelet reconstruction path, stream-based WSQ decode API coverage against the official NIST codestream corpus plus NBIS-generated decoder reconstructions, and a working managed encode path for raw-image normalization, forward decomposition, variance analysis, quantization, NBIS-style Huffman table generation, and WSQ bitstream emission. The managed decoder matches the local NBIS-generated raw reconstruction corpus byte-for-byte across the official public decoder vectors, the container writer uses NBIS-style numeric scaling for WSQ tables and frame headers, and the managed encoder now matches local `NBIS Release 5.0.0` codestream output byte-for-byte across all 80 public encoder cases while still satisfying the published NIST public-corpus checks for file size, frame-header values, quantization-bin widths, and coefficient-bin tolerances. Exact encoder-parity investigation has also shown that the published NIST reference corpus and local `NBIS Release 5.0.0` are not identical exact targets across all encoder cases: direct local `NBIS Release 5.0.0 cwsq` runs reproduce `0 / 80` bundled reference codestreams byte-for-byte. So the repo now treats exact local `NBIS Release 5.0.0` codestream parity as its primary internal exactness target while keeping the published NIST threshold checks as the certification-oriented acceptance floor. Exact bundled-reference codestream parity is still pending.
- Exact byte-for-byte emitted-codestream parity against the official NIST reference outputs is still pending, so the package is not yet ready for formal FBI certification testing.
- `OpenNist.Nfiq` now wraps the official NIST `nfiq2` CLI, including typed result parsing, in-memory 8-bit grayscale analysis via temporary PGM staging, batch analysis over image paths, and compliance evaluation against the published NFIQ 2.3.0 standard and mapped CSV baselines. The test suite validates the bundled public SFinGe sample images against the official example outputs and verifies both conformant and non-conformant CSV comparison behavior.
