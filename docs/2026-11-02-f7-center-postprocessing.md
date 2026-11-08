# ISS-0016: center post-processing across the F7 cycle

Baseline `81d1cab`; isolated worktree `E:\Source\_work\soba-woden-f7-center`.
Offline source candidate only. No game, graphics API, device, installation,
owner config/save/cloud or display changes. The exact owner-reported blurry
off-black image has not been reproduced in a renderer.

## Source defect and bounded correction

F7 Original calls TripleView.Stop; Angle and Angle+curve reuse the game camera
as the center with a custom projection/viewport. Side cameras already substitute
FXAA for temporal AA. The center did not, and Stop did not invalidate its history.
This leaves the center alone exposed to a temporal projection writer and history
from a different projection when Original returns. It is a concrete source-side
hazard consistent with the report, not proof of its exact rendered cause.

Unity's PPv2 [TemporalAntialiasing implementation](https://github.com/Unity-Technologies/PostProcessing/blob/v2/PostProcessing/Runtime/Effects/TemporalAntialiasing.cs)
assigns the camera's jittered projection; its [PostProcessLayer API](https://github.com/Unity-Technologies/PostProcessing/blob/v2/PostProcessing/Runtime/PostProcessLayer.cs)
provides ResetHistory for camera cuts. These are reference implementation/API
evidence; the fixture does not execute the game's native post-processing code.
The new ResetHistory calls compile against the pinned Woden interop inputs.

TripleView now temporarily substitutes FXAA for center TAA while it owns the
projection, matching the existing side-camera policy. It captures the actual
layer, resets history once on acquisition, and restores native AA plus fresh
history on Original/stop/camera handoff. A detectable different external AA
choice wins. Stable frames do not reset history repeatedly. Missing/disabled
layers stay supported, and non-temporal AA choices are retained.

No graphics preference is saved. Camera pose, near plane, lens, existing F7
Original/Angle/Curve choices and ConfigLine persistence are unchanged. This does
not adopt the separate pending STD-041/046 per-camera Game/Realistic redesign.

## Focused proof

The existing TripleFov fixture extracts actual AfterCarCamera/Apply/Stop bodies,
plus the new two PP helpers when present, with zero body substitutions. Fake
camera/PP properties and curved target tokens model side effects only.

- Baseline actual handlers: 35 checks, **17 failures**, expected exit 1 at
  center temporal-AA/history assertions. Existing lens/physical-mode assertions
  continue to pass; the regression is confined to the added post-processing cases.
- Fixed actual handlers: **35/35 pass**, including four Angle-to-Original cycles,
  two Angle+curve-to-Original cycles, source handoff, stable frames, external AA,
  missing/disabled layers and non-temporal AA. Curved textures/GPU draws are fake.
- Existing Core regression: **104 suites / 52,642 assertions**, zero failures.
- Existing source-linked UI fixture: **7,033 assertions**, pass; managed shim
  fixtures report 96 checks. Native ABI explicitly NOT RUN.
- Core and Plugin Release builds: **zero warnings/errors** with warn-as-error.

Lease `172fc405587842c2a9b6168560a778ee` released. TEMP/TMP restored.
The dependency verifier passes using ignored copies of primary checkout's pinned
core/interop, manifest and cached-core comparison files. No proprietary input
or generated output is staged for Git.

Evidence: `artifacts/soba-f7-center-20261102/` contains baseline source/extracted
methods, baseline/fixed build/run logs, Core/UI/plugin logs, dependency inventory,
hashes and result.json. Baseline log prints the historical fixture's generic
SUCCESSOR label; its input is the exact recorded 81d1cab source and exits 1.

SHA256 identities of the tested dirty-source build, not a released package:

- TripleView.cs: `C7850C030A75C558E564975C3D5733C1919E0CA2756639A59246A1AD25B65EE0`
- Plugin: `6EA920B54916D9BA1981C5332CDBE29417A4574D365476DB17A6328DCB0FA2DF`
- Core: `058EB723802EC63A2FCFC394A54F4F9D0854BA8A1FF5C6F8460CF4D79041999D`

Reproduce using the existing prepare.py with a fresh output path and build
`components/wheel/tests/WodenRallyEdge.TripleFovTests` with
`-p:TripleMethods=<absolute extracted methods.cs>`. Run its output DLL directly
without --expect-regression to observe a failing baseline exit. See retained logs
for exact paths and commands; original camera-property behavior remains modeled.

## Remaining acceptance and publication

ISS-0016 remains in_progress: agent-source fix landed, owner validation pending.
A future fresh
candidate must be packaged/installed separately through the qualified route.
With force muted, cycle Original/Angle/Angle+curve repeatedly on the selected
camera and separate-monitor span; inspect center and side images at each step,
verify native AA returns, HUD/pose/lens remain correct and saved F7 restart state
is preserved. Capture the owner-described symptom if it persists; camera pose,
near plane, history and target ownership are distinct diagnostic possibilities.
Owner acceptance, physical displays and device/FFB behavior remain unproven.
Coordinator #498416 permits the source-only public main push after visibility
and source checks. No binary/package/install publication is authorized here.
