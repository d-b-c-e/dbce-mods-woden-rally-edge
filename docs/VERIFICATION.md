# Verification — 2026-09-13

## 0.2.2 input boundary, native camera cycle and UX alignment

Release solution build passes with zero warnings/errors. The executable harness
passes **14 suites / 405 assertions**; CameraCycle covers stock wrap, bonnet/bumper
ordering, disabled-view skipping and handoff without invalid game preset values.
The schema has **199 definitions**, including camera authority and input ticks.
Controls action mapping was checked against native code and the installed action
names; runtime effectiveness is still untested. The revised six-page UI and
camera/FFB ownership gates require attended checks. No unattended force applied.

## 0.2.1 settings, FFB prototype and first runtime test

Release build and package pass with zero warnings/errors. The executable harness now passes 13 suites / 396 assertions. The package has 193 channel definitions and the same allowlisted MIT/plugin payload. Runtime boot, injected Update, F6 rendering, R12 device reads and stock Logitech reader suspension were observed. The owner has started UI/level testing.

0.2.0 crashed on its first level-entry contact read. The ErrorLog stack identified the generated `WheelCollider.GetGroundHit(out WheelHit)` wrapper. Inspection showed an 8-byte output slot for a non-blittable value result. 0.2.1 replaces that wrapper with a runtime-allocated, GC-rooted IL2CPP value passed by reference. The actual level-entry retest logged a successful 72-byte contact read, then at least 1640 local samples at approximately 51 Hz, followed by normal plugin/worker shutdown. No new crash appeared in ErrorLog. Payload semantics and physical driving/FFB still need an attended recorded session.

0.2.1 preserves user-edited configuration and bindings. The earlier plugin/config/logs were backed up outside the plugin scan directory. All deployed payload hashes were checked. No nonzero force was applied by the agent.

## Historical 0.1.0 foundation checks

Environment: Windows, PowerShell 7, .NET SDK 10.0.401. Plugin/core target net6.0; offline tools/tests target net10.0. All game-facing references were generated from the installed Woden build, not iRacing's game assemblies.

| Check | Result |
|---|---|
| Offline Cpp2IL/Il2CppInterop generation | 71 assemblies, metadata 39; completed without launching/loading native game code |
| Release solution build with `-warnaserror` | Passed; zero warnings/errors |
| Executable regression harness | 9 suites, 69 assertions, zero failures |
| UDP transport | A real loopback listener received the 324-byte active packet, then inactive packets after source timeout |
| Numeric capture | Synthetic fixture finalized and fully validated, with no dropped samples |
| Inspector | Accepted complete synthetic fixture; rejected missing-footer fixture with exit code 2 |
| Package | Allowlisted plugin payload and 173 channel definitions; no game/interop/loader libraries in ZIP |
| Installer fixture | Initial layout and blank UnityBaseLibrariesSource correct; repeat install refused without changing plugin |
| Real game | BepInEx directory absent after verification; no deployment, launch or torque output |

The final local package SHA-256 is `24e17dfd15e577b4f295716976db4ba6cee35474db1ddd360d45b4c7f5317908`. Its plugin DLL SHA-256 is `f54ac2b29089088abb440f763755b18b316bb2d3cd64cbfa1ae7e346c7305e0f`.

Package: `dist/WodenRallyEdgeWheel-0.1.0-dev.zip`. Synthetic recordings, installer fixtures and inspector outputs are in ignored `artifacts/`; they are not gameplay evidence. The manifest in the ZIP hashes its actual payload. Repackaging can change the ZIP hash without changing the plugin binary.

These checks establish a compilable and testable development foundation. Loader boot, hook execution, actual signal meanings/rates, driving controls, camera visibility and physical feedback remain the live gates in STATE.md.
