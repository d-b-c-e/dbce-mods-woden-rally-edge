# Super Woden Rally Edge setup

1. Close the game and extract the entire player ZIP into a new folder.
2. Run **Install.bat**. It finds Steam libraries or asks for the game folder. A first install downloads the pinned BepInEx loader; updates reuse the supported loader. Players need Windows PowerShell 5.1 and no SDK or compiler.
3. Launch normally through Steam. First launch after installing or updating the game can take longer while the loader prepares support files.
4. Press **F6 → Controls**, select and calibrate the wheel/pedals, then preview their direction. Select the intended force-feedback device explicitly in FFB. **F8** saves force feedback Off; choose On to resume. Existing bindings and tuning are retained.
5. Enable Telemetry only when ready to use a Forza UDP receiver such as SimHub with the matching port. Recording/playback sessions suppress physical output and network sends while retaining original physics/effect data for offline analysis.

For triples, first select your intended Windows display profile and one three-panel-wide game resolution. The mod's Auto mode creates three angle-correct views within that wide window; it does not switch Windows from independent monitors to Surround. Enter your own panel size, eye distance, bezel gap and side angles. Apply display choices before launch, then let the game apply its resolution once. Avoid live resolution/fullscreen changes during automated testing.

Change camera cycles enabled stock, Bonnet and Bumper views. On triples, **F7** switches the current camera's saved **Game/Realistic** set; F6 → Cameras has the same button and FOV slider. Numpad **+** zooms in, **−** zooms out, and **5** resets that camera/set to its calculated start. Position/pitch defaults: 8/2 forward/back, 9/3 up/down, 4/6 left/right, 7/1 tilt. Custom bindings stay intact. Game FOV wider than seat-true stretches sides. **Shift+F7** compares correction for this session only; ordinary triples remain corrected. See [camera sets](2026-11-03-camera-sets.md).

To update, close the game and run Install.bat from the newly extracted package. Backups are in `WodenWheelBackups` inside the game folder; the ownership receipt is `BepInEx/WodenWheel-install.json`. **Uninstall.bat** removes only verified owned files and keeps settings, recordings, other mods and the shared loader. Keep recovery.json and the backup if an external change prevents rollback. Do not overwrite an unknown changed file.

Supported target: Steam app 3218630 / build 21802346, Unity 6000.3.6f1, GameAssembly SHA-256 `f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c`. Unknown builds are refused. Cross-game physical force normalization and the full device/track matrix remain separate from playback acceptance.
