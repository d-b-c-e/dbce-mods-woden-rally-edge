# Independent captured force history

Follow-up: the first muted original now validates both recorded histories and
contains 375 eligible production Driving-reference rows, with native output zero.
The separate input trial remained incomplete because of a final native respawn;
normalization and physical acceptance remain open. See
[run01 evidence](SHIM-FORCE-CAPTURE.md#run01-useful-analysis-evidence-trial-incomplete).

The ordinary diagnostic recording previously preserved only the actual FFB
lifecycle. While physical output was muted, that lifecycle reset shaping every
tick, leaving `ffb.preview` zero even when useful tyre inputs were present.
This source change records an additional steering/damping history that survives
physical mute. It does not change force delivery, Classic/Grip arithmetic,
owner gains or saved tuning. No permanent install or physical acceptance is claimed.

## Contract and ownership

New requested ordinary captures and new stages declare
`analysisForceContract=woden.force-analysis@1`. `analysisForce` must equal
`ForceAnalysisHistory.Scope`, which explicitly excludes crash cues, output gates
and device writes. Ordinary capture and stage capture own separate instances of
the same Core helper. Stage capture copies only one numeric sample and its
channel/unavailable collections before adding its trajectory index and analysis;
it never mutates the ordinary publisher's sample or copies large artifacts.

The helper uses the production `ffb.grip.reference`, `referenceKind` and
`carEpoch` from that same tick. It never learns a second reference. Its histories
reset at capture start, car changes, effective tuning/model changes, non-driving
state, source discontinuity, duplicate/backward/nonfinite simulation time or a
gap beyond the existing 100 ms policy. Model refusals also break shaping history.
Device, focus, saved-off and diagnostic-mute gates do not independently reset
this history. A source non-driving state still does.
Compare reference acquisition only within one `runtimeSource` rule: captures before `0aca431` (including `d8df97b`) may contain reverse-learned Driving references, while exact replay still restores each recorded row.

`analysis.force.*` records preview, validity, reset count, model version, Grip
steering/damping terms, contract version 1, analysis/car/tune epochs, boundary
bits and a stable model reason. Boundary bits are 1 capture start, 2 new car,
4 tune/model, 8 simulation gap, 16 source discontinuity, 32 non-driving. Non-driving
reason is canonical 10; source discontinuity is 11; an analysis-only time gap is
18. No source state or `ffb.*`/`crash.*` field is overwritten. An analysis exception
records a `capture-failed` marker and shows Faulted, stops that recorder, and
leaves actual force lifecycle alone. The analysis reader refuses a saved prefix
with that failure marker.

## Replay and export

From `components/wheel`, without a game or device:

```powershell
# Ordinary capture: create the existing verified actual-force baseline first.
dotnet run --project tools/TelemetryInspector -c Release -- reprocess '<case>'
dotnet run --project tools/TelemetryInspector -c Release -- ffb-analysis-export '<case-or-stage>' '<new.csv>' 50 25 grip 2
```

The ordinary path verifies the existing source/config/profile, case, actual
force/crash recurrence and baseline before validating the additional history.
The stage path verifies the sealed artifacts, trajectory alignment, stable tune,
single car and zero delivery. Both replay every declared analysis field before
evaluating an optional candidate strength/load ratio on another private history.
Unknown/missing contracts, stripped metadata, missing/extra fields, changed
epochs/pre-clamp terms/preview, failed captures and malformed references refuse.

The CSV labels `stream=independent-analysis`, includes the exact history contract,
source and original force-config SHA-256, trial parameters, and
`physical_output=false`. `eligible` requires valid driving and, for Grip, the
production **Driving** reference; provisional/missing references remain excluded.
There are no crash cues in these values and no measured wheel torque claim.

Existing ordinary `reprocess`, `trial` and `ffb-export` retain actual gated-force
semantics. Old ordinary captures cannot be promoted to the new history. Legacy
stages retain their original independent recurrence and still use their existing
export. New stages dispatch their declared history. Stage review now reports
the exact `woden-grip-signal@4`, `@5` or `@6` identity instead of always `@4`.
Changing tune during a capture remains recorded but is not admitted as one
stable-config replay case; split cases as before.

## Device-free evidence

- The actual production `ForceController` linked against its existing fake
  device records 260 rows with zero opens/writes, zero actual preview and
  nonzero independent analysis using its qualified driving reference: **157
  eligible rows, peak 0.13051723**. Original
  actual/crash replay and the new analysis replay are exact on the same source.
- Boundary checks cover Classic and Grip 4/5/6, capture isolation, changing
  hardware gates, car/tune/model/state/time/refusal boundaries. Starting a cold
  stage snapshot leaves the ordinary history warm and its collections unchanged.
- New and legacy sealed-stage fixtures verify exact model labels, provenance,
  reference eligibility, tamper refusal and unchanged source hashes. Ordinary
  fixtures reject unknown/stripped contracts, missing/extra/tampered fields and
  explicit capture failure; they preserve every source/config/profile/case/
  baseline hash.
- `tools/game/Verify-Source.ps1` passed: zero build warnings/errors, Core **58
  suites / 49,727 assertions** including the immutable private model 5 source,
  UI **881** plus **67** production shim/lease checks, stage exporter **8,799**,
  triple geometry **22**, and force envelope **10,836**. Additional malformed
  reference/model/time/scope checks then passed Core **58 / 49,740**; the final
  plugin/solution build also had zero warnings/errors. Evidence is
  `artifacts/analysis-verify-source.log`, `artifacts/analysis-core-final.log` and
  `artifacts/analysis-build-final.log`. No game, native device or package was opened.

## Limits and next evidence

The private trial02 remains the original model 5 tape: 0/1,696 valid force rows.
The earlier explicit model 6 counterfactual repairs 1,151 curve rows but none
of those rows has a Driving reference. Its recorded reset history stays zero.
This change does not fabricate analysis for it or qualify normalization.
Run01 now supplies useful forward-driving/reference evidence, with its incomplete
input-trial limit above. Physical FFB coexistence and owner feel remain separate.

The owner's speedometer/other HUD jitter observation remains open. Existing
trial02 PNGs are unchanged older owner files and do not show fresh motion.
The HUD's distant world-space canvas placement is a source-only quantization
hypothesis, not an established cause. No HUD, camera, physics or triple code
changed, and no extra HUD-only game run was made.
