# Woden Rally Edge Wheel — working notes

Read `docs/STATE.md` first. This repository begins with compiled, offline-tested code. Do not call game hooks, wheel controls, bonnet placement, SimHub reception or FFB physically verified until an actual run and its evidence are recorded there.

## Boundaries

- Keep this repo private unless the owner explicitly requests otherwise.
- Game hooks, channel semantics, input mapping, camera and any future force signals belong here. Device and force output infrastructure, Forza encoding and generic recording belong in dbce-wheel-mod-toolkit. Do not grow another native DirectInput implementation.
- No proprietary game assemblies, generated interop, game assets, recorded owner sessions or loader caches in Git/release archives. Only the toolkit's MIT artifacts are vendored. Recording is separately pinned and unpublished; do not call it part of v0.12.0.
- Never silently change vehicle physics or assists. Native Logitech wheel ownership must be investigated before enabling a force route.
- Current plugin never initialises FFB or sends torque. Do not enable physical force output as an unattended test. Future force support needs the toolkit's watchdog, focus/stale/pause gates, exact device selection, exit guards, ramp-in and zero-before-window-destruction rules.
- Never stop a running game to deploy. Install only when closed. The initial installer refuses existing plugin installations; review backups/configuration when adding an update path.

## Architecture

`src/WodenRallyEdge.Core` is plain managed telemetry, calibration and output logic. `src/WodenRallyEdge.Plugin` targets net6.0 for BepInEx BE #788 and uses Woden-specific generated references. Keep injected MonoBehaviours limited to Unity messages. Counters must prove hooks actually run; an installed Harmony patch is not runtime proof.

`MainCar.FixedUpdate` postfix samples BEFORE Unity's next physics solve. Inputs may describe the upcoming solve while rigidbody/contact data describes the preceding solve. Preserve that phase label until a post-solve hook is verified. Use `Time.timeAsDouble` inside the hook; wall time comes from Stopwatch. Never differentiate local velocity directly across rotations. Reset derivatives at pause, respawn, identity/time gaps and teleports.

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

Machine: MOZA R12, SIMAGIC DS-8X, MOZA stalk. Game directory defaults to `D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge`. Future UI should have calibration, per-device bindings, telemetry status/capture, camera adjustment and understandable FFB tuning like the reference projects. UI implementation and physical testing remain separate milestones.
