# Woden camera starting-choice receipt

Source-only declaration for the matching Wheelkit reader. `Manage-Install.ps1`
adds `wodenFovSets` only on Install, with schema 1, the fixed BepInEx config and
`Triple` section. No installed receipt or owner configuration was changed.

`CameraFramingStore.Pick` consumes Bonnet/Bumper directly. Generic Chase uses
`ChaseRealistic`; indexed Chase and every Isometric slot use a saved slot boolean
with `_chase.Value` as fallback. Thus both stock families follow the profile's
Chase preference, including existing overrides. There is no cockpit mount.

The declaration names four generic booleans, defaults Game/Realistic/Game/Game,
and the two stock families with canonical nonnegative Int32 indexes. It declares
neither a native preset count nor arbitrary destinations. Wheelkit writes the
four generic choices plus existing canonical indexed choices only. It preserves
all FOV offsets, per-car position offsets, LastCamera and legacy global settings.
The entire config snapshot must stay unchanged from preview through Apply,
including no-op plans; receipt and every owned payload are separate dependencies.

`Test-WodenFovReceipt.ps1` executes the actual producer and install-only receipt
assignment through the PowerShell AST. Baseline ab0c0c3 is RED (missing producer);
the new producer is GREEN in PowerShell 7 and Windows PowerShell 5.1. Its
`-EmitFixture` output supplies Wheelkit's inert reader declaration fixture.

Matching Wheelkit source e4f4855 passed116/116 Release camera cases under
GO518906 (new Woden36/existing80). Restore/test27.06s, offline18-package feed,
isolated CLI/NuGet/TEMP, unchanged certificate metadata. All18 observed process
identities were absent at independent census; exact named lease released16:36:14Z.
This is not job containment. Evidence is in that Wheelkit worktree's
`artifacts/napo-woden-camera/cpu-518906/` (TRX, result, audit and closure).

This is no plugin rebuild, package, install, game/UI, physical or owner-acceptance
evidence. `runtimeVerified=false` and `offsetsOwnedByApply=false` remain explicit.
