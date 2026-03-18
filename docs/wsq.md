# WSQ Status

## Scope

`OpenNist.Wsq` is intended to provide a managed .NET API for reading and writing WSQ-compressed fingerprint imagery using stream-based input and output.

The target is standards-conformant WSQ behavior suitable for eventual FBI certification work, but the package is not at that level yet.

## Current implementation

The current codebase includes:

- a managed WSQ marker and segment parser
- parsing for transform, quantization, Huffman, frame-header, comment, and block-header structures
- managed Huffman coefficient decoding for WSQ block data
- managed unquantization and inverse wavelet reconstruction
- a stream-based `WsqCodec.DecodeAsync(...)` implementation
- fixture-backed tests against the official NIST WSQ reference codestream sets, including the non-standard filter tap-set vectors
- strict local decoder regression checks that now match raw reconstruction goldens generated from the NBIS `dwsq` reference decoder byte-for-byte across the public decoder corpus

## Not implemented yet

The following work is still outstanding:

- raw-image to WSQ encoding
- exact-reference encoder verification against the official NIST reference outputs
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

For that reason, the current repository tests use the official NIST codestream corpus together with local raw reconstruction goldens generated from the public-domain NBIS `dwsq` reference decoder. This gives the repository a concrete local decoder regression corpus while still remaining distinct from the formal NIST certification workflow.

The current repository state now achieves exact byte-for-byte parity against the local NBIS-generated raw reconstructions across the official public decoder corpus, including the published non-standard tap-set vectors.

Local comparison work established and verified the decode path in stages:

- container parsing matches the NBIS decoder path
- wavelet and quantization tree construction matches the NBIS decoder path
- Huffman coefficient decode matches the NBIS decoder path
- unquantization matches the NBIS decoder path
- inverse wavelet reconstruction and final raw pixel conversion now produce the same raw decoder output as the local NBIS-generated reconstruction corpus

## Practical note

Until encoding, broader interoperability testing, and certification prep are complete, `OpenNist.Wsq` should be treated as an in-progress implementation package rather than a production-ready FBI-certified codec.
