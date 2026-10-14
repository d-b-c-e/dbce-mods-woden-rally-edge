## Shared wheel controls without Wheelkit (private shim package)

This private package can also take its wheel bindings from a DBCE rig profile instead of the mod's own F6 bindings.
The ordinary package, without the shim, does not need any of this: bind everything in F6.

1. Close the game and install this package with **Install.bat**.
2. Double-click **Set Up Controls.bat**. It checks the package, then opens the DBCE mod setup locked to your Super Woden
   Rally Edge folder.
3. On **Controls**: Refresh devices, bind each action, Save, Preview, then Apply. Apply writes
   `BepInEx\config\dbce-controls.ini` and sets `[Input] Backend = Shim` in `BepInEx\config\dbce.wodenrallyedgewheel.cfg`.
   The mod reads both at its next start. The **Screens** and **Settings** tabs write only what you change there.
4. Start the game normally. Keep Unity gamepads disconnected, and choose Manual in the game for sequential or H-pattern
   gears. Force feedback with the shim, and physical force in general, are not yet qualified.

To go back to your own F6 bindings, close the game and set `[Input] Backend = Native` in
`BepInEx\config\dbce.wodenrallyedgewheel.cfg`; the F6 panel cannot change it. Your F6 bindings stay in
`BepInEx\config\wheel-bindings.json` throughout.

The setup helper runs from this package folder and is never copied into the game folder. Uninstall.bat leaves the
helper's own saved profiles in `%LOCALAPPDATA%\Dbce\StandaloneModSetup`.
