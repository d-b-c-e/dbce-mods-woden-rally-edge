# 0.2.2 development build

F6 opens settings and bindings. F8 immediately disarms mod FFB. This development build adds a full settings panel and provisional force output; physical handling, force direction and strength still require an attended drive. See `docs/STATE.md` for recorded runtime evidence.

Supports Windows Steam build 21802346, Unity 6000.3.6f1, GameAssembly SHA-256 `f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c`. Other native game builds leave hooks disabled.

## Install

1. Close Woden normally. Run `tools/Initialize-Dependencies.ps1`, `tools/Package.ps1`, then `tools/Install-Dev.ps1` from the repository.
2. Launch through Steam. First-start BepInEx reference generation can delay startup. The installer seeds the required empty `[IL2CPP] UnityBaseLibrariesSource =` setting.
3. Press F6. Configure the wheel, then enter a local driving session. The panel pauses an active race and restores only a pause it initiated when closed.

The initial installer refuses a running game, conflicting loader and existing plugin installation. The development ZIP contains the plugin, pinned toolkit dependencies, recording provenance and telemetry schema. It excludes the loader, game files and generated references. Manual deployment requires BepInEx BE #788 and the same empty base-library setting.

## Wheel and bindings

Start in **Setup**, then **Controls → Wheel and pedals**. Centre the wheel and release the pedals. Calibrate steering by turning right first, then sweeping both ends and choosing **Save calibration**. Calibrate throttle and brake with a full press/release. Each axis retains its own physical device GUID, endpoints, inversion and deadzone; separate USB pedals are supported. All three axes are required before the wheel override engages. The bars show device input. Enable Wheel controls and confirm the car responds in the first drive.

On **Controls → Buttons**, assign Shift up/down, Handbrake, Change camera, Look behind, Lights, Horn, Reset car, Pause, Settings, Stop FFB, Records and Next song. Button capture accepts a single physical button. H-pattern gears, analog handbrake and stock menu navigation are not implemented in this build. Use mouse/keyboard for menus. Existing saved binding keys remain compatible despite clearer display labels.

**Help → Advanced device details** shows raw axes, held buttons and exact device identity. Refresh after connecting/reconnecting hardware; it stops and disarms FFB first. Bindings save to `BepInEx/config/wheel-bindings.json`, with the previous file retained as `.bak`. Ordinary settings save to `BepInEx/config/dbce.wodenrallyedgewheel.cfg`.

0.2.2 feeds the action table immediately around **Controls.FixedUpdate**. That is where the game copies axes into MainCar and dispatches buttons. The ineffective 0.2.1 override happened after this copy. The new scoped route restores the original action table after the game reads it; wheel buttons use the game's own action/latch behavior. Effective driving response still needs the owner's retest.

## First FFB test

1. Verify wheel and pedals with FFB disarmed.
2. In **FFB**, select the physical FFB base, turn FFB On, leave strength at 10% and peak at 25%, and choose **Start FFB for this session**. Close F6 and drive slowly forward.
3. The provisional signal should oppose slip/steering motion. If direction feels wrong, use F8, stop, then change Invert and re-arm. Direction and load normalization have not been physically validated.

The model estimates alignment from front contact magnitudes and Unity sideways slip. It adds damping from calibrated steering movement and uses the shared toolkit's smoothing, slew limit, low-speed fade, peak cap and ramp. It is not measured rack torque. No surface/curb/engine effects are invented.

Arming never persists across launches. The panel, pause, focus loss, stale/invalid samples, missing front contacts, airborne front wheels, reverse and unavailable enabled wheel input stop force. Native output also has a 150 ms hold watchdog and exit guards. F8 disarms until explicitly armed again. The game's Logitech readers are suspended and its already-loaded SDK is shut down before mod ownership; unresolved shutdown blocks FFB.

## Telemetry and capture

**Telemetry** controls Forza UDP (default loopback 8000), detailed JSON UDP (8001), detail rate and live recording. Port 0 disables that stream; nonzero ports must differ. Choose **Apply connection** after editing ports/rate. Start/stop capture without restarting the game.

Capture files go to `BepInEx/WodenRecordings`, bounded to 20 minutes or 64 MiB per file. A late-start capture gets its own time origin. The schema distinguishes raw game scales, derived values and unavailable fields. FFB telemetry includes source load/slip, alignment/damping estimates, conditioned preview, accepted native command, delivery failures and exact tuning. Raw bound axes are recorded alongside calibrated applied input.

```powershell
dotnet run --project tools\TelemetryInspector -c Release -- inspect 'path\to\capture.jsonl'
```

The inspector validates the complete file and reports coverage, ranges and gaps. Missing/corrupt footers return a nonzero exit code. A valid file does not prove physical feel. Forza engine RPM, fuel and gear conversion remain conservative until their game scales are measured.

## Camera and removal

**Cameras** controls whether Bonnet and Bumper appear in the game's normal camera rotation. Use the normal Change camera action on keyboard/gamepad or its bound wheel button: stock views → bonnet → bumper → stock. Disable either view to skip it. The stock preset remains a legal game value even in a mod view. Look behind is held, with release restoring the forward view.

Select Bonnet or Bumper offsets to adjust that view's height, forward offset and pitch; Reset this view only resets the selected view. Game camera/player takeover releases the mounted view and stops FFB. An external camera writer is left in control; an explicit normal camera action can return to the cycle. Native transition, clipping, FOV, mesh hiding and per-car checks remain pending. See `docs/UX-ADOPTION.md` for other development UI gaps.

Close Woden normally before removing `BepInEx/plugins/WodenRallyEdgeWheel`. Configurations and recordings can be retained. The loader is separate; retain shared BepInEx files if other mods use them. The install receipt records whether this installer added it.
