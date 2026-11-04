# Woden camera sets — local source candidate

Kole511599, corrected511916: STD-046 applies to bonnet, bumper and each actual
stock chase/isometric preset. No cockpit is invented. This candidate is not yet
built or installed; the owner installation remains the earlier a1c8dd2 candidate.

Normal triples always use angle correction and configured curve correction.
Retired CompareView values are ignored without deleting the historical setting.
F7 and the F6 button save one starting-choice key for the current camera;
Shift+F7 changes only session correction state. Fresh defaults: bonnet Game,
bumper Realistic, chase Game. Existing visible global choices seed new keys.
Game FOV wider than seat-true stretches sides despite continuous corrected seams.

FOV offsets are per camera/set, shared by cars. Position/pitch offsets are per
telemetry CarId, camera and set. Existing manual poses seed fallback offsets;
reset writes zero offsets and says it returned to the calculation. Numpad plus
narrows, minus widens. Only untouched old layouts migrate in memory; customized
keyboard/wheel bindings stay intact. Camera/set/Custom and actual key reminders
appear near the bottom on camera/stage changes. LastCamera restores through the
existing legal native RecordedView route; late preset arrays and body meshes
remain pending until available instead of latching a missing first observation.

The chase lens uses Truckful's adopted ownership rule: remove our output before
native smoothing, preserve a foreign value, then apply this frame's set/offset.
Bumper leases the body renderer's forceRenderingOff override; native enabled
state, layers and colliders are not changed. Existing Woden body-fit and near-clip
calibration are retained. Art's placement numbers are not copied as constants.

Curve math is reused: exact CurvedPanelWarp.cs at toolkit7af26cd and
TripleRigDefinition.cs at74c5eda. UVs come from the measured panel-edge-plane rig;
WithSourceOn moves only the render source for Game/adjusted lenses. Existing HUD
presentation stays outside the warped scene.

New camera actions use ConfigLine atomic owned-key/batch publication. Cache
values follow successful disk writes. Closing an unedited F6 panel no longer
serializes its whole configuration. General non-camera Settings.Save remains
a separate STD-044 gap.

## Keys and Wheelkit handoff

`[Triple]` starting choices: BonnetRealistic, BumperRealistic, ChaseRealistic;
ChaseNRealistic/IsometricNRealistic remember native presets individually.
FOV: `<camera><Game|Realistic>OffsetDegrees`. Positions:
`Car<id>.<camera>.<Game|Realistic>.<Side|Height|Forward|Pitch>Offset`.
LastCamera stores a supported camera key. Wheelkit should write only starting
choices, including existing preset-choice keys, and leave offsets alone.
Proposed wodenFovSets receipt capability was sent to kole; muna owns manager
integration. No Wheelkit camera Apply acceptance is claimed.

## Evidence and next checks

Before production edits, actual old sourcef0e68ec0 reproduced12checks/8expected
failures under CPU512042. Includes retired comparison startup, chase/mounted
coupling, measured distance and keypad direction. Job empty, independent all-image
audit passed, handles0, exact lease released. Exact raw baseline inputs are frozen
under `bld/camera-sets-qualification/cpu-baseline02/` in the owning worktree.
First attempt511982 refused a forward-slash executable path before resume; its
failure/release evidence remains separate. No product test ran in that attempt.

First GREEN run (CPU514123) compiled CameraSets, then found the generated
Renderer has no forceRenderingOff member. It exited with no fixtures executed;
job/audit/handles/lease closure passed and My/Root certificates were unchanged.
RendererNative now uses the two exact injected names present in supported
UnityPlayer SHA256 9FEEDB4527A30FAE4762BFF45D204F8E85B90851B2C8AE8D24F436F7D474667B,
following existing native-self/bool adapters. Missing calls leave the body alone.
This is shipped-file evidence, not proof the runtime calls succeed.

Green and full-plugin retry await CPU GO. CameraSets uses actual Core, real
BepInEx ConfigFile and production planning/store handlers for restart, locked
saves, byte preservation, independent cars/sets, developer non-persistence,
measured-eye UVs, lens withdrawal and custom binding preservation. Existing
focused camera/INI/UX suites are retained.

After installation, owner checks remain: every stock preset plus bonnet/bumper,
F7/F6/restart, Shift+F7/restart, reset and Custom labels, held shifter, car/stage
changes, body visibility, unwarped HUD and curved straight edges. Rendered UI,
placement, actual device/force behavior and owner acceptance remain unproved.
