# DBCE mods for Super Woden Rally Edge

Play [Super Woden: Rally Edge](https://store.steampowered.com/app/3218630/) with a racing
wheel, pedals and a handbrake. One mod adds direct wheel and pedal controls, force feedback
for any DirectInput wheel, Forza-format telemetry for SimHub, bonnet and bumper cameras, and
angle-correct triple screens (NVIDIA Surround or three separate monitors).

**Early-tester build.** 0.2.14-beta.2 is the current public build. It has been driven on one
rig (MOZA R12, three 2560x1440 screens). Reports from other wheels and setups are welcome:
open an [issue](https://github.com/d-b-c-e/dbce-mods-woden-rally-edge/issues) with your wheel,
screen layout and what happened.

## Install

1. Close the game. Download **WodenRallyEdge-0.2.14-beta.2.zip** from the
   [release](https://github.com/d-b-c-e/dbce-mods-woden-rally-edge/releases) Assets (not
   GitHub's Source code download) and extract the whole ZIP into a new folder.
2. Double-click **Install.bat**. It finds the Steam game folder, backs up anything it replaces
   and keeps existing settings. The first install downloads the supported BepInEx loader and
   checks its hash. Only Windows PowerShell 5.1 is needed.
3. Launch the game from Steam. Press **F6** for the settings panel: pick and calibrate the wheel
   and pedals on Controls, and choose the wheel on FFB. **F8** turns force feedback off.

To update, run Install.bat from the new package. **Uninstall.bat** removes only the mod's own
files and keeps settings and the shared loader. Full steps: [setup guide](tools/game/player/QUICK-START.md).

## What works

- **Controls:** steering, throttle, brake and an analog handbrake from any USB devices, each
  bound and calibrated in F6; sequential shift up/down on any button, or an H-pattern shifter (Gear 1-6 and R; needs the game's manual transmission). Menus can be driven from
  wheel buttons.
- **Force feedback:** steering load from the tyres, for any DirectInput force-feedback wheel.
- **Telemetry:** Forza Horizon "Data Out" format (speed, RPM, gear, pedals and more) for
  SimHub and other dashboards; on by default (F6 → Telemetry).
- **Cameras:** Bonnet and Bumper views join the game's camera cycle; both fit each car's body. Numpad moves the camera
  (8/2 forward/back, 9/3 up/down, 4/6 left/right, 7/1 tilt, +/− field of view, 5 reset).
- **Triple screens:** three angle-correct views with the HUD and menus on the centre screen.
  On Surround, choose the Surround resolution in the game. On three separate monitors the mod
  opens one borderless window across them by itself.
- **Online:** the optional countdown-timer assist keeps those runs off the Steam leaderboards.

## Known limits

- Force feedback strength is not yet matched to our other mods' 50% scale; adjust Strength
  to taste.
- No H-pattern shifter or clutch yet.
- Tested on one wheel and one screen layout. Only the current Steam build of the game is
  supported; the installer refuses other builds.

## For developers

Game-specific code is in `components/wheel`, shared playback artifacts in `vendor/playback`.
No game files, saves or private recordings are in this repository or the release.
[Public release notes](docs/PUBLIC-RELEASE.md) · [stage playback](docs/STAGE-PLAYBACK.md) ·
[FFB comparison](docs/FFB-COMPARISON.md) · [roadmap](components/wheel/docs/ROADMAP.md).
MIT licence; see LICENSE.
