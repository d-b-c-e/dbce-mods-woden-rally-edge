# Supervised replay without foreground focus

**Qualified October 6:** request `27fb67943faa446caccd583242a78823` acquired with
`focused=False; backgroundAuthorized=True` and completed all 3,601 poses with
`unfocusedReplaySteps=3601`. Session-span.1 reported zero physical writes/Forza
sends, normal exit and exact restoration. This closes naturally unfocused
acquisition and full-reference continuation; it does not establish focus
transitions or the entire interruption matrix. See
[the current evidence and promoted install](2026-10-06-span-qualification.md).
The pending statements below describe the earlier focused-only checkpoint.

Claude's combined triple-screen Kenya runs reached the stage but never acquired
the car. A Windows prompt held focus in the first report; the retry had no
visible prompt or reported foreground obstruction, but still logged Unfocused.
The actual foreground HWND was not captured in that retry. The adapter reused
the ordinary wheel driving predicate, so it waited until its startup deadline
despite having latched physical output off. Moving focus was an unnecessary
unattended prerequisite; the visible window arrangement does not establish the
focus value seen by the game.

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
background condition still needs a live check. The next useful observation is a
naturally unfocused run, without changing focus or injecting OS input. No physical torque qualification
or complete pause/Stop/stage/device matrix is implied.

## Delayed 15:47 report and the remaining check

`artifacts/claude-triple-motion-02` is request
`76c07a3564c2426d93d3bba50e7bcc5d`. It failed at 20:45:15 UTC with zero replay
samples, then restored owner state at 20:45:24 UTC. Its log shows thousands of
local car callbacks and triples on at 7680x1440 while the ordinary force gate
reported Unfocused. This is the pre-fix failure, before the 21:08 installation,
not a failure of the later candidate. Do not erase it or count the subsequent
focused pass as background qualification.

After the delayed report, a read-only check verified all 13 currently installed
payloads against the 05837ee receipt. Keep that candidate and the current display
settings. Claude has the targeted runtime handoff, subject to the existing idle
gate and runner-owned rig lease; no competing launch was queued by Codex.

Claude's delayed 16:10 clarification supersedes the earlier request to move
focus with a UI helper: agents must not move focus or inject OS input. The
16:08 install and first replay were already completed. Do not repeat installation
or assume a subsequent natural launch will be unfocused just because older ones
were. Retain the next naturally occurring background result when a coordinated
run is otherwise needed. To qualify background acquisition, its log must show
`focused=False; backgroundAuthorized=True`, then
`unfocusedReplaySteps` greater than zero, all 3,601 poses including final readback,
normal exit and exact restoration with no force/network delivery. A run that
acquires focused and only later loses focus can qualify continuation, but cannot
qualify background acquisition. A fully focused pass leaves background coverage
open; do not force a transition or automatically repeat tests to obtain it.
The ordinary `ffb=Unfocused` state is expected while backgrounded;
use replay acquisition/pose results to judge playback, not the physical-force gate.
