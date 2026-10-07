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

## Owner work stream after 0.2.14-beta.1 (2026-10-06)

The owner approved shipping beta.1 to early testers and added three items:

1. **Bumper camera.** Owner: with one car the bumper view started inside the car; after moving it
   forward with numpad 8, both bumper corners were still clearly visible, as if the view were wider
   than 180°. Diagnosis (code and the 2026-10-06 log, no game run):
   - *Inside the car is a bug.* `MountedCamera.Pose` fits only the bonnet to the car body; the
     bumper uses one global pose for every car (default forward 2.2 m from the car origin). That
     car's body reaches 2.31 m forward, so 2.2 m is inside it. The owner's saved forward is now
     2.54 m, which will sit well ahead of shorter cars. Fix: fit the bumper to the front of the body
     bounds (plus a small margin), keep manual offsets on top of the fit, like the bonnet.
   - *The extra width is the FOV setting, not the projection.* This install has
     `[Triple] MatchGameFov = true` (chosen at the rig on 2026-10-04), so the centre screen shows the
     camera's own FOV (bumper 38° vertical; bonnet 70°) instead of the rig's real 33.6°, and the side
     screens continue at the same scale. With the sides turned 70°, the three screens then span about
     196° in the bumper view and about 244° in the bonnet view. With the rig's real geometry
     (`MatchGameFov = false`, eye 660 mm) they span 181°: the outer edges of the side screens are
     level with the eyes, so the bumper corners just beside a camera that has cleared the bumper
     belong at those edges. Owner chose (2026-10-06) real geometry for bonnet/bumper and the game FOV
     for chase cameras (`MatchGameFov = false`, `ChaseUsesGameFov = true`); bumper fit implemented.
2. **Normalize the FFB values** to the family scale: at Strength 50 forces should roughly match
   art of rally at 50 (toolkit STD-021, refines STD-003). Codex owns the tuning; the offline
   Strength 35 / cap 50 trial in `docs/FFB-COMPARISON.md` is the starting point; the owner's
   A/B at the rig is the acceptance.
3. **H-pattern shifting.** Gates select gears, out of gear is neutral, reverse works, on its own
   USB device (STD-020 H and DEV), as in #DRIVE Rally. Clutch routing is not implemented either.
   **Built 2026-10-07 (900f20c, hpattern.1), waiting for the rig.** Woden has no neutral or reverse
   gear, so out of gear cuts the drive and R swaps the pedals; gates step the game's own Shift
   up/down; needs the game's manual transmission. See CHANGELOG.

## Reference lessons carried forward

| Reference | Applied here | Still to implement |
|---|---|---|
| Toolkit | Pinned native/device and Forza artifacts, recording provenance, force shaping/lifecycle | Force profile adoption and physical lifecycle validation |
| Art of Sim Rally | Raw/derived telemetry, reset-safe acceleration, contact estimate, saved-rig calibration seed | Attended contact corpus and force tune |
| iRacing Arcade | BepInEx #788, per-game stripped interop, F6 settings/bindings, manual reconnect | Live driving-hook proof and complete mounted-view behavior |

Current reference docs were inspected on 2026-09-12. Their own verification limits remain theirs; no physical or runtime validation transfers automatically to Woden.
