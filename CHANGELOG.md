# Changelog

## 0.0.1 - 2026-03-28

## Release highlights

- Initial modular package lineup for NIST, WSQ, NFIQ 2, browser interop, and shared primitives
- Consumer-focused documentation site with .NET, TypeScript, and browser guides
- Structured error codes and grouped validation results across the main .NET libraries

## Added

- Added `OpenNist.Primitives` for shared result, error, validation, exception, and documentation-link types.
- Added `OpenNist.Nist` support for ANSI/NIST-style transaction decoding, inspection, and round-tripping.
- Added `OpenNist.Wsq` support for WSQ inspection, decode, and encode workflows.
- Added `OpenNist.Nfiq` support for NFIQ 2 scoring on supported fingerprint inputs.
- Added `OpenNist.Wasm` for browser and TypeScript integration.
- Added a documentation site with getting started guides, concepts, reference pages, and troubleshooting.
- Added stable public error codes and grouped validation results for NIST, WSQ, and NFIQ 2 workflows.

## Changed

- Changed the project structure to use focused packages instead of a larger monolithic layout.
- Changed the public docs to focus on consuming the libraries from .NET and TypeScript.
- Changed the browser docs experience to include package install guidance, article navigation, and inline references.

## Deprecated

- None.

## Fixed

- Fixed NFIQ validation reporting so multiple input problems can be returned together instead of one at a time.
- Fixed docs article navigation so the right-hand table of contents and reference links scroll within the article pane.

## Security

- None.

## Breaking changes

- None documented for `0.0.1`.
