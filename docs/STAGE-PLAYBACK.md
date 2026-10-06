# Developer recording and unattended playback — 2026-10-05

**October 6 candidate:** installed `b9e9929/session-span.1` adds the separate-monitor
span and countdown leaderboard guard. Its first launch was blocked by another
Steam session before any game process started; state was restored and a retry
queued after 02:00 CT. [Current qualification record](2026-10-06-span-qualification.md).
The accepted runtime history below remains separate.

The first cold-launch Kenya SS1 / car 8 reference passed all 3,601 poses and the owner accepted its route and camera changes. The game exited normally; owner files and raw preferences were restored exactly. Current installed runtime: `05837ee/session-focus.1`, which passed that full reference again at 21:09 UTC. Prior accepted runtime `a628e04` is backed up. See [integration evidence](2026-10-05-playback-integration.md) and [remaining public-release checks](PUBLIC-RELEASE.md).

Claude subsequently observed the combined runtime's triples in the Kenya stage
at 7680 pixels, but the adapter's focus gate blocked playback acquisition. One
run had a foreground prompt; another reported none but still logged Unfocused.
The [scoped background replay fix](2026-10-05-background-replay.md) is installed
and passed the full cold-start reference with the renderer reporting 7680
triples, normal quit and exact restoration. It retains normal wheel/recording
focus gates and does not change the display profile. That run stayed focused;
actual background coverage and new rendered acceptance remain separate checks.

From the canonical repository, after building TelemetryInspector:

```powershell
./tools/game/Run-StageSession.ps1 -Recording "$env:LOCALAPPDATA/Dbce/StagePlayback/references/woden-kenya-20261005" -Result ('results/replay-' + (Get-Date -Format yyyyMMdd-HHmmss))
# Optional separate-monitor span qualification; the original config is restored:
./tools/game/Run-StageSession.ps1 -Recording "$env:LOCALAPPDATA/Dbce/StagePlayback/references/woden-kenya-20261005" -SpanSeparateMonitors -Result ('results/span-replay-' + (Get-Date -Format yyyyMMdd-HHmmss))
# Separate owner-driven capture, never switching to playback mid-race:
./tools/game/Run-StageSession.ps1 -Record -Seconds 60 -Result ('results/record-' + (Get-Date -Format yyyyMMdd-HHmmss))
```

Choose a new result directory. Recording needs the owner to choose single-player Arcade practice and drive. Playback uses native menu controllers to select the recorded stage/car without manual setup. F12 or Stage-Session Stop terminates the request. Both modes retain original source signals while latching physical wheel/network output off, then save and close normally. Recover an interrupted run only after the game closes, using the same Result with `-RestoreOnly`.

The optional span switch changes only `Triple.SpanSeparateMonitors` after the
runner has acquired its lease and backed up the owner configuration. It does not
change Windows display profiles, the saved resolution or the owner's Triple mode.
The normal restoration path restores exact original config bytes. The switch is
replay-only. Run `tools/game/Test-SessionSpan.ps1` for its device-free config cases.

Supervised cold replay captures up to five native game frames and matching
screen size/triple/focus/sample metadata under `Result/visual`. Capture starts
only while validated playback is running with outputs muted. It does not enable
the developer input channel, move focus or change input behavior. A capture error
is logged separately from the route result; inspect the images before claiming
rendered triple-screen acceptance.

The PowerShell 7 session runner now enforces the shared rig slot through the
hash-pinned toolkit `Stage-RigLease.ps1`. A slot younger than two hours refuses
launch before owner backup or command arming. `rig-lease.json` preserves its
unique ownership token for closed-game `-RestoreOnly` recovery. A failed launch
removes only its own pending request; another session's slot/request is retained.
This runner-only change does not replace the accepted installed game DLLs.

## Commands and lifecycle

From this checkout, with the matching candidate plugin installed:

```powershell
./vendor/playback/Stage-Session.ps1 -Game woden -Action Record -Path C:/captures/woden-01 -Seconds 60
./vendor/playback/Stage-Session.ps1 -Game woden -Action Status
./vendor/playback/Stage-Session.ps1 -Game woden -Action Replay -Path C:/captures/woden-01 -Output C:/captures/woden-replay-01
./vendor/playback/Stage-Session.ps1 -Game woden -Action Stop
```

The same script is packaged beside the plugin. An optional `-Launch` opens Steam.
These low-level commands alone require matching single-player stage/car setup; use Run-StageSession.ps1 above for unattended native startup. This adapter records one
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

The separate `vendor/playback` pin is shared core 0.2.1 at
`af4d20d65a8c87bcbfe07a002fda693e6cc492b4`, with DLL/command hashes and MIT license.
It does not change the existing native/toolkit pin. The core has 62 offline
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
