# WSQ Reference Reconstructions

These files are local decoder goldens generated from the public-domain NBIS reference decoder, `dwsq`, from NBIS Release 5.0.0.

Layout:

- `Raw/BitRate075`: exact reconstructed rasters and `.ncm` sidecars for the official NIST 0.75 decoder corpus
- `Raw/BitRate225`: exact reconstructed rasters and `.ncm` sidecars for the official NIST 2.25 decoder corpus
- `Raw/NonStandardFilterTapSets`: exact reconstructed rasters and `.ncm` sidecars for the official non-standard tap-set decoder corpus

Each `.raw` file was generated with:

```text
dwsq raw <input.wsq> -raw_out
```

Each `.ncm` sidecar comes from the same NBIS run and is used by the tests to validate width, height, and pixel depth alongside the exact raw payload.

These artifacts are intended for local byte-for-byte regression testing of the managed decoder. The formal FBI/NIST decoder certification process still relies on NIST's official analysis workflow.
