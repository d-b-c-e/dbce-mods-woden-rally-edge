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
and plugin compile are planned. No compiler/test/package/install/game/UI/device
execution yet. CPU requires frozen packet/exactGO/namedlease/privatecaches/
certificates/allimageaudit/exactrelease. New rendered/device/owner acceptance open.
