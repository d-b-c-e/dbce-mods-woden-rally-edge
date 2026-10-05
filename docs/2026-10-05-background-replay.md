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

## Packaged and installed checkpoint

Clean source 05837eec1df75eefb6fc154be005512b842adb2a produced
WodenRallyEdge-0.2.14-session-focus.1.zip, SHA-256
A5906E5934E8A06BE593680F6F38BBF01116EC3DA75A424556BF28F29936C389.
The exact player stage passed all 111 installer checks under Windows PowerShell
5.1. At 21:08:13 UTC the receipt-owning installer updated all 13 payloads at the
Steam/Stream Deck target; 18 protected config/other files retained exact hashes.
Installed plugin SHA-256:
5854104D0C45ED09983DFD5453F32AF4332120DC81D1C6255189FB4ECA811883.
The prior a628e04 runtime is backed up in the game's WodenWheelBackups directory,
before-install-20261005-160810-5bc0cb66. Nothing is published.

The first cold replay, request 9fbe6ecbfa054c39b700334c7e45c0f5, passed all
3,601 poses including the final readback: maximum position error
0.00006103515625 m, rotation 0.00001690385577616028 degrees. Recorded stock
camera changes and bonnet selection were applied; the renderer logged 7680x1440
triples. No new screenshot acceptance is inferred from that log. Force writes,
Forza sends and runtime errors stayed zero. Normal exit and exact owner-state
restoration completed at 21:09:58 UTC. Original signal review still passes all
3,601 samples, 3,553 valid force rows / 3,477 nonzero, zero numerical mismatch
and zero physical-delivery attempts.

Private install and replay receipts:
LocalAppData/Dbce/StagePlayback/SessionEvidence/woden-background-20261005-160809.
This run acquired with focus and logged unfocusedReplaySteps=0. Therefore the
new runtime has passed its full foreground regression, while the specific
background condition still needs a live check. Claude has the working Windows
UI helper and the targeted focus-loss handoff. No physical torque qualification
or complete pause/Stop/stage/device matrix is implied.
