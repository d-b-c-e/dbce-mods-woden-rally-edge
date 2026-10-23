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

## Audit result: bounded private native trace, 2026-10-22

**Conclusion: no consumer found on the traced paths.** No native analog clutch
action/field read, pedal units or pedal-controlled engine/transmission coupling
law was identified. This is a bounded negative result, not proof that every
method in the game lacks clutch behavior. The A/B/C owner question above stands
**unchanged**; neither accelerator-cut emulation nor a shift interlock is selected
or implemented by this audit.

This follow-up used the same isolated feature checkout, initially at
`8a7d37e8ab30279be748c95844291620b346d23e`. A fresh read-only hash of the local
GameAssembly matched the supported pin exactly:
`f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c`.
Its paired `global-metadata.dat` SHA-256 is
`3b5d7167aea83febdc625f1635fc7cb414ff13ca609d78cc32158133c3877815`.
This verifies those analysis inputs, not the installed mod, settings or receipt.

Method labels and field layouts were cross-referenced with the existing private
metadata map (SHA-256
`72a8602cb4ea077bb51126d02d4a085061f7ae66e23f87961b2df026442d30f5`)
and generated interop Assembly-CSharp (SHA-256
`1aeedb12826c39307c053e5e4e434cf69416603a01e38fc04f21dd83215622c0`).
Interop establishes identities, not consumer behavior. Native reads, branches
and direct calls were inspected from the freshly hashed binary in memory using
existing Python 3.14, pefile 2024.8.26 and Capstone 5.0.7. The seven existing
private instruction listings for producer, Controls, car FixedUpdate, dispatcher,
Accelerate, Brake and Shift matched the fresh instruction decoding at every
listed address. No proprietary listing, assembly, decompiled source or recording
was added to this checkout or Git.

### Traced paths and findings

VAs below use preferred image base `0x180000000`, not a live process address.
Tokens are original game metadata method tokens, meaningful only for these pins.
An obfuscated method is identified by token and VA rather than a guessed name.

| Path / native method identity | Finding and boundary |
| --- | --- |
| `GamePadSystem.ReadInputs(Game_Pad)`, token `0x06008B52`, VA `0x180BFD6F0` | Reads cached controller state, populates raw values, projects configured raw indices into action values/pressed flags, then applies the keyboard overlay at `0x180BFE498`. No dedicated clutch read or coupling law found. The generic action projection can represent values without assigning drivetrain semantics; this producer alone cannot establish clutch support. The indirect call at `0x180BFE2AC` was not resolved. |
| `Controls.FixedUpdate`, token `0x060066C5`, VA `0x1809A41F0` | Traced selected-pad action reads: throttle 7, brake 6, steering 16/17, handbrake 0, shifts 1/3, plus existing restore/lights/pause/song/horn/records/camera actions. Throttle and brake reach `MainCar.Pedal_Acc_Value` / `Pedal_Brake_Value` at `0x1809A4558` / `0x1809A4571`; steering reaches its car field at `0x1809A4628`. Handbrake and shift calls reach `0x1804FF900` / `0x1804FFBE0`. No third pedal read, clutch action delivery or coupling law found. |
| `MainCar.FixedUpdate`, token `0x06000401`, VA `0x1804FD330` | Followed car input fields through context/lock resets, engine power computation, gear/RPM calculations and the input dispatcher call at `0x1804FF73C` to `0x180502C70`. No clutch pedal read or pedal-controlled coupling law found. Decoded the complete contiguous method range and followed these paths, including continuations beyond its first PE unwind entry. |
| `MainCar`, token `0x060003EB`, VA `0x180502C70` (obfuscated input dispatcher) | Reads steering, throttle and brake; calls Steer at `0x180502CC8`, Brake at `0x180502EF0` / `0x180502F15`, and Accelerate at `0x180502F1F`. Its zero-pedal and locked/status branches clear drive or apply brakes. No clutch input or coupling law found. A zero-throttle branch is not evidence of clutch disengagement. |
| Engine/transmission readers inside `MainCar.FixedUpdate` | `EngineSystem.ActualPower` is written at `0x1804FE0B3` and subsequently modified from native curves/state; the Manual branch reads `TransmissionSystem.Diff` at `0x1804FE0D3`. Gear momentum and gear-table calculations occur around `0x1804FE708` and `0x1804FEFCC`; the RPM branch reads `TransmissionSystem.Changing` at `0x1804FE953`, and throttle at `0x1804FE9D2` / `0x1804FEA41`. These are stock gear/change/RPM state readers, with no traced pedal clutch input or clutch coupling law. `Changing` is a boolean gear-change state, not an analog clutch value. |
| `MainCar.Accelerate`, token `0x06000410`, VA `0x1804FC6C0` | Forward drive reads throttle and `EngineSystem.ActualPower` at `0x1804FC839` / `0x1804FC841`, then applies native drive force subject to existing conditions. No clutch input multiplier, read or coupling law found. This does not establish free-revving or physical torque units. |
| `MainCar.Brake`, token `0x060003F4`, VA `0x1804FCA00` | Confirmed the existing bounded reverse finding: the eligible low-speed branch reads throttle at `0x1804FCD74` and negative `ActualPower` reaches the wheel motor-torque call at `0x1804FCE07`. No clutch input read or coupling law found. The unnamed native mode gate and unobserved branch execution remain limitations; a throttle-only cut cannot promise reverse disengagement. |
| `MainCar.Shift(int)`, token `0x06000406`, VA `0x1804FFBE0` | Reads ShiftOk, car status, `TransmissionSystem.Changing` and gear bounds, updates the gear and schedules the existing change coroutine through factory token `0x06000404` at `0x180501070`. No clutch input read, clutch threshold or pedal coupling law found in Shift or that factory. The coroutine body remains untraced. |

### Reproducible native byte-range identities

Each SHA-256 covers the original file-backed bytes at the half-open preferred-VA
range, including alignment bytes where present. These are explicit audit ranges,
not claims that one PE unwind record contains the whole logical method. In
particular, the first unwind entries for ReadInputs and car FixedUpdate end
before their continuations; stopping there would miss the keyboard and torque
paths. No binary bytes or instruction text are reproduced here.

| Method / audit range [start, end) | SHA-256 |
| --- | --- |
| ReadInputs `0x180BFD6F0`..`0x180BFE570` | `459445bca1e9b327f25940ece6bf804dce991a1a27ff25029eeffbc4334c6896` |
| Controls.FixedUpdate `0x1809A41F0`..`0x1809A4AB0` | `36f8e5842e54a7a844fd487f25fffb94c7698a6a44c0103b12d4b7226c5cbc3c` |
| MainCar.FixedUpdate `0x1804FD330`..`0x1804FF880` | `77c4d4c330937a4d6f030d10df0c4eb9b9242b33df6a26005e278b0b02d5cd54` |
| MainCar input dispatcher `0x180502C70`..`0x180503060` | `4966385f84e59829f1399ebbd7f281b8cdaeafa22575b305dff0f53e95272eb5` |
| MainCar.Accelerate `0x1804FC6C0`..`0x1804FC9A0` | `113d4f483b138550065b840ebc53685b5d95f268e26214e02eaa93daee674ae3` |
| MainCar.Brake `0x1804FCA00`..`0x1804FCF10` | `a8e9660d27179f248c7dde353ddf4061acbac458c2da5709f2562bca805aacbe` |
| MainCar.Shift `0x1804FFBE0`..`0x1804FFD00` | `c59a901386ac100d896bd618399959ee20d2232a8c3b09369f3dec3481fccb65` |
| Change-coroutine factory `0x180501070`..`0x1805010E0` | `3d44caa0439fac3967c976784de96bf3a1eb8517575e4108a43700f3b9d17fe2` |

### Untraced remainder and scope limits

The remainder includes the scheduled change coroutine's MoveNext body,
ReadInputs' indirect dispatch target, other obfuscated helpers/coroutines not
expanded above, other car/AI/replay input producers, alternate control backends,
and Unity/native physics internals. There was no whole-binary field-reference
closure or exhaustive call-graph proof. Their behavior and any undiscovered
consumer remain unknown. No clutch action, normalized pedal scale, bite law,
disengagement behavior or native-routed candidate can therefore be specified.

The ledger comparison still finds all 37 toolkit IDs represented; STD-016/017/020
remain unchecked and STD-031/037 pending. No adoption is promoted. Only this
document is changed. No build, test, install, game launch, input/device/display
operation or owner-recording read was performed. Existing ilspycmd 11.0.0.9375
was used only to read interop identities: its first inventory command emitted an
automatic update notice, so network-free execution of that invocation cannot be
asserted. Subsequent decompiler use explicitly set `--disable-updatecheck`;
no package installation or deliberate network query was performed.

## Name and string closure, 2026-10-23

A read-only follow-up tightened the static limit without disassembly. Inputs matched the pins above:
GameAssembly `f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c` and
`global-metadata.dat` `3b5d7167aea83febdc625f1635fc7cb414ff13ca609d78cc32158133c3877815`,
hashed in place in the installed folder (nothing written).

- **IL2CPP metadata:** a case-insensitive search of the whole `global-metadata.dat` (identifiers and
  code string literals) finds "clutch" exactly once, as the bare identifier `Clutch`.
- **Interop:** of the 73 generated interop assemblies in `components/wheel/lib/interop`, only
  `Unity.InputSystem.dll` contains a clutch-named identifier (Unity's generic HID usage name). Game code
  (`Assembly-CSharp`, `Assembly-CSharp-firstpass`) declares no type, field, method or property named clutch.
- **Assets:** no `*.assets` or `level*` file (2.8 GB, texture `.resS` streams excluded) contains "clutch".
  StreamingAssets holds only car skins and thumbnails. The stock action sets (`GamePadSystem.LoadActionSets`)
  therefore name no clutch action.

**Exact static limit:** the pinned build has no named clutch action, identifier, code literal or
asset string. The traced producer, Controls, car, dispatcher, Accelerate/Brake/Shift and engine/
transmission readers above have no clutch read. What a name search cannot exclude is an unnamed
(obfuscated) member implementing clutch-like coupling with no string at all. Only whole-binary dataflow
could rule that out, and there is no action source that could feed it a pedal value. The A/B/C owner
decision stands unchanged; clutch stays unsupported. No build, test, install, game, device or lease.
