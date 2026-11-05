# Finite FOV requests and final lens: isolated source candidate

Assigned by kole527686 after the installed five-game STD-046 matrix. Base
`9f961f38f80737c7c92e56cd2578c18321c2af7a`, installed compiled eec3b878/
Plugin248B8AD1/Core911D5EC8 remain unchanged. Worktree
`E:/Source/_work/napo-woden-final-lens-20261105`. PUBLIC: local only, no push.

CameraLens now separates finite requested FOV from the final effective1..179
degree lens. CameraFramingStore validates finite offsets without rejecting them
according to the current native lens. TripleGeometry uses the same effective
angle instead of reverting to physical eye distance when a finite request passes
the representable lens range. Ordinary values and zero Realistic geometry retain
their existing exact path; invalid/unknown native starts retain the physical
fallback. Keyboard FOV edits operate on the raw saved request, so a saturated
display cannot consume or replace the player's offset after a native lens or
seat change. Absolute F6 requests still subtract the current calculated start.

No key binding, capture, set selection, position namespace, legacy migration,
physics/force setting, saved owner data or package identity is changed. The F6
bounded field editors/per-field Defaults remain a separately ranked follow-up.

Unexecuted regression coverage: CameraFovProduction freezes unchanged actual
TripleView methods and real Core/store/geometry, with only native measurements,
camera availability and notices simulated. Four old-source modes must build then
fail at their named lens/store/projection/consumer assertion. Current must pass
all: raw large offsets/reload/owned scalar bytes/BOM/mixed endings, native lens
changes/saturated nudges, Game/Realistic scope, reset, finite rejection, no-source
guards and failed-save/explicit retry. Existing CameraSets539 and key-consumer66
passing evidence is retained; fresh affected build/fixture qualification needs
a frozen CPU GO. No new build/tests have run.

This is source preparation, not runtime qualification: no native Unity callback,
actual keyboard/F6 event, camera/frame/seam, physical device, installation or new
owner acceptance evidence. No rig/process/lease custody or installation action.
