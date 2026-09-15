# State — 2026-09-14

Handoff audit: [HANDOFF.md](HANDOFF.md) now records source/installation paths,
retained evidence and the next test. Read-only checks on 2026-09-14 matched all
nine installed 0.2.2 payload files and the existing package hash, and verified
dependency pins. No new game run or physical test occurred; code test results
below remain from 2026-09-13. Implementation baseline is `f3b5c0a` on `main`.

**0.2.2 development build installed for the next attended test.** 0.2.1's F6 UI, loader boot, device reading, stock Logitech ownership transfer and sustained car/contact sampling ran. The owner confirmed level entry after the WheelHit crash fix, then reported no effective throttle/brake/steering/camera response. 0.2.2 corrects the input boundary and builds camera cycling/handoff. No effective driving, new camera behavior or physical FFB is claimed verified yet.

## 0.2.2 changes and next test

Installed 2026-09-13 05:18 UTC with Woden closed. All nine payload files verified;
all pre-existing config hashes unchanged. Backup:
`BepInEx/WodenBackups/before-0.2.2-20260913-001820`.
Package SHA-256 `cfc0218c1a0d8a9f3fb9b518c546b077b11df26f8389c1d116fc5f7a1dea1554`.
The game was left closed; recording preference and owner bindings were preserved.

Native Controls.FixedUpdate reads the selected Game_Pad.PadActions, copies axes into MainCar's raw fields and dispatches buttons. The previous override of Controls fields around MainCar.FixedUpdate was too late. 0.2.2 temporarily writes the same selected action table around Controls.FixedUpdate, then restores it. Actions is a boxed value array: modified values must be assigned back through its indexer. Inputs now have applied-tick counters and the first action-route log identifies the expected axes.

Bonnet and bumper extend the native camera cycle without saving illegal stock preset indices. Look behind is a held camera action, not the game's unrelated L3 slot. Replay/photo/finish/inactive camera state, native transitions, changed native presets and external transform writers release the mounted camera and gate FFB. This is source/policy-tested behavior pending a real drive.

F6 now follows UX-1: Setup, Controls, FFB, Cameras, Telemetry, Help. Stop FFB remains visible; its latched status survives normal update gating. Existing config and binding keys remain compatible. UI layout changes have not yet been visually accepted. See [UX adoption](UX-ADOPTION.md).

Release build: zero warnings/errors; **14 suites / 405 assertions**, including native-cycle extension/skip/handoff policy checks. Schema: **199 definitions**, not a live coverage count. Added camera mode/view/ownership/transition/preset and applied-input tick channels. Raw native disassembly and owner data remain in ignored artifacts.

Next attended test: use Setup to check device bars, then start a short recording in Telemetry with FFB unstarted. Enter the same level; test all axes, Change camera and Look behind. Check `wheelTicks` in the log and camera/FFB state in the capture. Stop the recording before exiting. Only then perform a low-gain attended FFB test. The owner is away; no unattended force test or control injection substitutes for that check.

## Earlier 0.2.0/0.2.1 implementation and checks

This section preserves earlier evidence. The current 0.2.2 totals above are
14 suites / 405 assertions and 199 definitions; the older counts below do not
describe the current package.

- Private repository; supported Steam build 21802346 / Unity 6000.3.6f1 / metadata 39, guarded by exact GameAssembly SHA-256.
- Pinned BepInEx #788; 71 game-specific interop assemblies generated offline, and runtime generation completed on first actual launch. Empty UnityBaseLibrariesSource remains required.
- F6 panel: wheel/pedal calibration, inversion/deadzone, physical button capture, manual reconnect/raw diagnostics, FFB settings, bonnet offsets, telemetry ports/rate and live capture. F8 panic disarms output.
- Owner requested UI consistency: 0.2.1 replaces `ON Allow FFB` and similar text with labeled Off/On choices. No GameObject UI framework or input backend switch was introduced.
- Provisional front load/slip alignment estimate and calibrated steering damping. Shared toolkit shaping/output, exact FFB GUID, session arming, pause/panel/focus/stale/source gates, 150 ms hold watchdog, exit guards, zero-before-shutdown and ramp-in.
- 193 schema definitions, including reserved/unavailable channels. FFB preview/delivery/tuning and raw/calibrated physical inputs are retained. This count is not live channel coverage.
- Release build passes with warnings as errors. **13 executable suites / 396 assertions passed**, covering capture/persistence, reversed pedals, force symmetry/inversion/gain/cap/ramp/fade/invalid sources, motion, UDP, finite values, stale idle and complete recording. Late-start captures now use a capture-local time origin.
- Initial installer verified in a disposable fixture, then deployed to the actual game. 0.2.1 was deployed with the game closed, per-file payload hash checks and unchanged config hashes.

## Actual runtime evidence and crash

First actual launch loaded 0.2.0 and injected Lifecycle successfully. F6 opened and rendered at 1920x1080, showed R12 X/Z/Rz axis bindings and live centered/released readings; button page navigation and saved-setting changes were observed. No UI draw errors were logged. All four optional named UI engine bindings resolved, including stripped style/cursor setters.

The seeded local axes came from the owner's installed Art of Sim Rally settings: R12 GUID `d71b8350-61b7-11f1-8001-444553540000`, steering X with centre 32919, throttle Z and brake Rz with 0..65535 endpoints. These were imported values, not newly measured endpoints. Subsequent owner edits are preserved.

The log reported `Mod owns wheel route; 1 stock reader(s) suspended`. FFB remained disarmed during agent UI testing. The owner then tested the UI and changed settings; those preferences were preserved during the fix.

At 23:34 local, entering a level discovered player index 0 and crashed in `UnityEngine.WheelCollider.GetGroundHit`, followed by `Il2CppException.BuildMessage` access violation. The BE #788 wrapper generates WheelHit as a boxed value class containing a Collider reference, but offers an IntPtr-sized stack slot for its by-reference native result. Native code overwrites that slot. 0.2.1 `WheelContact.Read` allocates an actual IL2CPP WheelHit value, passes its unboxed storage directly to `il2cpp_runtime_invoke`, and retains its GC root. It does not use the broken generated out wrapper or guessed struct offsets.

0.2.1 boot and level entry are confirmed. The post-fix first-contact log reports **72-byte value storage, alignment 8**. Local ticks increased through 619, 1130 and 1640 over approximately 20 seconds at about 51 Hz; sample age remained 0.00–0.01 seconds. No sample/native-output errors were logged. The process then exited with `Stopped; outputWorkerStopped=True, recording=Disabled, drops=0`, without a new ErrorLog crash. A later UDP listener ran after that exit and received no packets, so no live payload/coverage claim is made from that attempt.

Previous plugin, configuration and crash logs were backed up under `BepInEx/WodenBackups/before-0.2.1-20260912-233855`. `update-receipt.json` records the package hash and backup. Raw game logs, personal bindings, references, caches and recordings stay out of Git.

## Remaining live gates

1. Record a short drive, pause, respawn and stage restart. Corrected level entry and sustained car/contact sampling are established, but the first run was not recorded.
2. Confirm the corrected Controls.FixedUpdate action-table route feeds the car and validate steering direction/travel, pedals, paddle actions and reconnect.
3. Validate contact-corner mapping, units, RPM/gear/clock scales, packet rates, capture coverage and SimHub interpretation. Sampling remains pre-physics-solve; no calibrated rack torque or slip-angle claims.
4. Attended low-gain FFB sign/feel, actual driver delivery and physical stop/resume behavior. Synthetic force tests and successful UI initialization do not transfer physical verification.
5. Bonnet/bumper position, clipping, restoration, native switching and camera-takeover FFB stop, including split-screen. H-pattern, analog handbrake, stock menu navigation, FOV/per-car views remain future work.

Toolkit v0.12.0 is still pinned, native component v0.5.0. Recording is a separately pinned unpublished extension, not part of that release. No public release or change to sibling repositories has been made.
