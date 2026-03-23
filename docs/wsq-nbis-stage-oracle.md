# WSQ NBIS Stage Oracle

## Purpose

This note describes the local NBIS-oracle approach that was used to debug the WSQ encoder blocker cases without conflating that work with the public NIST acceptance corpus.

The repository keeps the published NIST encoder corpus as the primary correctness target for exact public-corpus parity and certification-oriented checks. The local NBIS C code is used here as a stage-by-stage diagnostic oracle to answer a narrower question:

- At which internal encoder stage does the managed implementation first diverge from the NBIS reference behavior?

## Current scope

The oracle coverage started narrow and was eventually split in two layers.

The deep blocker-oriented layer still targets the original three `2.25 bpp` blocker cases:

- `a070.raw`
- `cmp00003.raw`
- `cmp00005.raw`

These remain useful because they still carry the most detailed source/bin follow-on diagnostics.

On top of that, the repository also tracked the then-current local-NBIS mismatch queue directly. That broader layer let the repo assert the exact first mismatch coordinate and local bin state for the remaining NBIS work queue instead of relying only on the older focused blocker subset.

## Stage breakdown

The NBIS-oracle tests currently compare these stages:

1. Normalized pixels
2. Final wavelet decomposition buffer
3. Subband variances
4. Quantization bins
5. Quantized coefficients

The tests use the local NBIS 5.0.0 C helpers in `tools/wsq-diag/`:

- `nbis_wavelet_dump`
- `nbis_dump`

Those helpers come from the NBIS 5.0.0 reference implementation and are treated as a local diagnostic oracle only. The repo now includes a rebuild helper at `tools/wsq-diag/build-nbis-5-oracles.sh` so the native oracle binaries can be refreshed against the same local NBIS 5.0.0 install.

The tracked diagnostic layer now also includes a managed quantization-trace view of the high-rate `qbin` synthesis loop. That trace is used to compare:

- the current managed high-rate variances
- a hybrid where only subband `0` uses the NBIS variance
- the full NBIS variance set
- the actual production high-rate `qbin` path, not just the older double-trace baseline
- narrower precision variants inside the high-rate quantization-scale loop, including float scale and float product experiments

The float-side high-rate variance oracle now also accumulates in `float`, not `double`, so that it stays structurally closer to NBIS and BiomSharp when comparing the early-region variance path.

## Why this exists

The remaining encoder issues are now small `+/-1` coefficient misses. Broad end-to-end parity tests are still useful, but they do not tell us where the first meaningful drift begins.

The stage-oracle tests narrow that down by checking whether drift begins in:

- normalization
- wavelet decomposition
- variance computation
- quantization-bin synthesis
- final coefficient quantization

That lets the repo record the current failure boundary explicitly instead of relying on repeated whole-pipeline experiments.

The newer current-mismatch layer extended that same idea from the original blocker trio to the whole remaining NBIS queue. Each non-exact case was pinned to:

- its first mismatch coefficient index
- the exact subband/row/column at that index
- the local managed-vs-NBIS quantized coefficient pair
- the local `qbin` and `halfZeroBin` deltas at the mismatch subband

## Outcome

That diagnostic path is no longer the active blocking queue. The managed encoder now matches local `NBIS Release 5.0.0` codestream output byte-for-byte across all 80 public encoder cases.

The tests and notes below remain useful because they capture the failure boundaries that had to be closed to reach that point, and they still provide a stage-by-stage local debugging workflow if the NBIS parity gate regresses in the future.

## Historical interpretation

With the current managed encoder path:

- normalization matches the local NBIS output for the focused blocker cases
- the first managed-vs-NBIS divergence already appears in the wavelet decomposition buffer
- the three remaining public-corpus blockers do not form a single bug class:
  - `cmp00003.raw` is currently a public NIST-only blocker at the failing coefficient
  - `cmp00005.raw` and `a070.raw` still align with NBIS at the failing coefficient and therefore remain NBIS-aligned blocker cases
- all three blocker coordinates are now tracked as narrow quantization-boundary misses:
  - the current managed pre-cast value sits just inside the wrong bucket
  - `cmp00005.raw` and `a070.raw` cross into the expected bucket under NBIS at that same coordinate
  - `cmp00003.raw` does not, which is another sign that it should not be debugged as an NBIS-aligned blocker
- the shared `cmp00005.raw` / `a070.raw` blocker class is now narrowed further:
  - the local miss is driven by `qbin`, not by `halfZeroBin`
  - substituting NBIS `halfZeroBin` alone leaves the blocker coordinate unchanged
  - substituting NBIS `qbin` moves the first whole-file mismatch elsewhere, which means the next fix should target `qbin` synthesis rather than broad zero-bin changes
  - replacing only the subband-0 variance with the NBIS value moves `qbin[0]` in the right direction for the blocker cases, but does not fully explain the shared `cmp00005.raw` / `a070.raw` class by itself
  - for that shared class, the active-set shape of the high-rate quantization loop stays the same when only subband `0` uses the NBIS variance, which means the remaining miss is not explained by a different positive-bit-rate elimination branch
  - replacing the NBIS variances for subbands `0-3` moves `qbin[0]` at least as close to the NBIS value as replacing subband `0` alone, which means the remaining shared blocker class depends on early-region variance coupling, not just the blocker subband in isolation
- the blocker diagnostics are now specific enough to compare local source/bin combinations at the exact failing coordinate rather than relying only on whole-file mismatch indices
- the broader current-mismatch diagnostics now also prove that the remaining NBIS queue is still dominated by tiny local bucket misses rather than large structural divergence
- the blocker diagnostics are now also specific enough to record where a local blocker fix fails next:
  - for `cmp00005.raw`, a tiny local downward `qbin[0]` bias fixes the first blocker coordinate but the next whole-file mismatch moves to subband `4`, row `0`, column `51`
  - for `a070.raw`, that same local bias fixes the first blocker coordinate but the next whole-file mismatch stays in subband `0`, at row `3`, column `21`
  - for `cmp00003.raw`, that same local bias is not the right class of fix at all; it jumps to a later mismatch in subband `5`, row `30`, column `24`
- the blocker diagnostics now also record the first whole-file mismatch location for the main source/bin variants:
  - source-only substitutions keep all three blocker files on the same first mismatch coordinate
  - NBIS-bin substitution fixes the local blocker coordinate for `cmp00005.raw` and `a070.raw`, but immediately moves the first whole-file failure elsewhere
  - reference-bin substitution collapses `cmp00003.raw` into the same deeper subband-0 blocker class as `a070.raw`
- the tiny uniform subband-0 `qbin` bias behaves like a weaker version of NBIS-bin substitution for the shared `cmp00005.raw` / `a070.raw` blocker class:
  - it fixes the local blocker coefficient
  - it then moves to the same next whole-file mismatch location as the NBIS-bin substitution for those two files
  - it does not do that for `cmp00003.raw`
- the tracked diagnostics now distinguish two different second-stage follow-on classes after the local `qbin[0]` blocker is removed:
  - `cmp00005.raw` leaves subband `0` entirely and jumps to subband `4`; at that follow-on point the managed coefficient still matches NBIS, while the local `qbin` remains slightly above both NBIS and NIST
  - `a070.raw` stays in subband `0` and moves deeper within that same region; at that follow-on point the float source already matches NBIS exactly, while the local `qbin` sits between NBIS and NIST
  - `cmp00003.raw` stays separate again by jumping to subband `5`
- the tracked diagnostics now also isolate the remaining region-2 `qbin` drift for `cmp00005.raw` more directly:
  - the follow-on mismatch is in subband `4`, not the original subband-0 blocker location
  - existing high-rate precision variants such as float `sigma`/initial-bin handling and float product handling do move that region-2 `qbin` toward NBIS and NIST
  - but they only move it slightly, and not enough to close the follow-on bucket by themselves
  - directly splicing the region-2 variance input from the float or NBIS-style single-precision accumulation path is not the right fix either; at the follow-on point it makes `qbin[4]` slightly worse, not better
  - replacing only the target subband-4 variance with the NBIS value also is not enough; replacing all region-2 variances helps at least as much, which means the remaining drift is broader region-2 coupling rather than a local subband-4 variance defect
  - the improvement from the full region-2 NBIS splice comes more from shared scale coupling than from subband-4 initial-bin changes alone
  - no single known precision toggle, including a more literal single-precision log/initial-bin path, crosses the local follow-on `qbin[4]` threshold by itself
  - at least one non-target region-2 subband can individually move the follow-on `qbin[4]` toward NBIS, but no single region-2 variance splice closes the gap exactly; the remaining overshoot is distributed across the region rather than dominated by one subband
- subband-0 serialized-bin substitution is unsafe as a broad fix:
  - it now regresses earlier on all three remaining public-corpus blocker files (`cmp00003.raw`, `cmp00005.raw`, and `a070.raw`)
  - it is also still unsafe for a curated exact `2.25 bpp` contrast set such as `cmp00016.raw` and `sample_01.raw`
- NBIS-style float product precision plus scale casting is also unsafe as a broad fix:
  - it can move the shared blocker class in the right direction
  - but it regresses recovered exact `2.25 bpp` guard cases such as `cmp00011.raw` and `cmp00017.raw`

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

The current stage-oracle tests depend on the local NBIS 5.0.0 helper binaries under `tools/wsq-diag/`.

That is deliberate:

- they are meant to speed up local debugging
- they are not the public acceptance contract
- if the local helpers are unavailable, the diagnostic tests simply return without asserting

If this oracle layer later becomes part of broader routine verification, the helpers or their stage outputs should be moved into a more explicit, reproducible fixture path.
