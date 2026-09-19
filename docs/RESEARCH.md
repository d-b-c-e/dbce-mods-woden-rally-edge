# Installed-build findings

Source: local Steam install and original feasibility survey in dbce-wheel-mod-toolkit, 2026-09-12. [woden-rally-edge-wheel, 2026-09-12]

This page preserves the initial static survey. For subsequent crash diagnosis,
runtime evidence and the installed 0.2.2 input/camera/FFB implementation, read
[STATE](STATE.md) and [HANDOFF](HANDOFF.md). Initial unknowns below are not a
replacement for those versioned results. [woden-rally-edge-wheel, 2026-09-14]

| Item | Observed |
|---|---|
| Game | Super Woden Rally Edge, Steam 3218630 |
| Build | 21802346, depot manifest 223460804701382137 |
| Unity | 6000.3.6f1, x64 IL2CPP metadata version 39 |
| Metadata | Readable field names, partly obfuscated method names; 9,751 types / 118,512 methods mapped in the original static survey |
| Offline generated references | 71 assemblies with BepInEx #788's Cpp2IL/Il2CppInterop libraries and no Unity unstripping |
| GameAssembly SHA-256 | f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c |
| global-metadata.dat SHA-256 | 3b5d7167aea83febdc625f1635fc7cb414ff13ca609d78cc32158133c3877815 |

MainCar exposes IsPlayer/PlayerIndex, a Rigidbody, Controls, detailed WheelData with explicit axle arrays, engine/transmission/steering structures, status/gameplay meters, CarClock and Car_Cam. Readable methods include FixedUpdate, Steer, Brake, Shift and HandBrake. The first plugin uses the readable FixedUpdate hook; it does not patch hardcoded addresses or assume Shift's integer argument is an absolute gear.

Static candidate RVAs: MainCar.FixedUpdate 0x4FD330, Controls.FixedUpdate 0x9A41F0, Car_Cam.LateUpdate 0x8557E0. These are a research reference for this binary, not the runtime binding mechanism. Obfuscated managed method names should be treated as generated/build-specific.

Native Logitech wrapper libraries and steering-reader components exist, with normalized steering/pedal fields and spring parameters. Their existence does not prove active native wheel support, correct DirectInput handling or useful FFB. The initial 0.1.0 survey had no mod force path. Later development added provisional toolkit FFB and stock-reader/SDK ownership transfer; stock-reader suspension was observed, while physical output acceptance remains pending in STATE.

WheelCollider exposes GetWorldPose, GetGroundHit, RPM, radius, motor/brake torque and steer angle. WheelHit has direct `m_*` data fields even where property getters were stripped. Centre and suspensionDistance are missing from the generated WheelCollider API; Rigidbody has no readable mass/rotation getter. The sampler uses the body's transform quaternion and keeps absolute suspension travel/mass unavailable.

The game has a native Chase camera mode hidden through a BIOS-menu setting; bonnet support was not verified in the research. Camera modes also include IsoMetric, Photo and Replay. An experimental transform override has now been compiled, but camera topology/culling and visibility still need inspection in a running scene.

Primary/public references: [Steam game page](https://store.steampowered.com/app/3218630/Super_Woden_Rally_Edge/), [wheel discussion](https://steamcommunity.com/app/3218630/discussions/0/682991471067102029/), [firsthand hidden-camera guide](https://steamcommunity.com/sharedfiles/filedetails/?id=3651982159), [BepInEx IL2CPP installation](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html). Earlier community wheel reports predate the installed February build and do not establish this binary's behavior.

## 2026-09-14: handbrake and reconnect stall

Static inspection of the guarded build confirms Controls.FixedUpdate calls
MainCar.HandBrake(bool released), with false for pressed. The pressed branch
sets HandBrakeActive, applies 2000 rear brake torque only where wheel rpm > 30,
and copies HandBrakeStiffnessLoss to HbStiffNessLossTemp. Release recovers the
latter toward zero at HB_RecoverSpeed. Accelerate separately applies a binary
0.3 power multiplier when HandBrakeActive. The serialized HandBrakeTorque field
is not the value used by this pressed branch. Do not claim a fully analog engine
response. 0.2.3 scales the actual native rear brake result and scoped grip-loss
parameter for partial axis pulls; full pull/buttons retain native behavior.

The 0.2.2 native log at 21:35:18 shows roughly 424 ms closing readers, opening
FFB and reopening readers, followed immediately by Stop/Free and another reader
enumeration. The consumer's transient Suspend shutdown plus stale/contact/camera
gates can repeat this cycle before any ordinary force write. 0.2.3 retains the
connection on transient gates and initializes at zero outside the car sampler.
This is a confirmed lifecycle defect; all perceived stutters still need a capture
for attribution. Native disassembly and owner logs stay in ignored artifacts/backups.


## 0.2.4 camera binding review

The local Woden interop exposes `Camera.fieldOfView` get/set, `nearClipPlane` get only, `Object.m_CachedPtr`, MeshFilter/sharedMesh/bounds and keyboard Key controls. The guarded UnityPlayer binary contains `UnityEngine.Camera::set_nearClipPlane_Injected`; the local `set_farClipPlane_Injected(IntPtr _unity_self, float value)` wrapper confirms the neighboring scalar property ABI. The optional near-clip delegate uses the native object pointer, not its IL2CPP wrapper pointer, and retains the game clip value if resolution fails. [Unity 6000.3 Camera bindings](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Runtime/Export/Camera/Camera.bindings.cs) define near/far/FOV as scalar camera properties. This is binding/source evidence; runtime resolution and hood framing need the next attended run.

Bonnet fitting uses the body mesh's local bounds transformed through the renderer into car-local space (eight corners), cached per car. It does not use a rotated world AABB. The windscreen/hood position is a geometric heuristic, not measured hood vertices; manual adjustment is provided. Global manual presets persist, but separate manual presets per car are not yet implemented.


## Countdown input / native warm-up review — 0.2.6

Native Controls.FixedUpdate reads the selected action table, forwards throttle/brake/steering and dispatches Camera (action 5) without a RACE check. Car_Cam.ChangeCamera checks the pressed latch and Chase mode. The mod formerly gated its complete action override and camera ownership on Runtime.Driving, which excluded WARMING.

The guarded MainCar.FixedUpdate has a WARMING branch into its free-rev path: Pedal_Acc_Value (0x3d0) drives the RPM value (0x368), with the native fuel-cut sound call. Separately, its WARMING path applies wheel brake torque. These offline native observations support forwarding calibrated controls while preserving the game's start-line lock, rather than spoofing RACE or modifying physics. Private disassembly remains in ignored artifacts. No native offsets are added to the implementation.

## Native countdown/time-limit review — 0.2.6

The guarded CountDown.Update (RVA 0x8a1900) owns TimeLeft, Active, InfiniteTime and TimerControl independently from CarClock. Active timing subtracts Time.time - TimerControl from TimeLeft, replaces TimerControl with Time.time, then warns/checks expiry. The native time accessor was resolved to UnityEngine.Time::get_time(); this is the same frame clock used by the hook. Advancing this component's anchor before Update scales the decrement before expiry is evaluated; post-update refunds would be too late. The hook never writes TimeLeft, global Time.timeScale, CarClock anchors or checkpoint awards.

Scope requires a selected RACE player without Replay, a RaceConditions.PlayerCarList count of one, and timer.GM equal to race.GM. Inactive, paused and infinite-time contexts bypass adjustment. An unconsumed anchor restores on early return or exception; the native replacement anchor is retained. The single-player ownership relationship and live countdown rate still need runtime confirmation. Private decompilation/disassembly stays in ignored artifacts.

## Native window-selection failure — 0.2.7

The 0.2.6 native log shows the owned game HWND replaced by a different foreground HWND before successful device initialization; InstallExitGuards then fails SetWindowLongPtr with Win32 error 5. Toolkit v0.12.0/native 0.5 source assigns g_hwnd from ResolveGameWindow, then overwrites it with ResolveHwnd(hwnd). With hwnd=0, that second function takes the unverified foreground window. 0.2.7 captures and validates the owned visible UnityWndClass handle and supplies it explicitly via the existing managed toolkit API, preserving all lifecycle gates. The guarded UnityPlayer binary contains that window class name; runtime handle selection still needs the next attended launch. Native source correction is tracked in the toolkit knowledge base; no native binaries were replaced.
