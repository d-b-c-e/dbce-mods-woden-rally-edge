# Next bounded camera item: reserve the active set-switch key

Selected after DRIVE rc29 install under kole522416. Initial source-only audit at
main9b6ad3302248beffcf06c2f89e58da8b6356a69b in isolated worktree
`E:/Source/_work/soba-woden-camera-key-audit-20261104`. Root/component AGENTS,
current camera handoff, source and adoption ledger read. No code/build/test,
game/UI/input/device, owner-state write or public push. Existing camera/offset/
reset qualifications and rendered/physical gates remain unchanged.

The offset/reset/Custom feature is already implemented. The next concrete gap
is keyboard dispatch ownership between its adjustment bindings and the camera
set shortcut, rather than another implementation of those features.

- `CameraShortcuts.BindableKeys` excludes None/F6/F8/Escape, but includes F7.
- `WheelInput` capture uses that list; its reserved-key message names F6/F8.
  A plain F7 can therefore be captured for a camera adjustment/reset action.
- `CameraShortcuts.KeyPressed` accepts the same saved key and ignores modifiers
  during ordinary dispatch. `WheelInput.Button` ORs that keyboard result with
  the existing wheel/shim input.
- `TripleView.FrameTick` separately consumes configurable `Triple.FovKey`
  (default F7). Plain key switches the saved set; Shift selects the session-only
  developer comparison. `Plugin` calls FrameTick before CameraShortcuts.Update.
- A saved adjustment on that key can consequently enter both consumers. A
  Shift+set-key press can enter an offset writer even though the comparison
  handler itself performs no save. This is a source-derived counterexample,
  not an observed game/device result.

Smallest next correction: reserve the active configured set key in camera-key
capture and keyboard dispatch when that shortcut applies, with an actionable
message naming it. Preserve existing saved binding bytes, wheel-button paths,
numpad5, Custom labels, per-car/set offset keys and the single-screen contract.
Cover default F7 and a configured alternative, plain/Shift activation, other
camera keys, wheel bindings and capture/save refusal through the actual policy
and consumer seam. Do not guess a replacement binding or rewrite a saved map.

No implementation or CPU request has been made by this selection. Woden's
public-push restrictions and the existing rig/owner evidence boundaries remain.

## Source correction prepared (kole522921)

The active parsed Triple.FovKey is reserved only while triples framing applies.
Capture refuses it before the publisher and names the reserved key; ordinary
keyboard dispatch filters it, while the existing wheel/shim OR route remains.
Observed reserved holds require their own release before acting after a mode/
set-key change. No saved binding, offset, numpad default, Custom calculation,
force, device path or single-screen setting is rewritten.

The focused UI-harness mode links the actual CameraShortcuts, WheelInput and
CameraFramingStore plus real Core/BepInEx ConfigFile. Engine/render classes remain
stubs; only the camera action facade routes into the actual private store. The
prior UI keyboard stub omitted F7; it now includes F7/F9 and the current camera
facade needed by the source-linked panel. The legacy full UI suite is not claimed.

Expected57 assertions cover default/configured/case-insensitive/numeric set keys,
plain/Shift dispatch, real-store developer non-persistence, original binding BOM/
unknown bytes, ordinary keys, wheel binding/capture, same-key hold release,
single-screen keys/capture, actual numpad5 reset namespace, other car/set and
Custom FOV preservation, blocked/locked capture and successful ordinary capture.
The actual old9b6ad33 CameraShortcuts/WheelInput blobs are exported byte-exact into
ignored bld/soba-camera-set-key/baseline. Baseline override properties affect only
the test project. Require the first old dispatcher to write a private offset and
fail the named regression, then current consumers to pass; no arbitrary crash is
accepted as expected red. The fixture catches failure and exits1 without a crash
UI. The full plugin compile remains part of the CPU request.

Two setup attempts are retained under ignored bld/soba-camera-set-key. GO523042
stopped during restore because the private feed lacked three net6 reference packs.
Packet02 GO523073 restored those exact qualified cached packs successfully, then
stopped during UI compilation: the newly linked CameraFramingStore requires the
production TripleGeometry helper. That helper is now linked too; its only external
type dependency is MountedView in the already referenced Core project. Neither
attempt executed the consumer or established the expected red/current pass. Both
completed certificate checks, independent process census and exact lease release;
no game, device, install or owner state was touched. Next is a fresh frozen packet
and serialized CPU GO, preserving both failed receipts.

Private canonical core/interop/recording/toolkit dependencies116files were copied
and hash-verified under ignored components/wheel/lib; no download/global install.
No product/native DLL is loaded by these tests; actual managed Core/BepConfig are.
Actual Unity/input/render/owner acceptance and the installed Woden remain unchanged.
