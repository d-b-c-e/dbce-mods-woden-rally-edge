# 0.1.0 development build

This is an **offline-tested first implementation**. It has not yet run in Super Woden Rally Edge. Wheel input and bonnet view are experimental and off by default. FFB output is not implemented.

Supports the Windows Steam build 21802346, Unity 6000.3.6f1, GameAssembly SHA-256 `f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c`. An unrecognized native game binary disables the plugin's hooks.

## Initial install from the repository

1. Close the game normally.
2. Run `tools/Initialize-Dependencies.ps1`, then `tools/Package.ps1`.
3. Run `tools/Install-Dev.ps1`. It installs the pinned BepInEx BE #788 and this plugin only when there is no conflicting existing loader/plugin. It does not start the game. Existing Woden plugin updates are intentionally not automated by this initial installer.
4. Launch Woden through Steam. BepInEx generates its runtime references on first launch; this can add a substantial startup delay. Its config must have `[IL2CPP] UnityBaseLibrariesSource =` with an empty value, as seeded by the installer.
5. Inspect `BepInEx/LogOutput.log` for plugin startup, discovered player index and increasing `hooks`/`localTicks`. A patch-installed line alone does not prove sampling.
6. For the first drive leave wheel and camera overrides off. Select a Forza Horizon 5 profile in SimHub with UDP port 8000, but expect only the currently supported motion/wheel channels. Stock engine/pedal/gear gauge mappings are pending.

The ZIP itself contains the plugin folder and its dependencies, schema and hashes. It does **not** contain the BepInEx loader or any game/interop libraries. Manual installation requires the same loader and empty Unity-base-library source setting. Copy `BepInEx/plugins/WodenRallyEdgeWheel` into the game with it closed.

## Configuration

The first plugin load writes `BepInEx/config/dbce.wodenrallyedgewheel.cfg`. Settings currently take effect on restart. Telemetry is loopback-only; either port can be set to 0 to disable that stream. Use distinct ports.

For a recording, set `[Diagnostics] RecordSession = true`, restart and drive a short scenario. Files go to `BepInEx/WodenRecordings`. The recorder stops accepting data at 20 minutes or 64 MiB. Logs report completion, limits and drops. Turn capture off after testing; it does not automatically start another file at a limit.

```powershell
dotnet run --project tools\TelemetryInspector -c Release -- inspect 'path\to\recording.jsonl'
```

The inspector fully validates JSONL and reports coverage, ranges and timing gaps. A structurally valid limit-ended recording may have `completed=false`; the output preserves this distinction. A missing/corrupt footer returns a nonzero exit code.

## Experimental wheel axes

List real devices without applying torque:

```powershell
dotnet run --project tools\TelemetryInspector -c Release -- devices lib\toolkit\native
```

Create `BepInEx/config/wheel-bindings.json` with an exact instance GUID and measured calibration for each axis, then enable `[Wheel] Enabled = true`. All three bindings are required. Separate USB pedals are supported by separate GUIDs. Native axis order is X, Y, Z, Rx, Ry, Rz, slider0, slider1. The toolkit requests 0..65535; verify raw endpoints on the actual device before using them. This build has no calibration wizard or automatic reconnect.

This is a **template, not a MOZA configuration**. Empty GUIDs are rejected; replace each one and measure the endpoints. Steer's Rest means full left and End means full right; Centre is the observed centre. Pedals omit Centre and use released/fully-pressed endpoints. Inverted pedals are represented by decreasing Rest/End values.

```json
{
  "steer": { "deviceGuid": "00000000-0000-0000-0000-000000000000", "axis": 0, "calibration": { "rest": 0, "end": 65535, "centre": 32767, "deadzone": 0 } },
  "throttle": { "deviceGuid": "00000000-0000-0000-0000-000000000000", "axis": 1, "calibration": { "rest": 65535, "end": 0, "deadzone": 0 } },
  "brake": { "deviceGuid": "00000000-0000-0000-0000-000000000000", "axis": 2, "calibration": { "rest": 65535, "end": 0, "deadzone": 0 } }
}
```

The route temporarily substitutes normalized Controls axes around MainCar.FixedUpdate and restores stock fields afterward, including exception finalization. Device/read failures leave the stock route active and require restart. Whether this hook is the final effective input source and whether stock Logitech code competes are still live-test questions. Buttons, paddles, H-pattern shifter, analog handbrake, stalk and menu navigation are planned.

## Experimental bonnet view

`[Camera] BonnetEnabled = true` overrides the live Car_Cam's Camera transform after its LateUpdate while driving. Height/Forward/PitchDegrees are bounded, car-local offsets. Stock position and rotation are restored before the next owned camera update and on pause/focus loss/shutdown. FOV, clipping, culling, mesh hiding and per-car presets are not implemented. Do not treat a successful build as visual validation.

To remove just the mod, close Woden and remove its `BepInEx/plugins/WodenRallyEdgeWheel` folder. Configuration and recordings can be retained. The loader is separate; do not remove shared BepInEx files if other mods use them. The initial install receipt records whether this installer added it.
