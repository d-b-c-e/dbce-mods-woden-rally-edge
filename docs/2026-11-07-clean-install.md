# Clean install on a stock game copy (2026-11-07)

Package: `0.2.16+4a503c1` (`components/wheel/dist/WodenRallyEdgeWheel-0.2.16-iss0047fix2-private-shim-dev.zip`), the
build installed on the rig. Run by lane-a with the rig lease, 18:38 to 18:48. This is Phase B; Phase A (file checks,
17/17) is `components/wheel/docs/CLEAN-INSTALL-PHASE-A.md`.

## Setup

- Copied the installed game without the mod files (`BepInEx`, `dotnet`, `WodenWheelBackups`, `winhttp.dll`, doorstop
  files) to `E:\Source\temp\woden-rally-edge\clean-2026-11-07\game`; all 744 files hash-matched the Steam install.
- Installed with the package's own `Manage-Install.ps1` (what `Install.bat` runs), no local loader archive: it downloaded
  the pinned BepInEx and checked its hash. Exit 0, every payload byte-exact.
- Backed up the owner's Unity prefs key and LocalLow saves (`WodenUserState.ps1 -Save`), Steam's cloud folder
  (`userdata\22623167\3218630`) and the owner install's `BepInEx\config`, then renamed the saves folder and prefs key
  aside so the copy saw a true first launch.

## Results

| Step | Result |
|---|---|
| Install | Exit 0; loader downloaded and verified; payloads byte-exact. |
| Start without `steam_appid.txt` | BepInEx built its interop in about 20 s and the mod loaded, then the game restarted itself through Steam into the owner install. That process was closed at once by its PID. Steam had restored the owner's cloud saves for it; nothing uploaded. The owner install's mod settings file was re-saved by the plugin (comments and order only) and was put back byte-exact. |
| First launch (copy has its own `steam_appid.txt`) | No saves, prefs or mod settings. Mod loaded (`Woden Wheel 0.2.16`), opened the game as one borderless window across the three screens, first screen the game's Message of the day (dark by design, drawn across all three). Force feedback stayed idle: no wheel bound. Closed cleanly; the mod wrote its settings file (376 lines). |
| Relaunch | Settings unchanged apart from the dev input switch turned on for the test. With F6 closed, the title (86/100/100% picture) and the main menu of a new career (97/92/97%) fill the screens (`Test-GamePicture -Menu` PASS). F6 opened on Setup: Steering, Throttle and Brake "Not available", "Next: bind or reconnect Steering", a Bind Steering button. Closed cleanly. |
| Uninstall | Exit 0. Every stock game file unchanged; no mod payload left. The loader, settings, logs, interop cache and backups stay, as designed (Phase A). |
| Owner state | Saves, Unity prefs and the owner's mod settings byte-identical to the backup. |

Installer checks: `components/wheel/tools/Test-Installer.ps1` passes. `Test-ManageInstall.ps1 -PackageRoot` on this
package stopped at its default-root `Install.bat` step in the agent shell: the `cmd` it starts could not find
`powershell.exe` (even `where` fails there). The real install above ran the same script, so re-run that test from a
normal desktop PowerShell.

## Steam Cloud

Woden keeps `LocalLow\ViJuDa\Super Woden Rally Edge\Save` in Steam Auto-Cloud. Steam syncs it when the game exits even
when the copy's exe was started directly. The copy's fresh day-1 save uploaded (cloud change 97 to 99), and
`CUSTOM_CAR_NAMES.txt` dropped out of the cloud list. After restoring the owner's files, the owner install was started
once through Steam and closed at its first screen. Steam then uploaded the restored files (change 100): the cloud
`Supersave0.sav` equals the owner's local save and `CUSTOM_CAR_NAMES.txt` is back. Before this run the cloud copy already
differed from the local save: it held a save the game wrote during the ISS-0047 run at 18:28. Now both hold the owner's
local save from November 3.

Turning Steam Cloud off for the game needs the Steam Properties dialog, so it was not automated. Next time, plan the
sync-back step, or ask the owner to switch cloud off for the game first.

## Notes

- F6 reacts only while the game window has focus (by design). The dev input channel passes the Message of the day with
  `pad A` and the title with `pad Start`.
- Left idle on the title, the game plays an attract demo in a centered 4:3 view with black side screens. This is the
  game's own demo, not the player camera.
- Not covered: binding the wheel and driving on the fresh install.

Captures, logs, hash lists, the owner backup and the run scripts are in the git-ignored
`artifacts/clean-install-2026-11-07/`. The stock copy was deleted after the run.
