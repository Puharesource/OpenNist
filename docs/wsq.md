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
- fixture-backed tests against the official NIST WSQ reference codestream sets, including the non-standard filter tap-set vectors
- strict local decoder regression checks that now match raw reconstruction goldens generated from the NBIS `dwsq` reference decoder byte-for-byte across the public decoder corpus
- integration tests that verify the managed encoder produces parseable WSQ codestreams whose quantized coefficient bins survive a full write/read cycle unchanged
- encoder-analysis tests that now satisfy the published NIST quantization-bin and coefficient-bin tolerance thresholds across the public encoder corpus
- emitted-codestream tests that now satisfy the published NIST file-size and frame-header checks across the public encoder corpus when encoded with the reference software implementation number used by the included corpus

## Not implemented yet

The following work is still outstanding:

- exact-reference encoder coefficient parity against the official NIST encoder corpus
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
- the repository also now contains an exact encoder coefficient-parity gate against the NIST reference codestream corpus, but it remains explicitly skipped until the managed forward transform matches the published reference bins exactly
- the managed encoder now satisfies the published public-corpus checks for file size, frame-header parameters, quantization bin widths, and coefficient-bin tolerances when encoded with the same software implementation number as the included reference corpus
- exact byte-for-byte reference-codestream parity is stricter than the published encoder thresholds and remains a separate skipped gate in the repository
- local comparison work now shows the managed forward transform matches the local NBIS encoder decomposition and quantized-coefficient analysis path on the current ARM64 development environment, but the stricter published NIST reference-bin parity gate is still pending
- the encoder internals are now split so WSQ value scaling, variance calculation, coefficient quantization, and quantization-bin synthesis can be investigated independently, and the remaining public-reference gap is currently isolated to quantization-bin synthesis and scaled-table behavior rather than the forward transform

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
