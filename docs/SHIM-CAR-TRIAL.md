# Bounded shared-Core original car trial

The second private run on October 12 **passed** all six raw shared-Core actions
through the native car, including Manual gear +1/-1, all 23 native plateaus and
final neutral/Core completion. The earlier incomplete run remains documented
below. Both runs restored owner state exactly. Physical controls, force quality,
pressure and HUD jitter remain unqualified; no permanent install occurred. Schema 5
`shim-car-trial` / `-ShimCarTrial` is a separate original input qualification.
The completed schema 4 [menu trial](SHIM-MENU-TRIAL.md) retains its menu-only scope.
The sealed Kenya reference stays immutable and is not replayed or replaced.

Existing `StageStartup` validates Kenya SS1 / car 8 / first Arcade round and uses
the native DailyMessage, Title, Map and `StagePresentation.ProceedToStage` path.
Only this explicit diagnostic mode sets the transient player-zero Arcade
transmission choice to Manual; actual `Aids.Automatic=false` and player zero are
required. It makes no trajectory/body/pose writes, physics suppression, camera
replay, background override, OS input, focus change or display override. The
actual native countdown must complete before delivery.

The accepted Controls bytes are copied exactly. Before every batch the addon
checks the independent run07 profile/workload and Controls hashes. Their revision
is `439A4118…130D36C`; brake rest is **1743**, not zero. Shared centred-axis
normalization uses the full range: steering raw neutral is 32768, independently
of stored rest 33265. The exact R12 and DS-8X instance/product/HID path/epoch must
be live and stable. DS-8X product in the accepted bytes is
`05310483-0000-0000-0000-504944564944`. Names cannot substitute for identity.

Only six driving actions are supported. Each 700 ms raw batch includes all four
axes and both shift buttons. Neutral precedes and separates one DS-8X up/down
press; handbrake 0.5/1; steering -0.5/+0.5; then throttle and brake 0.5/1. A brake/neutral
pair then `end` completes 23 native plateaus / at most 24 batches. No menu action
can follow. Each batch requires native Manual, not-changing, racing/unlocked
state and identity. The trial requires sustained native Controls/MainCar values,
native gear +1/-1 and the production shift query, Core injected/neutral provenance
and Core Completed. Desired values or accepted batches cannot pass this check.

Focus loss, owner keyboard/mouse activity, pause, changed car/profile/device
epoch, pad coexistence, unavailable native observations, recorder drops or output
activity stop the trial. Ordinary native keyboard pass-through is unchanged.
The existing no-force and progression/network guards remain latched. No force
model, saved tune, triple-screen code or native DLL pin changes.

Only schema 5 permits the exact nonce/case/expiry-bound `DiagnosticLaunch` with
`DisableForces=true`. It retains ordinary `Runtime.Force.Tick` and `TelemetryOutput`
recording of actual `ffb.*` validity/gates/model output; stage replay/recording
remains inactive. No substitute analysis signal is generated. The original
recorder must complete with zero drops. Native wheel motor/brake torque channels
and their missing list are retained. Native pressure is unavailable; proportional
handbrake torque and physical force remain unqualified by a boolean handbrake
response. Inspect the actual recorded producer after the run.

The supervisor adds only fresh `BepInEx/WodenRecordings/request-<nonce>` to its
existing config/save/plugin/report and raw-registry restore scopes. Existing
recordings remain outside that scope. The consumed request is covered by the
config tree. After normal exit, the new original source/force-config/capture-profile
files are archived to `after-*/tree/recording` before restoration. A surviving
process or incomplete restoration retains the lease. No rank/finish/replay,
exclusive force-reader coexistence or physical acceptance claim follows.

## Offline validation

`Test-CoreCar.ps1` reads the accepted profile and immutable run07 sources, compiles
the exact provisional pure Core pin with fake providers, and exports 2,640 frames.
It checks 5,166 raw Live/Injected values including brake rest and DS-8X buttons.
Source-linked production ShimInput/WheelInput/InputLease consume those frames:
six actions, one sequential edge each, native table restoration and terminal
neutral. Native engine behavior is a fixture, not runtime proof. Addon fixtures
cover countdown, native Manual/gear/query checks, source/epoch/focus/mute failures,
recorder identity/completion, and restoration of only the nonce recording folder.

```powershell
pwsh -NoProfile -File tools/controls/Test-CoreCar.ps1 -Profile 'D:/Program Files (x86)/Steam/steamapps/common/DRIVE Rally/BepInEx/config/dbce-controls.ini'
pwsh -NoProfile -File tools/controls/Test-CoreMenu.ps1 -Profile 'D:/Program Files (x86)/Steam/steamapps/common/DRIVE Rally/BepInEx/config/dbce-controls.ini'
pwsh -NoProfile -File tools/controls/Test-Recovery.ps1
dotnet run --project tools/controls/VerifyHooks -c Release -- .
```

## First private run — 2026-10-12

Reviewed runtime source `ecc6a61ac2c8bfed3c9b85feea1eeb0674553dce`, frozen in
`artifacts/shim-car-trial-candidate.json`, ran as PID 67224, created
`2026-10-12T11:07:20.8125086Z`. The run nonce was
`9952d38208b6471e83a85ed7437732dd`; evidence is private under
`results/shim-car-trial-01`. Native startup reached Kenya SS1 / car 8, player zero,
Manual, completed countdown and stable car instance 390318. No pose writes or
reference replay were used. Both exact R12 and DS-8X devices reported live/open
at epoch 1 with stable instance/product/HID identities.

The actual native Controls/MainCar consumer passed 17 bounded plateaus: initial
neutral; steering -0.5/+0.5; throttle, brake and handbrake 0.5/1; and neutral
between each. Each accepted plateau retained 21–25 native samples. Handbrake
readback proves its native boolean response, not proportional pressure/torque.
Batch 18 offered DS-8X shift-up. The production Gear up query became true at
probe 45.6998397 s, native gear changed 1 → 2 at 45.7002744 s, and native
`Changing` cleared at 45.9173943 s. The trial then stopped with
`Native racing state/countdown/focus changed.` It did not accept this batch,
deliver shift-down, reach the final brake/neutral pair or submit Core `end`.

Offline sequence correlation identifies the native state transition at the
stop boundary. All 1,340 addon native samples match the original recorder on
gear, changing, respawn and all three position fields. The last addon sample is
sequence 1541 (gear 2, changing 0, respawning 0). The next original sample, 1542,
has `sample.driving=0`, `game.respawning=1` and the `respawning` marker.
`Runtime.Driving` explicitly rejects that state. This explains the refused
continuation; the cause of the native respawn is not established. The preceding
sample contains negative local forward velocity; it is not evidence that the
shift mapping failed. Probe and recorder elapsed clocks differ, so this
correlation uses the shared sample sequence, not equal timestamps.

The ordinary diagnostic producer completed with 1,609 samples, 18 markers and
zero drops/errors/invalid records. Its 1,407 force-observed samples all report
`ffb.modelValid=0` and preview zero: 677 `friction curve unavailable`, 357
`reverse: signal not validated`, and 373 `recorded-discontinuity`. This run
preserves the actual producer but provides **no valid force-preview comparison**.
All observed physical force connections/attempts/delivery and Forza sends were
zero. No native pressure channel is qualified. Native wheel torque fields and
missing-channel lists remain in the original source without a physical claim.

Normal process exit completed at `11:08:23.6172573Z`. Host shutdown released two
devices, held zero, joined the control worker and resolved input release. That
cleanup is distinct from normal Core test completion: the last observed Core
run state was 3, not Completed (4). Independent readback at `11:09:18.1195466Z`
matched all 83 file entries and all 25 raw registry names/types/bytes with no
extras. The installed receipt remained
`8ff3fb09569eb69f109755de7221efbff82f39463bd92546559a3653104c2ab1`;
the game process was gone and the lease absent before EXIT was reported.

Evidence: `trace/observations.tsv`, `trace/result.json`, refreshed device reports,
`independent-readback.json`, `offline-readback.json`,
`shift-transition-readback.json`, and
`after-aa8426d1a61146deb7f789101187f50f/tree/{recording,shimReports}`.
Original `source.jsonl` SHA-256:
`d64cf8799ab43b1c4bbc40a08f26c82213f585d7c3f491ae103b3ffeecbdfe7a`.
Observation TSV SHA-256:
`8419d4a92e8bbfaf146425c5e0ffe6350932700239219e4ab6fb39b043e6db04`.

### Separate owner observation: HUD jitter

During this run the owner reported that the speedometer and other UI appeared
to shake or jitter. Keep this open for investigation. This is owner-observed
rendering behavior, not a captured-frame measurement, and is not assigned as
the cause of the input trial's interruption. A coordinated follow-up should
compare HUD motion against the native camera/world motion and triple HUD
anchoring before changing input, timing or camera code. No HUD fix or visual
acceptance is claimed here.

## Reordered candidate and second private run

The follow-up candidate moves both sequential shifts ahead of handbrake, steering,
throttle and brake. This isolates the remaining shift checks while the car is
still at its native starting position. The original hashed samples, 700 ms
pulses, six raw records per batch, neutral intervals, 23 native plateaus/24-batch
limit and final brake/neutral/Completed requirements are unchanged. It does not
claim to fix respawn or alter native physics. Neutral proof means observed zero
native control inputs; it does not claim that vehicle velocity or wheel torque
is zero. Missing final native-neutral evidence still refuses completion.

Device-free checks passed: actual pinned Core 2,640 frames / 5,166 raw provenance
checks, production UI/consumer 15,857 assertions, addon lifecycle 341 assertions,
protocol 88 and config 13 cases. The lifecycle fixture verifies the early native
gear +1/-1 sequence, neutral between shifts, native-state refusal, missing final
native-neutral refusal and separate Core Completed requirement. Private evidence:
`artifacts/core-car-fixture-5e4e660953534f1da8b234916a4dff96`,
`artifacts/shim-car-reorder-{tests,lifecycle}.log`. Plugin/addon builds passed
with zero warnings/errors; compiled metadata checks passed 48 plus 39 seams.

Reviewed source `7d98ca406d8b524a40d7137abdb805b680e11caa`, frozen in
`artifacts/shim-car-reorder-candidate.json`, ran as PID 63208, created
`2026-10-12T11:40:41.8123505Z`. The nonce was
`1db5c3066bf744f3a43b65fce98ae0ac`; evidence is private under
`results/shim-car-trial-02`. Native startup again verified Kenya SS1 / car 8 /
single player / Manual and completed countdown, with no pose writes. Both exact
profile devices reported live/open at epoch 1; the accepted Controls revision
and whole-file hash remained unchanged.

All 23 native plateaus passed with 22–25 samples each. The two production native
shift queries and gearbox readbacks passed: upshift 1 → 2 accepted at probe
31.4791234 s and downshift 2 → 1 at 33.4446653 s. The final native zero-input
plateau passed at 52.2158439 s, batch 24 submitted `end`, and Core reached
Completed (4) before the PASS verdict at 52.6422053 s. There were 1,696 native
car observations and zero legacy raw-injector commands. This qualifies the fixed
latched raw-profile workload through the actual consumer, not physical device
operation, arbitrary driving or a trajectory replay.

The original diagnostic recorder completed 1,895 samples / 11 markers with zero
drops/errors/invalid records. Its force-preview gap remains real: **0 of 1,696
observed samples valid**, including 1,151 `friction curve unavailable`, 230
`reverse: signal not validated` and 315 `recorded-discontinuity`. All 7,481 shim
observations had zero force connections/attempts/failures and Forza packets;
the original producer had only the diagnostic-force-disabled gate and zero
force writes/attempts. Handbrake remains a native boolean response; pressure,
proportional torque, physical force and exclusive reader/FFB coexistence remain
open. No normalization claim follows from this input PASS.

Offline follow-up found complete native front friction fields with coincident
positive slip knots and equal values in every friction-rejected row. Additive
Grip 6/config 4 fixes that admission only; the original model 5 source still
replays with its original invalid results. The counterfactual stays muted and
does not settle physical force or HUD jitter. See
[contract and checks](2026-10-12-grip-flat-knots.md).

Normal exit/restoration completed at `11:41:53.8380131Z`. Host shutdown resolved,
released two devices, held zero, joined both workers and reported input release.
Independent readback at `11:42:43.2328675Z` matched all 83 file entries and all
25 raw registry names/types/bytes, with no extras and the unchanged owner receipt
recorded above. The game was absent and the lease released before EXIT. This
functional run did not measure frame performance or resolve the separate owner
HUD jitter observation; unrelated offline builds were allowed during it.

Evidence: `trace/{observations.tsv,result.json,shim-ProfileInfo.txt,
shim-DeviceReports-latest.txt}`, `offline-readback.json`,
`independent-readback.json`, `restored.txt`, and
`after-d081a427509c4cd0996c2df23aeca218/tree/{recording,shimReports}`.
Original source SHA-256:
`932e1d4dbd9da5da043963e1c5c3c9668eba85d0e1da7faa6efbdb22ea43ce4e`.
Observation TSV SHA-256:
`5096ed40fda85af190f42b3c5d1824c8db687aeb7e15f8b598d12af2c111da6a`.

### Native brake/reverse and respawn evidence

A read-only metadata/instruction audit used the same supported GameAssembly
SHA-256 `f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c`.
The existing interop describes `MainCar.Brake`, but its wrapper alone cannot
establish behavior. The pinned native instructions at VA `0x1804fca00` show a
low-speed branch that increments `BrakeTimer` when `TrueSpeed <= 1`. With
`BrakeTimer > 30`, grounded, zero accelerator, positive fuel, inclination below
55, speed above negative `Max_Reverse_Speed` and a native mode gate, that branch
sets `MyGear=1`, clears wheel brake torque and writes negative
`EngineSystem.ActualPower` as motor torque. This branch does not test
`Aids.Automatic`. Thus a held brake can enter native reverse drive even in this
Manual trial. The capture's later negative velocity/torque is consistent with
that path; the audit does not prove the branch was executed in trial01.

`MainCar.FixedUpdate` also has native respawn eligibility for
`AutoRespawnOnFlip` with inclination above 85 for over 150 updates, an
`OutOfBoundsTimer > 120` path subject to a native mode gate, and
`RespawnTime >= 1`. `RespawnFunc` starts the native coroutine only when not
already respawning. Trial01 did not capture these eligibility fields, so the
particular trigger remains unknown. The native mode gate has not been named in
this bounded audit. No assist, setting, torque, timer or position is changed.
Private audit: `artifacts/shim-car-native-audit/{identity.json,selected.json,
MainCar.Brake.asm,MainCar.FixedUpdate.asm,MainCar.RespawnFunc.asm}`.

## Command used for the first coordinated run

Root source review, frozen hash readback and an explicit free rig slot precede
each run. Existing idle/lease checks still apply. Preserve this result directory;
any reviewed follow-up requires a fresh result path.

```powershell
pwsh -NoProfile -File tools/controls/Run-Controls.ps1 `
  -Result results/shim-car-trial-01 -ShimCarTrial -Seconds 180 `
  -CandidateDirectory components/wheel/src/WodenRallyEdge.Plugin/bin/Release/net6.0 `
  -ControlsSource 'D:/Program Files (x86)/Steam/steamapps/common/DRIVE Rally/BepInEx/config/dbce-controls.ini' `
  -ControlsSha256 5206EDFBD1B400E5D6A41D91AE836485BBE27D63992AD6F7483B3BABB0E72B79 `
  -NativeCandidate 'E:/Source/games/dbce-mods-woden-rally-edge/results/raw-controls-20261010-07/after-0aae30bae4c34d408de794f6932078ee/file/native' `
  -NativeSha256 24A9B624E3F3C474383C64A0177019BFB0837DE70B311854C59EE692D7DB03F5
```

Inspect the native trace/verdict, refreshed device/parser reports, Core
journal/shutdown, archived original recording/footer and independent owner file,
payload, registry and receipt readback. Report the limitations even on input PASS.
