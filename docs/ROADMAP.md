# Feature baseline and priorities

The owner asked for the wheel toolkit, art-of-sim-rally and iracing-arcade-wheel as feature baselines, with detailed and meaningful telemetry emphasized. These are implementation goals, not claims that sibling code can be copied unchanged into Woden. [woden-rally-edge-wheel, 2026-09-12]

## Milestone 1 — prove the telemetry seam

The compiled foundation includes the Woden sampler, per-channel units/provenance, conservative Forza mapping, detailed UDP and bounded numeric capture. Prove it live and establish an evidence corpus before tuning anything from it. Resolve player identity and sampling phase first; use actual recordings to interpret RPM/gear/timing scales. A minimum useful validated dashboard should include speed, normalized engine speed once proven, pedals, gear, race state and timing. The expanded stream should retain per-wheel contact, slip, commanded torque and body motion.

## Milestone 2 — reliable rig controls

Build an owner-facing setup panel with exact-device selection, raw-axis display, steering centre/endpoints, separate pedal calibration, inversion/deadzone, binding capture, persistence and reconnect status. Support separate USB devices, paddle/H-pattern shifter, handbrake and stalk buttons. Route menus and camera actions explicitly. Keep input changes confined to selected player controls; never switch the game's input backend or alter assists behind the UI.

Experimental direct axes exist now. Calibration UI, button mapping and reconnect recovery do not. Verify the game's stock Logitech reader before deciding whether it should be disabled under explicit mod ownership.

## Milestone 3 — meaningful force feedback

Use the existing toolkit output/lifecycle/profile implementation. Investigate native Logitech behavior and obtain one force owner. Review front-wheel slip/contact-force records; neither WheelHit.force magnitude nor sidewaysSlip is already an aligning torque. Develop an explicitly labelled estimate if no signed force can be recovered. A no-output preview should report source validity, sign, load, gain, clipping and suppression reason. Validate sign and saturation offline against recorded left/right turns, then conduct an attended low-gain test.

Required output behavior: pause/focus/menu/respawn/replay/stale suppression, watchdog and exit guards, zero before the game window disappears, strict GUID/device ownership, virtual-device rejection, panic stop and ramp-in. Do not fix shaker complaints by retuning steering torque. Keep wheel force and telemetry-driven shaker channels independently diagnosable.

## Milestone 4 — mounted views and product polish

Validate the experimental bonnet transform across cars and game modes. Add bumper view, per-car offsets, FOV and near-clip tuning, rear look, reliable stock restoration and camera actions. Test split-screen targeting explicitly. Then add a compact settings UI with controls, camera, telemetry/capture and force diagnostics; package transactional updates/backups and support bundles.

## Reference lessons carried forward

| Reference | Applied here | Still to implement |
|---|---|---|
| Toolkit | Pinned native/device and Forza artifacts, separate recording provenance, finite channels, no duplicated DirectInput layer | Force profile adoption and physical output lifecycle |
| Art of Sim Rally | Raw-vs-derived telemetry, reset-safe acceleration, contact evidence discipline, independent shaker/steering reasoning | Attended corpus, contact-based force model, rich calibration/settings UX |
| iRacing Arcade | BepInEx #788 / .NET 6, no Unity unstripping, per-game interop generation, minimal injected component, hook counters | Live hook proof, bindings/actions UI, reconnect and complete mounted-view behavior |

Current reference docs were inspected on 2026-09-12. Their own verification limits remain theirs; no physical or runtime validation transfers automatically to Woden.
