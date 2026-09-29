# 0.2.11 private development build

The startup message and title now retain input ownership while settings is open. F6 or the bound Settings button is observed before their direct keyboard shortcuts, including when the game callback runs before the mod update. Their local timers wait while the panel is open and resume normally after release. This successor has managed test coverage; its startup/title behavior still needs a force-disabled live check.

F6 opens Wheel settings in Simple by default. View: Simple / Advanced is saved explicitly. Tab / Enter navigate; arrow keys adjust focused sliders; scroll or Page Up / Down reveals longer pages. View changes preserve all tuning and runtime state. Finish or cancel calibration/connection edits before changing view. FFB is a single saved On/Off choice, default On for new settings. F8 immediately stops output and saves Off; choose On to resume. Physical force direction/feel and this build's handbrake behavior still need an attended drive. The repository's docs/STATE.md records verification history.

Supports Steam build 21802346, Unity 6000.3.6f1, GameAssembly SHA-256 `f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c`. Other builds leave hooks disabled.

Closing settings waits for keys, pointer and menu controls to be released before handing input back to the game. A short neutral interval prevents a held button from acting on the menu underneath. Keep settings open cancels that wait without returning game input. Binding also waits for release before accepting a new key/button. F6 accepts Unity's IMGUI keyboard events as well as InputSystem input, with duplicate events combined into one action; diagnostic logs identify availability and read failures. These paths have managed fixture coverage; actual game acceptance remains pending.

## Install and update

Extract the complete ZIP into a separate folder and double-click **Install.bat**. It discovers Steam libraries or asks for the game folder. The game must be closed. First install downloads the hash-pinned BepInEx prerequisite; updates reuse only the supported loader. No .NET SDK is needed for this player installer. Unsupported game/loader versions and linked target paths are refused before replacement.

Install backs up the existing plugin and all configuration under `WodenWheelBackups` in the game folder, verifies every packaged payload, and retains saved settings/bindings. Installer revision 3 fixes package-folder discovery through Install.bat on Windows PowerShell 5.1 and retains revision 2's closed-game and file-hash checks before every replacement, deletion and rollback. A failed replacement restores only verified bytes while the game remains closed. If the game starts or another process changes a target, it retains those files and reports **Recovery required**, with `recovery.json` and the original files in the backup. Close the game normally and review that report before using the installation; do not overwrite an external change blindly. The installer writes `BepInEx/WodenWheel-install.json` with owned file hashes and the backup path after success. Unknown mod files and recordings remain in place. It does not launch the game.

Double-click **Uninstall.bat** to remove only unchanged files named in that ownership receipt. Shared BepInEx, settings, unknown files and recordings stay. For an explicit settings removal, run `Uninstall.bat -RemoveUserData`; this also backs up the named Woden settings first. Recordings are always retained for manual review/removal. A modified owned file requires review before uninstall. A legacy development install without a receipt must be adopted with Install first. The repository-only `Install-Dev.ps1` retains its initial-install refusal behavior.

BepInEx BE #788 requires an empty `[IL2CPP] UnityBaseLibrariesSource =`. The allowlisted ZIP excludes the loader, game assemblies, generated references, settings and recordings. Updates preserve bindings and tuning.

## Controls

Start in Setup, then Controls. Centre the wheel and release pedals. For steering, turn right first, sweep both ends and Save calibration. For pedals, press fully and release. Each role retains its exact device, endpoints, inversion and deadzone. All three driving axes and Wheel controls On are required for the override. Calibrated controls now reach the game during its start-line countdown as well as racing, so throttle/revs, steering and bound buttons are available before green. The game retains its own start-line braking and lock; FFB still requires the racing state. Device bars alone do not prove car response.

Controls groups bind shifts, camera, look-behind, lights, horn, reset and other stock actions. Menu buttons includes Pause, Confirm, Back and four directions. Bound menu buttons navigate the mod panel and send events to Unity's selected stock menu item while outside driving. Screens without compatible Unity handlers show a keyboard/controller fallback; actual game-menu coverage still needs a live check. Driving and menu bindings can share a button because their contexts differ. H-pattern/clutch remain unavailable.

Handbrake (axis) is the fourth Controls row; Handbrake (button) is under Driving buttons. Both bindings contribute independently, with a held button requesting full handbrake. Bind starts provisional capture directly; Calibrate retains the assigned device/axis. Release before binding, pull fully, release, then Save calibration. Cancel/timeout retains the old assignment. Inversion, deadzone and a 0–100% bar are available. An unavailable axis releases its contribution; stock keyboard/gamepad handbrake remains usable.

The game only exposes a boolean handbrake. For an axis this build scales the stock rear brake command and grip-loss amount by pull strength. Full pull retains stock behavior. The native positive-wheel-speed threshold and binary engine-power cut remain unchanged; no new brake model or assist tuning is introduced. The adaptation only runs inside the selected player's scoped input call. Its game tuning value is restored after the call, including exception cleanup. Physical proportionality remains unverified.

Refresh devices after reconnecting hardware. Refresh stops output before changing handles, retains saved On/Off, and retries the selected wheel. Settings: `BepInEx/config/dbce.wodenrallyedgewheel.cfg`. Bindings: `BepInEx/config/wheel-bindings.json`, with a previous-file backup.

## Force feedback

In FFB, the device dropdown defaults to Use steering wheel (the saved Steering GUID). A legacy explicit selection is preserved. An explicit override stays independent of Steering. Missing, ambiguous, virtual or non-FFB targets remain inactive with a reason. Confirm the selected physical wheel and use the On/Off control. There is no session-start step. The default strength is 50% with a 25% peak cap. Original output gain is restored: force matches 0.2.3 at the same settings, and the configured peak cap directly limits delivered output. Existing tunes are preserved. Close settings and drive slowly forward. F8 saves Off if direction/behavior needs correction.

The model is a provisional front-load/sideways-slip estimate plus calibrated steering damping, not measured rack torque. Force is suppressed for panel/pause/focus/camera takeover, invalid/stale/airborne samples, reverse or missing enabled wheel input. The exact GUID, stock Logitech ownership transfer, 150 ms toolkit watchdog, exit guards, ramp and peak cap remain required.

Normal suppression zeroes/stops the effect while retaining the connection. A failed connection/write latches an error; choose On or Refresh to retry. The main thread no longer repeatedly enumerates all devices on contact gaps. Device preparation runs once at zero in Update rather than during car sampling. 0.2.7 captures the focused game window before device enumeration so a focus change cannot redirect the toolkit to another application. Waiting for a valid game window does not latch a connection failure or enumerate devices.

## Cameras

Use the normal Change camera action to cycle through stock views, Bonnet and Bumper. Advanced → Cameras adjusts side, height, forward position, downward pitch and field of view. Bonnet defaults to Fit to car: a body-relative placement intended to leave the hood visible, now corrected by +0.15 m up and +0.05 m forward using the owner's saved adjustment. Without body bounds, the fallback height is 0.7367809 m and forward position 0.9380049 m, with 8° pitch and 70° FOV. Adjusting a slider or tuning key switches to manual positioning; Reset this view restores the fitted default. Previously customized offsets stay manual. The new hood framing still needs an attended visual check across cars.

Mounted views use a 0.03 m near clip. Normal stock pose/FOV/clip values are restored on release; an external camera writer's changed values are preserved. Offset tuning is global; fitted defaults adapt to each car, while saved per-car manual presets remain future work.

Simple → Cameras → Adjustment bindings accepts a keyboard key or wheel button for Change camera, Look behind and each adjustment. New defaults are numpad 8/2 up/down, 9/7 forward/back, 4/6 left/right, 1/3 tilt down/up, +/− FOV and 0 reset. Existing saved or implicit old tilt bindings keep their meaning. Restore numpad defaults changes only adjustment bindings. Adjustments affect only the active Bonnet or Bumper during the countdown or driving; each press moves 0.05 m, 1 degree or 2 degrees of FOV. F6/F8 remain reserved; Escape cancels binding. Conflicts and modifier chords are rejected without clearing existing assignments. Held movement repeats after 350 ms at at most 10 Hz, with no catch-up after a stall; reset acts once. Captured/held keys must release after panel or focus suppression. The game's own keys remain active. Settings save automatically after adjustments settle.

## Telemetry and agent-controlled recording

Simple Telemetry has an Off/On master, actual destination, output status and active recording Stop. Advanced configures loopback Forza UDP (8000), detailed JSON UDP (8001) and rate. Port 0 disables a stream; nonzero ports must differ. Apply connection commits endpoint edits together. Cancel preserves the active destination. Connection changes and Telemetry Off do not restart or end a recording. The menu does not start recordings. Help can write a local support summary; it never uploads it.

An agent can prepare and launch one bounded capture:

```powershell
# Diagnostic launch with physical force suppressed; saved FFB preference unchanged.
.\tools\Start-RecordedGame.ps1 -CaseId corner-unwind-baseline

# Only for a requested attended FFB drive:
.\tools\Start-RecordedGame.ps1 -CaseId corner-unwind-baseline -AttendedFfb

# Prepare a request without launching; valid for 15 minutes:
.\tools\Start-RecordedGame.ps1 -CaseId fixture-drive -PrepareOnly
```

The plugin consumes the versioned request once and correlates it to `BepInEx/WodenRecordings/request-<guid>/source.jsonl`. Normal exit finalizes the 20-minute / 64 MiB source. The command waits a bounded time for start, normal exit and finalization; a timeout never stops Woden. After exit it validates driving coverage and reruns the actual managed force model into `force-observation.jsonl`, printing exact paths and hashes. The source, effective force config and capture-profile identities are bound by `case.json`; the observation header binds the exact manifest bytes. The legacy RecordSession configuration remains compatible but is unnecessary. Opening a menu does not start recording.

Default capture suppresses physical FFB for that launch without changing the saved preference. Use `-AttendedFfb` only for an attended owner drive. `-PrepareOnly` creates the expiring request and expected path without launching the game or touching a device. See [recorded playback boundaries](RECORDED-PLAYBACK.md).

Captures include raw/applied handbrake, FFB status markers, connection/write counters and main-thread timing. Timing channels distinguish frame intervals, input/device polling, native car update, sampling and force work; gear/shifting are available for correlation. A frame gap alone cannot attribute a hitch to shifting.

```powershell
dotnet run --project tools\TelemetryInspector -c Release -- inspect 'path\to\capture.jsonl'
dotnet run --project tools\TelemetryInspector -c Release -- reprocess 'path\to\request-case-directory'
dotnet run --project tools\TelemetryInspector -c Release -- compare 'baseline.jsonl' 'candidate.jsonl' 0.000001
```

Missing channels remain unavailable; raw game scales are unvalidated. Successful native output calls do not prove physical feel. Inspector rejection of missing/corrupt footers is intentional.

## Difficulty

In F6 → Advanced → Driving, enable Countdown assist to give yourself more time to finish. The saved default speed is 75%: 60 seconds on the countdown lasts about 80 seconds of driving. 50% doubles the available driving time; 100% or Off restores normal countdown speed. The slider spans 25–100%. The assist defaults Off and saves your choice across launches.

This applies only to an active single-player countdown/time limit. Elapsed lap/stage clocks, car speed, physics and FFB tuning remain unchanged. Checkpoint time bonuses keep their normal numeric value. Changing the setting affects future countdown ticks and cannot undo a timeout. The UI reports whether an eligible countdown was found; runtime behavior still needs an attended check.

## Camera behavior

Change camera cycles stock views → enabled Bonnet → enabled Bumper → stock. Look behind is held. Camera takeover releases mounted views and suppresses force. Position/clipping/transitions and split-screen still need live validation; per-car FOV is unfinished.

For removal, close Woden normally and use Uninstall.bat as described above; it preserves unrelated files within the plugin directory.
