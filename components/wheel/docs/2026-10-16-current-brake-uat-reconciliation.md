# Current Native brake and no-drive reconciliation

Request sana202107. Source/log review only, on product
`dd8aa4f509bd3fc580304d67d82b7c808185ae2d` and documentation tip44160cab.
No new definite source defect explaining the current owner no-drive report was
identified. The existing small-brake remedy is present. Physical pedal actuation
and release remain unqualified; no product edit or new probe is proposed.

## Current identity, freshly checked

Installed Plugin SHA256:
`BF5D0BA9C441D6EB9CD8FE196F628CDBB81AAA5283B2496BFA2F6FD2D8C2A806`.
Core: `050FE8DD0D58B55DFC8C88A897E622485478DAE05443A178F50B2E568914FE77`.
CFG: `97BBB73262EBDD972708A17F27E22403B2CDFDB0D84BE48B5D68CE0FBF456F5D`.
Bindings: `04EB80F80EB0D6F98728BC6E5AFFB680688C4DCCAE3FD5940FEB65014AA2D6F0`.

Current Native/Automatic bindings retain steering0, throttle2, brake5 and
handbrake7; Brake Rest0/End65535/Deadzone0.03. Handbrake has its axis and button18.
Gear up/down are sequential; no H-pattern gates are bound. Do not restore the
obsolete EC81/zero-deadzone owner profile. Accepted force tuning is unchanged.

## What the retained evidence establishes

| Evidence | Established result and limit |
| --- | --- |
| Original EC81 R8/R9 | Physical brake raw830-927 mapped to about1.3% with DZ0. R9 positive throttle with nonzero brake retained four500 brake torques; exact-zero brake reached forward speed71. Different starting states prevent a matched causal A/B claim. |
| Native instruction review | Accelerate tests exact-zero brake and applies propulsion through AddRelativeForce. The per-tick dispatcher was not decoded; its release behavior remains an inference from instructions plus sustained rows. Motor torque0 is not absence of propulsion. |
| cf655d45 private04EB candidate |240 exact joined source/probe rows: untouched brake raw841-926 maps to wheelInput/Controls/game brake0; synthetic throttle maps to1, all four brake torques0,54.527m world displacement and positive local forward velocity. This establishes that bounded native path, not physical throttle use. |
| Candidate final return |61 physical-reader rows and no active sample after expiry. This is return from synthetic injection, not proof that a person pressed and released a pedal. |
| dd8 private F6 run02 |Existing operator brake-rest.json observes mapped brake0 at saved3%. It is a device-input snapshot in F6, not a driving sweep or known physical release. Save/Cancel evidence is retained despite the original caller-close failure. |
| dd8 private cold run03 |PASS_PRIVATE_NATIVE_COLD_PERSISTENCE_HELP, persisted04EB, normal close and exact restoration. Persistence does not need another run. |

Original candidate interval failure remains intact: the10-second first-reader
interval ended after later respawn, with stale/missing eligible samples. It does
not invalidate the directly joined pulse, and the pulse does not pass that
interval. Current exact dd8 hardware driving is not inferred from cf655d45.

## Source causes reconciled

- `AxisCalibration.Normalize` returns exact0 at or below3%; integer raw1966 is
  inside that threshold,1967 is above it, and65535 still maps to1. No extra
  hidden floor or game physics/BrakeTimer/DeadZone write exists in this remedy.
- `DeviceHub.TryAxis` requires one exact saved instance and a successful current
  read. Unavailable input retains stock controls; it does not synthesize brake1.
- Native Apply requires the selected current car, focused eligible countdown or
  race, Wheel Enabled, F6 closed, no pause/replay/respawn/photo, and all three
  valid driving-axis reads. InputLease writes throttle7/brake6 through boxed
  array indexer writeback, then restores pre-lease values after native Controls.
- Automatic H-pattern pedal preservation3986afc and first-write restoration
  f09cecc1 are ancestors of dd8. Neither trigger explains this sequential-only
  profile; no duplicate correction is needed.
- Pending-close recoveryf61da7b is also an ancestor. A pending F6 close keeps
  ownership intentionally until100ms of fresh neutral stock/key/mouse/menu
  input. Unfocused, stale or held input has an explicit blocker; Escape/F6/Back
  cancels closing. An open or pending panel suppresses driving independently of
  brake calibration. The stock release threshold is0.25, not the game's exact
  zero service-brake semantics.
- Handbrake remains a separate merged axis/button/stock action. Any positive
  composed handbrake asserts its native action; current saved handbrake DZ0 is
  not a proven fault. No new deadzone, inversion or device change is justified
  without physical/current-row evidence.

Compared with capturedcf655d45, dd8 has identical Git blobs for DeviceHub,
InputLease, MenuOwnership, Panel, Plugin, GameSampler, AxisCalibration,
HandbrakeInput, PlayerControlState and HPatternShifter. The only production
WheelInput differences are the deadzone-save dispatch and camera-default log;
Core Bindings preserves saved keys and patches only the edited deadzone. This
narrows source risk; it is not an exact-binary runtime pass.

## Remaining owner actuation, in a later attended handoff

Use the saved candidate without retuning: first establish actual F6 closed,
focus, selected unlocked RACE, no pause/respawn/replay/photo and handbrake released.
Observe feet-off brake0, then actual brake partial/full travel and release to0.
Observe actual throttle rest/full/release and forward movement. Assess whether
the saved3% feels appropriate; do not recapture endpoints merely to repeat the
diagnosis. A menu-close blocker must be resolved or reported before judging
pedals. Physical force acceptance is a separate attended question.

If further evidence is needed, the existing GameSampler already separates raw
pedals, normalized wheel input, advancing appliedTicks, Controls values, native
game values, handbrake, eligibility and brake torques. Missing stays unavailable;
sustained same-record eligible rows are required. No new observer route is needed.
The corrected Todo camera-only session must not be expanded into this actuation.

## Retained evidence paths

Hub `E:/Source/dbce-project-mgmt/`:

- `artifacts/woden-native-analysis-sana-20261016/positive-comparison-09.json`
- `docs/reviews/2026-10-16-woden-tiny-brake-diagnosis-valo.md`
- `artifacts/woden-candidate-forward-runtime-livo-20261016/candidate-witness-analysis-01.json`
- `artifacts/woden-candidate-forward-runtime-livo-20261016/CLOSEOUT.md`
- `artifacts/woden-native-input-review-dine-20261016/REVIEW.md`

This worktree retains `artifacts/native-f6-persistence/`: operator-02/phase-1/
brake-rest.json, run-03/phase-2/persist.json and original run-02/run-03 results.
The candidate trace/source hashes were rechecked against the retained analysis;
no capture was rerun or reclassified. No build, test, game, device poll, input,
provider, lease, config/save write or installation occurred during this review.
All37 toolkit ledger IDs remain represented; STD-016/017/020 are unchecked and
STD-031/037 pending. No standards or physical acceptance status is promoted.
