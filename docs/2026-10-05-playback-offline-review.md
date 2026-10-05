# Woden recording/playback: offline continuation

The owner is using a separate session for Woden triple-screen work and may own
the display. This work is isolated on `codex/session-playback`; no Woden launch,
installed payload/settings change, input, display change or force output occurred.
Do not deploy or replace their current triple work. Coordinate integration after
their session is ready; preserve `E:\Source\_worktrees\woden-triple` and its
successors. The last inspected triple commit was `2850e85`; this can advance.

## Recovered original drive

The original case is still present at:

`D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge\BepInEx\WodenRecordings\request-ea1e228595484dcfab283ed440182b82`

Source SHA-256 is still
`785d8d679c9dc654ea860212074b920b59944d0c6315ad40a2e9f26bd83d23b8`.
Its original case manifest matches; the completed footer has zero drops, errors,
invalid samples, contention or limits. Of 11,961 samples, 6,981 form one complete
driving range: source sequences 4,027–11,007, car ID 42, 116.3328387188 seconds,
physics step .016666595805 seconds, maximum recorded speed 50.5744667 m/s.
Position, normalized quaternion, velocity, local angular velocity, controls,
RPM and gear are all present. Original force data and native-delivery evidence
remain unchanged; this was an attended force-enabled capture.

The companion retained log also matches its documented SHA-256
`86bca30b888439a3f226cd97de7c6b5374867eb213096e03ddf7397bc6bd5d12`, at
`E:\Source\games\dbce-mods-woden-rally-edge\artifacts\owner-0.2.12-first-drive\LogOutput.log`.

The read-only inspection command is:

```powershell
python components/wheel/tools/Inspect-LegacyTrajectory.py <original-case-directory>
```

It validates case/source hash, completion/counts, finite pose channels and
quaternion length; separates clock gaps, discontinuities and car changes; and
reports missing values as unavailable. Eight isolated tests pass. It neither
rewrites the capture nor manufactures a completion seal or scene identity.
Private reports are under `components/wheel/artifacts/startup-api`.

**Remaining provenance gap:** the source metadata and ordered markers do not
identify the scene/track or menu launch configuration. The retained plugin log
does not supply that identity either. Do not choose a scene from current saved
settings and pretend it was recorded. A future conversion must bind independently
verified track/setup evidence, preserve the original source, and rotate recorded
local angular velocity into world coordinates. This healthy source alone does
not establish a runnable trajectory case.

## Source improvements and native startup findings

New stage recordings now seal `stage-context.json` alongside the original signal
files. `StageCaptureContext` reads the actual scene, car/player, race mode,
competition/track assets, route/round indices, weather/laps, reverse/damage and
other named race settings. It is explicitly observed context, not a qualified
native startup recipe. No saved settings or native fields are written. This
addresses the historical missing-context problem for future captures. The
plugin builds with zero warnings; this new sidecar has not run in-game and is
not in the older frozen session-2 package.

Read-only interop inspection found native `MainMenu.ArcadeButton`,
`RouteSelectScreen.PracticeButton`, `ArcadeCarSelect.SelectCar`, and
`ChangeMenuButton.ChangeMenu/ChangeInmediate`. `RaceConditions.MyGameround`
exposes the selected track/hour/weather; `Progress.StatsData_` holds route/round
selection. Most startup implementations are obfuscated, so names alone do not
prove the correct action sequence. Earlier startup input findings remain valid:
DailyMessage and TitleScreenScript have direct legacy-input paths. Do not replace
semantic startup work with a blind key sequence or overwrite another session's
UI controls.

The game also exposes `ReplaySystem.Movie`, `StartingFrame`, `PlayingFrame`,
`StartRecording` and `StartPlaying`. That establishes native in-memory replay
machinery, not a durable external recording/playback workflow. Its file format,
save/loading behavior and compatibility with original telemetry are unqualified.
Investigate reuse before duplicating useful native presentation behavior.

Further read-only inspection of `ReplayMovie` found actor/car/skin arrays and a
list of frames. Each actor frame has body velocity/angular velocity, position,
rotation, wheel positions/rotations, RPM, brake/accelerator/steering, gear and
headlights. No scene/setup identity, original tire/contact forces or external
persistence entry point is exposed on that type. This is useful presentation
machinery, not a substitute for the sealed original signal capture. The exact
rotation semantics and native lifetime/ownership still need review before reuse;
do not fabricate wheel animation channels absent from the old source.

The stage adapter now reuses the existing `PlayerControlState.Driving` policy.
The prior duplicate predicate omitted native replay, photo mode and the native
car lock during continuous playback. Only our trajectory ownership is excluded
from the eligibility calculation; native replay remains a rejection and normal
force/input paths still treat our playback as non-driving. Owned-window focus,
pause/settings, respawn, phase, player identity, body and single-player checks
remain required. No native force policy was relaxed.

A process-lifetime banner now identifies recording, playback, terminal status,
physical wheel/motion suppression and F12 Stop. It uses the same stripped-build
GUI primitives as the existing settings panel, restores GUI state, and does not
resize, focus or consume input. It remains visible after Stop to explain the
latched output suppression. The banner has not been visually tested.

The complete wheel solution builds with zero warnings; the existing regression
harness passes 38 suites / 994 assertions and the UI harness passes 556
assertions. Those checks cover existing driving exclusions and UI behavior,
not live execution of the new stage adapter or rendering of its banner. No
successor package was built or installed; the frozen session-2 archive remains
unchanged. Current replay identity checks still cover game hash, scene/car and
timestep; full launch-context comparison and restoration remain future work.

## Next work, without retesting iRacing

1. Resolve the old drive's scene/setup identity or capture new context once a
   coordinated recording is available. Preserve the existing 116-second source.
2. Implement a bounded cold-launch state machine using verified native actions;
   record the launch recipe before it is needed for replay. Build/scenario/timestep
   checks and output mute remain mandatory. A stage-only adapter is insufficient.
3. Keep scenario state separate from the owner's normal triple presentation.
   Preserve the other session's geometry and display/launcher choices.
4. Add visible recording/playback status and a supervised exit/restoration path,
   then integrate the final triple source in an isolated branch before deployment.
5. When the rig is released, one complete reference run plus the required failure
   case is enough unless a new change/failure justifies more. Offline checks do
   not establish live hooks, visual acceptance or wheel-force normalization.

Art 0.4.0 is released. iRacing has two accepted unattended reference runs and an
intentional Stop/recovery pass. Its normal triple Steam launch options are prepared
but must wait until Steam can close without interrupting the owner's Woden work.
