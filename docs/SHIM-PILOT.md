# Shared input shim: wheel-only source pilot

Current install pointer (2026-10-13, 19:17 CT): the optional private-shim package from `f252debc` is installed under revision-5 receipt `3041965C` with all 23 payloads verified, including the pinned ABI-2 shim DLL. The saved Backend remains Native by default; no Shim opt-in or game launch occurred. The former `7c1e287` Native production payloads and receipt `66EC5A30` are retained in the installer backup. [Exact install readback](../../../dbce-project-mgmt/artifacts/woden-private-owner-install-20261013/README.md) is also summarized in `components/wheel/docs/STATE.md`. The October 12 pilot evidence below is historical. Ordinary Shim FFB-open/shared-reader coexistence and physical force remain unqualified.

2026-10-12. Based on Woden `ff1c594`. Three private force-fenced launches restored
exactly; run03 observed exact profile parsing, ordinary host ownership/ticks,
native pad isolation and the active panel. Positive device epochs were not
captured by its startup-only report; action delivery remains unqualified. No permanent
install, physical force or rendered shared-control acceptance.
The accepted Classic/Grip models, gains, triple/span code and owner files are
unchanged. The native producer audit is pinned to GameAssembly SHA-256
`f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c`.

## Opt-in and ownership

The existing `dbce.wodenrallyedgewheel.cfg` gains `[Input] Backend = Native`.
`Native` retains standalone F6 calibration and `wheel-bindings.json`. A reviewed
private pilot can set `Backend = Shim`, retain `[Wheel] Enabled = true`, and place
an accepted `dbce-controls.ini` in `BepInEx/config`. The plugin takes the exact
`[Controls]` bytes at launch; changes require restart. Saved bindings remain intact.
F6 Setup/Controls then show the shim's cached action status and supported scope,
without offering legacy calibration that would not affect active input.
Before ownership, Controls shows pending/refused status and retains the still-active
Native bindings. Shim-only window selection can use the verified live, visible,
current-process Unity main window behind a console. Physical FFB still requires
Unity foreground. Shared wheel actions require actual Unity foreground; native
pause/panel gates and fresh-neutral rearming remain. Loss of the owned window
terminally stops the shim rather than switching windows or restoring legacy input.

The privately supplied DLL copied to the plugin build output is the exact provisional DRIVE ABI 2
pin: see [provenance](../vendor/dbce-shim/PROVENANCE.md). It is **not** a new toolkit
pin. `ShimNative.Load` checks its hash, ABI, frame/argument sizes and all 31 action
names before opening an ordinary host. No injection/capture mode is exposed.
The existing public package allowlists and installed Wheelkit discovery remain
unchanged; they do not yet distribute or advertise this private pilot. Private
test staging must include this pinned DLL explicitly. An October 13 source-only
`Package.ps1 -PrivateShimPath` candidate adds the DLL and receipt capability only
to a separately named private archive; see [the package boundary](2026-10-13-private-shim-package.md).
Public distribution awaits
the upstream dependency/source-license disposition and consumer qualification.

Admission requires an observed native `GamePadSystem`, an owned game window,
`Gamepad.all.Count == 0`, no cached `Game_Pad.Controller`, and neutral cached raw
pad axes/buttons. It never removes, disables or name-matches a Unity input device.
Admission refusal leaves Native active with a reason in F6/logs. A gamepad arriving
after admission stops shim delivery for the process. It remains a native gamepad;
coexistence is unsupported. Neither legacy DeviceHub bindings nor another device
becomes a fallback after shim ownership. Missing/stale/nonfinite actions are neutral.

## Native source isolation and delivery

The pinned native `GamePadSystem.Update` at `0x180C000A0` invokes `ReadInputs`
(`0x180BFD6F0`). That function projects its cached Gamepad axes/buttons into the
action table, then independently calls legacy `Input.GetKey` at `0x180BFE498` for
each action's `KeyBoard_Key`. Null Controller alone is insufficient: cached raw
pad fields can still project. The wheel-only admission excludes that source;
it does not patch or replace the native producer. `MenuControls.Update` also
reads native keyboard keys, which remain untouched.

`WheelInput` combines shim axes with these clean native actions in the existing
scoped `InputLease`. Held keyboard steering takes priority; pedals take the maximum.
Native keyboard buttons remain set. The boxed action values are assigned back and
restored after the native consumer, including interrupted writes. Sequential shifts,
handbrake, camera/look-back, reset, horn, menus, Settings and Stop FFB use existing
game-specific consumers. Context/focus changes and unavailable buttons require a
fresh neutral release before a new button press. The October 13 source candidate
also feeds HPattern gates 1-6, reverse and neutral to the existing Manual gearbox
emulator; stick positions remain held across contexts. Clutch, select and gears
7/8 stay unsupported. See [H-pattern parity](2026-10-13-shim-hpattern.md).
Legacy binding reads are disabled only while the shim owns input. Lights/Records/Next song have no shared
action and keep native keyboard behavior.
Shared shift buttons also require Core's reported `Transmission = Sequential`,
the exact consuming car's native Manual gearbox, and no gear change in progress.
Automatic/HPattern/unknown profiles report unsupported sequential shift actions. F6
names a Sequential-profile/native-Automatic mismatch. A held button cannot cross
a blocked state into a fresh shift; release and press again. Native keyboard
shift actions retain their own game behavior.

Native Core remains the Controls parser and normalizer. After it accepts the exact
section hash, the already-vendored shared `ControlBinding` parser reads only the
steering instance GUID for FFB selection. An explicit FFB device remains explicit.
DeviceHub continues its existing polling/force close-readers/refresh/zero-reacquire
lifecycle but does not supply shim-owned wheel actions. Missing live axes withhold
the force input sample; terminal shim failure releases FFB without changing its
saved preference or tune.
The new shim owns a separate nonexclusive DirectInput reader. Closing DeviceHub
readers for the existing exclusive FFB open does not close that shim reader.
Their coexistence is not yet tested; offline identity/lifecycle tests do not prove
exclusive force acquisition or physical input recovery on the rig.

## FFB and direct-provider reader boundary (source review, 2026-10-13)

`ForceController.PrepareCore` resolves the force target from `WheelInput.SteeringGuid`, closes only `DeviceHub`'s `WheelFfbNative` read slots, opens the selected force device, and then refreshes those slots (`components/wheel/src/WodenRallyEdge.Plugin/ForceController.cs:136-176`; `DeviceHub.cs:39-52`). `WheelInput.SteeringGuid` already follows `ShimInput.SteeringGuid` while the shim owns input and withholds a stale Native binding while shared input is pending (`WheelInput.cs:17-24`). The earlier follow-steering identity problem is fixed in this consumer.

Ordinary `ShimInput` separately opens `dbce_shim.dll`'s direct-provider handle and keeps ticking it until a terminal stop (`ShimInput.cs:94-129, 204-221`). `DeviceHub.CloseReaders()` does not close or bridge that handle. The code therefore does not prove that the shim reader continues after the force device's exclusive acquisition. The no-force car trials return before force-device Open (`ForceController.cs:136-137`; [force-capture limits](SHIM-FORCE-CAPTURE.md)), so they cannot answer the driver question.

Art of Sim Rally's shared path is structurally different: its `ShimBridge` borrows existing wheel read slots and copies one snapshot per frame; force reconnect closes those slots and stops the attached shared session before reopening the force device (`src/ArtOfSimRally.Mod/Shim/ShimBridge.cs`, `Main.cs`, `WheelInput.Shim.cs`). That is a useful fallback design if Woden shows an actual sharing conflict, not evidence of simultaneous Woden direct-provider/FFB operation. A fake-device test that merely preserves Woden's current CloseReaders/Open/Refresh ordering cannot establish DirectInput coexistence, so it is not a new gate or a reason to change production source. The remaining qualification is a separate owner-attended ordinary Shim launch that observes shim ticks and input after an FFB Open; physical feel is a further attended matter. Saved Native-default installation and tune preservation do not depend on that result.

## Offline evidence and remaining work

Commands run from the repository root:

```powershell
# Explicit private dependency; see vendor/dbce-shim/PROVENANCE.md. Not in Git.
$env:PrivateShimPath = 'E:/private-dependencies/woden/dbce_shim.dll'
dotnet build components/wheel/WodenRallyEdgeWheel.sln -c Release -warnaserror
dotnet run --project components/wheel/tests/WodenRallyEdge.Tests -c Release --no-build
dotnet run --project components/wheel/tests/WodenRallyEdge.UiTests -c Release
dotnet run --project components/wheel/tests/WodenRallyEdge.InputTests -c Release
```

Results: clean build; 54 core/controller suites, 39,154 assertions; 879 UI assertions
including 65 production shim/lease checks and seven F6 status checks; 3,896 production
DeviceHub checks. The shim fixture calls only real DLL ABI exports, then replaces
handle delegates with bounded fake calls. It exercises real decode, backend,
WheelInput, ControlActions, InputLease and menu source. Force tests use the real controller with a
fake device. This is not proof of in-game response or physical torque.

The keyboard fixture now also uses the observed producer's exact action table:
native `AnyKey` and keyboard actions survive admission, opposite keyboard steering
and brake coexist with shared throttle, keyboard handbrake/horn and shifts remain
available even when shared gears are unsupported, and terminal shared transport
failure leaves native keyboard values intact while blocking legacy wheel fallback.
These source checks do not qualify live key delivery. The first ordinary menu
pilot refused in its developer addon; the second armed the addon but waited for
Unity foreground behind the BepInEx console. Run03 opened through the verified
Unity main window and ticked with unfocused wheel actions neutral. All restored
exactly. See `SHIM-ADMISSION-TEST.md` for its bounded pass and epoch/delivery limits.

Private native audit output is ignored under `artifacts/shim-source-audit` (REA
interop evidence plus bounded native disassembly); no proprietary dump is committed.
Managed UI command snapshots are ignored under `artifacts/ux-ui-fixture`.

Next: independent source review, then a coordinated muted game test of cold native
menus, steering/pedals/buttons, pause/settings/focus loss, device loss, gamepad
refusal and normal exit with exact owner restoration. Follow with attended physical
input/force checks. Exact Unity HID identity joining, controller coexistence,
H-pattern semantics, profile writer/receipt capability, public package integration
and full-session recording remain separate work. Do not claim full multi-device
parity or enable Wheelkit Apply for this protocol before those contracts are agreed.
# Next slice: bounded Core menu trial (October 12)

Source groundwork is in [SHIM-MENU-TRIAL.md](SHIM-MENU-TRIAL.md): distinct schema 4,
latched-only Open2, exact owner profile, known-neutral release admission and a
fixed startup/one-pair menu route through production consumers. Offline actual
Core frames and query/lifecycle fixtures pass. Private trial01 passed actual
Daily/Title -> Garage/Workshop/Garage with Core Completed, R12 epoch 1 and zero
force/network; all 83 file entries and 25 raw registry values restored exactly.
Driving/DS-8X and physical response remain unqualified. No force, tune, triple,
native pin or installed capability change.
