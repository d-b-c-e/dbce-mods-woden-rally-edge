# Recording/playback integration after Claude's handoff

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
