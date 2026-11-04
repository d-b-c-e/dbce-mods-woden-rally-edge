# Proposed Wheelkit camera adapter contract

Design only, kole request #507862, 2026-11-03. Reviewed game source `db5054ce6b346c69ab46c6b3784c6b09774e9d7d` and Wheelkit `3b2c6e9d5df4d286418e77f95256f7441751352e`. No implementation, receipt, installed state or adoption status changes. ISS-0023 remains open.

## First slice: eleven adjustment bindings

Export an explicitly chosen Wheelkit `CameraKeySet` into the existing `BepInEx/config/wheel-bindings.json` `CameraKeys` object. Do not create a second bindings store. `CameraKeys=null` means keep the game's saved keys, including its existing FOV direction. This slice works independently of driving-axis setup, input backend and triple-screen mode; it must not enable wheel input, change FFB, or reset camera poses.

The current readers are [Bindings.cs](../components/wheel/src/WodenRallyEdge.Core/Bindings.cs), [CameraTuning.cs](../components/wheel/src/WodenRallyEdge.Core/CameraTuning.cs), and [CameraShortcuts.cs](../components/wheel/src/WodenRallyEdge.Plugin/CameraShortcuts.cs). Woden stores Input System `Key` names, not Unity `KeyCode` names. Existing Wheelkit `WodenControlProfile` only preserves/validates this object during controls edits; it does not export the shared camera profile.

Translate by action meaning, never dictionary order or the old default key:

| Wheelkit action | Woden `CameraKeys` member | Effect in current `CameraTuning.Adjust` |
|---|---|---|
| `KeyForward` | `Camera forward` | Forward + move step |
| `KeyBack` | `Camera back` | Forward − move step |
| `KeyUp` | `Camera up` | Height + move step |
| `KeyDown` | `Camera down` | Height − move step |
| `KeyLeft` | `Camera left` | Side − move step |
| `KeyRight` | `Camera right` | Side + move step |
| `KeyPitchDown` | `Camera pitch down` | Pitch + tilt step; look down |
| `KeyPitchUp` | `Camera pitch up` | Pitch − tilt step; look up |
| `KeyFovDown` (Zoom in) | `Camera narrower` | FOV − step |
| `KeyFovUp` (Zoom out) | `Camera wider` | FOV + step |
| `KeyReset` | `Camera reset` | Existing active-view reset |

The complete finite key translation is:

| Wheelkit `KeyCode` spelling | Woden Input System spelling |
|---|---|
| `Keypad0` … `Keypad9` | `Numpad0` … `Numpad9`, respectively |
| `KeypadPeriod` | `NumpadPeriod` |
| `KeypadDivide` | `NumpadDivide` |
| `KeypadMultiply` | `NumpadMultiply` |
| `KeypadMinus` | `NumpadMinus` |
| `KeypadPlus` | `NumpadPlus` |
| `KeypadEnter` | `NumpadEnter` |
| `KeypadEquals` | `NumpadEquals` |

All target spellings appear in the pinned local InputSystem assembly's metadata strings; this read-only observation is not an executed enum/reader test. Implementation qualification must validate every entry against the actual pinned reader enum and `BindableKeys`. Do not use a prefix substitution or accept additional strings through `Enum.TryParse` alone.

STD-048 requires Plus to narrow and Minus to widen. Current Woden defaults do the reverse. The semantic mapping above deliberately gives `Camera narrower=NumpadPlus` and `Camera wider=NumpadMinus` when the owner explicitly chooses Wheelkit defaults. Preview must show the action labels and old/new keys, including this reversal. Do not reverse the numeric adjustment operations, silently migrate a saved game map during Apply, or relabel “wider” as “zoom in”.

## Receipt and write transaction

Proposed new capability: **`wodenCameraKeysV1: true`** in `BepInEx/WodenWheel-install.json`. This name is a design proposal, not a current capability. It declares the exact path, JSON action names, key-name translation and reader behavior above; it declares no camera-set, fitting, stage-restoration or runtime acceptance. Keep existing receipt identity/payload checks. The reviewed package/installer must carry the declaration only after reader/adapter fixtures pass; never retrofit an installed receipt by hand. Missing, false, string-valued, duplicated, malformed or oversized capability input must leave camera export unavailable, with an explicit Preview explanation; unrelated eligible Apply work can continue. Do not report the selected camera request successfully applied when it was omitted.

Plan before writing:

1. Validate a complete eleven-action source set, finite allowed key names and uniqueness. Translate all entries, then check conflicts against **preserved non-adjustment** keyboard actions, including `Camera`, `Rear view` and extension actions in `CameraKeys`, case-insensitively. Refuse a collision; never unbind those actions to make room. Preserve `Buttons`, including buttons for adjustment actions: keyboard export is not a command to clear joystick bindings.
2. Parse existing version-1 bindings using the existing controls-admission checks, without enumerating devices. Reject malformed JSON, ambiguous case variants/duplicate owned members, unsupported version or invalid unrelated controls that would make the consumer refuse the file. Preserve unknown members and their bytes. An absent file is unavailable for this first slice; do not manufacture an incomplete wheel setup. An absent `CameraKeys` object in an otherwise admitted file may be added with these eleven explicit entries; do not serialize unrelated reader defaults.
3. Merge camera and any simultaneous controls edits into **one** final document and backup/transaction. A key-only Apply must not require ready driving axes or change `[Wheel] Enabled`. Patch only the eleven owned string values (or the missing owned object/member), retaining `Camera`, `Rear view`, extension members, axes/calibration, buttons, unknown data, encoding/BOM and line endings. Do not route through whole-object `Bindings.Save` or whole-document JSON reformatting.
4. Refuse a changed dependency/file between Preview and Apply. Use existing Wheelkit atomic backup/write machinery. Read back the complete translated set and verify preserved non-adjustment data; verify with the Woden reader, not just JSON validity. A second Preview must have no changes. Use **More > Restore settings** and verify the exact pre-Apply bytes, including file existence when relevant; this is not Configure Undo.

## FOV starts are a separate capability

Do **not** map the four `TripleCameraStart` booleans onto global `[Triple] MatchGameFov`, `ChaseUsesGameFov` or the current bonnet/bumper pose FOV. Woden has global Physical rig/Adjustable FOV semantics. `MountedFovLocked` intentionally suppresses FOV adjustment in measured mode without saving unused values or disabling body fit. Exporting keys must preserve that behavior, F7 behavior, geometry, poses, camera steps and current saved framing.

Preview should state that Woden's independent cockpit/bonnet/bumper/chase Game/Realistic starts are unsupported, while allowing a supported key-only export. No `artFovSets` declaration is appropriate. A future start-set capability requires game-side identities/storage and switching for independently available views, calculated starts versus saved offsets, stock chase handling and per-car/set lifecycle semantics. Name and version that contract only when the consumer exists; do not write fictional cockpit/chase sets now.

## Required Wheelkit changes, for coordinator assignment

- Add the explicit Woden action/key adapter and proposed receipt gate to the shared profile plan. Retain `CameraKeys=null` semantics; do not require `TriplesOn` or a triple layout for keys.
- Extend the existing Woden binding transaction so camera and controls plans merge once. Add byte-preserving owned JSON-string patching if the existing writer cannot preserve unrelated bytes; do not reuse whole-document serialization by assumption.
- Add clear capability/unsupported-start and semantic old/new-key Preview/readback output; include the exact file in settings backup/Restore. Reject an invalid complete set before any file write.
- Qualify missing/false/malformed/duplicate receipt cases, every key translation, Plus/Minus and pitch direction, non-default permutations, non-adjustment conflicts, unknown JSON, BOM/CRLF/LF, changed-file refusal, combined controls+keys, no-write Preview, idempotence and byte-exact Restore with copied fixtures. Add a consumer-directed Input System enum/dispatch check. Reuse existing focused test harnesses; no broad rig suite is needed for this slice.

Game-side follow-up is package/installer declaration support and exact-reader fixtures; a default-direction migration is a separate STD-048 task. STD-048 asks untouched defaults to migrate while custom choices remain. Current `Bindings.Load` explicitly preserves saved maps, even former default layouts. Do not infer “untouched” solely from equality to an old map: record the provenance/policy decision in that migration task before changing load behavior. It is unnecessary for an explicit chosen-profile export.

## Evidence boundary

Current `TOOLKIT-ADOPTION.md` lists STD-042 partial and STD-046/048 pending. This design adopts none of them. Full STD-042 additionally needs every camera's sticky per-car/per-set adjustment, correct dynamic chase offset handling, fitted inheritance, late-view/stage restoration and name/key overlays. Woden's current 30–110° bounded pose is not the standard's 20–120° F6 range. Installed Cameras UI, a real selected-profile transaction and restored state, rendered direction/lock behavior, gameplay and owner acceptance remain separate future evidence. No new tests were run for this docs-only draft.
