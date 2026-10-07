# Grip review fixes and stage/export integration

Claude's `8bc3444` corrects the three defects reproduced in the
[82f2812 review](2026-10-07-grip-v4-review.md). A frozen `16f0f9c` snapshot with
the same eight independent source/fake-device cases now passes **59 suites /
37,973 assertions**. Stopped damping produces zero; rejected negative wheel
loads leave reference None/0; negative curve magnitudes reject and zero the
fake sink. The retained last nonzero fake write is historical, not delivery on
the rejected tick. Muted analysis still learns the reference with zero fake
opens/writes; focus recovery, car reset, event-time crash strength and legacy
Classic settings preservation pass. The actual controller's complete synthetic
160-row Grip capture now succeeds through `RecordedForceReplay.Reprocess`.

That Core success did not exercise the full stage tooling. `StageCaptureReview`
still required force-config v1 / Classic3; `ffb-export` invoked it first, so a
new Grip stage could never reach the new Grip exporter. This correction adds
v2/Grip4 dispatch, restores each recorded production reference instead of
relearning the missing pre-capture history, and validates model/tune/car/curve
identity and analysis reset/output. Grip's independent analysis begins with
the same `NewCar` reset as the producer. Classic captures without the newer
analysis-model field remain supported.

The exporter now rejects incomplete/fractional reference identities, separates
missing from provisional reference eligibility, and rechecks source seals before
creating its new CSV. Stage metadata names the selected analysis model and its
steering-only scope. Reports use the tape's capture provenance, so a synthetic
fixture is never described as an original live drive. No force math, owner
configuration, native pin, package or installed payload changed in this fix.

## Verification

- Full `tools/game/Verify-Source.ps1`: **51 suites / 37,957 assertions**, UI669,
  new stage analysis/export3267, triple geometry22, force-envelope10836. Builds
  have zero warnings/errors.
- New complete synthetic stages use the actual shared session/trajectory writers
  and seals. Classic and Grip pass; a reference learned before the tape, changed
  reference values, known-zero/provisional/qualified states, a discontinuity,
  mirrored slip and a live-curve change replay correctly. Fifteen altered-source
  cases are refused by both validation and export. Exclusive output creation and
  source-byte preservation pass.
- Actual Inspector CLI on the Grip fixture:160 rows,159 valid,139 eligible,
  zero model error, `source=synthetic-test`; explicit Grip trial reproduces the
  float requests/reset epochs. This is software-only fixture evidence.
- Original sealed Kenya Classic reference:3601 rows,3553 valid,3477 nonzero,
  maximum replay error0. Six corrupted-copy refusals pass, original hashes are
  unchanged, and the actual Classic Strength50/Peak25 export succeeds.

Private evidence is retained under `%LOCALAPPDATA%/Dbce/StagePlayback/SessionEvidence/`:
`woden-grip-followup-16f0f9c-20261007` contains the frozen force-review source,
`review-final.log` and final synthetic controller capture;
`woden-grip-stage-20261007` contains the stage fixtures, full source log, actual
CLI reports and Kenya corruption results. No owner recording is committed.

Run the new regression independently:

```powershell
dotnet run --project components/wheel/tests/TelemetryInspector.Tests -c Release
```

This closes the source/fake-sink defects and stage-model dispatch gap. A new
muted original live-physics Grip capture remains necessary, followed by the
owner's attended comparison of sign, release, impact and Strength50 feel.
Do not use forces sampled during kinematic playback as that new baseline.
Crash reconstruction/native delivery equivalence is a separate qualification;
the stage steering report does not certify it.
