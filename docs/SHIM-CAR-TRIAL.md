# Bounded shared-Core original car trial

Source candidate only; no game run or installation is qualified here. Schema 5
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
axes and both shift buttons. Neutral precedes and separates steering -0.5/+0.5;
throttle, brake and handbrake 0.5/1; and one DS-8X up/down press. A brake/neutral
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

## Proposed coordinated command

Root source review, frozen hash readback and an explicit free rig slot precede
this command. Existing idle/lease checks still apply. No schema 5 launch yet.

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
