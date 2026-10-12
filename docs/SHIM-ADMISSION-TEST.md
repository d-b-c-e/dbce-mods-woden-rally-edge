# Private ordinary shim menu admission

Prepared October 12; first launch refused before addon arming, then restored
exactly (see run 01 below). This is an at-rest native-menu
check of production `Backend=Shim`, not axes/driving, keyboard response, FFB
coexistence or physical acceptance. Native remains the public default. The
provisional research shim is not a promoted toolkit/player package pin.

The existing `StagePlayback.Target.MuteOutputs()` latch does not activate a stage
session or suppress ordinary `ShimInput.Poll`. Reuse it and the previously verified
raw-controls native force fence. The developer addon now has a distinct schema 3
`shim-admission` request: same cold nonce/expiry and plugin/Core/config/bindings/
native identity, plus exact `controlsSha256` and `shimSha256`. Schema 2 raw tests
remain separate. No stage request, normal DevInput, raw input command, synthetic
pad, automatic menu navigation or focus/display change is involved.

## Frozen candidate and source bytes

All SHA-256; verify these immediately before the reviewed run:

| Artifact | SHA-256 |
|---|---|
| Production Woden plugin from `da9e5b4ce885d6d7ffcedf8974deb4d5c07ffcac` | `cc7e01b25d508ec4e9e4eb2b30e9fb60f6804fb8af099a937c3b8aa3ae44d6aa` |
| Production Core beside that plugin | `ed05dba1aefcb0b4ea6790edc375e70d6fe812223ab27dde50cc13a48a6ec220` |
| Provisional `vendor/dbce-shim/dbce_shim.dll` (ordinary ABI 2) | `9b8a036f664cf8f45cccf82b98e5e4376f93215b60eac50b6db965bf498015d8` |
| Existing no-force `WheelFfb.dll`, raw-controls-07 retained post-run native | `24a9b624e3f3c474383c64a0177019bfb0837de70b311854c59ee692d7db03f5` |
| Current installed DRIVE `BepInEx/config/dbce-controls.ini` | `5206edfbd1b400e5d6a41d91ae836485bbe27d63992ad6f7483b3babb0e72b79` |
| Supported Woden `GameAssembly.dll` | `f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c` |

The proposed owner Controls source is `Profile = Moza KS Pro Triple`,
`Transmission = Sequential`, revision
`439A4118E99353997C8DF8555CA6B6C4F07DB89590C2B0A5EA60BA02B130D36C`.
Copy its complete bytes, without Wheelkit Apply, translation, normalization or
regeneration. Production Core still extracts and hashes the exact Controls
section; the native parser report must identify that same section hash.
If the source hash changes, stop and review the new source before running.

The addon is built separately (`tools/controls/Probe`); its exact hash is recorded
in `trace/identity.json`. Do not rebuild the frozen production candidate merely
to build the addon. Other installed dependencies and the installed receipt are
retained; addon admission verifies the actual candidate hooks before arming.

## Proposed commands, after source review and a rig slot

From `E:/Source/games/.worktrees/woden-shim-input`, verify the table above and run
the device-free checks listed in `tools/controls/README.md`. Then the single
supervised command below stages only within its owner snapshot and launches
ordinary Steam Woden. It requires a closed Woden, no conflicting cold requests,
five minutes of owner idle time and the shared rig lease. It never restarts an
owner app. Use a new result path; retain the supervisor while inspecting evidence.

```powershell
$repo = 'E:/Source/games/.worktrees/woden-shim-input'
$candidate = "$repo/components/wheel/src/WodenRallyEdge.Plugin/bin/Release/net6.0"
$native = 'E:/Source/games/dbce-mods-woden-rally-edge/results/raw-controls-20261010-07/after-0aae30bae4c34d408de794f6932078ee/file/native'
$controls = 'D:/Program Files (x86)/Steam/steamapps/common/DRIVE Rally/BepInEx/config/dbce-controls.ini'
$result = "$repo/results/shim-menu-admission-01"

pwsh -NoProfile -File "$repo/tools/controls/Run-Controls.ps1" `
  -ShimAdmission -CandidateDirectory $candidate `
  -ControlsSource $controls -ControlsSha256 5206edfbd1b400e5d6a41d91ae836485bbe27d63992ad6f7483b3babb0e72b79 `
  -NativeCandidate $native -NativeSha256 24a9b624e3f3c474383c64a0177019bfb0837de70b311854c59ee692d7db03f5 `
  -Result $result -Seconds 90
```

The runner performs no Apply and sends no inputs. It temporarily sets only
`Input/Backend=Shim` and the existing four output/developer mutes; all owner
tuning, triples and legacy bindings retain their values. It snapshots the whole
config/save/dev trees, original plugin/Core/shim/native/addon/log files, existing
shim reports and raw registry names/types/bytes before mutations.

From the supervising session, after `trace/identity.json` appears, inspect
`observations.tsv` and a current game frame. `shim-state` must show the actual
producer and pad classification, not an assumed wheel name match. Admission
requires zero Unity Gamepads and no native controller/raw pad state. An honest
`Shim refused; Native retained` result is a refusal, not a successful pilot.

```powershell
pwsh -NoProfile -File "$repo/tools/controls/Send-Command.ps1" -Result $result -Operation status
pwsh -NoProfile -File "$repo/tools/controls/Send-Command.ps1" -Result $result -Operation panel
# Inspect subsequent status/frame, then capture; do not send OS keys.
pwsh -NoProfile -File "$repo/tools/controls/Send-Command.ps1" -Result $result -Operation shot
# Inspect trace/shim-panel.png at native pixels, then end the test.
pwsh -NoProfile -File "$repo/tools/controls/Send-Command.ps1" -Result $result -Operation stop
```

`panel` invokes the production F6 panel method and temporarily selects Controls
in memory; this is rendered-panel evidence, **not a keyboard F6 response test**.
`shot` requests one screenshot using native `ScreenCapture`; a command reply
alone does not prove a PNG or correct pixels. No other visual/input operations
are admitted, and raw commands are rejected. The temporary presentation is
restored before production shutdown can save it.

The addon refuses a player car, nonzero force/open/network counters or observer
fault, and bounds itself by the original five-minute cold deadline. Stop/expiry
also calls the production shim's terminal stop: its independent reader is closed,
actions remain neutral, and DeviceHub cannot become a legacy fallback after shim
ownership. The process force/progression latch remains on. The supervisor sends
only normal close to its exact PID after stop, deadline or owner return; it never
kills a hung process. If exit/restoration is incomplete it retains the lease and
recovery metadata, and makes no success claim.

Closed-game recovery uses the same scope saved by the mode:

```powershell
pwsh -NoProfile -File "$repo/tools/controls/Run-Controls.ps1" -Recover -Result $result
```

## Required inspection and bounded verdict

1. `candidate-hashes.json`, `request.json`, `trace/identity.json`: exact reviewed
   plugin/Core/shim/native and accepted Controls/config/binding identity; ordinary
   admission mode; independently armed native no-force fence.
2. `shim-state`: actual native pad list/Unity count, `owns=true`, `ready=true`, at
   least three distinct increasing `tickFrame` values, no injected actions, no
   car; force opens/attempts/failures/connection and network packets stay zero.
   Native pad `AnyKey` is observed without modifying the keyboard overlay.
3. `shim-ProfileInfo.txt`: Core `ok=1`, expected section hash, owner profile,
   revision and Sequential mode. `shim-StartReport.txt` and
   `shim-DeviceReports.txt`: exact physical source identity/availability; never
   substitute a product/name-only match. Native availability or neutral at rest
   is not axis delivery/response proof.
4. Verify actual `shim-panel.png` pixels, with appropriate active or refused
   status and honest unsupported gear/coexistence labels. Retain the whole frame.
5. `trace/result.json` has zero raw commands; final log contains terminal shim
   stop and zero output counters. `runtime-verification.json` verifies loaded
   config semantics and unchanged owner bindings/Controls; `restored.txt` is
   accompanied by exact file and raw registry readback and released lease.
   New shim reports and original game log are archived under `after-*` before
   exact owner-state restoration. The installed receipt is never rewritten.

This first pass cannot qualify driving, pad coexistence or native keyboard
response. In particular the shim's ordinary DirectInput reader coexisting with
exclusive FFB acquisition remains an attended follow-up; this force-fenced test
does not exercise that ownership transition.

Preparation checks: addon and config verifier compile with zero warnings/errors;
47 protocol, 13 configuration, 173 actual-addon lifecycle, 48 actual production
metadata seams, 63 recovery, 28 file-restoration and 16 backend-staging checks
pass. These fixtures use fake engine/native/registry boundaries and isolated
files; these fixtures opened no game/device or installed owner state.

## Run 01: addon overload refusal, exact restoration

The reviewed 90-second pilot launched the ordinary Steam game as exact PID 71596
at `2026-10-12T08:26:24.6172680Z`. The addon refused in `VerifyShimAdmission`
before arming its native force fence: the name-only `Panel.Toggle` reflection
matched both the parameterless panel entry point and the renderer's private
five-argument toggle widget. Production shim terminal stop ran; the independent
config and process output mutes stayed latched. The log showed zero force writes
and Forza packets, no local car ticks, and ordinary splash/daily-message scenes.
There was no raw/OS input, driving or screenshot; parser/open/tick admission and
rendered UI remain unqualified.

The supervisor closed the owned process normally when native admission was not
confirmed. `restored.txt` is `2026-10-12T08:27:39.4844745Z`; independent readback
matches all 83 file entries and 25 raw registry names/types/bytes, with no extra
files and the original installed receipt unchanged. Woden is closed and the lease
released. Evidence is private `results/shim-menu-admission-01/`, including
`independent-readback.json`, config/runtime verification and the archived log
under `after-be09fdc962d8425aa5ea7f49edc1d758/file/log`.

The addon follow-up uses the exact parameterless method signature. The device-free
compiled metadata check now verifies 24 additional actual shim/panel/output seams,
and the lifecycle fixture includes the conflicting widget overload. All 173
actual-addon callback checks pass with that overload present; the addon builds
without warnings. Production plugin/Core/shim pins remain unchanged. A fresh
reviewed run must use a new result directory and verify the rebuilt addon hash.
