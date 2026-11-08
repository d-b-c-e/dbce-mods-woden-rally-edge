# Camera editor windows around saved offsets

Kole530891 following grid reconciliation39b6f5bf. Fresh worktree
E:/Source/_work/napo-woden-camera-ranges-20261105, base0d5a0f576c37aa206f3d4a899d36296cbe0a10dd.
Live visibility PUBLIC: local only, no push. Installed30FBAE77/490CC641 unchanged.

Current four pose sliders had fixed +/-2m,+/-4m,+/-90deg windows. A saved value
outside those windows jumped to an old endpoint on button/key/menu/drag input.
FOV showed final clamped lens and used20..120, so a saturated raw request could
be replaced by an unrelated in-range value. The source now expands camera-only
slider windows around finite saved values and adds a separate raw FOV request
accessor while retaining the existing effective-lens accessor. F6 shows requested
and effective values; final lens/projection remain1..179. No store/model/native
placement/force/key/default/reset changes. Legacy single-view controls remain.

CameraFieldDefaults fixture extracts actual CameraSlider/Slider and panel blocks,
actual raw/effective consumers, real store/BepInEx/Core; primitive event endpoints
alone simulated. Baseline must compile then fail RANGE_SIDE_NO_JUMP and
RANGE_FOV_REQUEST_RETAINED. Current covers five camera namespaces/both sets/all
five fields, positive/negative/outside/saturated/extreme finite values, passive
byte-preservation, actual buttons/arrows/menu/drag, reload, unrelated state and
lock/retry. Existing565 field Defaults retained; full actual panel UI fixture
and plugin compile are planned. Source, private package and file-only install are now qualified below.
New rendered/device/owner acceptance remains open.

## Retained qualification and failures

Product/compiler1d5364145d6d36abacdf61974885be30aa62279e. Later commits change only
tests/docs; final tested sourcea0d0f1a0c640665f8f087c1482685b07d99ea10d.
At large magnitudes the editor leaves at least2% abs(value) half-width so float
rounding cannot collapse a small ordinary window onto the current value. Bounds
remain finite; explicit representable edits and final rendering remain separate.

All intervals use BelowNormal, frozen pins/private caches and offline3packs,
certificate comparison, tracked lineage plus independent all-image audit/exact
lease release. Prior failures and their raw artifacts remain under
bld/napo-camera-ranges-20261105; they were not overwritten or relabelled PASS.

| Packet | Actual result and retained boundary |
|---|---|
| cpu01/531064 | Atomic slot refusal, no child/compiler/test/lease or owner write. |
| cpu01/1793882607507 under531064 | Baseline0d5 strict compile then RANGE_SIDE_NO_JUMP and RANGE_FOV_REQUEST_RETAINED actual runtime REDs. Initial plugin/current fixture0/0. Test-only minus-caption encoding error prevented the fake click; corrected to exact C# Unicode escape. Released341c169d. |
| cpu02/531153 | Strict current fixture0/0, real precision defect at saved1e8: ordinary half-width rounded back to same float. Product corrected in1d536414. Released380c685e. |
| cpu03/531200 | New full plugin/current fixture0warnings/errors.330 parameter cases,20 input-path cases and10 extreme cases reached lock case361, then fixture's unprimed read hit exclusive file lock before the writer. Fixture primes visible fields as existing refusal tests do. Released47b58dac. |
| cpu04/531239 | Actual range2527 and existing Default565 PASS over exact candidate Core. Whole-panel compile found stale fixture endpoint signatures from previous Defaults feature; added only boundary stubs. Released9ad0b025. |
| cpu05/531298 | Whole actual Panel strict compile0/0, broad suite's old numpad+ assertions expected wider FOV; corrected three test assertions to installed STD048 narrower contract. Releasedcaf8e423. |
| cpu06/531345 | Whole actual Panel compile0/0; broad suite FAILS at retired global Measured-perspective control. THIS GENERAL-SUITE FAILURE IS RETAINED, NOT FIXED; no full general-suite PASS is claimed. Releasedffdd12b1. |
| cpu07/531400 | New scoped whole-panel mode compiles0/0; fixture incorrectly matched only same-line row instead of shared stacked editor32px lower. Matcher corrected for both actual layouts. Releasedfbf02d42. |
| cpu08/531449 | PASS11.825s: actual whole Panel/current-camera drawing+scroll+five Default dispatch/readout320 checks at1280/860/540/360, with native/primitive endpoints simulated. Retained real consumer/store2527, Default565, baseline2RED and new full plugin0/0 without repeating passing scopes. All1604pins/certs exact;604-image census/7IDs absent/0scope; exact82ab190a released13:05:09.0546200Z. |

Source completion resultFD79CE1785733C301232810EFF0F14029C01240EF1157069B8B77F14B47DA237;
audit76B8B52070BF78084AC16472273D416940F6F384CC44774C5F52064D4FE61DB7.

## Immutable private package

Package01/531502 initially refused OTH's lease without entering a child phase.
Fresh1793884104097 under the same approval PASS24.827s: exact qualified Plugin/
Core reused without build or source-test repeat, all23 payloads/30 ZIP members,
fresh extraction and actual PS5 installer98 checks pass. The real producer keeps
four starts, private+ordinary metadata, config BOM/offset bytes and injected
update rollback. Exactly2 payloads differ from owner9CB32933; other21 retained.
All934pins/certs exact;608-image audit/exacta4f21716 released13:08:50.5485082Z.
Result2CE90A407EE84017A472043C129E7966991A4A2A77ABF24086554E359522CE38;
audit20FFF03DDA3EA54C9CBA07D9F6FDF5E83E56804277A2E236A256355E47C4E31B.

- Plugin A02C20CB54B33967A5B507F3D13D0324DC2D07AC57D66E8136986F5739D10346
- Core CAC1CC4F2F7F688D36F3A29A508D8DA4CE2D0E8154B0D6CD3CCCA0BBCD413EF4
- ZIP 4851B53E2948B0CA5C9100F95D15691BF0D7854F103742621FC46DA68F862114
- Manifest 653AEB60697599E74CDBC51891FE0ECCD9CDF2E1BF8454A534F12B786ED3B34B
- Archive: bld/napo-camera-ranges-20261105/package01/cpu-1793884104097/
  WodenRallyEdgeWheel-0.2.16-camera-ranges-private-update.zip

## File-only owner delivery

Kole531572 approves frozenA3774C46/164pins after teno's OTH rc13 release.
First admitted1793884369448 INSTALLED_VERIFIED16.207s, standingSTOP.
Exactly3 locked in-place writes: Core490CC641->CAC1CC4F, Plugin30FBAE77->
A02C20CB and actual packaged-installer AST receipt9CB32933->
8E30031CDE828A7CD385B8487451CD308E52CC2DEC1F7C703122DC32CF21C73C.
No added owner files/directories; other21 payloads unchanged.

Raw GetLastInputInfo idle17673.219s pre/17675.25s post-entry, gameclosed/Steam0;
all frozen pins/certificates exact. Qualified writer1B1F6DA9 retains original
SID/SDDL/creation+write ticks/raw attrs. Separate verification after writer exits
checks all23 payloads/24 original metadata+84 protected records/108SID+SDDL/
25 typed preferences and external backup. Installed Wheelkit.CoreCA985E33
actually reads four owner-root starting choices without Apply or config writes.
607-image independent audit sees all4 owned IDs absent/0scope. Exactlease
fba561b1a0694b6c84f8e5e27f613a45 released13:13:06.6011516Z/no custody.
Result6855DBF7359A66C9D7A353FD1D184954AB8B5325D70863CA8498DA0B616CDA21;
audit8F0B25DB5E25436DCCB03A34B0F2B3227D82A6F7736480829190922B604EF9F9.

External backup/guarded one-step rollback (gameclosed/coordinated rig):
E:/Source/_archive/2026-11-05/owner-before-woden-camera-ranges-20261105/Rollback.ps1 -Apply
SHA5AA5F9B7184DDCF9A8BB63371BE75B55547AB3FDD093ED25B7B00AB05184186E.
It preserves foreign drift and verifies all originals/security; no owner rollback
was needed. Completed frozen execution packets are historical after docs commits;
the external self-contained rollback does not depend on source HEAD.

No game/native-game-DLL/rendered UI/input/device/force or owner Apply ran.
320 is a managed drawing/event fixture, not pixels or seated acceptance. Legacy
single-view fields remain outside this increment; camera lifetime/scene handoff,
actual keys/F6 rendering and all-camera flat/curved seam/HUD/owner gates stay open.
