# Woden Rally Edge Wheel — working notes

Read `docs/STATE.md` first, then [the session handoff](docs/HANDOFF.md). 0.2.7 repairs FFB initialization window selection: the 0.2.6 log showed a foreign foreground HWND and exit-guard failure with zero force writes. Always pass the verified owned Unity HWND to the pinned toolkit; never bypass exit guards or return to hwnd=0. Bonnet defaults now include the owner's +0.15 m up / +0.05 m forward correction; preserve their saved manual view. Physical repair acceptance remains pending. 0.2.6 allows calibrated wheel/camera controls during the start-line countdown while preserving native start locks; FFB still requires active racing. The owner requested an optional countdown/time-limit assist: Setup → Difficulty, default Off / 75% speed, restricted to the owned single-player timer. Do not alter lap/stage clocks or enable it silently. Original force gain and the 50% default remain. The owner called 0.2.5 a good starting point but noted possibly lingering cornering force; record and investigate before retuning. Do not claim countdown behavior, hood framing, lens restoration or analog handbrake physically accepted until actual runs are recorded. Update STATE when evidence changes.

## Boundaries

- Keep this repo private unless the owner explicitly requests otherwise.
- Game hooks, channel semantics, input mapping, camera and any future force signals belong here. Device and force output infrastructure, Forza encoding and generic recording belong in dbce-wheel-mod-toolkit. Do not grow another native DirectInput implementation.
- No proprietary game assemblies, generated interop, game assets, recorded owner sessions or loader caches in Git/release archives. Only the toolkit's MIT artifacts are vendored. Recording is separately pinned and unpublished; do not call it part of v0.12.0.
- Never silently change vehicle physics or assists. Preserve the implemented stock Logitech reader suspension/SDK shutdown and refusal to arm when ownership cannot be resolved; the owner felt force on 0.2.3, but lifecycle/sign/load acceptance remains open.
- Experimental FFB now exists. The owner explicitly requested saved On/Off with a new-config default On and no per-session arming (2026-09-14). F8/Stop saves Off; only On resumes. Never enable physical force as an unattended test. Preserve toolkit watchdog, focus/stale/pause/panel gates, exact device selection, exit guards, ramp-in, peak cap and zero-before-window-destruction rules. Normal suppression must not reconnect/enumerate devices. No native condition/periodic effects bypass the constant-force hold watchdog.
- Never stop a running game to deploy. Install only when closed. The initial installer refuses existing plugin installations; review backups/configuration when adding an update path.

## Architecture

Follow the toolkit's [UX-1 standard](../dbce-wheel-mod-toolkit/docs/CONSUMER-UX.md)
and [setup guide](../dbce-wheel-mod-toolkit/docs/CONSUMER-SETUP.md). Common pages:
Setup, Controls, FFB, Cameras, Telemetry, Help. Display action labels separately
from existing binding keys so wording improvements preserve the owner's bindings.
Record remaining UX gaps in docs/UX-ADOPTION.md; the current renderer is mouse-first.

`src/WodenRallyEdge.Core` is plain managed telemetry, calibration and output logic. `src/WodenRallyEdge.Plugin` targets net6.0 for BepInEx BE #788 and uses Woden-specific generated references. Keep injected MonoBehaviours limited to Unity messages. Counters must prove hooks actually run; an installed Harmony patch is not runtime proof.

`MainCar.FixedUpdate` postfix samples BEFORE Unity's next physics solve. Inputs may describe the upcoming solve while rigidbody/contact data describes the preceding solve. Preserve that phase label until a post-solve hook is verified. Use `Time.timeAsDouble` inside the hook; wall time comes from Stopwatch. Never differentiate local velocity directly across rotations. Reset derivatives at pause, respawn, identity/time gaps and teleports.

**Never call the generated `WheelCollider.GetGroundHit(out WheelHit)` wrapper in this build.** BE #788 treats the boxed non-blittable WheelHit result as an IntPtr-sized out slot, corrupting memory on first level entry (0.2.0). Use `WheelContact.Read`, which passes allocated IL2CPP value storage by reference. Any other out/ref non-blittable game struct needs the same review before use. A try/catch cannot recover from this native stack overwrite.

Missing channels mean unavailable. Preserve genuine zero values. All live channel names must exist in `TelemetrySchema`. Raw RPM, fuel, slip and race values keep their game scales until measured. WheelHit.force is a magnitude, not signed lateral force or steering torque. WheelHit.sidewaysSlip is not a slip angle. Do not fabricate surface rumble/curbs, tyre temperatures, engine redline or suspension compression for dashboards.

## Commands

```powershell
.\tools\Initialize-Dependencies.ps1
.\tools\Verify-Dependencies.ps1
dotnet build WodenRallyEdgeWheel.sln -c Release -warnaserror
dotnet run --project tests\WodenRallyEdge.Tests -c Release
.\tools\Package.ps1
.\tools\Test-Installer.ps1
```

The tests are an executable harness: `dotnet test` does not run these assertions. Keep the classic `.sln`. Package via an explicit file allowlist. No CI/release automation has been set up. No commit/push to sibling projects is needed for ordinary Woden work.

Machine: MOZA R12, SIMAGIC DS-8X, MOZA stalk. Game directory defaults to `D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge`. F6 now provides calibration, per-device bindings, telemetry/capture, camera adjustment and FFB tuning. UI implementation and physical testing remain separate milestones. Woden strips GUI.Button/GUILayout and style/cursor setters; Panel uses IMGUI rectangles/events and UiNative resolves named Unity engine bindings. Do not replace this game's interop with sibling references.
