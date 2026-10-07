# Grip v4 source and fake-device review

**Follow-up:** the three reproduced defects are fixed in `8bc3444` and pass
the independent checks on `16f0f9c`. Core Grip replay and the later stage/export
dispatch correction are covered in the [October 7 follow-up](2026-10-07-grip-stage-followup.md).
The findings below retain their original candidate identity; physical acceptance
and a new live-physics Grip capture remain pending.

Reviewed `82f2812f5d552c941d0ac51e45626df7b03c8737`, including `4ec62c1` and
`ca5e856`, following Claude's October 7 approximately 01:00 CT handoff. The
subsequent GameSampler isolation change `66d46e8` was excluded from the frozen
source. No package, install, game/Steam launch, device initialization, physical
output, display, focus or input action accompanied this review. Claude retains
runtime implementation ownership.

**The direction is sound, but fix the low-speed damping and malformed-input
cases below before an owner A/B. V4 replay and normalization remain unfinished.**

## Confirmed behavior

The unmodified executable regression harness independently passes **47 suites /
37,821 assertions**. The actual-source UI fixture passes **644 assertions**.
Eight extra review cases pass: five verify intended behavior and three reproduce
defects. With those cases, the harness reports 55 suites / 37,837 assertions.
Use `dotnet run` for these executable harnesses; `dotnet test` does not run them.

- The adapter now names its friction evaluator as an estimate, preserves signed
  per-wheel cancellation and uses a fixed slip mapping instead of an ineffective
  IdealSlip control. Existing tests cover opposing/unequal/one-wheel inputs.
- A 0.5-second recovery ramp starts at zero. Added actual-controller checks show
  focus loss stops the fake device, recovery ramps again, and a new car clears
  the reference and starts with zero output.
- Physical muting still learns the production driving-load reference. An
  independent analysis model sharing that reference reaches -0.4 in the synthetic
  peak-slip case while production preview stays zero and the fake device has
  **zero opens and writes**. This exercises the two real model classes and their
  controller reference sharing; it is not execution of Unity's StagePlayback hook.
- Event-time crash strength survives a Strength50 contact followed by a tick at
  Strength20: recorded arrival50/current20, cue0.65915495. This tests production
  capture, not complete offline crash reconstruction.
- An old config with Strength49.583332/Peak25 and no Model retains Classic after
  an actual Settings save/reload. Existing configuration is not silently migrated.
- The untouched historical Kenya stage source passes strict `stage-review`:
  **3,601 rows, 3,553 valid force samples, 3,477 nonzero, zero replay error**,
  zero recorded delivery attempts. All seven source/reference files remain
  byte-identical before/after. This is v3 compatibility evidence.
- A complete, explicitly synthetic 160-row v4 capture is correctly refused by
  `RecordedForceReplay` (`Unknown JSON key: loadRatio`), before a case is written.
  The old reader cannot falsely certify it as Classic. V4 support is deliberately
  absent at this candidate; this is a limitation, not a newly discovered regression.

## Fixes before physical comparison

### High: low-speed damping bypasses the force fade

`GripSignal.Evaluate`, around line150, adds damping after AxleForceCurve's speed
envelope. After reference and ramp warmup, a stationary sample with zero slip
and steering0 to0.2 over20ms produces **-0.5** and writes that value to the fake
device, while reporting `low-speed fade`. Strength50/Damping0.05 are used;
GripSmoothing0 isolates the damper. Classic returns0 for the same sequence.

The new damper can therefore load the wheel while the car is stopped. Preserve
the intended low-speed policy for damping as well as tyres, without applying
the tyre envelope twice. If stationary damping is deliberately wanted, make it
an explicit separately qualified behavior rather than an accidental side effect.
Keep saved tuning and Art's reference fixed. Test zero speed, the fade band,
Strength0/50/100, invert, recovery and release through the actual fake sink.

### Medium: rejected loads still qualify the reference

`GripSignal.Evaluate`, around line106, calls `Reference.Observe` before validating
each grounded contact. Its preliminary accumulation checks finite values, but
not nonnegative force magnitudes. A negative left load can be concealed by the
right load so the total remains positive.

Reproduction: after the initial sample, 110 rows with FL=-4999 and FR=5000 all
return `invalid front contact`, yet qualify **reference1 / Kind=Driving**. The
next genuine10000-load row leaves the running reference at91.081 and saturates
the tyre target at-1. The recovery ramp still applies, but the reference remains
contaminated. This is injected malformed data, not an observed game load.

Validate both grounded-wheel loads before accumulating; reject nonfinite,
negative or unrepresentable totals and ensure invalid intervals cannot qualify
the mean. Keep calibration independent of physical delivery gates. Reference
learning need not require unrelated telemetry or force-curve channels, but it
must require valid inputs to its own computation.

### Medium: negative friction values reverse a valid request

The curve checks near line123 require finite extremum/asymptote values but allow
negative magnitudes. In the same positive-slip0.2 case, extremumValue=-1 and
asymptoteValue=-0.75 yield `Valid=true` and **+0.4** at the fake device instead of
the normal-0.4. These are deliberately malformed inputs, not observed game data.

Reject negative curve magnitudes, require finite knot positions, and check
intermediate values and conversion to the shared float model. Preserve valid
zero stiffness/magnitudes and genuine signed-wheel cancellation. Invalid curve
data must stop the selected model instead of silently reversing its direction.

## Replay and documentation still needed

The driving-load mean is an explicit game-specific policy change from the earlier
resting-reference proposal. It is labelled in code and can be tested offline;
it is not itself proof of a matched Art Strength50 tune. The old Kenya source has
no friction-curve channels and cannot serve as original v4 input. Any exercise
using assumed curves must stay labelled synthetic/assumed-input.

Complete v4 dispatch in `RecordedForceReplay`, `StageCaptureReview` and export,
including original reference state, model/car/tune/curve/reset boundaries,
selected actual and enabled analysis streams, and eligibility that excludes
unavailable/provisional reference intervals. Reconstruct ordered crash contacts
and sampled cue state separately; a Classic steering pass is not complete crash
validation. Carry over iRacing's unknown-seed/overflow/event-time-setting cases,
preserving Woden's float steering-plus-cue composition. The sampled
`crash-constant-fallback@2` remains distinct from Art's native finite effects.

Update the player panel, Settings descriptions and TelemetrySchema: they still
say reference is measured at rest / kind2 is resting, although it now means
driving mean. The panel also says `50% matches art of rally's 50%`; this has not
been calibrated or physically accepted. Describe the shared-model goal and
experimental estimate honestly. Correct GripSignal's stale straight-segment
remark. These content corrections belong with the runtime implementation.

All25 ledger IDs now have adoption rows. STD-016/017/020/022 remain **unchecked**
(telemetry audit, empty-page audit, feature checklist, triple selector), outside
this force review. STD-021/025 remain partial with this scoped evidence.

## Frozen evidence

Private snapshot:
`%LOCALAPPDATA%/Dbce/StagePlayback/SessionEvidence/woden-grip-review-82f2812-20261007`.
459 files independently hash-verified; manifest SHA-256:
`E098B0B1906DB6CEDE7C3B9E5E6EB8F3A92B1631D7541E7FCAE99C3F63FFAD6E`.
242 archived source files match (five newline-only differences); the only
modified tracked file is the test Program.cs with eight appended registrations.
GripReviewChecks.cs is the extra test file. Production source has zero differences.
The ZIP preserves the original source; receipts, numeric results, synthetic v4
capture, immutable Kenya copy/report and scratch-copy commands are included.

After the fixes and v4 replay tests, collect a new coordinated muted original
recording with real curve inputs. Keep software qualification and the later
attended sign, release, impact and Strength50 comparison as separate evidence.
