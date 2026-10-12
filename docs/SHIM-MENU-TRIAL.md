# Bounded shared-Core menu trial

October 12 source candidate, **not live-qualified or installed**. This is the next
slice after [ordinary shim admission 03](SHIM-ADMISSION-TEST.md). That run proved
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
