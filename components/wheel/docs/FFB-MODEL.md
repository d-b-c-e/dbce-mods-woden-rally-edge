# Provisional force model v3

This game-side estimate uses the toolkit v0.12.0 ForceShaper and native v0.5.0 constant-force device lifecycle. It does not change vehicle physics or assists. The owner felt FFB on 0.2.3; physical direction and normalization have not been formally validated. [woden-rally-edge-wheel, 2026-09-12]

Front WheelHit force magnitudes weight `tanh(sidewaysSlip / slipScale)`. The signed sum is negated, normalized by a user-set reference front load, and clamped to -1..1. Calibrated steering velocity contributes opposing damping capped at 0.5 normalized units. The user strength percentage scales the combined estimate. Shared conditioning adds smoothing, soft saturation, slew limiting, a fade between 3 and 12 km/h, a half-second ramp and a hard peak cap. Model 3 restores model 1 output by removing model 2's final 0.75 multiplier, at the owner's request after correcting their wheelbase strength. Default strength remains 50%; the default peak cap directly limits delivered output to 25%. The estimated force units do not establish rack torque or calibrated tyre forces.

The preview runs while disarmed when valid driving samples exist. Zero-only native preparation requires saved FFB On, an exact physical FFB GUID, stock Logitech ownership transfer and focus. 0.2.7 also captures a visible UnityWndClass handle belonging to this process before any device enumeration, revalidates ownership and passes it explicitly to the pinned toolkit. Missing windows wait without touching readers; no zero HWND is passed to the native foreground fallback. Guard failure still stops/closes output and retains the original error. Nonzero delivery additionally requires a closed panel and valid forward-driving contact samples. There is no per-session arm action. Enabled wheel input must be available. A single grounded front wheel can contribute; zero front load or unavailable front-contact data suppresses output. Reverse is suppressed pending sign validation.

0.2.2 also requires recent positive player-camera ownership. Game takeover,
replay/photo/finish, camera transitions and an external pose writer release the
mounted camera and suspend FFB. Native output acceptance does not establish that
these transitions have been physically tested; that remains an attended gate.

The device controller closes read slots before initializing FFB, then reopens them to share the native wheel handle. Teardown sends zero, stops effects, shuts down FFB, then reopens affected readers. Initialization sets a 150 ms constant-force hold watchdog and requires exit guards. Normal transient stops retain the device; failed output writes latch until On or Refresh explicitly retries. Shared-reader recovery uses a zero write through the toolkit, without enumeration. No condition or periodic effects bypass the hold watchdog. F8 saves FFB Off; only an explicit On resumes. New settings default On while existing preferences are preserved.

The game ships Logitech steering components. Mod ownership disables those readers, stops their coroutines, and resolves `LogiSteeringShutdown` only from an already-loaded game SDK module. The official [Logitech steering SDK](https://www.logitechg.com/sdk/LogitechSteeringWheelSDK_8.75.30.zip) declares its void shutdown export. Failure to resolve a loaded SDK's shutdown blocks mod force output. Native ownership behavior and physical feel need separate validation.

Woden's Unity binding layer strips GUI controls and style/cursor setters. The F6 panel uses fixed IMGUI rectangles/events, with named engine bindings for style/cursor properties. Signatures were checked against local generated code and Unity's [GUIStyle source](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/IMGUI/GUIStyle.bindings.cs) and [Cursor source](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Input/Cursor.bindings.cs). Runtime rendering confirmed those optional bindings resolve for the guarded game build.

The first contact sampling crash exposed an unrelated generated wrapper defect: `GetGroundHit(out WheelHit)` offered 8 bytes for a 72-byte result. WheelContact now uses actual IL2CPP value storage, as verified during the subsequent level-entry run. Do not restore the generated out wrapper. Contact telemetry and this force estimate both depend on that corrected boundary.

## 0.2.7 owner force-feel investigation

The owner now feels force and reports improvement; 6,723 writes completed with zero failures. [Command-log and synthetic-response analysis](FFB-FEEL-ANALYSIS.md) finds substantial peak clipping and a 200–217 ms synthetic release tail. Synchronized slip/steering capture is still needed to attribute the lingering sensation. No tune change accompanies this analysis.

## Grip v7: centering from the wheel position (2026-10-24)

The owner reported Grip v6 as "loose towards the center, like centering is only kicking in towards the extremes", even
at Strength 75 and GripLoadRatio 1.50. His recorded drive (`ffb.grip.*` per tick) was compared with his art of rally
recording (2026-10-13, Strength 50) by steering input. The comparison is software requests, not torque:

- At 60+ km/h with |steer| under 0.1, Woden's front slip was small and uncorrelated with the wheel. The tyre force had
  the restoring sign only 41-54% of the time, while Art's has it 95-97% of the time. Rescaled to 50 / 2, Woden's signed
  restoring force there was +0.005 to +0.011, against Art's +0.071 to +0.127. Art builds about 1.6 per unit steer from
  center and levels off near 0.17. Woden reached its force only at large angles.
- The damping term had the opposite sign to the tyre force's restoring direction. It pushed the wheel along its own
  motion, which loosens the center further, rather than resisting that motion.
- Smoothing is about 10 ms and the 3-12 km/h fade is by design; neither explains the complaint.

Version 7 adds `0.12 x tanh(steer / 0.05)`. It scales with (speed / 60 km/h)^2, full from 60 km/h, and with the 3-12 km/h
fade, the front load against its reference (at most 1.5x), Strength / 50 and Invert. The damping now resists the
wheel's motion. The defaults are GripLoadRatio 3 and Damping 0.02. On the owner's recording at Strength 50, the
rebuilt force by |steer| band at 60+ km/h is 0.88-1.03 of Art up to 0.4, and 1.29 at full lock, where Art's fronts slide.
It now has the restoring sign throughout. Versions 4-6 keep their arithmetic and replay exactly. Saved values are kept;
F6 Advanced -> FFB -> Reset FFB tuning applies the new defaults. Analysis: hub
`artifacts/woden-centering-claude-20261024`.

## Grip v8: the owner-accepted v7 feel is Strength 50 (2026-10-25)

The owner drove v7 (c1b126f) without resetting his tune: Strength 74.745544, GripLoadRatio 1.5029 and Damping 0.05.
His verdict was that it "feels great just as it is now", and he asked for that to become the default normal at 50%.
Version 8 is version 7 with `BaseGain = 74.745544 / 50` on the tyre force and the centering, the two terms Steering
strength scales. Damping keeps its own gain. The defaults are now GripLoadRatio 1.5 and Damping 0.05 (resisting, as in
v7), and Reset FFB tuning applies them.

v8 at Strength 50 equals v7 at 74.745544 bit for bit (`CenteringChecks`, "owner-accepted v7 at 50%"). The art of rally
comparison behind v7 is superseded as the target by this acceptance; the v7 section above keeps it as history.

A one-time, logged migration runs on load (`[ForceFeedback] DefaultsVersion = 1` marks it) for Grip configs saved
before v8:
- Values still at an earlier default (Strength 50, GripLoadRatio 2 or 3, Damping 0.05 or 0.02) take the v8 defaults.
- Any other Strength is divided by the base gain, so its feel is unchanged. The owner's 74.745544 becomes 50.
- Other values, and Classic configs, are kept.

