# HUD jitter: bounded source review

Baseline: clean `89adf82d1b71857bf5d1f4cfdedb0b910066a6b8` in the isolated
`codex/woden-shim-input` worktree. The owner reported the speedometer and other
HUD elements shaking. Earlier trial02 PNGs were old retained files; forward
run01 produced no new WGC frames. Neither recording establishes a rendering cause.
This review leaves force, analysis, physics, owner settings and installation alone.

## Exact rendering path

In `components/wheel/src/WodenRallyEdge.Plugin/TripleView.cs`:

- `NewCamera` (209–215) creates an unparented, untagged camera. It does not become
  `Camera.main` or follow the car transform.
- `NewHudCamera` (218–228) selects depth-only clearing, UI-layer mask `0x20`,
  source depth +10, vertical FOV 60, near 0.1, far 10, position `(0,-10000,0)`.
- `MoveCanvases` (232–247) changes only active root ScreenSpaceOverlay canvases
  to ScreenSpaceCamera, assigns that HUD camera and plane distance 1. It does not
  select existing WorldSpace or ScreenSpaceCamera canvases. A scan every two
  seconds picks up new overlays; already-moved canvases no longer pass the filter.
- `Apply` (140–145) maintains the HUD viewport at the middle third and its depth.
  The player/side cameras receive their own projections; the HUD does not copy
  the moving car camera's position, rotation, custom matrix or post-processing.
- `RestoreCanvases` (250–260) restores render mode and world camera. The current
  code does not retain the original plane distance, a restoration concern for
  a diagnostic depth experiment, not evidence of ongoing jitter.

`TripleNative.cs` resolves the exact Canvas plane/world-camera/render-mode engine
setters. Its current Canvas availability check omits the optional plane setter;
the retained live log explicitly reported all bindings present, so that omission
does not explain these runs.

Forward run01's retained log names four moved canvases: `CanvasNOFX`, `UI_LAYER_3`,
`UI_Layer_2`, `CanvasFX`. It records the HUD mask `0x20`, perspective rendering and
three 7680x1440 views. The source camera's post-processing AA is None. These logs
do not contain per-frame canvas matrices, local vertex/material data or actual
HUD pixel displacement. The initial logged full-screen HUD rect precedes the
same `Apply` call's center-third assignment; it is not evidence that the rect
oscillates during driving.

## Precision exposure and its limits

At absolute Y 10000, adjacent float32 values differ by `0.0009765625`. A 60-degree
camera's plane at distance 1 is `2*tan(30 degrees)` units high. At 1440 pixels,
one world-Y representable step therefore spans **1.217848 pixels**, with up to
**0.608924 pixels** of rounding error for one rounded coordinate.

The private calculation under `artifacts/hud-jitter-source-89adf82/` models an
explicit float32 local → world → camera round trip. A smoothly changing 0.1-pixel
input can stick then jump by 1.217848 pixels. Repeating one unchanged point 1,000
times gives exactly one output, however. The large fixed position alone does
not produce temporal jitter; changing geometry, transforms, numeric operation
order or another render mechanism is still needed. The shipped Canvas batching,
vertex shader and native transform path have not been proven to take this exact
rounded route. This is a precision-risk calculation, not a Unity-rendered test.

Unity 6.3 documents that [planeDistance sets the ScreenSpaceCamera plane](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Canvas-planeDistance.html)
and [worldCamera supplies Canvas sizing](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Canvas-worldCamera.html).
The [Canvas manual](https://docs.unity.cn/6000.0/Documentation/Manual/class-Canvas.html)
describes automatic resizing to maintain screen size as distance changes.
Context7 was unavailable; these are Unity's primary docs. The general manual
page is 6000.0, while the API references match this game's 6000.3 family.

## Optional diagnostic proposal

Do not move the camera near the origin: UI mask `0x20` admits a layer, not an
explicit canvas allowlist. A different camera position could expose other native
UI-layer geometry. [Unity's culling-mask contract](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Camera-cullingMask.html)
does not establish that this layer contains only the four moved canvases.

The smaller proposed experiment keeps `(0,-10000,0)`, identity rotation, FOV 60,
near 0.1, viewport, depth, culling mask and gameplay projections. Change only
Canvas plane **1 → 100** and HUD far clip **10 → 110**. The world-Y quantum is
unchanged, but its screen contribution becomes **0.01217848 pixels**, 100 times
smaller. The far-plane vertical bounds are approximately -10063.51 to -9936.49,
so the camera remains far from the origin. This reduces the root precision
exposure; it does not establish that it fixes the owner's symptom.

Implemented private source scope, pending review and any runtime authorization:

- Small pure `TripleHudDepth` selection/constants: normal 1/10. Only an explicitly
  compiled `WODEN_HUD_DEPTH_DIAGNOSTIC` build under the existing diagnostic no-force
  and stage-output-mute state selects 100/110. No new saved setting or default.
- `TripleView` consumes the selected pair, logs it and preserves/restores each
  moved canvas's original plane distance. Both normal and diagnostic candidates
  now require the exact get/set bindings before moving a canvas and read back the
  selected distance. Failure attempts rollback and stops further migration.
- Source-linked checks in the existing executable test harness compare ideal
  pixel projection across center/edges, resolutions and aspect ratios; bound the
  float32 error; cover unmuted/default refusal and clip margins. No new framework.

The pinned generated `UnityEngine.UIModule.dll` has no managed plane-distance
property (SHA-256 `2003ecca6028817dfc4f51e2b4661d41da3e670af1b530384fc1fbefc627bfae`;
REA static evidence `ev_b5442456f9cfe59027faedfe5378771da07268124edd2b48c590676434f1b6d8`).
The existing `TripleNative` resolver therefore binds scalar `get_planeDistance_Injected`
alongside its existing setter, using the native Unity object pointer. The pinned
UnityPlayer (`9feedb4527a30fae4762bff45d204f8e85b90851b2c8ae8d24f436f7d474667b`)
contains both exact names, and Unity's [6000.3 Canvas reference source](https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/6000.3/Modules/UI/ScriptBindings/UICanvas.bindings.cs)
declares the scalar float property. Static names/types do not prove a successful
native resolver/call/readback; that remains an explicit first runtime check.

Device-free verification: the production-linked geometry helper passes **8,142**
checks covering all build/mute combinations, default values, ideal projection
at 720/1080/1440/2160p, flat-plane clipping and conditional float32 error. It also
proves that a foreign point 50 units ahead changes culling classification and
that fixed coordinates do not spontaneously jitter. The existing Core harness
passes **58 suites / 50,479 assertions**. Normal and diagnostic plugin builds
both have zero warnings/errors; no force/analysis source changed. Logs:
`artifacts/hud-depth-{core-tests,default-build,100-build}.log`.

Private build commands (no install):

```powershell
dotnet build components/wheel/src/WodenRallyEdge.Plugin/WodenRallyEdge.Plugin.csproj -c Release --no-restore -warnaserror -o artifacts/hud-depth-default
dotnet build components/wheel/src/WodenRallyEdge.Plugin/WodenRallyEdge.Plugin.csproj -c Release --no-restore -warnaserror -p:DefineConstants=WODEN_HUD_DEPTH_DIAGNOSTIC -o artifacts/hud-depth-100
```

The `artifacts/hud-depth-pins.json` readback identifies both exact binaries and
their compiled flag values. It is a private diagnostic candidate, not an owner
package or a recommendation to change the accepted layout.

The enlarged far range can admit foreign UI-layer geometry within the isolated
volume. Native child depth offsets may also scale with the Canvas: a flat plane
at 100 fits inside 110, but that does not prove all children do. Before recommending
this candidate, read the actual moved canvas rect/corners, scale, local depth,
camera matrices and UI renderer bounds; verify the native visible set, clipping,
masking, pixel layout and normal restoration. Do not silently relayer foreign
objects or patch the game's CanvasScaler/materials to force the result.

Post-reboot source follow-up: restoring the canvas plane, render mode and original
camera now uses independent attempts. A failing plane call no longer skips the
mode/camera restoration before the diagnostic camera is destroyed. No rendered
comparison ran before the outage; the precision hypothesis remains unverified.

## Two-frame comparison on a later useful run

No new launch is authorized by this document. During a separately reviewed muted
run, the existing window-only WGC helper binds the exact owned PID/start/Unity HWND.
Read trace incrementally from a retained offset rather than searching a trailing
window, which missed the transient batch-start event in forward run01.

Capture a bounded native-resolution burst after HUD layout settles. Select two
adjacent saved frames with recorded timestamps and unchanged visible static HUD
labels/frames; compare native pixel crops of the speedometer surround, a text
baseline and a second distant HUD anchor. Ignore changing digits, needles, timer
text, world motion and particle effects when measuring anchor displacement. Record
actual camera/canvas transforms at both samples through the existing passive
observer seam if the diagnostic addon is extended; no input or focus operation.

If the candidate is later approved, use matched immutable stage/pose and viewport
for baseline and depth100 captures, keep both frame pairs and quantify the same
anchor offsets plus clipping/foreign-renderer inventory. Pixel equality on one
pair cannot prove continuous stability; missing samples remain explicit. A whole
HUD shift with stable native transforms, versus only a rotating/changing element,
separates the next investigation. A geometry calculation or one sharper screenshot
does not resolve the jitter report or qualify a production default.

## Outcome and the speed digits (2026-10-13)

The position fix (canvas plane 100, HUD far 110; `TripleHudDepth.Production`) is in production. The retained A/B
qualifies it: tach-needle reversals over 0.2 degrees fell from 69 to 1, and static gauge ROIs from a 2.46 px to a
0.04 px frame-to-frame shift. The owner has not explicitly accepted it yet. No further routine run or release gate is
attached to it. Evidence: hub `artifacts/woden-hud-jitter-review-20261013` (`needle/`, `mufa-native-speed/`).

The digital speed readout is a separate, stock behavior, kept as is:

- The game's `HUD.FixedUpdate` writes `Int32.ToString(abs(MainCar.TrueSpeed))` to the Speed text every physics step
  with no smoothing. `HUD` has no `Update` or `LateUpdate`. A units setting formats a converted float instead.
- `MainCar.FixedUpdate` sets `TrueSpeed = (int)Math.Round(SpeedMomentum x 180)`. Recorded `game.trueSpeed` equals
  `round(game.speedMomentum x 180)` on every moving row checked (542/542). `SpeedMomentum` moves in steps of 1/180,
  one displayed km/h each.
- That momentum reverses direction on about 17 to 19% of full-throttle ticks, whether straight or steering, and 4.4%
  at zero throttle, in the owner's own 2026-10-01 drive. So the digits can alternate by 1 km/h at up to 30 Hz. The mod
  does not write `TrueSpeed` or `SpeedMomentum` while driving; replay writes the recorded value per row.
- The tach needle is smoothed per physics step toward its RPM curve. The HUD camera is fixed. Car-camera shake rotates
  only the camera rig. Triples render at about 170 fps against the HUD's 60 Hz, as the stock single view does.

**Possible future choice, not implemented:** an opt-in display filter, a postfix on `HUD.FixedUpdate` that rewrites
only the Speed text from a short hysteresis or average of `TrueSpeed`. It would leave `TrueSpeed`, physics and
leaderboard values untouched, but it would depart from the stock HUD, so it waits for the owner's choice.
