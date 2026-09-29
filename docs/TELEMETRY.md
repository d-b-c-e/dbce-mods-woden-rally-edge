# Telemetry contract, version 1

The priority is useful data with traceable meaning. The versioned dictionary in `TelemetrySchema.cs` is exported into every development package as `telemetry-schema.json`. Each definition has units, origin (`measured`, `derived`, `raw`) and source. Definitions include a few reserved channels that the current stripped API cannot supply; their presence in the dictionary does not promise a live value.

## Three outputs

| Output | Default | Contents |
|---|---|---|
| Forza Horizon 5 Data Out | UDP 127.0.0.1:8000 | Toolkit's 324-byte packet, physical motion and wheel rotation. Raw game scales are not converted speculatively. |
| Detailed telemetry v1 | UDP 127.0.0.1:8001, up to 20 Hz | JSON snapshot with session/sequence, wall/simulation clocks, sampling phase, driving state, available numeric channels and read failures. |
| Diagnostic recording | Off | Toolkit JSONL metadata, numeric samples at sampled physics rate, ordered state/reset/gate/model markers and validating footer. Correlated owner requests also write config/profile identity for device-free model reprocessing. 512-record queue, 20-minute / 64 MiB caps. |

The network worker has one pending slot. A slow consumer drops old network ticks; overwrite/error counters are logged every ten seconds. Recording has its own queue and drop counters. Its producer copies finite numeric values; serialization and disk writes happen on the toolkit worker. Maximum detailed UDP payload is 60,000 bytes; a complete schema sample is tested below that budget. UDP does not guarantee delivery or order.

Detailed receivers must treat silence beyond 500 ms as stale, use `sessionId` plus `sequence`, discard out-of-order data, and clear channels absent from the latest snapshot. Do not retain an old wheel contact force after takeoff. Forza output independently emits inactive packets after a half-second source timeout, while the game Update heartbeat explicitly parks it on focus/pause loss. Shutdown sends three inactive packets; UDP delivery is still not guaranteed.

The 0.2.12 recorded-playback adapter uses model validity/reason, reset epoch and output-gate channels to verify the original `ForceSignal` baseline, then permits separate immutable candidate-config trials without a device. Observation magnitudes are managed model previews before native delivery. See [recorded owner-drive boundaries](RECORDED-PLAYBACK.md).

## Sampling and validity

0.2.2 adds `wheelInput.appliedTicks` plus `camera.mode`, `camera.mountedView`,
`camera.playerOwned`, `camera.changing` and `camera.stockPreset`. These distinguish
device readings from a run through the Controls action-table hook and document
the camera ownership gate used by FFB. There are 199 schema definitions;
availability/meaning still require recorded runtime checks. Camera mode values
retain the game's enum; mounted view is 0 stock, 1 bonnet, 2 bumper.

The hook is **MainCar.FixedUpdate postfix, before the next Unity physics solve**. It reads the current game fields and last available PhysX state. A current input command and a wheel contact may belong to adjacent solve phases. This is explicitly included in the detailed packet and recording metadata. A truly post-solve sampler is a validation milestone.

The selected car must have `IsPlayer`, the configured exact `PlayerIndex`, state `RACE`, focus, and no pause/replay/photo/respawn/locked state to count as driving. These field gates are implemented from static inspection and still need live validation. Countdown/warming state is inactive in this first build.

`elapsedSeconds` uses a monotonic Stopwatch. `simulationSeconds` uses `Time.timeAsDouble` inside FixedUpdate, where Unity specifies that it returns fixed simulation time. Acceleration differentiates world velocity over simulation time and then rotates into the current vehicle frame. It does not differentiate changing local axes. Pause, car/session changes, time gaps, invalid orientation and implausible position discontinuities reset derivatives and distance. The first sample after each reset has no acceleration channel. [Unity time documentation](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Time-timeAsDouble.html).

Absent means unavailable; zero means a reported zero. Nonfinite values are omitted and identified in `unavailable`. A failed optional group may retain successfully read earlier members; consumers should check both availability and the failure list. `sample.driving`, sequence and simulation time are retained in numeric recordings; state transitions are markers. Gameplay meters can be present even while inactive, but must not drive effects then.

## Available signal families

| Family | Meaning and limits |
|---|---|
| World position, world/local velocity, local angular velocity, quaternion | From Rigidbody and transform. Local axes are right/up/forward. Unity's conventional metre/second scales are used; actual game-world scale is not road-validated. Orientation comes from `rb.transform.rotation` because the Rigidbody rotation getter is stripped. |
| Speed, acceleration, kinematic G, segment distance | Derived from motion. G is kinematic acceleration divided by 9.80665; it is not accelerometer specific force and has no gravity compensation. Distance resets at discontinuity, not an odometer. |
| Individual wheel RPM, rad/s, radius, steer angle | Actual WheelCollider state; corners use game axle membership and lateral geometry, never array-order assumptions. Ambiguous/duplicate wheel mappings are rejected. |
| Motor/brake torque | WheelCollider commands in Unity units of N*m. These are not measured engine output or net contact force. |
| Contact, force magnitude, point, normal, collider ID | Ground-hit data, present only while grounded. Collider ID is session-local and is not a surface classification. |
| Forward and sideways slip | Original Unity WheelHit values. No conversion to physical slip ratio or angle is assumed. |
| Wheel hub offset and vertical offset rate | GetWorldPose wheel centre expressed in collider-local coordinates; unknown static centre offset remains. Rate is a relative hub-motion derivative, not calibrated damper speed. |
| RPM, gear, gear count, actual power, gear momentum | Raw MainCar engine/transmission fields. RPM/redline units, reverse/neutral indexing and game power scale still require recordings. |
| Inputs and assists | Car steering/pedal fields, Controls inputs, handbrake, automatic/ABS flags. The mod's own calibrated axes are separately named `wheelInput.*` only when applied. |
| Race/status/gameplay meters | Raw rank, lap/checkpoint counters, last/best lap, life/fuel/oil/temperature/G-force/grip. No assumed seconds, Celsius, litres or percentages. Clock `Penalty` is a boolean retained numerically in the raw family. |

WheelHit's force is a **magnitude**. Multiplying it by slip does not magically produce a measured signed tyre force or aligning torque. Unity's native wheel API is documented at [WheelCollider.GetGroundHit](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/WheelCollider.GetGroundHit.html) and [WheelHit](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/WheelHit.html).

## Deliberate gaps in this build

The generated Woden API has no WheelCollider centre/suspension-distance getters and no Rigidbody mass getter. Absolute suspension extension/compression and mass are reserved but absent. Do not substitute a constant 0.5 suspension signal. Surface roughness, curb/puddle flags, tyre temperature, engine redline, engine power/torque, fuel percentage and validated timing/gear mappings are also absent from Forza.

Standard Forza has no per-field validity mask. Unsupported fields therefore encode zero, which a dashboard can mistake for real data. Treat the rich channel dictionary as authoritative; hide unsupported dashboard gauges until their mappings are validated. Stock pedal/RPM/gear gauges are not yet meaningful in the Forza output. The calibrated wheel route supplies normalized input only when explicitly enabled.

Forza velocity, acceleration and angular velocity use the vehicle's local frame. Position is world-space. This follows the [Forza Data Out specification](https://forums.forza.net/t/forza-motorsport-7-data-out-feature-details/74013). The toolkit encodes bytes without changing those coordinate frames.

## Validation drive

### Experimental FFB and physical input channels (0.2.0)

`wheelRaw.steer/throttle/brake` are bound DirectInput values before calibration, absent on device-read failure. `wheelInput.*` exists only when the mod applies its calibrated controls. Comparing both with `controls.*` and `game.*` helps establish whether the hook feeds the final game input.

`ffb.frontLoad` sums the two front contact magnitudes. The provisional model computes `-sum(load * tanh(sidewaysSlip / slipScale)) / loadReference`, clamps it, and adds damping from calibrated steering velocity. `ffb.alignmentEstimate` and `ffb.dampingEstimate` retain those components; `ffb.preview` is toolkit-conditioned output before device permissions. It remains useful with FFB disarmed. These are estimates, not measured rack torque or signed lateral force.

`ffb.sent` is the normalized request accepted by the native API. `ffb.accepted` is absent when no write was attempted. Delivery counters expose API/init failures; they do not establish physical torque. `ffb.armed` records session permission. `ffb.tuning.*` preserves strength, peak, reference load, slip scale, smoothing, damping, inversion and model version 3 per sample. `ffb.modelValid`, reason, reset epochs and output-gate code make the exact model stream reprocessable; matching string markers retain human-readable transitions. `sample.discontinuity` identifies the current sample's reset without guessing from the following marker. See [the code table and evidence levels](RECORDED-PLAYBACK.md).

Live capture can be started after hours in-game: recording timestamps begin at the first captured sample, while `sample.simulationSeconds` preserves the game clock. Stopping/restarting capture creates a distinct file. Do not interpret a recorder limit as a complete normal shutdown.

The offline adapter refuses incomplete, dropped, limited, contended, missing-channel and idle-only sources. It reruns the actual pure `ForceSignal` and emits normalized software observations without native device calls. This establishes signal reproducibility only; the JSONL cannot restore Unity state or deterministically drive the game.

Capture separate short runs for stationary idle; constant-speed straight; acceleration/braking; left/right corner; reverse/gear changes; jump/landing; rough surface; pause/resume; respawn and stage restart. Compare speed/RPM/gear/timers with the HUD and compare left/right loads with the turn direction. Record scale/corner/contact findings in STATE with exact build and session filenames. Evaluate shaker effects with standard SimHub fields once verified; no extra SimHub plugin is required or supplied.

## 0.2.3 input and hitch diagnostics

Raw/applied handbrake values join the schema. Axis pull scales native rear brake
command and grip loss; the stock binary power cut remains. FFB status changes
are recorded as ffb markers, with connection attempts/duration and delivery counters.
Timing channels measure Update intervals, device polling, Controls.FixedUpdate,
native car work, sampler work and force work. These are CPU/Stopwatch observations,
not GPU frame time or proof that shifting caused a hitch. No new gameplay recording
has yet validated channel coverage/scales for this build.


0.2.6 adds `wheelInput.preRaceTicks`: successful scoped wheel input overrides during the native WARMING/countdown state. Camera authority and applied input can now be present before green; they do not imply `sample.driving` or FFB permission. The optional difficulty assist adds `assist.countdown.speedPercent` (configured speed, 100 when Off; not proof of activation), `assist.countdown.adjustedUpdates` (cumulative anchor adjustments attempted by the actual hook) and `assist.countdown.timeLeft` (recently observed owned timer seconds; omitted when unavailable). Countdown records do not replace elapsed lap/stage clocks. Schema: 215 definitions.
