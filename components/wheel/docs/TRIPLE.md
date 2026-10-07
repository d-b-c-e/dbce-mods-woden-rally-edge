# Triple screens

Woden renders across three screens from one wide window (NVIDIA Surround, or a
borderless window spanning three monitors). Port of iRacing Arcade's `TripleView`;
toolkit standards STD-007 and STD-008.

**Status (2026-10-04):** working under Surround at 7680x1440. The owner drove stages in
bonnet and bumper views; triple views, centred HUD and menus verified at the rig.
Separate monitors without Surround: renders when the window spans all three (seen with
the dev span), but the mod does not create the span itself yet.
**Combined build 0.2.14 (2026-10-05, unattended):** with the saved resolution at entry 13
(7680x1440) the Kenya replay start showed triples on (side terrain continuous, HUD on the
centre, game vfov 45 -> centre 37-42). The replay itself did not drive: stage playback needs
the game window focused (`PlayerControlState.Driving`), and a firewall prompt held the
foreground. Retry with nothing else in front.

## How it works

- **Views** (`TripleView.cs`): a `Car_Cam.LateUpdate` postfix (Priority.Last) splits the
  window into thirds. The game camera stays the centre view with an off-axis (Kooima)
  projection from `TripleGeometry`; two side cameras copy its pose and settings, yawed by
  the panel angle, each with its own PostProcessLayer (`Init(m_Resources)`;
  `usePhysicalProperties` keeps PPv2 from resetting the projection). HUD canvases move to
  a HUD camera on the centre third.
- **Stripped engine** (`TripleNative.cs`): this build lacks many Camera/Canvas setters, so
  they are resolved as `il2cpp_resolve_icall("UnityEngine.Camera::set_depth_Injected")` etc.
  If a required binding is missing, triple stays off and says why.
- **Resolution** (`DesktopResolution.cs`): the game's list (`ResolutionManager.ResList`,
  copied in `Progress.Resolutions`) is hardcoded and stops at 5120x1440; the saved
  `Resolution` pref is an index into it, applied once by `FirstResolutionSet`. A prefix adds
  the desktop size (from `GetSystemMetrics`) when missing, and also when the saved index
  points past the stock list, so the index stays valid when switching between Surround
  (entry 13 = 7680x1440) and three independent monitors (entry 13 = 2560x1440). 7680x1440 is
  then a normal choice in Options and startup is a no-op at the desktop size.
- **Menus** (`MenuBorders.cs`): hides the game's pillarbox images
  `Menu Camera/CANVAS/ScreenBorders/BorderL|R` on a three-wide window; the game re-enables
  and moves them per menu, so they are re-checked twice a second.

## Settings (`dbce.wodenrallyedgewheel.cfg`, `[Triple]`)

| Key | Default | Meaning |
|---|---|---|
| Mode | Auto | Auto = on when the window is at least 2.9x as wide as tall; On; Off |
| ToggleKey | F11 | Toggle in game |
| CenterHud | true | HUD and menus on the centre screen |
| MatchGameFov | false | Use the camera's own FOV (then F6 bonnet/bumper FOV sliders drive it) instead of the eye distance. With 70° sides the screens then span more than 180° (bumper 38° vertical: about 196°; bonnet 70°: about 244°; real geometry: 181°). Owner's install: true until 2026-10-06, then false at the owner's request (real geometry for bonnet/bumper) |
| ChaseUsesGameFov | true | Stock chase cameras keep the game FOV |
| PanelWidthMm / PanelHeightMm / EyeDistanceMm / SideAngle / BezelMm | 708.4 / 398.5 / 660 / 70 / 8 | Rig geometry |
| OfferDesktopResolution | true | Add the desktop size to the game's resolution list |
| HideMenuBorders | true | Hide the menu pillarbox on three-wide windows |

TODO (STD-004): move these into F6 like the other mods; F10 vs F11 toggle consistency with DRIVE.

## Testing safely (read before any display test)

On 2026-10-04 stacked runtime resolution changes at 7680 (windowed, borderless resize,
title-screen apply, `Screen.SetResolution` FullScreenWindow twice) reset the NVIDIA
driver twice and the machine needed a hard reboot. Rules:
- Monitor profiles only via `MonitorProfileSwitcher.exe --ipc apply "Sim Racing Surround"`
  / `"Sim Racing"`; wait for the verified reply, then ~30 s before launching.
- Set the resolution before launch (registry `HKCU\Software\ViJuDa\Super Woden Rally Edge`:
  `Resolution_h2981718891` = 13, `Screenmanager Resolution Width_h182942802` = desktop width),
  not at runtime. The dev `window` command allows one change per launch.
- The legacy `tools/dev/Display-Watchdog.ps1` kills by process name and changes the
  monitor profile. Do not use it for recording/playback. Toolkit `docs/STAGE-SUPERVISION.md`
  defines the replacement with exact process identity and current-request output-mute
  confirmation; Woden has not adopted that heartbeat yet. Its existing stage runner
  requests normal Stop on timeout and retains explicit recovery instructions.

## Dev tools (owner-away development only)

`[Dev] InputCommandFile = true` enables `dev\cmd.txt` beside the plugin (`DevInput.cs`):
`press/hold/release <Key> [ms]`, `pad A|B|Start|... [ms]`, `stick x y ms`, `shot name`,
`status` (also lists ResList), `ui` (wide/black UI graphics with scene paths),
`hide <path>`, `window W H [mode]` (one per launch). It forces `runInBackground` and writes
`dev\heartbeat.txt` each second. `tools/dev/Woden-Dev.ps1` wraps launch, commands and
screenshots. Woden pauses when unfocused; startup screens read legacy Input (need focus),
later screens read GamePadSystem (`pad`/`stick` work without focus); attract-mode DEMO PLAY
uses `Car_Cam` and is a good render test. Turn the dev channel off after testing.

## Separate monitors without Surround (STD-015, 2026-10-06, unverified)

`SpanWindow` ports DRIVE's plain-launch span: with three or more equal-height monitors side by side and a
single-screen primary, `Screen.SetResolution` calls made while `ResolutionManager.FirstResolutionSet` runs (the
game's single startup apply) become one windowed apply at the virtual-desktop span; the window is then made a
borderless popup and placed over the span without activation, re-checked every 2 s. Patched one by one after
`PatchAll` so a missing `SetResolution` overload disables only this. `[Triple] SpanSeparateMonitors` is **on by
default** since 2026-10-06: Astra's cold Kenya replay on "Sim Racing" with the span (results/woden-kenya-span-after2-20261006-020506)
passed 3,601 poses at 0.06 mm with `Span window: spanning 7680x1440 at (-2560,0)`, five frames at 7680x1440 windowed
with triples on, the bonnet view continuous across both seams and the HUD centred; owner state restored. The game
re-runs `FirstResolutionSet` on scene loads; each is substituted, so no apply is added. Set it false for the stock
single screen.
