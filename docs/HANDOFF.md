# Session handoff — 2026-09-14

Start with [STATE.md](STATE.md), then this page. This handoff describes the
implementation at `f3b5c0afa8a31c4bcf748a6126da3eb0ff382be7` on `main` in the
private `d-b-c-e/woden-rally-edge-wheel` repo. Documentation-only commits may
follow it. Working folder: `E:\Source\woden-rally-edge-wheel`.

## What the next session inherits

The owner wants wheel controls, meaningful detailed telemetry, FFB, and Bonnet
and Bumper in the game's normal camera-button rotation. Game camera/player
takeover must release the mounted view and stop FFB. F6 settings and bindings
must converge on toolkit UX-1, including explicit `FFB: Off/On` wording. Keep
existing owner bindings and tuning through updates.

**Installed: 0.2.2 development build. Not yet accepted in a drive.** The owner
confirmed that 0.2.1 loads a level after the native contact-read fix, then reported
no steering/throttle/brake/camera response. The next test is of 0.2.2's corrected
input boundary, not a repeat of the already diagnosed crash. F6/device reads and
sustained car/contact sampling were observed on earlier builds; that does not
validate the new controls, cameras, UI layout or physical FFB.

## Resume at the installed build

Game root: `D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge`.
Steam app 3218630, build 21802346, Unity 6000.3.6f1, IL2CPP metadata 39.
The exact supported GameAssembly hash is in [development instructions](DEVELOPMENT-BUILD.md).

Paths below are relative to the game root unless marked as repository paths:

| Evidence / data | Location |
|---|---|
| Installed plugin and update receipt | `BepInEx/plugins/WodenRallyEdgeWheel/update-receipt.json` |
| Pre-0.2.2 plugin/config/log backup | `BepInEx/WodenBackups/before-0.2.2-20260913-001820` |
| Pre-crash-fix backup | `BepInEx/WodenBackups/before-0.2.1-20260912-233855` |
| Settings | `BepInEx/config/dbce.wodenrallyedgewheel.cfg` |
| Bindings and previous version | `BepInEx/config/wheel-bindings.json` and `.bak` |
| Runtime / crash logs | `BepInEx/LogOutput.log`, `BepInEx/ErrorLog.log` |
| Owner recordings | `BepInEx/WodenRecordings` |
| Repository package | `dist/WodenRallyEdgeWheel-0.2.2-dev.zip` |
| Repository staging manifest | `dist/stage-11599d50fddd4f3e839fc6b51cd3f378/manifest.json` |

On 2026-09-14, read-only checks matched all nine installed payload files to that
manifest and matched the ZIP to the hash in STATE. The receipt records the
2026-09-13 05:18 UTC installation and preservation of the configuration. Logs
still predate that installation; they are not 0.2.2 runtime evidence. No game was
launched or input/force applied for this documentation audit.

`tools/Install-Dev.ps1` is an **initial installer**, not an updater. It deliberately
refuses the existing installation. The reviewed 0.2.2 update was performed by
ignored `artifacts/update-0.2.2.ps1`: closed-game/build/path checks, per-file
manifest verification, backup outside plugin scanning, configuration hashes,
replacement with rollback on caught failure, then a receipt. It contains fixed
stage/version paths and is historical tooling, not a reusable player updater.
For a future build, review a new update plan against the actual receipt and
configuration; do not bypass the initial installer's refusal or replay the old
script blindly. A complete transactional player updater remains unfinished.

## First attended test

1. Use the installed build; no reinstall is needed. Open F6, confirm version and
   saved device readings, and leave FFB unstarted. Saved FFB On does not arm it;
   arming is session-only. All three axis bindings and Wheel controls On are
   required for the current input override.
2. Start a short recording in Telemetry, close F6, and enter the same level.
   Verify steering, throttle and brake, then paddles, Change camera and held
   Look behind. Device bars alone do not prove the game received input.
3. Check `wheelTicks` and the first `Wheel action-table route active` log. Compare
   `wheelRaw.*`, `wheelInput.*`, `wheelInput.appliedTicks`, `controls.*` and
   `game.*` in the capture. If ticks stay zero, inspect player/driving gates,
   selected pad, binding readiness and device reads before moving the hook again.
4. Cycle stock → Bonnet → Bumper → stock, disable either added view, and test
   pause, reset, finish, focus loss and game camera takeover. Record visual
   placement and ownership behavior separately from input success.
5. Stop and inspect the capture. Only after input/camera checks, perform the
   attended low-strength FFB sign/stop/resume test in DEVELOPMENT-BUILD. F8 is the
   latched stop. Never hard-kill the game with live output.

Record exact package/hash, car/mode, device, actions, symptoms, log/capture paths
and results in STATE. Do not turn an untested item into a pass based on compilation.

## Implementation map and traps

| Area | Start here | Preserve |
|---|---|---|
| Controls | `src/WodenRallyEdge.Plugin/Plugin.cs`, `WheelInput.cs` | `Controls.FixedUpdate` scoped action-table lease; boxed values must be written back to the array and restored. The old MainCar input hook was too late. |
| Native contact crash | `src/WodenRallyEdge.Plugin/WheelContact.cs` | GC-rooted, runtime-allocated 72-byte IL2CPP WheelHit storage. Never call the generated broken `out WheelHit` wrapper. |
| Cameras | `src/WodenRallyEdge.Core/CameraCycle.cs`, plugin `MountedCamera.cs` | Legal stock indices, native camera action, held rear look, foreign-writer handback and FFB ownership gate. |
| FFB | Plugin `ForceController.cs`, `StockWheelOwner.cs`; core `ForceSignal.cs` | Exact GUID, stock Logitech shutdown, session arm, all lifecycle gates/watchdog/exit cleanup; load/slip estimate is not rack torque. |
| Telemetry | Plugin `GameSampler.cs`; core `TelemetrySchema.cs`, `TelemetryOutput.cs`, `ForzaProjection.cs` | Pre-next-solve phase, absent vs zero, unverified game scales/corners, raw vs applied input and delivery evidence. |
| UI | Plugin `Panel.cs`, `UiNative.cs`, `Settings.cs` | Stripped Unity bindings and existing stored keys; current UX gaps are in UX-ADOPTION. |

Native action indices are recorded in `InputLease`: throttle 7, brake 6,
right/left steering 16/17, and button mappings alongside them. Rear view must
remain a mod camera action; the game's L3 slot is unrelated.

## Rebuild and retained evidence

Use the exact commands in [README](../README.md) and [AGENTS](../AGENTS.md).
`Initialize-Dependencies.ps1` downloads the pinned BE #788 loader and regenerates
Woden interop if absent. Keep `UnityBaseLibrariesSource` empty. Never reuse
another game's interop. Toolkit v0.12.0/native v0.5.0 and the separate unpublished
Recording DLL are committed pins; a dirty toolkit checkout is not a build input.

The last code checks remain **14 executable suites / 405 assertions**, zero
build warnings/errors, installer fixtures and 199 schema definitions. This
documentation audit verified dependency hashes but did not rerun the code suite.

Ignored local `artifacts/` holds native disassembly (`Controls.FixedUpdate.asm`,
`MainCar.*.asm`, `Car_Cam.*.asm`), API inspections, fixture output and the one-off
update helper. `disasm_inputs.py` also references a temporary metadata project
under `%TEMP%/dbce-woden-research-20260912`; it is not a portable bootstrap.
These are supporting investigations, not required build inputs. The important
conclusions and fixes are retained in source, STATE and [RESEARCH](RESEARCH.md).
`artifacts/live-detail-20260912-234109.jsonl` is **empty**: the listener ran after
the game exited. It is not a successful capture.

Personal recordings, logs, generated interop, caches and ZIPs are intentionally
not in Git. A fresh clone can rebuild against the supported installed game but
does not contain that local runtime evidence. Preserve it on this machine.

Use [UX adoption](UX-ADOPTION.md), [telemetry semantics](TELEMETRY.md),
[force model](FFB-MODEL.md) and [roadmap](ROADMAP.md) for the remaining work.
The shared [UX-1](https://github.com/d-b-c-e/dbce-wheel-mod-toolkit/blob/master/docs/CONSUMER-UX.md)
is published in toolkit commit `04a97cf`; it is the family baseline, not proof
that this panel or the sibling mods fully implement it. The local toolkit
checkout has unrelated uncommitted work: do not reset, rebuild its moving
Recording source, or absorb that work into this consumer's handoff.
