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

Packet03 GO523134 compiled old/current consumers with zero warnings/errors and
qualified the actual old named regression: writes0->1, private config changed,
binding bytes unchanged. Current dispatch/reset checks reached the first capture
status assertion, which failed because the focused fixture had never opened the
settings panel. Actual MenuOwnership.AfterStock deliberately reports unknown
input outside that panel; the refusal correctly stopped before key handling.
The fixture now opens the actual source-linked panel, observes one synthetic
stock pad, and asserts the real neutral-interval gate for every capture. It does
not bypass the production gate. Expected total is now66 checks. The old witness
is retained for a current-consumer/full-plugin continuation; full plugin has not
yet compiled.19.01s/certmetadata unchanged/17identities absent/censusPASS; exact
aad6589083194467aa5e805676796e6b lease exited2026-11-05T01:35:57.423Z.
Remi523170 independently reviewed the source/dependency closure and522pins,
finding no further missing references/links; no execution was claimed.

## Qualified source-only result

Packet04 GO523238 PASS on clean sourceeec3b87845c648ebe2d0dbdfbfa9a704b2021c63:
66/66 focused source-linked checks; complete plugin/Core compile0warnings/0errors.
All526source/tool/evidence pins and certificate metadata unchanged;17observed
identities absent in independent all-image census.20.12s; exact
bcb8d8983fb8434ebb8c0637b92b442f lease exited2026-11-05T02:06:12.319Z.
Qualified old523134 RED witness/assembly/log retained and pinned, not rerun.

- Plugin248B8AD1265FF488819E658E4C83CDD791A9EF5B18FFAEFCC0FB7A46D252393D
- Core911D5EC8C6E85AE8B70BD3CD011FD192D7452F4D89B51AFFEAC35C1FEA692048
- Result6158C561F55B190814134E3A7319D35BDE4723BB827FD054C2129A63077EB7B1
- Audit093A76055241D270EFDC200E8E8BE76921BA4FF07D00F9BC31E6908061963EFB

Raw evidence: ignored bld/soba-camera-set-key/packet04/cpu-523238; earlier
attempts retained beside it. This source-only assignment is complete. No main
merge, package, public push, owner install, product/native module load or game/
UI/input/device/force activity. Actual Unity handler/rendered capture and owner
acceptance remain open. The existing owner install remains unchanged.

## Landed on local main and installed

Kole523283 authorized delivery after source qualification. Canonical local main
fast-forwarded9b6ad33->acd5110; GitHub visibility PUBLIC verified. No push.
Complete private update package keeps21existing payloads and replaces the
qualified plugin/Core pair. ZIPBC8F8BDBDA1329E6511A4DACC563AF1CD4CC1DA57292FC5D5AE524E60B762EBF;
manifestE49438620FB4CEE28D6501FBD0698F2DA3B4515F4A0DF57EDB4086809F6FB04E.
All30ZIPmembers verified. Actual packaged PS5 installer98checks passed under
CPU523407: fresh/update/injectedrollback, private+ordinary hashes, fourstarts,
configbytes/BOM/offsets. Initial delivery523390 fixture-order refusal retained;
corrected continuation reused the sealed ZIP without rebuild/repackage.

File-only install523477 PASS: installed0.2.16+eec3b87845c648ebe2d0dbdfbfa9a704b2021c63,
Plugin248B8AD1265FF488819E658E4C83CDD791A9EF5B18FFAEFCC0FB7A46D252393D,
Core911D5EC8C6E85AE8B70BD3CD011FD192D7452F4D89B51AFFEAC35C1FEA692048,
receiptFC5ABFF67B891943533CEC654339CD490297899EBFFE591CFE8D6C68876BC4E5.
All23owned payloads/fourstarts/private+ordinary metadata verified.81protected
config/save/unknownplugin files retain SHA/creation+write ticks/rawattrs and
inventory;25typed preferences exact.24original/81protected backups verified.
10.19s/inputpins+certmetadata unchanged/3observed identities absent/censusPASS;
exacta033b92a4b33442eb226dd6c7e8513a9 exited2026-11-05T02:21:59.971Z.
Independent postrelease readback/gameclosed/tokenabsent PASS.

Guarded rollback dry-run PASS:
E:/Source/_archive/2026-11-04/owner-before-woden-camera-set-key-20261104-202031-53a14222/Rollback.ps1 -Apply.
ResultBB3F377A03149C311ACF2BE39112346494C4D717413C1A00712BD81C0A8C0866;
audit412F72D8AC75B96C8F97877DE3C4695A3DA989BBE67799A049EC16833119A1D0;
independent06D63FEE7441167F129EA06972E42A7A1AD6BD8F2CAEE1216E6EA4F4E53E2A57.
Raw evidence/package remains in the retained evidence worktree
E:/Source/_work/soba-woden-camera-key-audit-20261104/bld/soba-camera-set-key/;
install01/install-523477 is the final install receipt. Preserve this worktree
until evidence/package retention is transferred. No custody/game/GUI/input/
device/FFB/config writes/public push. Native/rendered behavior and owner
acceptance remain open; portfolio camera_sets stays partial.
