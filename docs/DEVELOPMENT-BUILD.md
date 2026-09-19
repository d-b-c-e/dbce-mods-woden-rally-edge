# 0.2.7 development build

F6 opens Wheel settings. FFB is a single saved On/Off choice, default On for new settings. F8 immediately stops output and saves Off; choose On to resume. Physical force direction/feel and this build's handbrake behavior still need an attended drive. See [STATE](STATE.md).

Supports Steam build 21802346, Unity 6000.3.6f1, GameAssembly SHA-256 `f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c`. Other builds leave hooks disabled.

## Install and update

The repository initializer, package script and Install-Dev.ps1 provide an initial development install only. The installer refuses existing plugins and running games. For this existing installation use the reviewed backup/update procedure in [HANDOFF](HANDOFF.md). Never stop a game to deploy.

BepInEx BE #788 requires an empty `[IL2CPP] UnityBaseLibrariesSource =`. The allowlisted ZIP excludes the loader, game assemblies, generated references, settings and recordings. Updates preserve bindings and tuning.

## Controls

Start in Setup, then Controls → Axes. Centre the wheel and release pedals. For steering, turn right first, sweep both ends and Save calibration. For pedals, press fully and release. Each role retains its exact device, endpoints, inversion and deadzone. All three driving axes and Wheel controls On are required for the override. Calibrated controls now reach the game during its start-line countdown as well as racing, so throttle/revs, steering and bound buttons are available before green. The game retains its own start-line braking and lock; FFB still requires the racing state. Device bars alone do not prove car response.

Controls → Buttons binds shifts, camera, look-behind, lights, horn, reset, pause and other stock actions. H-pattern and menu navigation remain future work; menus use mouse/keyboard.

E-Brake is the fourth row in Controls → Axes and also appears under Buttons. Calibrating an axis or binding a button selects that input type, keeping the other binding stored. Use axis reactivates a stored axis without recalibration. Release before Calibrate, pull fully, release, then Save calibration. Inversion, deadzone and a 0–100% bar are available. An unavailable axis releases its contribution; stock keyboard/gamepad handbrake remains usable.

The game only exposes a boolean handbrake. For an axis this build scales the stock rear brake command and grip-loss amount by pull strength. Full pull retains stock behavior. The native positive-wheel-speed threshold and binary engine-power cut remain unchanged; no new brake model or assist tuning is introduced. The adaptation only runs inside the selected player's scoped input call. Its game tuning value is restored after the call, including exception cleanup. Physical proportionality remains unverified.

Refresh devices after reconnecting hardware. Refresh stops output before changing handles, retains saved On/Off, and retries the selected wheel. Settings: `BepInEx/config/dbce.wodenrallyedgewheel.cfg`. Bindings: `BepInEx/config/wheel-bindings.json`, with a previous-file backup.

## Force feedback

In FFB, confirm the selected physical wheel and use the On/Off control. There is no session-start step. The default strength is 50% with a 25% peak cap. Original output gain is restored: force matches 0.2.3 at the same settings, and the configured peak cap directly limits delivered output. Existing tunes are preserved. Close settings and drive slowly forward. F8 saves Off if direction/behavior needs correction.

The model is a provisional front-load/sideways-slip estimate plus calibrated steering damping, not measured rack torque. Force is suppressed for panel/pause/focus/camera takeover, invalid/stale/airborne samples, reverse or missing enabled wheel input. The exact GUID, stock Logitech ownership transfer, 150 ms toolkit watchdog, exit guards, ramp and peak cap remain required.

Normal suppression zeroes/stops the effect while retaining the connection. A failed connection/write latches an error; choose On or Refresh to retry. The main thread no longer repeatedly enumerates all devices on contact gaps. Device preparation runs once at zero in Update rather than during car sampling. 0.2.7 captures the focused game window before device enumeration so a focus change cannot redirect the toolkit to another application. Waiting for a valid game window does not latch a connection failure or enumerate devices.

## Cameras

Use the normal Change camera action to cycle through stock views, Bonnet and Bumper. Cameras → Position adjusts side, height, forward position, downward pitch and field of view. Bonnet defaults to Fit to car: a body-relative placement intended to leave the hood visible, now corrected by +0.15 m up and +0.05 m forward using the owner's saved adjustment. Without body bounds, the fallback height is 0.7367809 m and forward position 0.9380049 m, with 8° pitch and 70° FOV. Adjusting a slider or tuning key switches to manual positioning; Reset this view restores the fitted default. Previously customized offsets stay manual. The new hood framing still needs an attended visual check across cars.

Mounted views use a 0.03 m near clip. Normal stock pose/FOV/clip values are restored on release; an external camera writer's changed values are preserved. Offset tuning is global; fitted defaults adapt to each car, while saved per-car manual presets remain future work.

Cameras → Bindings accepts a keyboard key or wheel button for Change camera, Look behind and each adjustment. Defaults match the other mods: numpad 8/2 height, 9/7 forward/back, 4/6 side, 1/3 pitch, +/− FOV and 0 reset. Adjustments affect only the active Bonnet or Bumper during the countdown or driving; each press moves 0.05 m, 1 degree or 2 degrees of FOV. F6/F8 remain reserved; Escape cancels binding. Reusing a tuning key clears its previous camera assignment. The game's own keys remain active. Settings save automatically after adjustments settle.

## Telemetry and agent-controlled recording

Telemetry configures loopback Forza UDP (8000), detailed JSON UDP (8001) and rate. Port 0 disables a stream; nonzero ports must differ. Apply connection commits endpoint edits together. There are no recording start/stop buttons in the menu.

An agent can prepare and launch one bounded capture:

```powershell
# Diagnostic launch with physical force suppressed; saved FFB preference unchanged.
.\tools\Start-RecordedGame.ps1

# Only for a requested attended FFB drive:
.\tools\Start-RecordedGame.ps1 -AttendedFfb

# Prepare a request without launching; valid for 15 minutes:
.\tools\Start-RecordedGame.ps1 -PrepareOnly
```

The plugin consumes the request once. Normal exit finalizes files under `BepInEx/WodenRecordings`; each capture is bounded to 20 minutes / 64 MiB. The legacy RecordSession configuration remains compatible but is unnecessary for this workflow. Opening a menu does not start recording.

Captures include raw/applied handbrake, FFB status markers, connection/write counters and main-thread timing. Timing channels distinguish frame intervals, input/device polling, native car update, sampling and force work; gear/shifting are available for correlation. A frame gap alone cannot attribute a hitch to shifting.

```powershell
dotnet run --project tools\TelemetryInspector -c Release -- inspect 'path\to\capture.jsonl'
```

Missing channels remain unavailable; raw game scales are unvalidated. Successful native output calls do not prove physical feel. Inspector rejection of missing/corrupt footers is intentional.

## Difficulty

In F6 → Setup → Difficulty, enable Countdown assist to give yourself more time to finish. The saved default speed is 75%: 60 seconds on the countdown lasts about 80 seconds of driving. 50% doubles the available driving time; 100% or Off restores normal countdown speed. The slider spans 25–100%. The assist defaults Off and saves your choice across launches.

This applies only to an active single-player countdown/time limit. Elapsed lap/stage clocks, car speed, physics and FFB tuning remain unchanged. Checkpoint time bonuses keep their normal numeric value. Changing the setting affects future countdown ticks and cannot undo a timeout. The UI reports whether an eligible countdown was found; runtime behavior still needs an attended check.

## Cameras and removal

Change camera cycles stock views → enabled Bonnet → enabled Bumper → stock. Look behind is held. Camera takeover releases mounted views and suppresses force. Position/clipping/transitions and split-screen still need live validation; per-car FOV is unfinished.

Close Woden normally before removing only `BepInEx/plugins/WodenRallyEdgeWheel`. Preserve personal configurations, recordings and shared loader files unless separately removing those is intended.
