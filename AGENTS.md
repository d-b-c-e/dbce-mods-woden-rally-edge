# Woden Rally Edge Wheel — working notes

Read `docs/STATE.md` first, then [the session handoff](docs/HANDOFF.md). 0.2.7 repairs FFB initialization window selection: the 0.2.6 log showed a foreign foreground HWND and exit-guard failure with zero force writes. Always pass the verified owned Unity HWND to the pinned toolkit; never bypass exit guards or return to hwnd=0. Bonnet defaults now include the owner's +0.15 m up / +0.05 m forward correction; preserve their saved manual view. The owner felt improved force on 0.2.7; formal sign/load/lifecycle acceptance remains pending. 0.2.6 allows calibrated wheel/camera controls during the start-line countdown while preserving native start locks; FFB still requires active racing. The owner requested an optional countdown/time-limit assist: Advanced → Driving in 0.2.8, default Off / 75% speed, restricted to the owned single-player timer. Do not alter lap/stage clocks or enable it silently. Original force gain and the 50% default remain. The owner called 0.2.5 a good starting point but noted possibly lingering cornering force; record and investigate before retuning. Do not claim countdown behavior, hood framing, lens restoration or analog handbrake physically accepted until actual runs are recorded. Update STATE when evidence changes.


The currently installed build is **0.2.14**, receipt source `e3d118e6d72e1e9847b73620e02e9f101fb22de3`; all nine receipt payload hashes matched on October 3. Saved FFB is Off at 49.583332% strength / 25% peak. See STATE for the installation boundary. The earlier 0.2.12 runtime source `bf58042`, installer revision 3, backup `WodenWheelBackups/before-install-20260929-003548-381c44f1` and private evidence `artifacts/deployment-0.2.12-preinstall` are historical. See `docs/UX-OVERNIGHT-2026-09-16.md` for the earlier option inventory and evidence split. Preserve Simple/Advanced, additive handbrake bindings, transactional saves, wheel menu buttons, owner tuning and the HWND repair. Camera defaults must preflight all conflicts before changing any assignment (toolkit addendum `483bebd`). Run `dotnet run --project tests/WodenRallyEdge.UiTests -c Release` as well as the executable regression harness; its fake Unity/device fixture is not runtime acceptance.

0.2.12 was reviewed and installed closed-game. Source is `bf58042fbf6ebea9175d8a85f332dd1ffd203ea6`; ZIP SHA-256 is `09088883102cc212b8fd0119b09fe1440efb9e1bb2158b3a60666185df7d279c`. Its recorded-playback capability is `signal-reprocess`: preserve original Recording JSONL, request/source/config/profile identity, exact case and baseline bytes, ordered model validity/reset/gate semantics and pure actual-`ForceSignal` observations. Tuning trials must use a separate hashed config and new observation path while retaining the original case hash. Structural timeline/count mismatch is unavailable, not a numeric comparison. Never describe it as deterministic game-input replay or delivered/physical output. Refuse incomplete/dropped/missing-channel/idle-only sources. The retained 0.2.11 ZIP is immutable and was never installed. Keep shared contract source `87b47d53121c0b3fc8479e622ae8a79c8ca3fa58` separate from pinned native and unpublished Recording binaries. See `docs/RECORDED-PLAYBACK.md`.

## Boundaries

The first attended 0.2.12 live recording completed with 116 seconds of driving, zero capture drops/errors, 6,922 FFB writes/attempts and zero reported failures; see STATE for exact case identity and limits. The owner said force felt good but very weak. The saved 25% cap clipped 43.3% of driving samples, so increasing strength alone cannot raise the peak. Kirby is coordinating a data driven FFB strength approach across rig projects; do not silently change this owner's tune. A new launch still needs an attended owner and serialized desktop lease. The earlier force-disabled 0.2.9 check verified IMGUI F6 opening, Advanced rendering, calibration Cancel and release-to-close, with 437,436 stock producer reads, normal exit, zero force writes and exact config restoration. Direct startup keyboard paths were subsequently confirmed in source. Preserve the scoped DailyMessage/TitleScreen guards in `StartupMenuHooks.cs`, early Settings observation, `InputPolling.OncePerFrame` with same-frame failure retention, and legacy keyboard release checks alongside InputSystem. Preserve stock GamePadSystem/EventSystem ownership and shared 100 ms fresh-neutral close/capture gating. Do not infer the cause of the failed 0.2.8 F6 smoke from the working fallback. Preserve every frozen ZIP, including the uninstalled 0.2.11 candidate. See STATE for exact evidence and identities.

- Keep this repo private unless the owner explicitly requests otherwise.
- Game hooks, channel semantics, input mapping, camera and any future force signals belong here. Device and force output infrastructure, Forza encoding and generic recording belong in dbce-wheel-mod-toolkit. Do not grow another native DirectInput implementation.
- No proprietary game assemblies, generated interop, game assets, recorded owner sessions or loader caches in Git/release archives. Only the toolkit's MIT artifacts are vendored. Recording is separately pinned and unpublished; do not call it part of v0.12.0.
- Owner capture requests are created only when invoked. `Start-RecordedGame.ps1` defaults to one-launch force suppression; `-AttendedFfb` is explicit. Its wait timeouts never authorize stopping Woden. `-PrepareOnly` must remain a no-launch/no-device fixture path. Preserve source recordings and exclusive case/observation creation.
- Never silently change vehicle physics or assists. Preserve the implemented stock Logitech reader suspension/SDK shutdown and refusal to arm when ownership cannot be resolved; the owner felt force on 0.2.3, but lifecycle/sign/load acceptance remains open.
- Experimental FFB now exists. The owner explicitly requested saved On/Off with a new-config default On and no per-session arming (2026-09-14). F8/Stop saves Off; only On resumes. Never enable physical force as an unattended test. Preserve toolkit watchdog, focus/stale/pause/panel gates, exact device selection, exit guards, ramp-in, peak cap and zero-before-window-destruction rules. Normal suppression must not reconnect/enumerate devices. No native condition/periodic effects bypass the constant-force hold watchdog.
- Never stop a running game to deploy. Install only when closed. The packaged `Manage-Install.ps1` supports verified updates, backups, rollback and owned-file uninstall; `Install-Dev.ps1` remains initial-only. Backups for the packaged route live at `<game>/WodenWheelBackups`, outside plugin scanning.

## Architecture

Follow the toolkit's [UX-1 standard](../dbce-wheel-mod-toolkit/docs/CONSUMER-UX.md)
and [setup guide](../dbce-wheel-mod-toolkit/docs/CONSUMER-SETUP.md). Common pages:
Setup, Controls, FFB, Cameras, Telemetry, Help. Display action labels separately
from existing binding keys so wording improvements preserve the owner's bindings.
Record remaining UX gaps in docs/UX-ADOPTION.md. The custom renderer now has keyboard/mouse fixtures; native UI and wheel-only navigation acceptance remain separate.

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
dotnet run --project tests\WodenRallyEdge.UiTests -c Release
.\tools\Package.ps1
.\tools\Test-Installer.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\Test-ManageInstall.ps1 -PackageRoot <stage>
```

The tests are an executable harness: `dotnet test` does not run these assertions. Keep the classic `.sln`. Package via an explicit file allowlist. No CI/release automation has been set up. No commit/push to sibling projects is needed for ordinary Woden work.

Machine: MOZA R12, SIMAGIC DS-8X, MOZA stalk. Game directory defaults to `D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge`. F6 now provides calibration, per-device bindings, telemetry/capture, camera adjustment and FFB tuning. UI implementation and physical testing remain separate milestones. Woden strips GUI.Button/GUILayout and style/cursor setters; Panel uses IMGUI rectangles/events and UiNative resolves named Unity engine bindings. Do not replace this game's interop with sibling references.

## Toolkit standards

At the start of every session, compare the wheel toolkit's ledger
(`E:\Source\toolkits\dbce-wheel-mod-toolkit\STANDARDS.md`) with this repo's
`TOOLKIT-ADOPTION.md`. Report any entry that is `pending`, `unchecked` or missing
from the adoption file, and bring it in when your work touches that area. When you
adopt one (or find it does not apply), update `TOOLKIT-ADOPTION.md` in the same
commit. When you set a new family-wide standard, append it to the toolkit ledger
and commit it in the same turn; do not leave it only in an uncommitted file.
