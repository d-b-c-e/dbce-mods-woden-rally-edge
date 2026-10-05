# Recording/playback integration after Claude's handoff

## First integrated live recording

The owner drive `results/owner-drive-20261004-2355/recording` completed with
3,601 poses and matching original samples over 59.999745 seconds: Kenya SS1,
Arcade route 0 / round 0, car 8, one player. There were no dropped samples,
recorder errors or physical delivery/connection attempts. The actual ForceSignal
reprocess matches every captured preview and validity/reset epoch with zero error;
3,553 samples are valid and 3,477 have nonzero preview, ranging from -0.25 to +0.25.
These are software estimates, not measured torque. The original archive is sealed
and unchanged. The game closed normally and all owner files/raw preferences were
restored with readback at 2026-10-05T04:56:19Z.

Installed capture source is `eb02b9c`, package
`WodenRallyEdgeWheel-0.2.14-integrated-session-3-dev.zip`, SHA-256
`030eff97b1246d98f713db02f2429ed3f621ae0e5288c1c2ba0189072239cc5e`.
The old unreceipted development folder and receipt were preserved privately under
`results/integrated-session-3-install`; the normal installer then installed and
verified all 13 payloads. Five config files and 25 raw preference values were
unchanged. The recorded installed plugin hash is
`ad9fe1d80d01c9c6daf1de8c50c2b84d7f84817933491c96b3f3e8e6adb260f7`.

**Single-screen observation:** the owner correctly observed one screen. Claude's
handoff explicitly left the desktop on "Sim Racing" (independent monitors), while
his accepted triple test used "Sim Racing Surround". The installed config retains
Triple Auto / MatchGameFov true and the integrated triple renderer/desktop-resolution
sources match `feb875b`. Startup logged desktop 2560x1440 at index 13, so Auto's
wide-window gate stays off. Merging code does not carry an active desktop profile.
Preserve the existing display environment during playback development; do not
claim that this recording visually retested triples or change resolutions repeatedly.

## Unattended startup candidate

`Run-StageSession.ps1 -Recording <recording> -Result <new-directory>` now validates
the sealed original source and actual force reprocess before launch. It explicitly
requests `coldStart=native-arcade-v1` on a supervised, expiring launch. Ordinary
low-level stage requests do not navigate menus. This first adapter supports the
observed single-player Arcade first-stage path, without rivals, ghosts or rolling
start; other contexts refuse rather than guess.

Native disassembly on the supported game hash establishes:

- `DailyMessage.Update` and `TitleScreenScript.FixedUpdate` call
  `MenuCameraScript.LoadScene` (RVA 0x4770e0), not `LoadScene.LoadScene_`.
  The first capture's empty nativeSceneRequests array is therefore an observer gap,
  not proof that no native transitions happened. The actual observed scene sequence
  is complete; a new passive observer targets the real entry point.
- `StagePresentation.Start` resolves `Progress.ArcadeRoutes` from the saved Arcade
  route/round and calls `NormalRound`. Startup restores only bounded transient
  Arcade choices after MapScreen initialization, checks native assets, and waits
  for the presentation's input-ready flag.
- `StagePresentation.ProceedToStage` (0xa91580) sets circuit direction and invokes
  the game's normal fade/load. Loaded player identity and full recorded race rules
  must match before acquiring the rigidbody.
- `Car_Cam.InstantCameraChange` (0x855740) reads the existing preset index without
  incrementing it. Recorded stock/mounted view selections come from the aligned
  original stream. Existing mounted-camera restoration and triple hooks remain.

Private method maps/disassembly are under `components/wheel/artifacts/startup-api`;
no proprietary assemblies or owner captures are committed. Native startup and
trajectory playback still require the upcoming live check.

Device-free command:

```powershell
dotnet run --project components/wheel/tools/TelemetryInspector -c Release -- stage-review results/owner-drive-20261004-2355/recording
python tools/game/Test-StageReview.py results/owner-drive-20261004-2355/recording results/new-review-fixture
```

The real source passes; six isolated altered copies correctly refuse seal corruption,
shifted row alignment, physical delivery, changed preview/reset and missing channels.
The original files retain their hashes. Lifecycle fixtures additionally prove that
recording, ordinary, malformed, unknown and duplicate requests cannot opt into menu
startup (573 UI assertions total). These checks do not certify native rendering.

## Earlier integration preparation

The owner ended the separate triple session and authorized integration and a
fresh recording/playback test. Local `main` at `95974ec` already merged Claude's
`feb875b` and playback `86cbc6d`. GitHub main had diverged with additional FFB
capture/validation work at `16b144e`. Merge `83b72d9` retains both histories;
no history rewrite or source conflict was necessary.

The combined source passes the complete device-free verification: 42 suites /
37,760 regression assertions, 556 existing UI assertions, 22 geometry assertions
and 10,836 envelope checks, with zero compiler warnings. The subsequent actual
`StageRunLifecycle` fixture adds 12 assertions (568 UI assertions total).
Those fixtures do not establish live native stage hooks or visual behavior.

## Capture and recovery

`tools/game/Run-StageSession.ps1 -Record -Seconds 60 -Result <new-directory>`
backs up owner config, saves, controller scheme and exact raw PlayerPrefs,
arms an expiring correlated request, then starts normal Steam app 3218630.
Select a single-player Arcade practice stage and drive normally. The first
60 seconds of eligible driving are captured; the banner distinguishes recording
from playback. After saving, a supervised launch exits normally after eight
seconds and restores owner state with readback. It never automatically starts
playback after recording. F12 stops early.

Low-level commands without `autoExit=true` retain their original no-quit behavior.
Malformed, expired and ordinary requests cannot enable the automatic exit. A
terminal or rejected supervised request exits after the same delay. The helper
does not change monitor profiles, resolution, window position or saved FFB tune.
`-RestoreOnly` with the same Result recovers after external interruption once
the game is closed. A timeout requests Stop and retains recovery instructions.

The live closed-game environment preflight passed for 60 owner files and all raw
preferences: `results/integration-environment-preflight-20261005`. No game was
launched by that check. Original settings include saved FFB On; the diagnostic
request mutes physical wheel and UDP output for its process and keeps original
signal sampling. Do not confuse temporary output suppression with a saved opt-out.

Capture context now includes a bounded observed scene sequence and passive
`LoadScene.LoadScene_` observations with native selection fields, in addition to
actual stage/car/race context. These are observations, not assumed startup actions.
The upcoming real capture must establish which native transitions actually ran.
The wrapper explicitly refuses replay until cold-launch support is implemented;
the low-level stage adapter alone is insufficient for the owner's unattended goal.

## Installed-state boundary

Before this integration, the installed plugin is Claude's unreceipted triple
development build, SHA-256
`20FF7773F79CC0DB7A3828D348BD8510D200CCFA7C42CD556BFC77F49C24E8F1`;
Core is `96FA09A0B13387A52BE6EA71618B99EBCEDE509562FD2A3399F5654B9AB4A4F4`.
The old `WodenWheel-install.json` still identifies 0.2.14/e3d118e and must not be
used as proof of those newer installed runtime bytes. Preserve an exact backup
before replacement. Native WheelFfb and managed FFB still match their existing
pins; do not repin hardware infrastructure for this integration.

The historical original recording remains unchanged at the path documented in
`2026-10-05-playback-offline-review.md`. A new capture is necessary to bind exact
scene/setup provenance; it does not replace that retained force baseline.

## First cold playback and owner report

Installed a628e04 at 05:18:06 UTC. `results/first-cold-playback-20261005-0018` completed all 3,601 poses: position error 0.00006103515625 m, rotation error 0.0000172453823 degrees. Camera changes, native menu startup, normal shutdown and owner restoration passed; output writes/attempts and Forza sends were zero. Owner confirmed playback worked, but expected triples and saw one screen. That presentation defect remains open. The original reference is now verified outside this worktree under LocalAppData/Dbce/StagePlayback/references/woden-kenya-20261005.
