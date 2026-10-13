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
| `source.stage/` | The stage tool's own bundle, sealed by its `complete.tsv` over `trajectory.meta`, `trajectory.tsv`, `source.jsonl`, `force-config.json`, `channels.json`, `stage-context.json`. Poses come from the `MainCar.FixedUpdate` prefix and signals from the same tick's postfix, with `capture.trajectoryIndex` 0..n-1. The tape's `physicalOutput` says whether forces were allowed (not measured). |
| `session.json` | `woden.full-session-package@1`: `game`, `gameBuildSha256`, `recordedUtc`, `origin`, `outcome` (`stopped` or `completed-stage`), `durationSeconds`, `car`, `biome`, the four payload names and `playbackUnavailableReason`. Never `playbackAdapter`. |
| `complete.tsv` | Outer seal over `session.json`, `source.jsonl`, `source.session.jsonl`, `source.stage-session.json`, `source.stage/complete.tsv`. |
| `full-session-incomplete.txt` | One line, written **instead of** `session.json` when the capture failed, recorded no sealed stage, the source failed, or the package could not be verified. |

`session.json` exists only after the stage, timeline and source footer were verified (nested seal, pose count,
every stage row aligned to its pose, zero dropped/invalid/error/rejected records, no `capture-failed` marker,
same game build) and the outer seal verified.

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
  `session.json` or `full-session-incomplete.txt` exists, bounded at 60 s after packaging starts, so the
  companion's Saved finalization follows the package. On game close the package gets a bounded 10 s; then the
  hold is released so the last status is the recorder's real state, even if a slow commit is still running. A
  descriptor left without its outer seal is never a package: readers require `complete.tsv`.
- **Origin** is `diagnostic`: the ordinary request carries no explicit owner-origin evidence (DRIVE's rule).
- **Supported scope** is whatever the stage tool's eligibility admits. S2 decides which stages cold playback can
  start; S1 records without that judgment.

Fixtures: `tests/WodenRallyEdge.Tests/FullSessionChecks.cs` compiles the production coordinator and drives it like
`CarHook` with the real recorders and seals. With `WODEN_FULL_SESSION_FIXTURE_OUT` set, the package suite copies its
sealed package there for consumer tests.
