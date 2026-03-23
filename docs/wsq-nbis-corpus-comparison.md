# WSQ NBIS Corpus Comparison

## Purpose

This note answers a narrower diagnostic question than the main WSQ parity work:

- If the local NBIS encoder binary is run directly against the bundled raw-image corpus, does it emit `.wsq` files that are byte-for-byte identical to the bundled NIST reference codestreams?

This is useful because it distinguishes two different claims:

- whether the bundled public reference corpus should be treated as the primary acceptance target
- whether a local NBIS build should be expected to reproduce those exact codestream bytes

The repository treats the bundled public NIST corpus as the primary encoder acceptance target. Local NBIS remains a diagnostic reference.

## Method

The direct comparison was run locally on:

- `NBIS Release 5.0.0`
- `cwsq` at `/tmp/nbis_v5_0_0/Rel_5.0.0/imgtools/bin/cwsq`
- `Darwin arm64`
- `macOS 26.3.1`

For each raw input in the bundled encoder corpus:

1. Run `cwsq` at `0.75` bpp and `2.25` bpp with `-raw_in width,height,8,500`.
2. Compare the emitted `.wsq` bytes directly against the bundled reference `.wsq` file for the same bitrate.
3. Re-run the same local encode twice for a repeatability check.
4. Record exact byte equality, byte length, and SHA-256 for the produced and bundled files.

The raw local output for that run is written to the ignored file:

- `tmp/nbis-vs-nist-corpus-comparison.json`

## Result

The local `NBIS Release 5.0.0` encoder did **not** reproduce the bundled public reference codestreams exactly:

- `NBIS Release 5.0.0`: `0 / 80` exact byte-for-byte matches and `0 / 80` same-size matches

The local `NBIS Release 5.0.0` encoder was deterministic in repeat local re-encodes on the same machine and settings.

So the direct answer is:

- the bundled reference `.wsq` files are **not** just a byte dump of what the local `cwsq` binary emits on this machine

## Interpretation

This does **not** mean the bundled corpus is wrong.

It means the following targets are not equivalent:

- exact bundled public reference codestream identity
- exact output from a local `NBIS Release 5.0.0 cwsq` build on this platform

That distinction matters because the official encoder procedure is not framed as plain local-NBIS byte identity. The public FBI/NIST material evaluates encoder behavior using file size, frame-header values, and quantized coefficient/bin behavior rather than raw whole-file equality alone:

- [FBI WSQ certification overview](https://fbibiospecs.fbi.gov/certifications-1/wsq)
- [NIST WSQ certification procedure](https://www.nist.gov/programs-projects/wsq-certification-procedure)
- [NIST NBIS distribution](https://www.nist.gov/services-resources/software/nist-biometric-image-software-nbis)

## Practical use in this repository

This repository therefore uses the comparison in two different ways:

- bundled NIST reference codestreams remain the primary certification-aligned acceptance target
- local NBIS output remains a stage-by-stage diagnostic oracle
- local NBIS parity is now pinned to `NBIS Release 5.0.0`
- exact local NBIS codestream parity is now tracked by a repository-side full contract, and the managed encoder currently matches local `NBIS Release 5.0.0` byte-for-byte across all 80 public encoder cases

That is why the repository keeps both:

- public-corpus contract tests against the bundled reference files
- NBIS-backed diagnostic tests and notes for explaining where the managed implementation diverges from a local NBIS build

## Next step

The direct `cwsq` comparison should be kept as a local diagnostic report, not promoted into a hard pass/fail repository gate. It helps separate:

- NIST-only parity targets
- NBIS-only parity targets
- genuine managed-implementation bugs
