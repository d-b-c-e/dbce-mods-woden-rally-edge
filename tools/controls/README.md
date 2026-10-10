# Production Apply and raw controls qualification

This developer harness is separate from the player package. Its intended proof
is Wheelkit production Apply -> the installed consumer reads those files -> an
independent raw device sample -> the game's menu or car responds. A trajectory
replay, an action synthesized after binding lookup, or a successful native
submission does not pass that chain.

The candidate will use the shared toolkit `NativeTestInjectionClient` and an
explicitly hash-pinned native test build. The installed player native pin stays
unchanged outside the bounded run. No physical force or network motion output
is allowed, including on invalid request, timeout, or stopped injection.

## Adapter seams

- A separate BepInEx developer addon depends on `dbce.wodenrallyedgewheel`.
  There is no player configuration option for raw injection.
- A cold `inject.on` plus a nonce-bound request identifies exact plugin, Core,
  config, bindings and native hashes and a new absolute evidence directory.
- Arm the native irreversible no-force fence before any force call. Refusal is
  terminal. Retain managed force-open refusal and latch Woden's existing
  `StagePlayback.Target.MuteOutputs()` for force/network/progression protection.
  Keep stage replay/diagnostic driving and the normal DevInput channel inactive.
- Patch observation only at `DeviceHub.Poll`, `DeviceHub.TryAxis`,
  `WheelInput.Button`, `WheelInput.Apply`, and the normal runtime tick. The first
  two show raw delivery and normalized values; `Apply` shows the actual input
  lease used by Controls. Record real selected UI/camera separately. Never call
  a consuming button method just to observe it.
- Refuse binding/calibration edits throughout the process. Keep normal focus,
  pause, ownership, countdown and settings guards. No OS input, focus change,
  virtual pad, display mode override or replacement car input.
- Bound request/command size, commands, duration and trace. Commands carry the
  run nonce and monotonic sequence; require creation after the process started.
  No blind retry when command completion is unknown. Stop closes readers to
  drop native samples, while the force/progression mute remains latched.

## Required checks before a run

Source/fake-native review, exact game/mod metadata hook check, bounded command
tests (stale/wrong nonce, duplicate, timeout, malformed and oversize), independent
output config mute, shared rig lease and owner-idle gate. Freeze the production
Wheelkit harness and original profile; generate raw inputs from that profile,
not the game's translated store. Snapshot recovery to disk before mutation.

The runner must hold the lease until the matching process exits and restore
configs, payloads, saves, raw Unity registry state and original logs exactly.
Unexpected process identity or incomplete restoration retains a recovery record
and fails the run. Input results and restoration results remain separate.

Status (October 10): source candidate builds with zero warnings. 31 protocol
checks, 40 exact compiled metadata seams and 97 assertions against the actual
addon callbacks pass. The latter use fake engine/native boundaries and real
mailbox files, covering unknown completion, malformed/stale commands, expiry,
lost native latch, callback faults, deferred reader closure and retained mutes.
28 file-restoration and 50 production recovery checks pass in isolated trees.
The recovery fixture stubs only the registry boundary, never touches owner data,
and covers partial restore, corrupt backups and changed lease/request ownership.
No addon installed or launched; actual Harmony callbacks and raw input in Woden
remain unqualified. Woden's existing InputPolling already samples once per frame.

```
dotnet run --project tools/controls/ProtocolTests -c Release
dotnet run --project tools/controls/LifecycleTests -c Release
dotnet run --project tools/controls/VerifyHooks -c Release -- .
dotnet build tools/controls/Probe/Woden.ControlsProbe.csproj -c Release
pwsh -NoProfile -File tools/controls/Test-OwnerFiles.ps1
pwsh -NoProfile -File tools/controls/Test-Recovery.ps1
pwsh -NoProfile -File tools/controls/Test-ApplyCheck.ps1
```

The cold files are under `%LOCALAPPDATA%/dbce/super-woden-rally-edge/`.
The evidence-directory mailbox is `command.json` -> `command-N.json` plus a
nonce/sequence-correlated `reply.json`. The native client is vendored with an
exact source/hash in `Probe/VENDOR.md`. It has no external/private dependency at
runtime. A command is archived before submission; a missing reply means unknown
completion, never permission to retry it.

`Run-Controls.ps1 -Result <new-path> -NativeCandidate <dll> -NativeSha256 <sha>`
builds Wheelkit's production qualification harness from the reviewed full commit
`8c7243ec4c7d11d992b99ba8009bc6d790679595` in a new git archive. `writer.json`
pins the source archive, catalog and built files. It deliberately stages wrong
steering and Confirm indexes after saving owner state, applies the original
selected profile to an isolated fixture and the live files, and compares both
previews and resulting bytes. Independent assertions require those two original
profile bindings to be repaired. It then writes the independent raw workload.
`Test-ApplyCheck.ps1` executes this production path on synthetic files only.
The runner checks output mute
again after Apply and before the plain Steam launch. It never sends controls
automatically. Inspect a current game-window frame before each `Send-Command.ps1`
call. A missing reply is terminal for the sender; do not clear pending-command
or retry. `-Recover -Result <same-path>` is for a closed game only, validates
the recovery scope/hashes and retains the lease on incomplete restoration.

Main plugin load calls Runtime.Start (output construction and nonexclusive
DeviceHub reads), not Force.Prepare; force opening is in the later Runtime.Update
path and checks the independently disabled setting. The addon patches Initialise
before reflecting game-specific seams. Native Arm still refuses if any force
attempt occurred first. DeviceHub.CloseReaders calls native CloseRead and clears
its lists; it is safe when Runtime.Stop subsequently closes the same empty hub.
No real force is used to validate these source properties.

The cold five-minute expiry also bounds supervision; load time does not extend
the session. Initial Windows process metadata error 299 is retried only before
identity admission. Later command identity failures remain terminal. Recovery
recreates deleted owner directories and removes only newly created empty ones.
This input-only run does not switch display profiles or reuse the older display
watchdog that kills processes by name and switches the global topology.
