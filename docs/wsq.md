# WSQ Status

## Scope

`OpenNist.Wsq` is intended to provide a managed .NET API for reading and writing WSQ-compressed fingerprint imagery using stream-based input and output.

The target is standards-conformant WSQ behavior suitable for eventual FBI certification work, but the package is not at that level yet.

## Current implementation

The current codebase includes:

- a managed WSQ marker and segment parser
- parsing for transform, quantization, Huffman, frame-header, comment, and block-header structures
- fixture-backed parsing tests against the official NIST WSQ reference images
- a concrete `WsqCodec` entry point, with full pixel encode/decode still pending

## Not implemented yet

The following work is still outstanding:

- Huffman-coded coefficient decoding
- wavelet reconstruction for WSQ decoding
- raw-image to WSQ encoding
- exact-reference encoder verification against the official NIST reference outputs
- broader interoperability and certification preparation work

## Reference material

The implementation work is being aligned with the official FBI/NIST WSQ material and NIST's public-domain NBIS reference source:

- FBI WSQ certification overview
- NIST WSQ certification procedure
- NIST NBIS public-domain WSQ source distribution

## Practical note

Until encode/decode is complete, `OpenNist.Wsq` should be treated as an in-progress implementation package rather than a production-ready codec.
