# Woden clutch input: consumer survey and implementation contract

Investigation date: 2026-10-22. Status: **clutch unsupported; no behavior implemented or qualified**.

## Identity and evidence boundary

This survey reads isolated checkout `E:/Source/.fleet-worktrees/20261022/woden`,
branch `fleet/eight-hour-woden-20261022`, baseline commit
`3bfc210cb0f38656b5a3fddbb68531abcbb431ad`. It changes only this document.
`components/wheel/Directory.Build.props` declares version `0.2.16`; the actual
plugin project targets .NET 6 / BepInEx IL2CPP. There is no new compiled candidate,
binary hash, package or installed receipt from this investigation.

The supported target recorded in `components/wheel/docs/STATE.md` is Steam build
`21802346`, Unity `6000.3.6f1`, metadata 39. `Plugin.SupportedGameHash` pins
GameAssembly SHA-256
`f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c`.
That is a source-supported target, not a fresh readback of the owner's install.
The install headers in AGENTS/STATE/HANDOFF are dated history, not a new installed
identity assertion. Preserve their owner configurations, Native default and tune.

Evidence below consists of current consumer source and existing repository reports
of pinned native audits. No new binary disassembly or runtime observation was
performed. Proprietary assemblies, generated interop, native instruction dumps,
decompiled game source and owner recordings must remain outside Git and packages.

## What actually consumes input

Paths in this table are repository-relative. Symbols identify the evidence at the
baseline above; a generic field name or ABI slot is not a game consumer.

| Surface | Evidence | Clutch implication |
| --- | --- | --- |
| Native producer | `docs/SHIM-PILOT.md`, Native source isolation: pinned `GamePadSystem.Update` at VA `0x180C000A0` calls `ReadInputs` at `0x180BFD6F0`; cached pad state is projected into actions, then legacy keyboard keys overlay them at `0x180BFE498`. | A real stock keyboard/controller producer exists. The report does not establish a clutch action or drivetrain reader. |
| Selected action table | `components/wheel/src/WodenRallyEdge.Plugin/ControlActions.cs`, `For`: selects `Controller_Int` in `Game_Pads`, or the native fallback pad when no list exists; Shim validates that exact pad. `Plugin.cs`, `ControlsHook`, scopes `InputLease` around `Controls.FixedUpdate` and restores in postfix/finalizer. | A clutch implementation needs an identified consumer on this exact selected-car path; no global pad overwrite. |
| Current wheel action writes | `InputLease.cs`: handbrake 0, downshift 1, upshift 3, brake 6, throttle 7, steering 16/17; other mapped buttons retain stock values through merging. `WheelInput.Apply` supplies steering, throttle, brake, handbrake and shifts. | No clutch read/write is present. Assigning an unused action index is unjustified. |
| Native standalone persistence | `components/wheel/src/WodenRallyEdge.Core/Bindings.cs`: version-1 JSON has Steer, Throttle, Brake and Handbrake; `Copy`, `Axis`, `SetAxis`, validation and load enumerate those axes. `WheelInput` supplies capture, normalization and checked Save. | There is no persisted Native clutch axis. A generic pedal parser does not add one. |
| Gearbox | `WheelInput.cs`, `HPatternShifter.cs`: use `car.Aids.Automatic`, `MyGear`, `Gears` and `TransmissionSystem.Changing`; native up/down actions perform shifts. `NativeGearboxApply.cs` sets the selected player's saved transmission choice only at the first eligible main menu. | Manual is native, but clutch coupling is not demonstrated. Do not silently select Manual or alter the saved gearbox choice to enable clutch. |
| H-pattern emulation | `HPatternShifter.Target`, `Step`, `Pedals`: forward gears 1..6 are reached through timed shift presses; higher gates clamp to the car's highest gear. Neutral cuts wheel throttle; R selects first and swaps wheel throttle/brake. Automatic bypasses pedal emulation. | These are adapter behaviors, not native neutral/reverse gear or a clutch. Throttle cut does not demonstrate free engine revving or drivetrain disengagement. |
| Native reverse drivetrain | `docs/SHIM-CAR-TRIAL.md`, Native brake/reverse and respawn evidence: pinned `MainCar.Brake` at VA `0x1804FCA00` has a low-speed brake-timer branch, with grounded/fuel/inclination/speed/mode conditions, writing negative `EngineSystem.ActualPower` as wheel motor torque and selecting gear 1. It does not test `Aids.Automatic`. | Cutting throttle alone cannot be assumed to disengage reverse drive: the brake path can produce motor torque. The report's unnamed mode gate and actual branch execution remain limitations. |
| Shared parser/ABI | `ShimActionMap.cs`: ABI 2 has 31 action names, including `clutch`; `Vendor/Input/ControlBinding.cs` classifies it as a pedal. Telemetry's generic Clutch channel is also not a drivetrain consumer. | Representation and normalization exist generically; delivery does not follow from their presence. |
| Shared Woden adapter | `ShimActionMap.UnsupportedNames` includes clutch/select/gear7/gear8 and sends that unsupported list to `ShimNative.Open`. `ShimInput` driving axes are steer/throttle/brake; `WheelInput.ApplyShim` consumes those and handbrake/gears. | Clutch is explicitly omitted by the adapter. Even removing the exclusion would only expose a value; it would not establish a game consumer. |
| Capability/UI | `Package.ps1` and `Manage-Install.ps1` agree on 27 delivered actions and clutch unsupported. `SharedBindingHost.Reason` refuses clutch edits while retaining its saved value; Panel says clutch is not routed. | Keep this truthful unsupported capability until end-to-end qualification. |

The checked-in reports of successful H-pattern Apply-to-car observation cover
specific steering/pedal/gear actions only (`TOOLKIT-ADOPTION.md`, October 13;
`docs/2026-10-13-shim-hpattern.md`). They do not qualify clutch. Older release notes
saying there is no H-pattern are historical and do not override current source.

**Finding:** adapter omission is established; native clutch support is
**unproven**, not proven absent from every native method. Neither this source
survey nor the recorded reverse audit identifies a native clutch input, its
reader, coupling law or units. Before calling any implementation native clutch,
a bounded private audit of the pinned producer, `Controls.FixedUpdate`,
`MainCar` input/FixedUpdate and engine/transmission torque readers must establish
the exact path (or document that the relevant paths have no such consumer).
Keep only method identities, hashes and findings in Git. An interop setter or a
field named clutch without a traced read is insufficient.

## Smallest viable behavior and missing owner decision

The smallest deliverable today is this contract and the existing unsupported
state. There is no approved basis to implement physics or choose an emulation.
If a real native analog consumer is found, the smallest candidate is a calibrated
pedal routed to that consumer with its measured stock law, no new bite curve,
stalling, clutch kick, wear, shift requirement or torque model.

If no native consumer is found, the exact owner decision is:

> Should clutch mean (A) continuous reduction of wheel-requested drive input,
> explicitly an accelerator-cut emulation with no promise of free revving or true
> torque disengagement, (B) a pedal threshold that permits manual shift requests
> while leaving drive input unchanged, or (C) remain unsupported until a real
> drivetrain-disengagement implementation is separately designed? For A choose
> the partial-pedal law and reverse-drive behavior; for B choose press/release
> thresholds, whether sequential shifts also require it, and whether a blocked
> H-pattern gate is retried while held or requires a new selection. For either,
> choose whether Automatic is unaffected and whether input loss disables the
> clutch feature or inhibits wheel drive until deliberate recovery.

This is a recorded prerequisite for future implementation, not a question whose
silence authorizes a default. A and B produce different driving behavior; neither
may be advertised as native clutch. Directly changing engine power, wheel torque,
gear ratios or rev behavior is outside those minimal alternatives and needs a
separate handling design and STD-018 review.

The following semantics must be settled and tested before enabling support:

| Condition | Contract / decision boundary |
| --- | --- |
| Released | Normalize calibrated released endpoint to 0 pedal depression. With an eligible live binding, preserve current drive and shifting unless the owner explicitly chooses a shift interlock. |
| Partial | Normalize measured travel into 0..1, inversion once. Native path follows the verified native law. Accelerator-cut emulation needs the chosen attenuation law; an interlock needs explicit hysteresis. Do not invent a bite point or claim proportional clutch torque. |
| Fully depressed | Normalize full endpoint to 1. Native path follows verified disengagement semantics. A may reduce wheel drive request but is not a true clutch; B permits shifts and does not cut drive. Test the chosen endpoint behavior independently. |
| Manual + H-pattern | Preserve existing gate targeting, changing-gear guard and position semantics unless the chosen clutch policy explicitly changes them. Neutral remains the existing drive-cut emulation, never a fabricated gear 0; a clutch must not cause an unsolicited gear selection. |
| Manual + sequential | Preserve native shift behavior by default; whether B interlocks sequential requests is an explicit owner choice. No queued press may escape a blocked/context transition without the agreed release rule. |
| Automatic | Current H-pattern leaves supplied pedals unchanged and does no synthetic shifts. A clutch must not silently force Manual or disable the native automatic system. Automatic clutch effect, if desired, requires an explicit decision and distinct observation. |
| Reverse / neutral | Retain current pedal swap and neutral behavior unless deliberately revised. Assess ordering against R pedal swapping and the native brake-to-reverse torque path. Merely multiplying throttle cannot promise reverse disengagement; braking must remain available. |
| Player/context ownership | Only `Runtime.Select`'s `IsPlayer` car with matching `Settings.Player`, its `MyControls` and selected action table may receive it. Reuse `PlayerControlState.WheelAvailable`: eligible racing/countdown, focus and capture/panel/pause/replay/respawn/photo/lock guards. Preserve native countdown locks; never affect AI, other local players or replay-owned cars. |
| Unbound/unavailable/stale/disconnected | Distinguish unavailable from a live released pedal. Never reuse a last value or invent a full press, a release-triggered shift or a fallback device. The selected loss policy must be explicit: removing an engaged clutch effect can unexpectedly restore drive; holding a stale value is also wrong. Refuse a candidate whose policy is unresolved. Clear synthetic state within the failed-read tick; retain ordinary stock input and show the reason. |
| Reconnect/context return | Preserve device identity/epoch rules; do not turn a reappearing device or held shift into an authorized fresh press. Require deliberate safe recovery under the chosen policy. Do not add a second reader. |
| Keyboard/controller | Keep stock bindings and action restoration, including exceptions. Any emulation acts on wheel requests before stock composition; never attenuate stock keyboard/controller throttle or rewrite their shifts globally. Native fallback remains intact. Shim remains wheel-only: it refuses Unity gamepad coexistence before ownership and stops on later arrival; clutch must not hide that limitation or disable the gamepad. |

## Concrete implementation reach (future work, not changed here)

1. **Native persistence, calibration and F6:** extend `Bindings` property/copy/
   lookup/set/load validation and serialization compatibly; old version-1 files
   and optional/unbound clutch must retain their meaning. Extend `WheelInput`
   capture/read/clear paths and Panel's Native axis rows, live/unavailable text,
   inversion/deadzone and Save/Cancel/failure handling together. Reuse
   `AxisBinding`, `AxisCapture` and DeviceHub identity; clutch must not become a
   prerequisite that disables the existing three driving axes. Audit all axis
   enumerations and the Native `woden-bindings-1` writer contract before opting in.
2. **Shim availability and consumption:** update `ShimActionMap` exclusions,
   `ShimInput` status/context admission, `SharedBindingHost.Reason`, cached live
   presentation and `WheelInput` consumption only after a consumer exists.
   Preserve finite/range checks and statuses; validity cannot be inferred from
   `Axis` returning zero. Add an identified game hook or scoped lease only as
   required by that consumer. Keep `InputLease` exact restoration and independent
   keyboard composition. The ABI's existing clutch index must not be renumbered.
3. **Capability, package and writer compatibility:** change `Package.ps1` and
   `Manage-Install.ps1`'s matching delivered/unsupported/condition contracts,
   capability/receipt fixtures and any delivery verifier that pins them in one
   reviewed implementation. Keep old receipts unsupported; advertise only the
   exact qualified consumer hashes/source and relevant transmission conditions.
   A writer must refuse unsupported clutch, never silently drop it or write an
   unread key to pass. Preserve Native opt-in default and old schema-1 behavior.
4. **Existing source/UI fixtures:** extend Core `WodenRallyEdge.Tests` (bindings,
   calibration, H-pattern), production-linked `WodenRallyEdge.UiTests`
   (`ShimChecks`, `ShimCarChecks`, `ShimAppliedCarChecks`, `SharedBindingChecks`,
   panel checks), and `WodenRallyEdge.InputTests` reader/lifecycle fixtures.
   Preserve the existing explicit unsupported-clutch capture assertion until
   support is real. Update `tools/controls/Probe/AppliedCarWorkload.cs` and
   ProtocolTests unsupported lists only with qualified observation semantics;
   update `Test-PrivateShimInstall.ps1` and the matching installer fixtures.
5. **Native-car observation:** extend the existing raw-controls/CarTrial path,
   rather than a second harness. `ControlsProbe` already records native wheel
   motor/brake torque and the trial reads native gear/change state. A future
   clutch trial needs fresh sequence-bound native input/coupling evidence plus
   engine RPM, drive/brake torque and gear/context readback where actually
   observable. Name missing channels. Do not infer clutch from an injector ack,
   shim normalized value, telemetry slot or pose replay.

Separate coordination needs, with **no file claims or edits outside this repo**:
Wheelkit's Native/shared writers, capability parsing and fixture bytes; toolkit
canonical controls parsers/vectors, schema-2 consumers/force-target helpers and
any shared-provider change; hub controls coverage/catalog records and eventual
rig qualification. The generic ABI already has a clutch slot, so no ABI expansion
is justified by this investigation. Preserve proprietary dependency boundaries.

## Standards comparison and validation proposal

Read-only comparison of toolkit `STANDARDS.md` with `TOOLKIT-ADOPTION.md` on this
date: ledger IDs STD-001..037 are represented. The adoption file additionally
records STD-038 source qualification. Existing **STD-016/017/020 unchecked** and
**STD-031/037 pending** remain open; partial adoption rows retain their limits.
No adoption status is promoted. Updating adoption belongs in the eventual
implementation commit, when these areas are actually adopted; this task is
restricted to this document.

| Standard | Obligation for clutch work |
| --- | --- |
| STD-004 / STD-030 | F6 arrives with the feature, using existing binding presentation and explicit Save failure/draft semantics. Omit an adapter-unsupported action from editable UI; keep the gap in capability records. Once supported, a temporarily unavailable action stays visible with its reason and disabled edits. Do not hide failures. |
| STD-018 | Classify the actual implementation. Ordinary controller mapping does not block scores; physics/grip/extra assists require stage-latched native upload blocking, visible reason and refusal to enable when the guard cannot be installed. Do not assume a new torque/clutch model is ordinary mapping. |
| STD-031 | No normalization or force-reference changes; preserve the owner's existing tune and pending physical acceptance. |
| STD-033 / STD-034 | One logical binding on its named instance/device and existing store. Separate configured, game-observed and owner-accepted results. Later Apply qualification needs production writer/fixture parity, independently sourced raw samples, deliberate wrong-binding repair, fresh native observations and exact restoration. |
| STD-035 | Capability/source/binary provenance first; merged-result collisions and mode compatibility; consumer-parser fixtures. Same-handle identity/epoch, failed-read clearing and no stale/reopened injection. Later live qualification needs independent force-mute interlocks and a cloud-safe owner-save plan; local hashes alone do not establish Cloud convergence. |
| STD-036 | Pedal and shifter may be on separate devices; read each bound instance through the existing provider. Refuse a writer plan it cannot represent. |
| STD-037 | Shared measured endpoints remain pending end to end. Do not relabel schema-1 `calibrated` metadata as applied calibration or advertise `ini-controls-2`. Schema 2 requires all input/profile/host/force-target readers and writers opted in, canonical vectors and capability opt-in; no working calibrate affordance before the whole path exists. Native JSON calibration is a separate existing path. |

Focused device-free proposal for a later implementation task:

1. Core assert-based vectors for released/partial/full, inversion once, valid/
   invalid endpoints, missing/nonfinite values and old/new binding round trips;
   the chosen law/interlock only, with no speculative physics cases.
2. Production-linked UI/lease fixtures for the complete Manual/Automatic x
   H-pattern/sequential matrix, reverse/neutral ordering, changing-gear guards,
   selected/foreign player, countdown/native lock, context transitions and stock
   composition/restoration on normal/throw paths. Test stale/disconnect and
   reconnect without fabricated shifts or unexpected wheel-drive restoration.
3. Existing fake-reader InputTests for per-device identity and failure lifecycle;
   shared editor draft/Save/cancel/unavailable presentation checks. Existing
   private installer/capability fixtures must reject forged or mismatched support
   and retain old unsupported receipts. Coordinate identical writer fixture bytes
   with Wheelkit separately.
4. Run only the affected existing project checks first; later use the repository
   `tools/game/Verify-Source.ps1` as integration coverage when authorized. It builds
   and runs checks and was deliberately **not executed** for this investigation.

Source/fixture success will not prove native clutch response or pedal feel. A
future separately authorized save-safe, force-muted native-car run must observe
released/partial/full/release plateaus, each chosen transmission/gear behavior,
failure recovery and stock input preservation before capability promotion;
physical pedal and owner driving acceptance remain additional gates.

Validation performed here: read-only source/report/ledger survey and document diff
review. No builds/tests, dependency installation, network access, installs,
game/device/display/input operations, resource leases or public publication.
