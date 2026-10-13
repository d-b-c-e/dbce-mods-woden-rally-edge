# Ordinary full-session capture (S1)

An ordinary request-based recording (`woden-record-next-launch.json`, the installed companion) also records the
launch's **first driven stage** in DRIVE's full-session package layout. Nothing here changes the ordinary signal
recording, its case finalizer, forces, input, physics or progress. No playback adapter is declared: a package is
saved evidence until the cold-start route (S2) is qualified. Old flat captures are never retrofitted.

## Files beside the ordinary capture (`BepInEx/WodenRecordings/request-<id>/`)

| File | Content |
|---|---|
| `source.jsonl` | The ordinary signal recording from boot (unchanged). |
| `source.session.jsonl` | Observation journal `woden.full-session-observation@1`: `seq`, `seconds`, `kind`, `detail`. Header (schema, gameBuild, bootRequested, source, `diagnostic`), `scene`, `load` (from, to, native method, selection snapshot), `stage-open`, `pause`, `resume`, `stage-sealed` or `stage-incomplete` (reason, poses, finished), `stop`, and `end` with the ordinary footer's status, stop reason and counters. |
| `source.stage-session.json` | Timeline `woden.full-session@1` (toolkit `shared/session/FullSessionTimeline.cs`): Boot, Menu (`scene:<name>`), Loading (`scene:` or `load:<to>`), Driving, Paused, Finish states, `Poses` intervals and the stage reference (directory, seal SHA-256, pose count). Adapter `woden.observed-stage@1`. |
| `source.stage/` | The stage tool's own bundle, sealed by its `complete.tsv` over `trajectory.meta`, `trajectory.tsv`, `source.jsonl`, `force-config.json`, `channels.json`, `stage-context.json`. Poses come from the `MainCar.FixedUpdate` prefix and signals from the same tick's postfix, with `capture.trajectoryIndex` 0..n-1. The tape's `physicalOutput` is the configuration when the stage opened: whether force delivery was allowed then. It is not a per-tick delivery record or physical torque proof; the force-observation rows retain the later history if settings change. The stage can last as long as the ordinary capture, so its signal writer takes the ordinary recording profile (20 minutes / 1.75 GiB); the supervised stage tool keeps its own. |
| `session.json` | `woden.full-session-package@1`: `game`, `gameBuildSha256`, `recordedUtc`, `origin`, `outcome` (`stopped` or `completed-stage`), `durationSeconds`, `car`, `biome`, the four payload names and `playbackUnavailableReason`. Never `playbackAdapter`. |
| `complete.tsv` | Outer seal over `session.json`, `source.jsonl`, `source.session.jsonl`, `source.stage-session.json`, `source.stage/complete.tsv`. |
| `full-session-incomplete.txt` | One line, written **instead of** a package when the capture failed, recorded no sealed stage, the source did not complete (including a limit stop) or failed, or the package could not be verified. |
| `full-session-pending.json` | `woden.full-session-pending@1`, at most 1 MiB, written only at game close when the package was not finished in the game: game build, `origin` and `inputOrigin` (the finalizer refuses an origin that does not follow from it, and ends in the marker if the source states another), signal name, the timeline's SHA-256, the stage seal, pose count and the observation rows so far (each exactly as it will be written, without `end`). The companion's finalizer turns it into the package or the marker; it is never removed and is history once either exists. |
| `full-session-packaging.lock` | Transient, delete-on-close: held across every package-output write by the game's worker or close and by the finalizer, so only a live writer holds it. |

A valid package is `session.json` with its verified outer `complete.tsv`. `session.json` is written only after the
stage, timeline and source footer verified (nested seal, pose count, every stage row aligned to its pose, a
completed footer, zero dropped/invalid/error/rejected records, no `capture-failed` marker, same game build); it is
removed again if the outer seal then fails. The capture counts as finished only once the package or the marker
really exists; if the marker cannot be written, nothing claims an outcome and the status hold stays bounded.

## Rules

- **One stage per launch.** The stage opens on the first tick the stage tool would record (single player, driving
  eligibility, race checks) and seals on the first of: scene or car change, native load, ineligibility, finish,
  physics-step gap, timestep change, 30 s without a physics callback outside a pause, pose limit, or the ordinary
  recording stopping. A pause (including a timeScale-0 pause) closes a pose interval without sealing. Later stages
  stay analysis-only.
- **Supervised exclusion.** Arming requires no stage request, mute, replay path or active session. Once armed, the
  launch never polls the supervised stage commands and a later request file does not mute it
  (`StageLaunchOwner`). If outputs are ever muted mid-session, the capture fails.
- **Unmuted.** The capture never calls `MuteOutputs`, never resets force state, and uses the stage writers
  directly rather than `StageSession.Record`.
- **Status.** The ordinary `control/status.txt` shows `Stopping` (inactive) after the recorder closes until
  a valid package or `full-session-incomplete.txt` exists, so the companion's Saved finalization follows the
  package. Bounds: 60 s after packaging starts for a package not yet committing (it is abandoned), 300 s for a
  commit already running (released without claiming a package). On game close the hold is released so the last status
  is the recorder's real state. A descriptor left without its outer seal is never a package: readers require
  `complete.tsv`.
- **One owner of the outcome.** The package worker, game close and the status hold race for a single state. Whoever
  wins it writes every package output (timeline, observation, descriptor, seal, marker, handoff) under the packaging
  lock; a loser, or a winner that cannot take the lock, writes nothing. The worker's verification is read-only, so it
  can lose until it begins its commit. A capture abandoned at the 60 s hold keeps its timeline only if the worker had
  already completed it; otherwise, like a close whose stage seal outran 10 s, the source and stage files and the marker
  are its evidence.
- **Interrupted commit.** If a writer ends mid-commit, `session.json` is left without `complete.tsv` and no marker; it
  is never a package. The finalizer, finding that with the lock free (so its writer has ended), writes the marker and
  leaves the payloads untouched; the companion runs it for that state too.
- **Detached stage seal.** At the stage end the game thread only reserves the stage's journal row, observes the finish
  at that time and detaches the writers. The signal drain, the tape seal and its SHA-256 run on a worker. The row
  reads `stage-sealed`, or `stage-incomplete` if that seal fails (then the capture is incomplete, as before).
- **Game close.** No source is parsed or hashed on the game thread. Within 10 s the close waits only for the
  detached stage seal or a commit the worker already began; otherwise it claims the outcome, writes the timeline
  and `full-session-pending.json`, and the companion completes the package after the game exits
  (`WodenCaseFinalizer package <capture>`): the same verification and commit as the worker, so the same bytes. A
  stage seal still running at the end of the 10 s leaves the capture incomplete, as does a handoff over 1 MiB (only an
  observation near its 10,000-row limit could reach that). Stop in the game is unchanged: the worker packages within
  the 60 s hold.
- **Companion order.** `Start-RecordedGame.ps1` runs the force-case reprocess, then the package step when a handoff
  exists (also when the force case failed), and only then writes `control/finalization.json`. Its `State` is the force
  case alone; the package's own files say how the package ended.
- **Origin.** The request's optional `InputOrigin` (`human` or `automated`, exactly; omitted means `unknown`) is written
  to the source metadata, the observation header and the descriptor's `inputOrigin`. The DRIVE-shaped `origin` is `owner`
  only for `human` and `diagnostic` otherwise. It is never inferred from forces, injection or focus, the source and the
  descriptor must agree, and it never gates admission or playback.
- **Supported scope** is whatever the stage tool's eligibility admits. S2 decides which stages cold playback can
  start; S1 records without that judgment.

Fixtures: `tests/WodenRallyEdge.Tests/FullSessionChecks.cs` compiles the production coordinator and drives it like
`CarHook` with the real recorders and seals. With `WODEN_FULL_SESSION_FIXTURE_OUT` set, the package suite copies its
sealed package there for consumer tests.

### Long-capture measurements (October 13)

The 20-minute profile passed two synthetic 71,940-pose captures through the real
writers, seals and force finalizer. Stop-in-game packaging took 20-24 seconds.
Direct game close exceeded the 10-second package wait at this size, and stage
sealing stalled the game thread for 0.6-0.8 seconds. Both are addressed by the
detached stage seal and the game-close handoff above (Rules); remeasure with
`WODEN_LONG_SESSION_EXIT=1`, which runs the close and the finalizer's package step
instead of the in-game worker.

The metadata/footer retention fix preserves full source validation and reduces
measured finalizer peak managed memory from 3.2 GB to 2.0 GB; the 71,940 source,
model and driving row counts are unchanged. These are warm-cache synthetic
measurements, not a runtime performance guarantee. See the hub's
`artifacts/woden-recording-capacity-20261013/README.md` for exact workloads and
limits. No owner settings, installed payloads or force tuning changed.
