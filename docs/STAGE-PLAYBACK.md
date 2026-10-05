# Developer recording and trajectory playback candidate - 2026-10-04

This is an implementation candidate, not runtime-qualified support. The plugin
builds and offline tests pass. It has not been installed or exercised in this
game. Art of Rally's successful trajectory tests do not qualify these hooks.
Steam is waiting on a launch prompt and the Windows control helper is unavailable.
Other sessions own uncommitted triple-screen work; preserve that work when
integrating this branch before a live deployment.

## Commands and lifecycle

From this checkout, with the matching candidate plugin installed:

```powershell
./vendor/playback/Stage-Session.ps1 -Game woden -Action Record -Path C:/captures/woden-01 -Seconds 60
./vendor/playback/Stage-Session.ps1 -Game woden -Action Status
./vendor/playback/Stage-Session.ps1 -Game woden -Action Replay -Path C:/captures/woden-01 -Output C:/captures/woden-replay-01
./vendor/playback/Stage-Session.ps1 -Game woden -Action Stop
```

The same script is packaged beside the plugin. An optional `-Launch` opens Steam.
Select the matching single-player stage/car manually. This adapter records one
continuous stage; menu automation and progression through a sequence of stages
are not implemented. Request files have a unique ID and expiry, and status must
acknowledge that ID. F12 stops the active stage operation. Restart the game to
restore physical output. Arming/capture/playback does not rewrite saved settings.

The trajectory reader verifies the completion seal, game build, scene/car key and
physics timestep before taking ownership. Playback sets a kinematic body and
suppresses competing physics/reset paths. The following tick must observe the
commanded position within 2 cm and rotation within 0.2 degrees, including the
last frame. This verifies applied motion, not deterministic physics resimulation.
Stop, lost eligibility, scene/car change, changed timestep, missing callbacks,
control-storage failure or a mismatched pose restores the body's previous flags.
Independent cleanup continues even if one writer or flag restoration fails.

Capture uses exclusive new files, a bounded background queue and finite samples.
Original signal writers must close with no drops/errors and at least two samples
before `complete.tsv` is written. Incomplete directories are preserved and
refused for replay; choose a new directory for a retry. Keep captures private.

## Original signals and normalization

Both recording and playback latch physical wheel/UDP output off for the process.
Original live physics samples, tuning and force/telemetry meanings remain in the
capture. Replay poses cannot recreate the tire/contact forces that produced them:
use the original `source.jsonl` for force-model analysis and comparisons.
A received/accepted native command is not measured wheel torque. Missing channels
are unavailable; they are never silently filled with zero.

Each capture contains `trajectory.tsv`, `trajectory.meta`, `source.jsonl` and a
hash seal, with game-specific sidecars below. Compare matching driving conditions,
speed ranges and channel units; normalized requests are not automatically equal
physical steering forces across games. Separate requested output from device
permission/acceptance and from the explicitly labelled hypothetical analysis
model. No automatic gain or normalization tune is applied to owner settings.

The separate `vendor/playback` pin is shared core 0.2.0 at
`b97989b8c64de1a2e5a5487c91134c3fbe0b8ea4`, with DLL/command hashes and MIT license.
It does not change the existing native/toolkit pin. The core has 50 offline
assertions; the command script passed isolated request/no-overwrite checks.

## Required live qualification

Woden captures `force-config.json` and `channels.json`. The original `ffb.*`
values retain actual delivery gates. `analysis.force.preview` uses a separate
history of the actual ForceSignal model without hardware delivery gates, with
valid/reset channels. Its load/slip model is an estimate, not measured rack torque.
`motion.speed` is Rigidbody velocity magnitude; its assumed Unity metre scale
still needs road-distance validation. Saved FFB may remain Off.

The adapter uses the selected MainCar, a single-player RACE state and scene/car
identity. It guards physics, resets, finish, score, achievement and SaveOnEnable
entrypoints while the process is muted. Unity 6 stripped Rigidbody getters use
injected native bindings; live binding resolution and all guards remain untested.
Wheel tests: 38 suites / 994 assertions; plugin build: zero warnings.

Run a fresh short capture with real game physics, stop and verify every stream;
then replay that exact capture and check camera/HUD behavior as well as pose
errors. Exercise Stop, focus loss, pause, retry, finish and scene exit. Verify
physical outputs stay suppressed after every termination, original signals stay
nonempty, and results/progression are not written. Compare capture-on/off timing
before using recordings to diagnose stutter. Full-stage and multiple-car tests
remain necessary. Builds and metadata fixtures do not establish these outcomes.
