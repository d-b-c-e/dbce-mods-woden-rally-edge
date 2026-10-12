# Grip 6: native coincident friction knots

Source and device-free qualification only. Nothing was packaged, installed or
run in the game for this change. Classic, saved gains/tunes, the shared shim pin,
field extraction and physical-output gates are unchanged.

## Observed defect

The original schema 5 car trial02 remains **0/1,696 valid force evaluations**.
All 1,151 `friction curve unavailable` rows contain complete finite front-wheel
friction data: extremum/asymptote slip `2/2`, values `1/1`, stiffness `1`, on
both fronts. `GripSignal` 5 rejected them solely because it required
`asymptoteSlip > extremumSlip`. The other 230 reverse and 315 discontinuity
refusals are independent and remain refused.

Private source:
`results/shim-car-trial-02/after-d081a427509c4cd0996c2df23aeca218/tree/recording/source.jsonl`,
SHA-256 `932e1d4dbd9da5da043963e1c5c3c9668eba85d0e1da7faa6efbdb22ea43ce4e`.
The read-only native audit is under `artifacts/shim-friction-native-audit/`.
The supported GameAssembly pin is
`F422894D8D2B0DF4EDB7E5259E5E60CB8C4F8DEA2E85EBDFC09DD6766349250C`.
Source and installed `WheelFrictionCurve` interop layouts agree: five floats at
offsets 0/4/8/12/16; the native getter returns those 20 bytes. The native car grip
helper reads the curve and changes stiffness. This supports the existing field
extraction; no replacement getter or inferred default curve was introduced.
Proprietary/generated audit files and captures remain private and ignored.

## Additive contract

| Model | Force config | Admission and arithmetic |
|---|---|---|
| Classic 3 | `woden.force-config@1` | Unchanged Classic; its diagnostic Grip shadow retains strict knots |
| Grip 4 | `woden.force-config@2` | Strictly ordered knots; Strength also scales damping |
| Grip 5 | `woden.force-config@3` | Strictly ordered knots; independent damping |
| Grip 6 | `woden.force-config@4` | Grip 5 plus exactly equal positive slip knots with exactly equal finite nonnegative values |

Model 6 is `woden-grip-signal@6`. The original equation and
`unity-friction-two-piece-flat-tangent-estimate@1` stay unchanged: the first
piece rises to the shared knot/value, then the flat asymptote applies. The
zero-width second piece is never evaluated. There is no epsilon admission,
curve sorting, invented knot separation, default data, gain or tune adjustment.
Zero curve values and zero stiffness remain valid zeros. Unequal values at
coincident knots, reversed/nonpositive knots, negative values/stiffness, missing
fields and nonfinite fields are refused.

New Grip evaluations select version 6. Recorded models 4/5 retain their exact
identity and behavior in ordinary and sealed-stage replay/export. Model 5's
required steering/damping terms remain required alongside model 6's; old records
are never relabelled. Config/model mismatches fail before an observation is
written. Existing owner configurations which select Classic still select
Classic, and saved numeric tuning is preserved.

## Offline evidence

- Core/production-controller suite: **56 suites, 48,612 assertions**, including
  the optional exact private capture check. Models 4/5 keep the recorded flat
  curve rejection; production controller model 6 records valid nonzero synthetic
  previews through a fake device, and all versions reprocess exactly.
- The real trial02 model 5 source reprocesses all **1,696 model rows** unchanged.
  A separate model 6/config 4 trial accepts exactly **1,151** of those rows;
  **230 reverse / 315 discontinuity** refusals remain. Original source/config/
  profile plus generated baseline/case hashes are unchanged by the trial.
- Recorded mute/reset order keeps every counterfactual preview **zero**. This
  is an admission result, not a useful force envelope, normalization result,
  delivered torque, or owner acceptance. No continuous shaping history is
  substituted for the ordinary muted capture.
- Sealed-stage reader/exporter: **4,247 checks**, including models 5/6, model
  retention and mandatory pre-clamp-term refusal for both versions.
- Actual panel/settings fixture: **881 assertions** and **67 production shim/
  lease checks**. Plugin Release build: **zero warnings/errors**.
- Existing force-envelope fixture: **10,836 checks**, nine signed pulse pairs.
- `tools/game/Verify-Source.ps1` was attempted but stopped in dependency preflight:
  this isolated worktree lacks the cached
  `components/wheel/lib/loader/bepinex-788/BepInEx/core/0Harmony.dll` mirror.
  The targeted commands above pass against the existing pinned source references;
  the aggregate verifier is **not** claimed to pass. No dependency or cache was
  changed to bypass that check.

From the worktree root (no devices or game):

```powershell
$env:WODEN_GRIP_CAPTURE = 'E:/Source/games/.worktrees/woden-shim-input/results/shim-car-trial-02/after-d081a427509c4cd0996c2df23aeca218/tree/recording'
dotnet run --project components/wheel/tests/WodenRallyEdge.Tests -c Release
Remove-Item Env:WODEN_GRIP_CAPTURE
dotnet run --project components/wheel/tests/TelemetryInspector.Tests -c Release
dotnet run --project components/wheel/tests/WodenRallyEdge.UiTests -c Release
dotnet build components/wheel/src/WodenRallyEdge.Plugin -c Release --no-restore -warnaserror
```

Without `WODEN_GRIP_CAPTURE`, the portable Core suite runs the same synthetic
producer/refusal checks and skips the private original. Logs:
`artifacts/grip6-{core-tests,stage-tests,ui-tests,plugin-build}.log`.
Additional logs: `artifacts/grip6-{envelope-tests,verify-source}.log`.

## Separate owner observation: HUD jitter

During trial01 the owner saw the speedometer and other UI shake or jitter.
That remains open. Neither this curve-admission defect nor the control verdict
explains the rendering observation. No timing, camera, physics, HUD or triple
code changed, and this offline work provides no rendered/performance evidence.
See [the retained observation](SHIM-CAR-TRIAL.md#separate-owner-observation-hud-jitter).
