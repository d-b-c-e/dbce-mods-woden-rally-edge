# Super Woden Rally Edge mod — agent guide

Portfolio rules: `E:\Source\dbce-project-mgmt\HOW-WE-WORK.md` (read first; it replaces older process, lease, review and testing instructions). Current focus: `E:\Source\dbce-project-mgmt\NOW.md`. Feature status lives in the portfolio grid (`python E:\Source\dbce-project-mgmt\tools\portfolio_grid.py plan <grid-id>`), not in this file. Grid id: `woden`.

## Game and mod
- Super Woden Rally Edge, Steam app 3218630, `D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge`. Unity with BepInEx BE #788 (generated interop references; plugin targets net6.0). Runtime flavor (Mono vs IL2CPP) is unknown — check `components/wheel/AGENTS.md`.
- Direct wheel/pedal/handbrake controls, force feedback for any DirectInput wheel, Forza telemetry, bonnet and bumper cameras, triple screens, F6 panel. Public repo (the owner pushes).
- Source: root `src/WodenRallyEdge.Core` and `.Plugin`; the older wheel component with its solution is `components/wheel`.

## Build
Exact root build command: unknown — check README. From `components/wheel`:
```powershell
.\tools\Initialize-Dependencies.ps1
dotnet build WodenRallyEdgeWheel.sln -c Release -warnaserror
.\tools\Package.ps1
```
Source verification without a device: `tools/game/Verify-Source.ps1`. Player ZIP: `tools/game/Package-Delivery.ps1`.

## Test
- Executable harnesses (`dotnet test` does not run them): `dotnet run --project tests\WodenRallyEdge.Tests -c Release` and `tests\WodenRallyEdge.UiTests`; installer checks `components/wheel/tools/Test-Installer.ps1`.
- In game: launch via Steam, F6 opens the panel, F8 turns FFB off. Logs: `<game>\BepInEx\LogOutput.log`.
- Back up and restore around a run: BepInEx configs and bindings, saves and `LocalLow\ViJuDa\Super Woden Rally Edge`, and the HKCU Unity preferences.
- Starting a copied exe directly: it restarts itself through Steam into the owner install unless `steam_appid.txt` (3218630) sits beside it; put that file only in a test copy. Saves still sync through Steam Auto-Cloud when it exits, so a fresh-profile run uploads its save; restore, then sync back (`docs/2026-11-07-clean-install.md`).

## Install on the rig
With the game closed, run `Install.bat` from the built ZIP (packaged `Manage-Install.ps1`: verified update, backup, rollback, owned-file uninstall). Backups go to `<game>/WodenWheelBackups`. Rollback: `Uninstall.bat` or the backup.
From an agent shell, run it as `env -u PSModulePath powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File Manage-Install.ps1 -Mode Install -GameDir "<game>" < /dev/null` (bash). An inherited PowerShell 7 module path breaks `Get-FileHash` and the Steam registry lookup, and the installer then waits on a folder-picker dialog.

## Shared code
Vendored toolkit code is under `lib/` and `vendor/` (shim, playback) and `components/wheel`; refresh with `components/wheel/tools/Sync-Toolkit.ps1`. Change shared code in the toolkit and re-sync; never edit the vendored copy.

## Game-specific rules
- Never call the generated `WheelCollider.GetGroundHit(out WheelHit)` wrapper: it corrupts memory on first level entry. Use `WheelContact.Read`.
- `MainCar.FixedUpdate` samples before the next physics solve; keep the phase label and reset derivatives at pause, respawn and teleports.
- Woden strips `GUI.Button`/`GUILayout`; the panel uses IMGUI rectangles and events.
- Recording and playback are developer-only (STD-050): reach them only through `DevSession` behind `[Dev] InternalTools`. Player packages ship no Recording/Playback DLLs, so product code must never touch those types directly.
- Missing telemetry channels mean unavailable; preserve genuine zero. Do not fabricate tyre temperatures, redline or suspension data.
- Never silently change vehicle physics or assists. Never stop a running game to deploy.
- No proprietary assemblies, generated interop, recordings or owner settings in Git or packages.
- Public repo: never push or publish without the owner.

## Key docs
- `components/wheel/AGENTS.md` (deeper technical notes; the top is dated install history, skip it)
- `components/wheel/docs/STATE.md`, `docs/PUBLIC-RELEASE.md`
- `tools/game/player/QUICK-START.md`
- `TOOLKIT-ADOPTION.md` (reference)


<!-- BEGIN project-file-location -->
## Project files and temporary work

Keep project-related research, evidence, scripts, packages, and other working files inside the relevant project folder whenever practical. Keep disposable or machine-specific outputs git-ignored when they do not belong in version control; never ignore files that should be committed merely to hide them. When a project folder is unsuitable, use `E:\Source\temp\<project>` instead of creating scattered drive-root folders. Record the location of material needed by later work and update references when relocating it.

Clean up temporary files and disposable copies promptly once they are no longer useful. Preserve active work, user data, settings, saves, required evidence, backups, rollback artifacts, and worktrees until their retention or replacement is established. Follow existing project, rig, and data-loss safeguards; do not remove files merely because they are old or untracked.
<!-- END project-file-location -->
