# Normalized collision-envelope comparison

These fixtures feed synthetic signed half-sine pulses (40/120/300 ms; model peak 0.2/0.5/1) into the exact pinned shared ForceShaper with Woden conditioning: literal 50% model strength, 25% command cap, 35 ms smoothing, 1.5/s configured slew, 0.5 soft saturation and 0.5 s ramp. They do not add a game collision effect or use event bypass. They are consumer conformance fixtures, not a fork of shared algorithms.

The rig identity is MOZA R12 from owner notes. Driver gain and physical torque calibration are unknown. The current user's reported 50% and the historical trace's cap/occupancy are separate evidence. No gain/cap/settings change is proposed by these metrics. Commands map to nominal DirectInput units by multiplying by 10000, not Nm.

Report peak, RMS, absolute impulse, cap occupancy, unwind time and observed finite-difference slew; retain algorithm DLL/source hashes and sample interval. Pause/focus/stale/respawn labels here verify conditioner reset only; actual controller gate/lifecycle coverage remains in the wheel harness. These synthetic envelopes cannot classify the unlabeled historical capture as collision data or prove delivered force equivalence. Evidence-based collision extraction and a shared impact contract remain pending coordination and attended rig acceptance.
