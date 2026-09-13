# State — 2026-09-12

Initial **0.1.0 development foundation**. Steam build 21802346 has been inspected on disk. The game has not been launched, modified or driven during this implementation. No force was applied. No claim of working in-game wheel support is made.

## Implemented and checked

- Private GitHub repository and local checkout.
- Offline Cpp2IL/Il2CppInterop generation: 71 Woden assemblies, Unity 6000.3.6f1 / metadata 39. The generator reads game files and does not load the native game DLL for execution.
- Complete solution builds with warnings treated as errors against those references.
- Toolkit v0.12.0 native/managed binaries with manifest hashes; separately vendored unpublished Recording DLL with source hashes and base revision.
- Game-specific Harmony sampling, detailed finite channel schema, motion derivation, Forza mapping, UDP worker, bounded recording and inspector.
- Experimental exact-GUID physical axes and bonnet view, both disabled by default.
- Offline regression harness: **9 suites / 69 assertions passed**. Validates calibration, coordinate frames, reset gaps, finite values, conservative mappings, full JSON size, real loopback UDP, stale idle packets and recording roundtrip.
- Initial installer passed the disposable-folder test: loader/config/plugin layout, repeat-install refusal and preservation of the existing plugin. Inspector accepted the complete synthetic session and returned exit code 2 for the missing-footer fixture.
- `dist/WodenRallyEdgeWheel-0.1.0-dev.zip` built with an allowlisted plugin payload and **173 channel definitions**, including explicitly reserved/unavailable fields. A schema definition count is not a live channel coverage claim.

## Not verified in Woden

Loader boot, component injection, Harmony call counts, player-index selection, scene transitions, race/pause/replay/photo/respawn gates, actual packet rate, wheel-corner geometry, drive units, numeric recording under IL2CPP, wheel input order/feel, camera placement/restoration/culling and SimHub interpretation.

The installer is supplied for the initial development install. The actual game remains clean. Installer verification uses a disposable fixture under `artifacts/`, never the real game directory.

## Next work in order

1. Install the development build with the game closed; first launch with wheel/camera/recording off. Prove loader boot and injected Update counters. Read discovered player indices; keep exact selection.
2. Drive one short stock-controls session with recording enabled. Confirm hook rate, phase, player identity, pause/respawn/restart behavior and packet freshness.
3. Interpret RPM/gear/clock/pedal scales using stationary, acceleration, braking, shifting and reverse recordings. Compare with the stock HUD. Validate Unity distance/speed scale before claiming calibrated SI. Promote proven fields into Forza individually.
4. Recover suspension centre/distance without guessing; investigate a post-physics sampling point and scene/respawn identity. Validate wheel airborne/contact transitions and hub displacement.
5. Test the experimental axes on the R12 and pedals; record raw/configured/applied input. Investigate stock Logitech reader activation/ownership and whether it competes. Build an interactive binding/calibration UI and reconnect flow.
6. Validate bonnet placement for several cars, transitions, rear view, photo/replay and split-screen; add per-car offsets, bumper, FOV and restore controls.
7. Choose a documented FFB signal after examining real front-wheel contacts. Build a no-output signal preview and replay tests before attended low-gain force testing through the toolkit.

Do not enable public releases until the live gates pass and the unpublished recorder dependency has an acceptable published pin or explicit release policy.
