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

Status: source candidate builds with zero warnings; 31 device-free protocol
checks and 40 exact compiled metadata seams pass. No addon installed or launched;
no Woden raw-input qualification claimed. Review, fake lifecycle checks, the
supervised recovery runner and actual runtime hooks are still owed. Woden's
existing InputPolling already samples once per frame at early consumers.

```
dotnet run --project tools/controls/ProtocolTests -c Release
dotnet run --project tools/controls/VerifyHooks -c Release -- .
dotnet build tools/controls/Probe/Woden.ControlsProbe.csproj -c Release
```

The cold files are under `%LOCALAPPDATA%/dbce/super-woden-rally-edge/`.
The evidence-directory mailbox is `command.json` -> `command-N.json` plus a
nonce/sequence-correlated `reply.json`. The native client is vendored with an
exact source/hash in `Probe/VENDOR.md`. It has no external/private dependency at
runtime. A command is archived before submission; a missing reply means unknown
completion, never permission to retry it.
