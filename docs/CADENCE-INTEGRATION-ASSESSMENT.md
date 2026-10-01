# Woden cadence integration gates

Source-only assessment, 2026-10-01. No production integration or physical acceptance is claimed.

## Exact sources

- Woden baseline: `4e9d2c4aa0a3c98c462df9c8c46bcbd65d32bd08`.
- Reviewed shared cadence implementation: `4ef65c160146a847e9c88a51dda1dfc746db141d`, isolated toolkit checkout, not live toolkit WIP.
- Reviewed delivery ZIP remains unchanged: SHA-256 `86817054d050d8ee5c0c098fab6042d81e302ef2cd98b19427fcb52ea291b90a`.

## Missing collision producer

The production plugin source has no collision callback or event producer. `GameSampler.cs` supplies motion, game state and wheel contacts; `TelemetrySchema.cs` has no verified collision timestamp, signed direction, normal relative speed or calibrated severity channel. Wheel contact normals and contact-force magnitudes describe ground contacts. Finite-difference body acceleration does not identify a collision or distinguish braking, road contact, a reset and a crash. These inputs must not be relabeled as verified impacts.

`ForceSignal.cs` produces contact-weighted alignment and steering damping. `ForceController.Tick` sends its shaped preview through the existing guarded device route. There is no collision-specific demand to connect to `CadenceImpactMixer.Trigger`. Consequently impact input must remain unverified and disabled; no collision fixture can establish a missing production producer.

## Shared dependency gate

Woden Core references its pinned `components/wheel/lib/toolkit/dotnet/Dbce.Wheel.Ffb.dll`. The reviewed cadence class belongs to the shared `Dbce.Wheel.Ffb` project and calls the shared `ImpactMixer.EnvelopeIntegral`. Replacing that pinned assembly with a full newer toolkit build is a dependency migration, not a drop-in consumer edit. Preserve the reviewed HAT/input override and native API provenance when coordinating it. Do not copy either shared algorithm into a per-game source fork. A separately built shared-owned pure assembly would also require an independently reviewed shared packaging change.

## Required adapter behavior after dependency resolution

Use adjacent monotonic **output-call** intervals, not simulation time or recorded sample elapsed time presented as actual device-call timing. The shared API rejects nonfinite signals, nonpositive intervals, discontinuous intervals and intervals above 50 ms. Preserve saved strength, inversion, smoothing, peak cap, lifecycle gates and the 150 ms native hold watchdog. Impact remains disabled until a verified producer and calibration exist.

Every failed `Trigger`, rejected `Mix`, pause, stale sample or invalid signal must invoke the existing guarded zero route immediately. Resetting mixer state alone cannot stop a previously accepted native command. Zero bypasses slew limiting. The current controller already routes a blocked active session through `Suspend` and `ZeroAndStop`; a new mixer must not bypass that route. Repeated already-suspended calls need not submit another command because the native effect is stopped.

Final output-clock limiting changes some structural outputs even without impacts. Existing recorded comparisons observed 190 differing output-clock rows, maximum normalized difference 0.0087415241; they are candidate arithmetic comparisons, not an adopted waveform or physical calibration. Keep them separate from unchanged legacy output and from synthetic impact fixtures.

## Next bounded implementation milestone

1. Coordinate one shared dependency artifact exposing the reviewed API while preserving the input/native pins; review its exact source and binary provenance before changing Woden references.
2. Add an injectable monotonic output clock at the actual Woden controller seam, and wire structural preview through the shared limiter without inventing collision events.
3. Replay the actual controller against recorded inputs and a fake device. Compare legacy and candidate accepted commands; cover pause, stale clock, rewind, nonfinite signal, failed writes, resume and shutdown, including immediate guarded-zero assertions.
4. Keep impact disabled. A later game-specific producer needs source evidence, signed normalized fixtures and rig acceptance before activation.

The production integration is blocked by the dependency gate; collision activation is independently blocked by the missing producer. This assessment does not alter settings, force caps, runtime binaries, the approved package, live checkout ownership or release status.
