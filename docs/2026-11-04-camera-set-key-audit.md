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
