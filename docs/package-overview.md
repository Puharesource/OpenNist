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

The repository is still early, but implementation has started.

- `OpenNist.Wsq` now contains a managed WSQ parser, quantized coefficient decoder, inverse wavelet reconstruction path, stream-based WSQ decode API coverage against the official NIST codestream corpus plus NBIS-generated decoder reconstructions, and a working managed encode path for raw-image normalization, forward decomposition, variance analysis, quantization, NBIS-style Huffman table generation, and WSQ bitstream emission. The managed decoder matches the local NBIS-generated raw reconstruction corpus byte-for-byte across the official public decoder vectors, the container writer uses NBIS-style numeric scaling for WSQ tables and frame headers, the forward transform now uses NBIS-aligned fused multiply-add accumulation on the current ARM64 development path, and the managed encoder now satisfies the published NIST public-corpus checks for file size, frame-header values, quantization-bin widths, and coefficient-bin tolerances when encoded with the reference software implementation number used by the included corpus. The current exact encoder-parity gap is isolated to quantization-bin synthesis and scaled-table behavior rather than the forward transform.
- Exact byte-for-byte emitted-codestream parity against the official NIST reference outputs is still pending, so the package is not yet ready for formal FBI certification testing.
- The remaining packages are still mostly at the project-structure stage.
