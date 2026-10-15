# Woden owner UAT: bounded native input diagnosis

## Result

The retained evidence does **not** establish a stuck service brake, absent
throttle, held handbrake, or a calibration defect. No input/calibration/force
change is justified by these files alone. The post-F6 input gate is a separate,
observable state; Livo owns its Panel/MenuOwnership correction.

This source-only investigation responds to sana163300/163403 under UAT162171.
Isolated base: `4d439612d49b7d49624fbdf52f2bc70833577647`.
Canonical's four existing dirty documentation files were preserved. No game,
device enumeration/poll, input injection, config write, install, build or test
was performed. There is no request for the owner to reopen or reproduce now.

## Preserved owner evidence

Private local snapshot, not added to the game repository:
`E:/Source/dbce-project-mgmt/artifacts/woden-input-brake-uat-20261015/run-01`.
Capture time: `2026-10-15T23:17:57.1163279Z`; stage status was appended afterward.
Every copy matched its source hash before and after copying.
Manifest SHA256: `C14D6CB5E05DE2B825EF4E933A6490740A545D6E81E9ED62A0F6E2DD12D2E222`.

| File | SHA256 |
| --- | --- |
| LogOutput.log | D185AF237B0238D62D906ACBA544560C4E19B6B502F889FBF6560EC6A64E681E |
| ErrorLog.log | A72963C538EE116368343F5497F50538C2DF311EA1A8996886C391E7DA5AE388 |
| dbce.wodenrallyedgewheel.cfg | 97BBB73262EBDD972708A17F27E22403B2CDFDB0D84BE48B5D68CE0FBF456F5D |
| wheel-bindings.json | EC81BEB085E6483F83998EDEB949661FC889C5D319D542865128ADB8E21D2097 |
| WodenWheel-install.json | A93D0E5661E4826F7F5CBDF0C2410769AF81F5B7E72BCE41CE228B606777315E |
| stage-status.txt | C43B01103460C1F5B8DBF35A81B27A147B08573CF37A1CC289333D4363F48E3D |

Actual installed Plugin `533D217E6F4BA7ED39B5C1EADC2D6212AB5971C5D9187F462A93191395CAE01A`
and Core `0F99A84733B65731E6773C1A4659BCA6D554D4CAA78E27F4C55B5106EE7E74C0`
match the receipt's `0.2.16+d45e2ce2b92fe0335c43e28ae57b93dfc101a76f`.
The log's `0.2.14` label comes from the unchanged `Plugin.Version` constant;
it does not identify a different installed binary. The inspected input source
files are unchanged between installed source d45e2ce and this worktree base.

## Binding and delivery trace

- Saved backend is Native; Wheel Enabled is true; NativeGearbox is Automatic.
  The cfg is now 97BBB732, distinct from pre-UAT A9D5. It was preserved as found.
- Native JSON binds steering axis0 (6184 / center33484 / 59740), throttle axis2,
  brake axis5 and handbrake axis7, all on instance `d71b8350-61b7-11f1-8001-444553540000`.
  All three pedal calibrations are rest0/end65535, non-inverted; analog
  handbrake is enabled, and handbrake button18 is also assigned.
- There are no H-pattern gate bindings. `WheelInput.HPatternBound` is false on
  this Native profile: `HPatternShifter.Pedals` cannot be cutting throttle in
  neutral or swapping throttle/brake for reverse on this saved path.
- `DeviceHub.TryAxis` uses the already-polled exact instance and axis cache.
  `AxisCalibration.Normalize` maps raw0 to0 and raw65535 to1 for these pedals.
  This proves the mapping, not the current hardware rest or travel direction.
- `WheelInput.Apply` requires the selected/focused, unpaused countdown/race
  state, closed F6, Wheel Enabled and all three driving-axis reads. Failure to
  read a driving axis retains stock controls; it does not synthesize full brake.
- `InputLease` writes throttle to action7 and brake to action6, restoring the
  stock table afterward. The retained log explicitly names those actions
  Accelerate and Brake and shows the wheel action route active.
- Handbrake is independent action0: `HandbrakeInput.Amount` merges the normalized
  handbrake axis, bound button and stock handbrake. A held handbrake is therefore
  another possible drive suppression, independent of service-brake movement.
  The stock handbrake contribution and raw axis7 value are not retained here.
- Before F6, the log reaches hooks2320/wheelTicks2269. At F6 it reaches
  hooks2443/wheelTicks2392, then these counters stop with Settings open and later
  Unfocused. That establishes post-F6 gating, not the earlier pedal symptom.

## Evidence needed after an authorized handback

The current session says capture disabled and recording Disabled. Its stage
status says idle, active=False, samples=0. No raw pedal samples, normalized
pedal values, final control values or wheel brake torques survive in these logs.
Earlier controlled private-Apply acceptance is a different profile/run and
cannot establish the owner's present physical rest values.

The existing `GameSampler` already exposes `wheelRaw.throttle/brake/handbrake`,
`wheelInput.throttle/brake/handbrake`, `controls.throttle/brake`,
`game.throttle/brake/handbrake`, pause/lock/state and wheel brake torques. No new
reader is needed. One later authorized original observation can distinguish:

- high normalized brake at physical rest: binding/range/device evidence;
- correct brake but absent throttle: delivery/gate evidence;
- handbrake active with service brake released: independent handbrake evidence;
- wheel input absent only while panel/paused/unfocused: eligibility evidence.

No calibration inversion, deadzone, device reassignment, graphics or force
retuning is proposed. Existing adoption gaps STD-016/017/020 unchecked and
STD-031/037 pending are not promoted by this read-only diagnosis. No new
baseline, compilation, test or runtime result is claimed.
