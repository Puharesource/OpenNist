# WSQ Status

## Scope

`OpenNist.Wsq` is intended to provide a managed .NET API for reading and writing WSQ-compressed fingerprint imagery using stream-based input and output.

The target is standards-conformant WSQ behavior suitable for eventual FBI certification work. The current repository now passes the published public-corpus encoder and decoder checks locally, but it has not gone through the formal FBI/NIST certification process.

## Current implementation

The current codebase includes:

- a managed WSQ marker and segment parser
- parsing for transform, quantization, Huffman, frame-header, comment, and block-header structures
- managed Huffman coefficient decoding for WSQ block data
- managed unquantization and inverse wavelet reconstruction
- a stream-based `WsqCodec.DecodeAsync(...)` implementation
- a managed encode path that reads raw grayscale input, normalizes pixels, performs forward WSQ decomposition, computes subband variances, quantizes coefficients, builds Huffman tables, and emits WSQ markers, tables, comments, and compressed blocks
- a forward WSQ decomposition path that now uses NBIS-aligned fused multiply-add accumulation in the encoder hot loop to match the local NBIS analysis path on the current ARM64 development environment
- NBIS-aligned scaling rules in the managed WSQ container writer for transform, quantization, and frame-header numeric serialization
- a more literal NBIS-style higher-rate encoder-analysis path for `2.0 bpp` and above that uses float normalization and forward decomposition together with an NBIS-style quantizer
- encoder analysis now normalizes quantization-table values to the same WSQ-scaled precision that is serialized into the codestream
- fixture-backed tests against the official NIST WSQ reference codestream sets, including the non-standard filter tap-set vectors
- strict local decoder regression checks that now match raw reconstruction goldens generated from the NBIS `dwsq` reference decoder byte-for-byte across the public decoder corpus
- integration tests that verify the managed encoder produces parseable WSQ codestreams whose quantized coefficient bins survive a full write/read cycle unchanged
- encoder-analysis tests that now satisfy the published NIST quantization-bin and coefficient-bin tolerance thresholds across the public encoder corpus
- emitted-codestream tests that now satisfy the published NIST file-size and frame-header checks across the public encoder corpus when encoded with the reference software implementation number used by the included corpus
- exact local `NBIS Release 5.0.0` codestream parity across all 80 public encoder cases

## Not implemented yet

The following work is still outstanding:

- exact-reference encoder codestream verification against the official NIST reference outputs
- broader interoperability and certification preparation work

## Reference material

The implementation work is being aligned with the official FBI/NIST WSQ material and NIST's public-domain NBIS reference source:

- [FBI WSQ certification overview](https://fbibiospecs.fbi.gov/certifications-1/wsq)
- [NIST WSQ certification procedure](https://www.nist.gov/programs-projects/wsq-certification-procedure)
- [NIST NBIS public-domain WSQ source distribution](https://www.nist.gov/services-resources/software/nist-biometric-image-software-nbis)

## Verification note

The local test strategy distinguishes between encoder and decoder verification:

- encoder verification can compare generated `.wsq` output against the official NIST reference codestreams for the published encoder corpus
- decoder verification cannot compare decoded output byte-for-byte with the original encoder RAW inputs, because WSQ is lossy
- the FBI/NIST decoder procedure compares a decoder under test against NIST's reference reconstruction output, not against the original RAW source image
- the published encoder procedure compares file size, frame-header parameters, quantization bin widths, and quantized coefficient bin indices rather than plain whole-file equality alone
- the managed encoder now satisfies the published public-corpus checks for file size, frame-header parameters, quantization bin widths, and coefficient-bin tolerances when encoded with the same software implementation number as the included reference corpus
- direct local `NBIS Release 5.0.0 cwsq` runs on `Darwin arm64` produce `0 / 80` exact byte-for-byte matches and `0 / 80` same-size matches against the bundled public reference `.wsq` corpus, so the bundled corpus is not just a raw copy of local NBIS encoder output; the details are recorded in [`docs/wsq-nbis-corpus-comparison.md`](wsq-nbis-corpus-comparison.md)
- those same direct runs also show that local `NBIS Release 5.0.0` output is deterministic on repeated local encodes
- because of that split, the repository now treats exact local `NBIS Release 5.0.0` codestream parity as the primary internal exactness target for the managed encoder, while keeping the published NIST threshold checks as the certification-oriented acceptance floor
- the managed encoder now matches the local `NBIS Release 5.0.0` codestream byte-for-byte across all 80 public encoder cases
- the repository now measures exact local NBIS parity independently from the bundled public NIST corpus instead of treating both outputs as one shared exact target
- because local `NBIS Release 5.0.0` still does not reproduce the bundled public reference corpus byte-for-byte, the repository treats the published NIST reference corpus as the primary certification-aligned parity target and the local NBIS build as a diagnostic reference rather than a second hard truth source
- the encoder internals are now split so WSQ value scaling, variance calculation, coefficient quantization, and quantization-bin synthesis can be investigated independently when working toward any future bundled-NIST parity effort
- the repository also now contains an NBIS-backed exact codestream contract across all 80 public encoder cases, with the full `80 / 80` exact set protected by the test suite
- the older NBIS stage-oracle and mismatch diagnostics remain in the repository as historical debugging aids for the path that led to that parity point

For that reason, the current repository tests use the official NIST codestream corpus together with local raw reconstruction goldens generated from the public-domain NBIS `dwsq` reference decoder. This gives the repository a concrete local decoder regression corpus while still remaining distinct from the formal NIST certification workflow.

The current repository state now achieves exact byte-for-byte parity against the local NBIS-generated raw reconstructions across the official public decoder corpus, including the published non-standard tap-set vectors.

Local comparison work established and verified the decode path in stages:

- container parsing matches the NBIS decoder path
- wavelet and quantization tree construction matches the NBIS decoder path
- Huffman coefficient decode matches the NBIS decoder path
- unquantization matches the NBIS decoder path
- inverse wavelet reconstruction and final raw pixel conversion now produce the same raw decoder output as the local NBIS-generated reconstruction corpus

## Practical note

Until the remaining exact-codestream parity work, broader interoperability testing, and formal certification steps are complete, `OpenNist.Wsq` should still be treated as an in-progress implementation package rather than a production-ready FBI-certified codec.
