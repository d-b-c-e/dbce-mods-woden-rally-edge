# Supervised replay without foreground focus

Claude's combined triple-screen Kenya run reached the stage but never acquired
the car: a separate Windows prompt held focus. The adapter reused the ordinary
wheel driving predicate, so it waited until its startup deadline despite having
latched physical output off. Moving focus was an unnecessary unattended prerequisite.

The candidate vendors clean toolkit playback core 0.2.1 / af4d20d. Its explicit
ReplayActive property distinguishes an accepted armed replay from an armed
recording; neither a status string nor !Recording is a safe substitute.

Only an accepted replay with a supervised native-startup request, validated
sealed stage context, latched output mute and configured background execution
may replace the focus predicate for trajectory eligibility and its camera.
Unity runInBackground is enabled on the Unity thread for this short-lived process,
including terminal normal-quit ticks. No persistent preference, display mode or
InputSystem device-focus behavior is changed. Normal input, physical force and
owner recording remain focus-gated. Pause/settings/photo/native replay/respawn,
car identity, start/end lock and stage/timestep checks remain effective. Session
completion, Stop and failure revoke replay eligibility. F12 requires game focus;
the external Stop command remains available without it.

Offline source verification passed 42 suites / 37,760 core assertions, 630 UI
assertions (50 new policy cases), 22 triple geometry checks and 10,836 envelope
checks, with zero warnings/errors. Toolkit mode/lifecycle coverage passed 62
assertions. Private build dependencies were restored byte-for-byte from the
archived prior worktree and then passed the existing dependency verifier.

Live cold-launch verification and exact packaged installer checks are pending.
The accepted prior installed runtime remains a628e04 until an installation
receipt below supersedes it. This change does not qualify physical torque or
complete the wider pause/Stop/stage/device matrix.
