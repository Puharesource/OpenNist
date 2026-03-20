# WSQ NBIS Stage Oracle

## Purpose

This note describes the local NBIS-oracle approach used to debug the remaining WSQ encoder blocker cases without conflating that work with the public NIST acceptance corpus.

The repository keeps the published NIST encoder corpus as the primary correctness target for exact public-corpus parity and certification-oriented checks. The local NBIS C code is used here as a stage-by-stage diagnostic oracle to answer a narrower question:

- At which internal encoder stage does the managed implementation first diverge from the NBIS reference behavior?

## Current scope

The initial oracle coverage is intentionally narrow. It only targets the three remaining `2.25 bpp` blocker cases:

- `a070.raw`
- `cmp00003.raw`
- `cmp00005.raw`

These are the files that still remain outside the promoted active exact `2.25 bpp` coefficient set.

## Stage breakdown

The NBIS-oracle tests currently compare these stages:

1. Normalized pixels
2. Final wavelet decomposition buffer
3. Subband variances
4. Quantization bins
5. Quantized coefficients

The tests use the local NBIS C helpers in `tmp/wsq-diag/`:

- `nbis_wavelet_dump`
- `nbis_dump`

Those helpers come from the NBIS reference implementation and are treated as a local diagnostic oracle only.

## Why this exists

The remaining encoder issues are now small `+/-1` coefficient misses. Broad end-to-end parity tests are still useful, but they do not tell us where the first meaningful drift begins.

The stage-oracle tests narrow that down by checking whether drift begins in:

- normalization
- wavelet decomposition
- variance computation
- quantization-bin synthesis
- final coefficient quantization

That lets the repo record the current failure boundary explicitly instead of relying on repeated whole-pipeline experiments.

## Current interpretation

With the current managed encoder path:

- normalization matches the local NBIS output for the focused blocker cases
- the first managed-vs-NBIS divergence already appears in the wavelet decomposition buffer
- later variance, quantization-bin, and coefficient differences follow from that stage drift

This does not override the public NIST acceptance target. It simply tells us where to inspect the managed implementation next.

## Relationship to NIST acceptance

The oracle tests are diagnostic, not the final acceptance gate.

- Public NIST codestream references remain the main exact public-corpus target.
- NBIS stage oracles help explain *why* a case is still missing that target.
- A change should only be kept if it improves the public NIST-facing contract without regressing the current green baseline.

## Scaling this to all files later

If this approach proves useful, the next expansion path is:

1. Promote the stage-oracle data source from the 3 blocker files to all `2.25 bpp` files.
2. Add optional row-pass and column-pass wavelet checkpoints for node-level decomposition debugging.
3. Persist stable NBIS stage snapshots into a clearly labeled fixture format if local tool execution becomes too slow or too environment-specific.
4. Add a separate `0.75 bpp` oracle pass only after the `2.25 bpp` blocker work is settled.

## Practical note

The current stage-oracle tests depend on the local NBIS helper binaries under `tmp/wsq-diag/`.

That is deliberate:

- they are meant to speed up local debugging
- they are not the public acceptance contract
- if the local helpers are unavailable, the diagnostic tests simply return without asserting

If this oracle layer later becomes part of broader routine verification, the helpers or their stage outputs should be moved into a more explicit, reproducible fixture path.
