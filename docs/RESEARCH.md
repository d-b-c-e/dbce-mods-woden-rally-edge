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
