# Developer recording and trajectory playback candidate - 2026-10-04

**Owner handoff / integration:** Claude's triple source and the current remote
main are combined at `83b72d9`. The owner authorized installation and a fresh
recording. See [the current integration record](2026-10-05-playback-integration.md).
The new recording wrapper backs up/restores owner state, observes native launch
context and saves/closes after a distinct recording phase. Cold-launch playback
is still under implementation and is explicitly refused by that wrapper for now.
The offline-only restriction in the historical checkpoint below is superseded.

**October 5 offline continuation:** the owner is using a separate Woden
triple-screen session and owns the screen. No launch/deployment/display/input
work here. The original 116-second owner drive was recovered and hash-verified;
all pose channels exist, but scene/setup provenance is missing. New source adds
a sealed, read-only `stage-context.json` for future captures, shared driving
eligibility checks and an explicit recording/playback status banner. Build,
38 regression suites / 994 assertions, 556 existing UI assertions and eight
legacy-source inspection tests pass; the banner and stage hooks have no new
runtime qualification. Read
[the offline review](2026-10-05-playback-offline-review.md) before proceeding.

This is an implementation candidate, not runtime-qualified support. The plugin
builds and offline tests pass. It has not been installed or exercised in this
game. Art of Rally's successful trajectory tests do not qualify these hooks.
The Windows control helper is unavailable. The owner has a separate active
triple-screen session; preserve that work when
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
The source banner labels each mode and keeps output suppression visible after
Stop; this addition is not in the older frozen session-2 package.

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
`50b6b679e3c6e01025b6a50f8dd0f3eb85d9d4bb`, with DLL/command hashes and MIT license.
It does not change the existing native/toolkit pin. The core has 54 offline
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

Captures now include capture.trajectoryIndex and require exactly one source sample
for each trajectory row. Pause/focus loss is checked from the render heartbeat
even when physics callbacks stop. All new adapter runtime checks remain pending.

## Local package checkpoint

Clean source `4ee01b0be5ad84534f9d6e602602094d4c30f591` produced
`WodenRallyEdgeWheel-0.2.14-session-2-dev.zip`, SHA-256
`21C0CF1FA2C55D28DE03E141FAA254C83F3DAB6092723D5891E15B24D69B2A0F`.
Build: zero warnings; core tests: 994 assertions; UI fixture: 556 assertions;
exact-package disposable installer fixture: 111 checks passed. Receipt log:
`components/wheel/dist/session-2-installer.log`. This package was not installed.

The owner's later garbled display/input failure has NVIDIA timeout evidence but
no identified game/mod cause. Live work is stopped during incident investigation.
Claude's committed triple/DevInput work at `2850e85` remains separate; integrate
and review it before deployment rather than overwriting the current installation.
