# Edit a saved deadzone without recapturing

Native Controls now offers **Deadzone** beside each bound axis. The editor keeps
the saved device, axis, Rest, End, Centre and inversion; only an explicit **Save
deadzone** changes Calibration.Deadzone. The draft uses the existing calibration
normalization: input inside the deadzone becomes exactly zero, and the remaining
travel rescales to full input. Cancel, Escape and timeout retain the original
binding. A failed Save keeps the original live binding and the proposal for
explicit Retry/Cancel. Editing this setting does not require a connected device
or a new calibration sweep.

New Throttle, Brake and Handbrake captures start the existing visible deadzone
control at 3%. New steering stays at 0%; recalibration of an existing binding
keeps its saved deadzone. Loading an existing binding changes nothing. The Shim
backend continues using the existing shared binding editor and profile rules.
No extra normalization layer, game DeadZone, timer, brake torque, assist or force
tuning is changed.

The owner EC81 brake calibration remains Rest0/End65535/Deadzone0 during source
work. R9 recorded physical brake raw830..927 (about1.27..1.41%), while the pinned
native Accelerate method uses an exact-zero brake test. Root's joined comparison
selects240 full-throttle rows retaining physical brake with all four brake
torques500 and no forward progress, versus239 exact-zero brake rows reaching
signed speed71. Their starting states differ. Motor torque does not measure
propulsion here: native acceleration uses Rigidbody.AddRelativeForce. The
per-tick dispatcher was not decoded; its release gate remains an inference from
the native method plus actual A/B rows. See hub review
`docs/reviews/2026-10-16-woden-tiny-brake-diagnosis-valo.md` (4152e442), root analysis
`artifacts/woden-native-analysis-sana-20261016/positive-comparison-09.json`
(a27342e8), and raw R9 evidence/closure (18d7c185).

For the candidate/UAT handoff, explicitly suggest **Brake deadzone3%** through
the new control, with owner notice. This retains every captured endpoint and
maps raw<=1966 to exact zero while65535 remains full input. Do not copy another
profile's Rest1743 or silently modify the owner file. Owner UAT should check
feet-off idle followed by throttle; runtime acceptance remains pending. Do not
repeat the captured baseline solely for its missing MaxReverse getter or its
retained first-reader interval failure.

Authored checks are UNRUN pending a named build slot: the existing exact
calibration suite covers recorded raw values, threshold/full-scale and reversed
pedals; source-linked UiTests `--deadzone-only` covers preservation, draft/Cancel,
explicit Save, failure/Retry, timeout, defaults and rendered controls at720p and
triple width. No game, install, owner setting write or physical acceptance follows.
