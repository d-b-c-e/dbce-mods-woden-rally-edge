# Current camera/set per-field Defaults: source candidate

Queue authorized by kole528621 after DRIVE raw/final-lens delivery. Fresh source
worktree napo-woden-field-defaults-20261105, base local main
b84cd999d6a3b7b64a2c6986cd80ff6c0e4b59c6. PUBLIC/local only, no push. Installed
finite-FOV source8c98267/Plugin9F72D8D8/Core9D30ED0F remains unchanged.

Five current camera/set rows now have a Default command: side, height, forward,
tilt and FOV. The existing number-editor geometry reserves96px for each command.
Other sliders keep their existing default-free geometry. Each callback calls a
dedicated production one-key writer with zero offset; it does not rebuild a pose
or use Settings.Save/Dirty. FOV remains shared by camera/set across cars; position
belongs to car/camera/set. Explicit zero overrides a legacy fallback after reload,
even before cache/UI priming. Cache values publish only after the save succeeds.
Whole Reset/bindings and accepted force/settings behavior remain unchanged.

Scope is the current Game/Realistic camera-set editor. Legacy single-view absolute
pose editors and the separate fixed-editor-range gap remain queued. Existing
native fitting/calculated starts supply the reference; no Art placement constants
or guessed camera identities are introduced.

CameraFieldDefaults fixture prepares exact actual Slider, FOV/position UI blocks,
TripleView consumer bodies, real Core and CameraFramingStore/ConfigLine/BepInEx
storage. Only native camera/focus/availability, primitive drawing/buttons/events
and visible notices are simulated. Baseline must build then separately fail the
named DEFAULT_SIDE and DEFAULT_FOV runtime assertions. Current tests cover five
fields, five native/mod preset identities, both sets, one-key byte/BOM/mixed-ending
scope, other cars/sets/FOV preservation, reserved Default row width, failure/cache/
notice/explicit retry, duplicates, unavailable source, legacy/reload before UI
priming, whole Reset and repeated defaults. Qualified under CPU529665 on source52288c2e: actual old source strict build followed by named DEFAULT_SIDE and DEFAULT_FOV runtime REDs; current actual UI/store565 PASS; full plugin0warnings/errors; CameraSets80 plus seven focused459=539 PASS against the exact new built Core. Primitive/native endpoints remain simulated.

Frozen CPU admission will use full private dependency closure, all three offline
NET6 targeting packs and independent per-project assets. Full plugin compile and
retained camera/math scopes remain distinct from native UI/keyboard/rendered
layout, game/device/physical and new owner acceptance. No owner writes/package/
install/game/input/device action or process/lease custody in source preparation.


CPU529665 completed46.020s with935pins/cert metadata unchanged.
Independent all-image audit:608images,37tracked identities absent,0scoped
children. Exact lease23050e407a0144f0b5020c45981594f7 released
2026-11-05T11:07:11.1661666Z. No game/UI/input/device/owner writes or custody.
Result E9F152158035975068988523300605D785C9FCB2DDD016A853CA9C0B1EDBCCEB
Audit BC9A23B255F40F0A326E720BACBA5C408EF328E26331E7E3DAE8FAF133E5A324.
Raw evidence: bld/napo-field-defaults-20261105/cpu01/cpu-529665.
Qualified Plugin30FBAE774B2DEB604229EEA4D60ADD6AB87FA06A0EF18F3D44D2F45DD504F672
and Core490CC6419078734868CA80B2B564B1BBD0998FC8D2A0474D312C6D69E679F501.
No passing finite-FOV or key-capture suites were repeated.
Package/owner delivery are the next separate gates.
