# Course route capture (read-only, opt-in, private)

## October19 current-main integration

Kobe286136: Mubi reconciled existing c575200 onto the current4b6b900 close/recovery
baseline in isolated mubi/woden-course-integration-20261019. The six route-capture
paths apply cleanly as8fb62ff; only this integration note and TOOLKIT-ADOPTION.md
follow. No control/focus/close/recovery source or default is changed.

Qualification is reused, not repeated: all product compiler inputs equal c575200,
vendor/build inputs are unchanged, and116 core/interop/toolkit/recording dependency
files match the qualified private worktree and canonical dependency copies by SHA256.
Zola279786 and retained research-private/course-map-zola-20261018/
woden-course-run-01-build.log prove Plugin/Core Release0warnings/0errors;
woden-course-run-01-harness.log proves94suites/51700assertions PASS, including
the four capture suites. No new build/tests are warranted by a conflict-free merge
which leaves these inputs identical. The current tools/controls tree is byte-identical
to4b6b900. Runtime's Activity.StopAndDrain still precedes CourseRouteDump.Stop.

Toolkit90f14a0 and its offline reader/comparison remain the existing separate
private dependency; no new protocol, serializer or shared source is introduced.
The existing extractor output stays compatible with that reader. Source integration
does not install the plugin, arm a request, read a running stage, prove units, or
register a SimHub definition. The capture/runtime gates below remain unchanged.

2026-10-19, zola (kobe 278516). Source and offline fixtures only; it has never run in the game.

## Purpose

This captures the stage's own route and the game's own progress, once, for the toolkit's course seam (toolkit
`docs/COURSE-TELEMETRY.md`, "Woden route extraction handoff"). Course ids, axes, projection and the SimHub layout
stay toolkit code. They are applied offline to these files, so this repo gains no second telemetry framework. The
Forza output, recordings, force and every config are untouched.

## Arming (one launch)

The capture is off unless an operator places a request at
`<game>\BepInEx\config\woden-course-route-next-launch.json`:

```json
{"Version":1,"Id":"<new guid>","ExpiresUtc":"<now + at most 20 min, ISO 8601>","MaxSeconds":600}
```

- **Claiming.** `CourseRouteRequest.Consume` renames the file to `.consumed-<guid>` before reading it, like
  `DiagnosticLaunch`, so a request can never arm a second launch.
- **Validation.** It refuses a wrong version, an empty id, an expired request, one more than 20 minutes ahead, or
  `MaxSeconds` outside 10-600.
- **Consent.** Creating the file writes to the owner's game folder, so it happens only under a runtime grant.

## What it reads

`CourseRouteDump.Observe` runs from the existing `MainCar.FixedUpdate` postfix, after the sampler, for the selected
local car. Any exception is caught there and abandons the capture. It can never reach the sampler's "Sample failed"
path.

- **Route**, read once on the first tick with `Status == RACE`:
  - `car.field_Private_WaypointProgressTracker_0.circuit` (`WaypointCircuit`): `waypointList.items[i].position`,
    `isClosedCircuit`, `length`, `Kms`, `unitConversionFactor` and `distances`.
  - Whether `FindObjectOfType<StageProgress>().MyCircuit` is the same object.
  - Identity: `race.MyGameround.Track` `name` and `CircuitName`, `RaceConditions.ReverseTrack` and `TotalLaps`, and
    the game assembly hash.
- **Every tick until `END`/`DESTROYED`, the time limit or the row cap:** the car's `transform.position`, the
  tracker's `progressDistance`, `StageProgress.PlayerPercentage`, `Status`, and the sampler's simulation seconds.

`field_Private_WaypointProgressTracker_0` is an obfuscated interop name. Check it again after any interop
regeneration.

## Output and limits

Output goes to `%LOCALAPPDATA%\Dbce\CourseExtraction\woden-<track>-<fwd|rev>-<yyyyMMddTHHmmssZ>\`:
- **`route.json`** (`dbce.course-route@1`): identity, route facts, and the waypoints as unconverted Unity world
  `[x, y, z]` (`frame: unity-world`).
- **`progress.tsv`**: `tick sim_s px py pz progress_distance player_percentage status`.
- **`complete.tsv`**: a `dbce-playback-seal-v1` seal with the size and sha256 of each file.

Limits:
- **Route:** at most 20,000 waypoints. More refuses the whole capture; it is never truncated.
- **Rows:** at most 36,000, and no longer than the request's seconds; then it stops as `LimitReached`.
- **Disk:** at most 8 MB.
- **Bad rows:** non-finite values, time going backwards and unsafe status text are counted and skipped.
- **Writing:** once, into a temporary folder that is then renamed. An existing folder is never overwritten, and a
  refused or faulted capture writes nothing. The write leaves the physics thread; at shutdown it runs after the
  activity gate drains.

## Fixtures

`tests/WodenRallyEdge.Tests/CourseRouteChecks.cs` holds four suites in the executable harness:
- **Request:** claiming, plus six refusals.
- **Identity:** the exact build, raw names, the direction in the folder name, and eight refused routes.
- **Coordinates:** Unity `x, y, z` stored bit-exact and in order, with nothing swapped or negated.
- **Lifecycle:** no rows before the route, the seconds/rows/bytes caps, faults writing nothing, write-once, and
  no overwrite.

All inputs are synthetic.

## Not proven

- The interop reads have not run.
- Whether `length` and `progressDistance` are metres, and whether `PlayerPercentage` is 0-1 or 0-100, is still
  unknown.
- The comparison against the private Kenya SS1 reference waits for one granted run; see the toolkit handoff for
  the pass thresholds.
