# Woden combined input and close-release capture: prepared, not run

This is the fallback accepted in sana163659/163681. The unused standalone
monitor queue position is closed: no device opened, no monitor child, no lease
acquired; the actual shared lease was absent on handoff. The toolkit's
`ffbprobe` applies scripted forces and is not a read-only monitor. Wheelkit's
standalone CLI has no raw-input monitor command. No new reader is proposed.

## Frozen source and next candidate

- Retained UAT evidence and diagnosis: `2026-10-15-uat-native-input-diagnosis.md`,
  commit `f491e3ec215dce9d25363468aa3457553728d8f2`.
- Close-release candidate source: Livo `f61da7b38afd95ea0bee5bdc7daacfbbb89ce2e8`.
  Reported affected build and 33 focused assertions passed; independent process
  and lease closure is `74C494FF5BCA1B8CB4B92CADEACC0BC12EB64D7C901094D84A2C404A5E860FBC`.
- Retained outputs: hub `artifacts/woden-close-release-uat-20261015/checks-01/plugin`.
  Plugin `F51301F01B52155B0D12DFD1C6D674BD88835817379EEB37BDEFC2D70BE8F7D6`;
  Core `A9BD59926163F37A62242E208A8822801DF725B5FC4E16D867068087B08D0EAF`.
  These output hashes were read back during this preparation and match Livo's
  report. They are not an installed/runtime claim; recheck them at staging.

## Existing path and boundaries

Use `components/wheel/tools/Start-RecordedGame.ps1` for one ordinary diagnostic
launch, preserving Native backend and the owner's `wheel-bindings.json`. Its
existing version-2 request starts the original `GameSampler` recorder without
changing binding/calibration or creating a second device reader. Omit
`-AttendedFfb`: the request sets `DisableForces=true`, consumed before DeviceHub
construction and force preparation. Also use the already-qualified load-time
physical-output suppression required by the assigned runtime recipe.

The companion's `-PrepareOnly` **writes** a one-launch request under config;
it is not a read-only command and has not been invoked. Before any later run,
the owning supervisor must hold the shared lease, pin the candidate and normal
rollback payloads, snapshot current owner configs/saves/raw preferences using
the existing Woden session custody, and own exact helper/game identities.
Do not overwrite an existing recording request.

`DisableForces` does not disable dashboard UDP. For unattended operation, the
temporary run must also suppress physical motion/shaker consumers using the
existing private output settings (`Telemetry.Enabled=false`, ports0), with
exact owner-byte restoration afterward. Keep owner tune, bindings, backend,
gearbox and display topology unchanged. Runtime startup calls Settings.Save;
post-exit byte restoration is therefore required even without F6 edits.

Do not substitute `ShimCarTrial`, `PrivateApply`, latched forward input or pose
replay: they change the input source/workload and cannot identify the current
physical device's released state. Do not ask the owner to reopen now. Per
sana163795, existing state-gated native menu navigation may reach the car. This
plan has not run that navigation or exercised the rig.

## One bounded observation

After candidate/custody admission, use a unique request and case ID. Retain the
original source JSONL, final log and request metadata. `GameSampler.Read` requires
a MainCar; it cannot provide these raw/normalized/control channels from the
front end alone. State-gated menu navigation can enter the existing native scene,
but the first marked car interval must use the actual physical reader before
any injected gameplay controls. If no local car is reached, report unavailable
coverage and stop this one attempt. Current-rest samples cannot reproduce the
previous UAT symptom or establish response to physical pedal movement.

Only after that first interval is sealed may an explicitly authorized synthetic
driving phase run, separately marked. Its results establish synthetic delivery
only and must not be substituted for physical-rest samples. Keep a clear boundary
between menu navigation, physical-reader observation, close/cancel checks and
any synthetic driving. Stop without retry if an interval cannot be admitted.

Join original samples by session/sequence/car/time, not independent min/max:

| Layer | Existing channels or evidence |
| --- | --- |
| Raw cached device | wheelRaw.steer, wheelRaw.throttle, wheelRaw.brake, wheelRaw.handbrake |
| Normalized wheel delivery | wheelInput.throttle, wheelInput.brake, wheelInput.handbrake, wheelInput.appliedTicks |
| Native Controls | controls.throttle, controls.brake, game.handbrake |
| Car / eligibility | game.throttle, game.brake, game.trueSpeed, game.status, game.paused, game.locked, game.replay, sample state |
| Physical brake consequence | wheel.fl/fr/rl/rr.brakeTorque, when present |
| Close-release gate | Livo's existing menu log Status held=stock pad/read + field or PadActions[index]/value; Panel.Message ReleaseBlocker |

The candidate's 10-second Runtime log includes MenuOwnership.Status; preserve a
bounded log interval covering any authorized close-wait attempt and cancellation.
Do not infer a pedal name from a numeric PadActions index without the actual
table identity. The named blocker is a current observation, not proof it caused
the earlier UAT failure. Fresh-neutral ownership and force suppression remain.

## Stop, inspect and restore

Use the companion's exact `-Stop -RequestId` path or normal owned exit; no
process-name kill, silent retry or config retune. Retain source even if the force
case finalizer refuses idle/insufficient-driving capture: input diagnostic
coverage and force-case qualification are different results. Existing
TelemetryInspector `inspect <source.jsonl>` checks footer/channel coverage;
an aligned original sample review distinguishes brake, missing throttle,
handbrake and eligibility. Missing channels remain unavailable.

After exact process exit, restore candidate payloads and every owner config/save/
preference byte, then independently verify original identities absent, scope0,
exact rollback and lease absence before releasing the queue. No game run,
recording request, config edit, install, build or device access was performed
while preparing this plan.
