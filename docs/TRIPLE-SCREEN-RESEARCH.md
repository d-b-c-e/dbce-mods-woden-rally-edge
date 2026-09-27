# Triple-screen workstream — source audit, 2026-09-26

## Status and scope

**Research plus offline private-target prototype.** No game launch, installation, display
activation, window change, package, or visual acceptance exists. This note lives on the isolated
`codex/triple-screen-research` branch. The installed wheel build and owner settings were not
changed. The 0.2.10 desktop-testing pause remains in force; any later game launch needs the
resumed desktop work and serialized lease described in `STATE.md`.

The [separate probe project](../experiments/WodenTripleScreenProbe/README.md) now compiles a
default-off, three-camera design against the pinned game interop. It reads v1 layout, uses a
pinned toolkit source snapshot, aims only at small private render targets, and reserves frame
evidence/status files for an opt-in run. Offline checks pass (22 layout/geometry assertions;
zero-warning plugin build). It has not run inside Unity. Its named stripped-engine render
bindings and actual visuals remain unverified; it claims no `three-projections` capability.

The desired result is three independently rendered, angle-correct views from one eye, ideally on
three separate displays. Three viewports in one Surround or borderless window are fallback output
paths. A wider single camera is not true triple-screen support.

## Owner rig input — 2026-09-27

Use **70° from the center panel to each side panel** for owner-specific layouts and future
projection validation; 60° is superseded. Other reported rig values remain three 32-inch
2560×1440 1500R panels, 660 mm eye distance, and an 8 mm bezel gap. The 32-inch diagonal and
1500R label do not establish the visible active-area width/height required by
`TripleRigDefinition`; retain measured values from the optimizer profile rather than deriving
new dimensions here. The toolkit's current planar builder also does not apply the 8 mm gap, so
its matrices cannot yet be called bezel-corrected.

The optimizer's saved monitor profile and Art of Rally deployed desired-layout copies have been
updated to 70°. ETS/ATS generated configurations have not yet been regenerated. This is rig
input, not Woden runtime or visual validation, and it does not change generic toolkit defaults.

## Evidence available now

| Subject | Finding | Limit |
| --- | --- | --- |
| Installed game | The closed game's `GameAssembly.dll` SHA-256 is `f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c`, matching the wheel plugin's pinned Steam build 21802346. | No triple-screen runtime check. A later game update requires a new camera audit. |
| Loader | BepInEx BE #788 and Unity 6000.3.6f1/IL2CPP are the established loader surface. The plugin checks the game hash before patching. | A separate adapter must pin and guard the same build; generated interop stays local and out of Git/packages. |
| Camera | `MountedCamera` patches `Car_Cam.LateUpdate`; the selected player's `field_Private_Camera_0` is a Unity `Camera`. It recognizes `Chase` and `IsoMetric`, snapshots/restores pose, FOV and near clip, and releases on transitions, replay, photo/finish, stale ownership or foreign writers. | The hook proves camera access, not three render passes, camera cloning safety, orthographic state, or projection binding availability in this stripped player. Bonnet/Bumper visual acceptance is still pending. |
| UI | The wheel panel uses IMGUI; stock Unity menus use separate input/UI paths. | Stock Canvas/render mode/display targets, mouse routing, safe area and split-screen have not been audited live. |
| Rig math | Private `dbce-triple-screen-toolkit` at `1d67d06` provides `TripleRigDefinition`, `TripleRigBuilder` and `ProjectionCalculator` for planar panel corners and off-axis frusta. All seven core tests passed on this date. | Its matrices are row-major neutral math; a Unity conversion and seam/culling validation remain game-adapter work. It does not yet model bezel gaps or curved surfaces. |
| Window placement | Public `srwe-cli` fork at `c729a2c` supports explicit PID/HWND targeting, per-monitor DPI awareness, borderless exact client-area placement and resulting-rectangle checks. | It cannot create projections or guarantee that Woden rebuilds its backbuffer after resize. No Woden placement test exists. |

The current camera code modifies the active camera only for Bonnet/Bumper or held Look behind.
A separate adapter should observe the final player camera pose after that hook, then apply three
render views without changing the saved manual bonnet pose or the wheel mod's camera/FFB gates.
Its Harmony ordering and teardown must be tested with and without the wheel plugin installed.
Do not place this adapter in the wheel plugin's payload or settings.

## Projection and presentation design to test

1. Read the optimizer's v1 desired layout, reject other `schemaVersion` values, validate the
   physical measurements, and hash the accepted file. Reuse toolkit geometry; do not copy the
   formulas into the game hook. A future public build needs a permitted pinned toolkit release or
   vendored source, since private GitHub access cannot be a build prerequisite.
2. Use the current selected player's camera as the pose source. For each panel, transform the
   toolkit's eye-relative panel basis into Unity camera space and set an independent view and
   asymmetric projection. Document/test handedness, row-major field mapping, near/far planes,
   depth convention, graphics API conversion, jitter and culling. Keep all three cameras at one
   world-space eye. Sample `Camera.orthographic` and the native mode first; `IsoMetric` is a mode
   name, not proof of orthographic projection. If it is orthographic, exclude that mode until a
   separate physically coherent design is validated.
3. Prefer Unity `Display.Activate` plus one camera `targetDisplay` per display if the exact
   connected-display mapping and game's output lifecycle work. Additional display activation is
   process-lifetime state, so recovery means normal exit and restart. Verify primary/secondary
   order, per-display resolution/refresh, focus, input, UI and scene transitions.
4. Fallback: render the same three distinct cameras into thirds of one wide backbuffer, with the
   center HUD/menu once. Validate Surround first if configured; otherwise use `srwe-cli apply`
   against the **verified owned game PID/HWND** for an exact borderless client rectangle. Check
   Unity's actual backbuffer and viewport after placement. SRWE must not target another process.
5. Recreate or release view cameras on scene, player, mode, resolution, pause, replay, photo,
   split-screen and plugin unload boundaries. Restore any changed stock camera fields on every
   exit path. Do not touch input, vehicle physics, clocks, assists, force output or leaderboards.

Unity's public APIs expose `Camera.targetDisplay`, `Camera.rect`, `Camera.projectionMatrix`,
`Camera.cullingMatrix`, `Canvas.targetDisplay`, and `Display.Activate`, so these are plausible
routes. Their use in **this stripped IL2CPP player** is not verified. Cloning a Unity camera can
also duplicate listeners/scripts/effects, so the first prototype must inventory camera components
and render order before creating clones. Source/API feasibility does not equal gameplay support.

The chase view is the best first visual candidate because it is a perspective view following the
car. Correct side panels could add peripheral track context, but the game's elevated arcade
framing may make the gain modest and may expose culling/effect assumptions. The isometric mode
needs its actual projection and tracking behavior checked before claiming any benefit. Evaluate
both with shared world-line/seam landmarks, not FOV alone.

## Contract and reusable gaps

Use the toolkit's v1 `triple-screen-layout`, `adapter-manifest`, and `runtime-status` schemas.
`activeCapabilities` must include `three-projections` only after three distinct scene views have
rendered; `activeCameraCount`, `acceptedLayoutSha256`, topology and `lastSuccessfulFrameUtc` must
reflect observed state. Installed DLLs, three windows, or a matching layout hash alone are
insufficient. Until an observed render, status is `inactive`/`starting` with no claimed camera
capability. Reject unknown versions and atomically replace status files.

Reusable toolkit gaps exposed by this consumer:

- V1 layout names a topology but has no stable physical display IDs/order or per-display output
  rectangles. Separate-display adapters need a versioned monitor mapping or a validated optimizer
  sidecar. Do not silently reinterpret v1 fields.
- The v1 schema includes `bezelWidthMm`, but current `TripleRigBuilder` assumes active edges meet;
  do not advertise `bezel-gap` or claim corrected seams until math and numeric tests cover it.
- A documented Unity matrix/handedness conversion fixture shared with other Unity consumers
  would reduce adapter errors, while staying outside the engine-independent core.

No toolkit source or contract was changed in this audit. These gaps are reported here for the
consumer tracker when a concrete adapter chooses its monitor mapping.

## Fair-play boundary

The [Steam store](https://store.steampowered.com/app/3218630?l=english) lists Steam leaderboards,
online time records, and local split-screen play. The [game's terms](https://store.steampowered.com/eula/3218630_eula_0)
prohibit third-party software or game manipulation used for an unfair advantage. Three panels can
reveal more track than stock framing, so even a rendering-only adapter is not automatically fair
for ranked times. Keep experiments off leaderboard-submitting runs and local PvP, and seek the
developer's position before recommending ranked use. Do not alter submission, anti-cheat,
visibility/culling rules, timing, or gameplay to make the mod appear acceptable. A rendering-only
manifest describes technical behavior, not permission from the developer.

## Safe prototype gate and acceptance

The build hash is confirmed and the game is closed. Before a runtime prototype, use a **separate**
BepInEx plugin ID/package with an explicit owned-file list, a copy of any files it could replace,
and a closed-game removal procedure. Never overwrite the installed wheel payload or owner config.
In-process projection changes must have a stock-camera restoration path; multi-display activation
requires normal game exit to reset. Test the prototype first without physical force enabled and
only after the desktop pause is lifted and a serialized lease is granted.

Evidence milestones:

1. **Offline prototype:** adapter builds against pinned generated references, validates v1
   layouts, computes/converts three matrices, and passes numeric seam/asymmetric-rig tests.
2. **Runtime prototype:** logged distinct camera/render frames and status for the exact build;
   normal stock fallback and clean exit, with wheel plugin interaction checked.
3. **Visually verified support:** three screens show continuous world lines and appropriate side
   angles while driving; center UI/input works; chase/isometric behavior, HUD, effects, photo,
   replay, split-screen, resize/focus, frame pacing and teardown are tested. Separate displays
   receive an independent pass; Surround/span are reported separately if those alone work.

The first milestone has a compiling source prototype and offline layout/geometry checks, but its
Unity render bindings and frame readback have not run. Runtime and visual milestones are open.
Existing wheel UI fixtures and 0.2.9 limited live evidence do not advance triple-screen acceptance.

## Repository decision

A dedicated **private game adapter/mod repository is warranted** for implementation. It needs a
different plugin identity, package, release cadence, render tests and fair-play guidance, while
the wheel repo owns input, camera adjustments and force. This branch holds the initial game-side
audit until that repository exists. The future adapter can reference pinned, permitted toolkit
artifacts, but must not require access to the toolkit's private repository for any public build.
