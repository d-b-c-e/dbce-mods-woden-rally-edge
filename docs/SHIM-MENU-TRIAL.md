# Bounded shared-Core menu trial

October 12 private trial 01 **passed its bounded native menu route** and restored
the owner state. No permanent install. This follows
[ordinary shim admission 03](SHIM-ADMISSION-TEST.md). That run proved
ownership and live at-rest readings while Unity was unfocused; it did not prove
menu action delivery. Native remains the standalone default. No force model,
saved tune, triple-screen behavior, toolkit pin or Wheelkit capability changes.

The private developer addon accepts a distinct cold schema 4 request with
`mode=shim-menu-trial`. It retains schema 3's exact plugin/Core/config/bindings/
Controls/native/shim hashes, nonce, expiry, native no-force latch and existing
progression/network mute. No stage session or ordinary raw injector is activated.
Only status and stop commands are accepted from the operator in this mode.

## Fixed route and evidence

The addon submits at most five atomic batches to the existing ABI 2 `open2`
control worker: DailyMessage Confirm, Title Start, map right, map left, end.
Each action pulse is 150 ms, followed by observed injected provenance, returned
Core neutral and a settling interval. A batch reply or desired value is not a
selection verdict. The actual native selection path must change as follows:

```
Menus/MenuTree/World_Map_Screen/Map_Buttons/Garage_button
Menus/MenuTree/World_Map_Screen/Map_Buttons/WorkShop_button
Menus/MenuTree/World_Map_Screen/Map_Buttons/Garage_button
```

The paths were observed in native raw-controls-20261010-07. Before navigation,
the trial reads the active native Selectable, interactability and reciprocal
right/left neighbors. Any different route refuses. It never sets a scene, menu
selection, progress field or game action table directly. Startup uses the real
production DailyMessage/TitleScreen hooks and their existing temporary Start
lease; navigation uses WheelInput -> MenuNavigation -> native IMoveHandler.
After the title pulse, Confirm/Start can never be re-enabled during this process.
There is no race, car, online, settings or purchase route in this trial.

Ordinary keyboard pass-through is unchanged. In this explicit diagnostic mode,
startup callbacks refuse physical keyboard/mouse activity, and the addon
temporarily disables each current native BaseInputModule while retaining the
enabled/focused EventSystem and its real handlers. It restores those modules on
termination. Actual Unity foreground and the production focus gate are required;
the runner temporarily sets `[Logging.Console] Enabled=false` within the existing
complete config backup to avoid the console stealing initial focus. It never
moves focus. Startup waits are bounded at 35 seconds per step plus the supervisor
and cold-request deadlines. Focus loss, owner input, car creation, changed native
pad classification, force/network activity or failed readback ends the trial.

`ShimInput.Trial.cs` exposes only cold configuration and monotonic action scopes.
It verifies frame sequence, latched/native-suppression flags and current fences.
Known neutral arms a release only for an owned, current, allowed action with
`TestNeutral`, zero value and `injected=false`. Ordinary input never treats that
status as Live. The first injected edge is covered by production query tests.
All other actions are unsupported in Core for this run. Shutdown retains sticky
shim ownership and zero wheel actions, with no legacy-reader fallback; existing
process output mutes remain latched until exit.

The observer now records shared sequence/run and all four trial actions. It
refreshes `shim-DeviceReports-latest.txt` and appends changed, timestamped native
snapshots to `shim-device-history.jsonl`; it never opens or enumerates another
reader. Positive device epochs must come from these reports, not assumptions.
Inspect `observations.tsv`, the Core journal, result verdict and host shutdown.
`passed` means the bounded native menu route only, not driving, real buttons,
device interruption, F6-key behavior or exclusive FFB coexistence.

## Live trial 01: October 12, 05:17–05:18 Central

Candidate source `094f650ff3a76d01ca8e4ca109a112ea070b8ff3`; the frozen plugin,
Core, addon, native shim, no-force native, Controls and supported GameAssembly
hashes all matched before preparation. PID 39224 started
`2026-10-12T10:17:13.5768972Z`, under lease
`cca334a61c084696a97fc7dfe4c43401`. Owner idle exceeded 5,884 seconds.

The actual production button queries observed Confirm at 11.753 s, Pause/Start
at 13.658 s, Menu right at 16.693 s and Menu left at 17.364 s. Native selection
readback was Garage at 16.374 s, Workshop at 16.696 s and Garage at 17.366 s.
The trial verified reciprocal native neighbors, saw injection then neutral,
offered `end` as batch 5 and recorded its passing verdict at 18.227 s.
There were 155 shim observations, 154 owned, with observed sequences 1–2597 and
Core states Latched/Waiting/Running/Completed. The journal independently reports
completion from batch 5, five offered batches and zero dropped status/events.

The native parser confirmed `Moza KS Pro Triple`, Sequential, exact revision
`439A4118E99353997C8DF8555CA6B6C4F07DB89590C2B0A5EA60BA02B130D36C`
and accepted Controls hash. Refreshed reports observed the R12 instance
`d71b8350-61b7-11f1-8001-444553540000`, product
`0006346e-0000-0000-0000-504944564944`, exact HID path, **epoch 1**, opened/live.
This narrow capability mask opens only the menu device; it does not qualify DS-8X
actions or driving axes. Every observed force open/attempt/failure/connection and
network counter was zero. The old raw injector accepted zero commands/reads.

Normal exit and supervisor restoration completed `10:17:51.4369155Z`.
Independent readback at `10:18:53.4041262Z` matched all **83 file entries and 25 raw
registry names/types/bytes**, no extras or mismatches. The installation receipt
remained `8ff3fb09569eb69f109755de7221efbff82f39463bd92546559a3653104c2ab1`.
The process was gone and lease absent before EXIT. Native host shutdown is
resolved: released 1, held 0, input released, control joined/complete/final tick.

Private evidence: `results/shim-menu-trial-01/trace/{observations.tsv,result.json,
shim-ProfileInfo.txt,shim-device-history.jsonl,shim-DeviceReports-latest.txt}`,
`independent-readback.json`, `runtime-verification.json`, `restored.txt`, and
`after-267ea65811bc4d60809ad6955c5686ab/tree/shimReports/run-9a1908a9e5b74cf4afdc5306e1e969e7/`.
Observation SHA-256:
`69459af1795d04a1e4383176b1f9f8431a95fa64f05992d65f65483d9fb1477e`.

This proves the fixed latched shared-Core menu route through the actual native
consumers. Physical buttons, ordinary keyboard response, driving inputs,
DS-8X/sequential delivery, interruptions, exclusive FFB coexistence and physical
feel remain separate. No further menu run is needed before the separate car slice.

## Offline validation

The fixture compiles pure Core from the exact provisional source
`f49cfc559f065b778967a7c94068a75df538ec69` into an ignored private artifact folder.
It reads the owner's accepted Controls bytes without rewriting them. Fake
providers supply identity/caps and deliberately held physical Confirm while
Core emits 500 ABI frames; there is no DirectInput, game or force call. Those
frames pass through source-linked production ShimInput, WheelInput, startup
hooks and MenuNavigation, with native behavior/event-handler fixtures. Both
startup transitions and both selection moves must occur exactly once. These
checks do not establish runtime game behavior.

```powershell
pwsh -NoProfile -File tools/controls/Test-CoreMenu.ps1 -Profile 'D:/Program Files (x86)/Steam/steamapps/common/DRIVE Rally/BepInEx/config/dbce-controls.ini'
dotnet run --project tools/controls/LifecycleTests -c Release
dotnet run --project tools/controls/ProtocolTests -c Release
dotnet run --project tools/controls/VerifyHooks -c Release -- .
dotnet build components/wheel/src/WodenRallyEdge.Plugin -c Release --no-restore
dotnet build tools/controls/Probe -c Release --no-restore
```

The initial source pass produced 500 real Core frames, 2,187 UI assertions,
220 addon lifecycle assertions, 59 protocol and 13 config cases. Lifecycle
coverage includes the fixed five-batch workflow, actual selection readbacks,
fault closure, module restoration and a concurrent Core-style held file reader
during the same atomic POSIX batch rename used by Art's reviewed trial.
The broader Core/controller regression passed 54 suites / 39,154 assertions;
plugin/addon builds had no warnings or errors and compiled hook verification
passed 48 existing plus 30 shim seams. Final candidate build hashes belong in
the review receipt before the rig run.

## Proposed live command after review and an explicit rig slot

Verify the frozen candidate/plugin/Core/addon hashes supplied with source review
and the unchanged pins below. No rebuilding after verification. The accepted
Controls whole-file hash is
`5206edfbd1b400e5d6a41d91ae836485bbe27d63992ad6f7483b3babb0e72b79`;
the provisional `dbce_shim.dll` remains
`9b8a036f664cf8f45cccf82b98e5e4376f93215b60eac50b6db965bf498015d8`.
The supported GameAssembly hash remains
`f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c`.

```powershell
$repo='E:/Source/games/.worktrees/woden-shim-input'
pwsh -NoProfile -File "$repo/tools/controls/Run-Controls.ps1" -ShimMenuTrial `
  -CandidateDirectory "$repo/components/wheel/src/WodenRallyEdge.Plugin/bin/Release/net6.0" `
  -ControlsSource 'D:/Program Files (x86)/Steam/steamapps/common/DRIVE Rally/BepInEx/config/dbce-controls.ini' `
  -ControlsSha256 5206edfbd1b400e5d6a41d91ae836485bbe27d63992ad6f7483b3babb0e72b79 `
  -NativeCandidate 'E:/Source/games/dbce-mods-woden-rally-edge/results/raw-controls-20261010-07/after-0aae30bae4c34d408de794f6932078ee/file/native' `
  -NativeSha256 24a9b624e3f3c474383c64a0177019bfb0837de70b311854c59ee692d7db03f5 `
  -Result "$repo/results/shim-menu-trial-01" -Seconds 90
```

The existing supervisor owns lease/idle admission, exact PID/start identity,
normal close, full file/config/save and raw registry restoration. Require no
remaining game process, resolved host shutdown, exact independent readback and
released lease before reporting EXIT. A different selection, unfocused window,
unavailable device/profile action or timeout is an incomplete/refused result,
not permission to send another action or expand the route. No permanent install.
