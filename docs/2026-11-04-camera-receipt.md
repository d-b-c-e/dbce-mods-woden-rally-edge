# Woden camera starting-choice receipt

Initial source-only declaration for the matching Wheelkit reader. `Manage-Install.ps1`
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

## Owner receipt upgrade

GO519745 completed after the full shared-rig series. Only
`BepInEx/WodenWheel-install.json` changed, from31BE6508 to3A760EEE. The actual
producer added only wodenFovSets, preserving existing private/ordinary metadata;
all23 payload bytes remain unchanged. Installed Wheelkit0.6.68 Core4B16C553
admits four starting choices. This read-only production-reader result does not
establish GUI reachability or an owner-profile Apply.

Independent verification passed41 backups and104 protected config/plugin/LocalLow
files, including bytes, creation/write ticks and attributes. One-step rollback:
`E:/Source/_archive/2026-11-04/owner-before-woden-camera-receipt-20261104/Rollback.ps1`.
The8.61-second interval had6 observed identities absent at final census;
certificate My/Root metadata unchanged, exact lease released17:26:38Z.
Evidence: `components/wheel/bld/camera-receipt-delivery/install-519745/`, result
022E8FF8 and independent auditE9037B2E. No game/UI/input/device or physical test.

The initial519593 wrapper attempt failed before lease/output/owner writes on a
literal source-root typo. Corrected path and hash-case text were approved as
8B3177F1/84279977. Original failed script/plan are hash-verified in
`pre-admission-519593/`; the consumed GO was not reused.
