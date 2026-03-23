# NIST WSQ Reference Images v2.0

These test fixtures come from the official NIST WSQ certification reference dataset:

- <https://nigos.nist.gov/wsq/reference_images_v2.0_raw.tar>
- <https://www.nist.gov/programs-projects/wsq-certification-procedure>
- <https://fbibiospecs.fbi.gov/certifications-1/wsq>

Contents in this test project:

- `Encode/Raw`: the 40 official headerless RAW encoder inputs
- `ReferenceWsq/BitRate075`: the 40 NIST WSQ reference files at bit allocation target 0.75
- `ReferenceWsq/BitRate225`: the 40 NIST WSQ reference files at bit allocation target 2.25
- `ReferenceWsq/NonStandardFilterTapSets`: the 6 official WSQ decoder vectors compressed with non-7/9 filter tap sets
- `ReferenceReconstruction/Raw/BitRate075`: local exact decoder goldens generated from NBIS `dwsq` for the 0.75 corpus
- `ReferenceReconstruction/Raw/BitRate225`: local exact decoder goldens generated from NBIS `dwsq` for the 2.25 corpus
- `ReferenceReconstruction/Raw/NonStandardFilterTapSets`: local exact decoder goldens generated from NBIS `dwsq` for the non-standard tap-set corpus
- `raw-image-dimensions.json`: the RAW image dimensions published on the NIST certification page

Important note:

The exact byte-for-byte WSQ comparison tests in this repository are an intentionally strict local contract. The NIST/FBI certification procedure compares encoder output using the reference file size, frame header parameters, and quantized wavelet coefficient bin index values rather than plain whole-file equality alone.

The decoder reconstruction goldens in `ReferenceReconstruction/` were generated locally from the public-domain NBIS reference decoder (`dwsq`) in NBIS Release 5.0.0. They are used for local byte-for-byte decoder regression tests and do not replace the official NIST certification workflow.
