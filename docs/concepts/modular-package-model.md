# Concept: Modular Package Model

OpenNist is intentionally split into focused packages instead of one large assembly.

## Why it is modular

The project handles several distinct concerns:

- ANSI/NIST transaction parsing and encoding
- WSQ image compression and expansion
- NFIQ 2 quality scoring
- browser interop

Those concerns do not need to ship as one dependency bundle. Keeping them separate makes it easier to:

- reference only the format or workflow you need
- benchmark and test each area independently
- evolve browser interop separately from core .NET libraries
- keep exactness-sensitive code isolated

## Current package set

- `OpenNist.Primitives`
- `OpenNist.Nist`
- `OpenNist.Wsq`
- `OpenNist.Nfiq`
- `OpenNist.Wasm`

The repository previously carried broader scaffolding packages, but the current direction is to keep the solution smaller and more explicit.

## Dependency direction

The main library packages are peers, not a deep inheritance stack. That keeps individual format and quality packages easier to consume and reason about.

In practice:

- `OpenNist.Primitives` holds shared low-level result, error, validation, exception, and documentation-link building blocks
- `OpenNist.Nist` focuses on transaction structure
- `OpenNist.Wsq` focuses on WSQ
- `OpenNist.Nfiq` focuses on scoring
- `OpenNist.Wasm` exposes browser-facing interop over those capabilities

## What this means for consumers

If you only need one workflow, you should be able to depend on one package. Most consumers should start with `OpenNist.Nist`, `OpenNist.Wsq`, `OpenNist.Nfiq`, or `OpenNist.Wasm` rather than depending on `OpenNist.Primitives` directly. If you need a browser experience, use the WebAssembly interop layer rather than rebuilding the bridge yourself.
