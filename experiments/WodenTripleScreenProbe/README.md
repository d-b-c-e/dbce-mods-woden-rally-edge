# Woden triple-screen private-target probe

**Offline prototype only. Not installed, run, packaged, or visually verified.** This project is
outside `WodenRallyEdgeWheel.sln`, the wheel plugin, and its installer allowlist. It has its own
plugin ID and cannot change the wheel mod's settings or payload. Do not use it for leaderboard
submissions or local competitive play. The user has paused desktop testing.

## What the code is designed to do

- Default off. Only an explicit `BepInEx/config/dbce.woden-triplescreen-probe.json` with
  `{"enabled":true}` can load the hook. This file has **not** been created in the game.
- Verify the exact Steam build's `GameAssembly.dll` hash before patching. Read and strictly
  validate a v1 `desired-layout.json` from
  `%LOCALAPPDATA%/DBCE/TripleScreen/games/super-woden-rally-edge/`; reject stale 60° input and
  require the reported 70°/70°, 660 mm, 8 mm, 1500R, 2560×1440 owner rig. No Woden layout has
  been exported or installed.
- Reuse a pinned snapshot of toolkit geometry. Convert its physical panel bases to Unity local
  coordinates and use Unity `Matrix4x4.Frustum` with toolkit off-axis extents. The 8 mm gap is
  retained in the v1 layout but **not modeled** by the toolkit builder. The panel width/height in
  the offline fixture come from the optimizer's 32-inch diagonal estimate, not a measured chord.
- Observe only the selected player chase camera after the wheel plugin's Harmony patch. Skip
  orthographic/isometric, transitions, replay, respawn, unfocused and multiple-player conditions.
  The three probe cameras are separate GameObjects with 192×108 private `RenderTexture`s. The
  source camera and any Canvas/display/window state are never assigned by this code. Manual
  renders are sampled every 30 game frames, with small pixel readbacks every fourth probe frame.
- Write `probe-frame-evidence.jsonl` and a v1 `probe-runtime-status.json` only after opt-in.
  Status remains `degraded` after private-target readback, with empty `activeCapabilities` and
  `topology:null`. Return from a native Render call plus sampled pixels is not proof of visually
  correct or separately presented views.
- Resolve stripped Unity camera setters and `Camera.Render` by named IL2CPP bindings before
  creating any targets. Missing bindings reject the probe. Calling-convention and render-pipeline
  behavior still require an attended runtime check; an exception cannot recover from a native
  ABI mismatch. The existing wheel mod has verified only its near-clip binding, not these probe
  bindings.

## Standalone build

The ten geometry source files and two v1 schemas in this project are exact local copies from
private `dbce-triple-screen-toolkit` commit `885eda53f40ffe28114fdafb69d77e9277b6344b`.
They avoid a private Git submodule/package requirement at build time. The toolkit currently has
no standalone license file; confirm redistribution terms before any public release. Neither
generated Woden interop nor game/loader binaries are committed here.

On the owner's machine, the already generated and hash-pinned Woden local dependencies can be
selected explicitly:

```powershell
dotnet run --project tests/WodenTripleScreenProbe.Tests -c Release -warnaserror
dotnet build src/WodenTripleScreenProbe.Plugin/WodenTripleScreenProbe.Plugin.csproj -c Release -warnaserror /p:WodenDependencyRoot=E:\Source\woden-rally-edge-wheel\lib
```

For an independent private checkout, first generate the game/loader references using that
checkout's `tools/Initialize-Dependencies.ps1`, with the matching locally installed game. The
default MSBuild dependency path is the containing Woden repo's ignored `lib` directory. This
requires Woden's locally owned game and public loader, not access to the private triple-screen
toolkit. The probe has no installer or release archive.

## Rollback and attended gates

The safe rollback for a future test is to close the game normally, remove only a separately
installed `BepInEx/plugins/WodenTripleScreenProbe` directory and its opt-in file, and restart.
That directory would own only `WodenTripleScreenProbe.dll`,
`WodenTripleScreenProbe.Core.dll`, and `Dbce.TripleScreen.Core.dll`; a future installer must
verify those hashes and back up any pre-existing path before writing.
The code's `Unload`/lifecycle cleanup releases its three textures/cameras and leaves the stock
camera untouched. Verify the exact owned files before any install; never replace wheel payloads,
owner settings, or an unknown mod. No installation has occurred.

Before an attended launch: review the named native binding ABI against this Unity build;
inventory the chase camera's pipeline/effects and UI; prepare a non-submitting local test mode;
obtain the resumed desktop work and serialized lease; then verify wheel plugin coexistence and
normal exit with physical force suppressed. Frame evidence must show three distinct targets and
live scene content before any presentation experiment. Visually verify seams, culling, HUD,
effects, scene changes, resize, pause/replay and performance before status can claim true triples.
The separate-display and Surround/span paths remain entirely unimplemented.
