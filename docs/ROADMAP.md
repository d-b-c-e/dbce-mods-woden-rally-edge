# Feature baseline and priorities

The owner asked for the wheel toolkit, art-of-sim-rally and iracing-arcade-wheel as feature baselines, with detailed and meaningful telemetry emphasized. These are implementation goals, not claims that sibling code can be copied unchanged into Woden. [woden-rally-edge-wheel, 2026-09-12]

## Milestone 1 — prove the telemetry seam

The compiled foundation includes the Woden sampler, per-channel units/provenance, conservative Forza mapping, detailed UDP and bounded numeric capture. Prove it live and establish an evidence corpus before tuning anything from it. Resolve player identity and sampling phase first; use actual recordings to interpret RPM/gear/timing scales. A minimum useful validated dashboard should include speed, normalized engine speed once proven, pedals, gear, race state and timing. The expanded stream should retain per-wheel contact, slip, commanded torque and body motion.

The 0.2.12 candidate adds exact request/source/config/profile identity, verified model-3 baseline reprocessing and separate immutable tuning trials under recorded-playback v1. Offline fixtures prove contract compatibility and candidate comparisons; the next attended owner drive must supply the first recording with actual driving coverage. Treat structural validity, coverage, reproducible model-preview requests, delivered native output and physical acceptance as separate gates.

## Milestone 2 — reliable rig controls

0.2.3 adds Button/Axis handbrake selection, calibration and proportional native
rear braking/grip loss. The game's power cut remains binary. Validate partial,
full and released input in a drive; H-pattern remains future work. 0.2.8 adds bound panel/Unity menu navigation, with actual screen coverage pending.

Build an owner-facing setup panel with exact-device selection, raw-axis display, steering centre/endpoints, separate pedal calibration, inversion/deadzone, binding capture, persistence and reconnect status. Support separate USB devices, paddle/H-pattern shifter, handbrake and stalk buttons. Route menus and camera actions explicitly. Keep input changes confined to selected player controls; never switch the game's input backend or alter assists behind the UI.

The 0.2.0 F6 panel implements direct axes, calibration, button capture, persistence and manual reconnect. Live R12 reads and stock Logitech ownership transfer have been observed in Woden. Effective driving input, paddle actions and reconnect under a loaded force effect still need a drive. H-pattern remains future work; 0.2.8 stock-menu routing uses selected Unity UI handlers and still needs an in-game walk.

## Milestone 3 — meaningful force feedback

0.2.3 removes per-session arming, retains the connection through transient gates,
and latches connection errors instead of looping. Confirm delivery/sign/feel and
normal recovery live. Agent-launched recordings now include FFB status markers
and timing to investigate remaining hitches; shifting causation is unproven.

The 0.2.0 provisional model estimates alignment from front load and sideways slip, adds steering damping, and uses the toolkit output lifecycle and conditioning. Synthetic tests cover sign symmetry, inversion, gain, hard cap, ramp, fade and invalid sources. Obtain a real contact corpus and validate sign/load normalization during an attended low-gain test. Shared profile-data adoption and richer force suppression markers remain future improvements.

Required output behavior: pause/focus/menu/respawn/replay/stale suppression, watchdog and exit guards, zero before the game window disappears, strict GUID/device ownership, virtual-device rejection, panic stop and ramp-in. Do not fix shaker complaints by retuning steering torque. Keep wheel force and telemetry-driven shaker channels independently diagnosable.

## Milestone 4 — mounted views and product polish

0.2.2 builds bonnet/bumper in the native camera cycle, held rear look and camera-takeover FFB suppression. 0.2.4 adds body-fitted bonnet placement, global manual offsets/FOV, shorter near clip with restore and rebindable tuning controls. Validate those across cars and game modes; saved per-car manual presets remain. Test split-screen targeting explicitly. 0.2.8 implements the UX-1 Simple/Advanced split and transactional player install/update/uninstall. A complete support archive remains future work; Help currently writes a local summary.

## Optional time-limit assistance — 0.2.6

The owner requested a more forgiving countdown/time limit. Advanced → Driving in 0.2.8 exposes an opt-in saved countdown rate without changing elapsed lap/stage times or vehicle physics. Native countdown hook and settings tests pass. Confirm the visible timer rate, checkpoint additions, pause/restart, expiry and persistence during an attended single-player run.

## Reference lessons carried forward

| Reference | Applied here | Still to implement |
|---|---|---|
| Toolkit | Pinned native/device and Forza artifacts, recording provenance, force shaping/lifecycle | Force profile adoption and physical lifecycle validation |
| Art of Sim Rally | Raw/derived telemetry, reset-safe acceleration, contact estimate, saved-rig calibration seed | Attended contact corpus and force tune |
| iRacing Arcade | BepInEx #788, per-game stripped interop, F6 settings/bindings, manual reconnect | Live driving-hook proof and complete mounted-view behavior |

Current reference docs were inspected on 2026-09-12. Their own verification limits remain theirs; no physical or runtime validation transfers automatically to Woden.
