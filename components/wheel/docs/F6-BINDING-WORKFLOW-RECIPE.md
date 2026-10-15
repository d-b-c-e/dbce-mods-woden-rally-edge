# Private F6 binding workflow preparation

Status: **source preparation only; new seam and guidance are unbuilt/unrun**.
Qualified base `f703484f4a629012af3033b5191713691eb56fff` retains its actual
plugin 0-warning/0-error build and R8 UI 5,249-assertion pass. The separate
guidance change is `a135912863354d81851356aae76d20f052a9745b`, reviewed by Lola
130360. Affected offline checks and a new final payload freeze precede runtime.
The matching standalone helper's compatibility guard must match that final
payload before delivery; this preparation does not claim a bundled download.

## Existing route and limits

Reuse `tools/controls/Run-Controls.ps1` with explicit `-ShimF6Workflow`. This is
the existing restored temporary candidate overlay in the installed game folder,
not a new private-copy launcher or lasting install. Its existing cold request,
shared lease, output fences, exact process/normal close, raw registry and full
owner-file restoration remain in charge. Current owner-activity policy applies.
Unknown requests/commands are preserved and block admission.

Schema 9 uses the ordinary admission mode and exact extension-3 `4AF269C9`
shim plus load-muted native `088F1089` and its `6068B7A8` provenance. No raw
injection, gameplay trial, replay, recording or DevInput is enabled. The
temporary console-off setting is restored with the existing config snapshot;
it does not set foreground or falsify Unity focus.

Only the actual shared production host edits the profile. Commands have the
existing nonce/sequence reply plus a canonical unique step GUID, at most 64
steps. Status may report a background/read-only host. Edits require actual
Unity focus, the owned OS foreground window, open menu Controls, ordinary
reader readiness and fresh accepted observations. No second reader opens.

The probe permits fixed mod-page views and production-host operations. It
does not navigate the game's menus. Begin requires an explicitly selected
device already present in the accepted host observations. No input edge is
created; Begin/Cancel observations cannot establish physical binding delivery.

## Frozen inputs before the affected offline batch

| Input | Pin/path |
| --- | --- |
| Source | `dine/woden-f6-runtime-20261015`, isolated worktree `E:/Source/games/.worktrees/dine-woden-f6-runtime-20261015`; final source manifest is in hub `artifacts/woden-f6-runtime-prep-20261015`. |
| Shim | `E:/Source/toolkits/.worktrees/valo-shim-snapshot-20261015/native/shim/build/x64-mt/dbce_shim.dll`, SHA `4AF269C9F2FDBAE3CEF1A78E563C3877CB0AB820D4BE4C2579A1AD6EA25CECC5`. |
| Muted native | `E:/Source/games/dbce-mods-woden-rally-edge/components/wheel/dist/stage-automation-row-playback-20261013-01/BepInEx/plugins/WodenRallyEdgeWheel/WheelFfb.dll`, SHA `088F1089BEB6D18D478F37582C59330C6BDA6BC37D5E365F6BA7EBF80040F567`. |
| Native provenance | Same stage root `WheelFfb.noforce.provenance.json`, SHA `6068B7A848F85FE42AE88EE8A86B10F1EF4C4E307B9C4235BE44C1A84BBEA8E5`. |
| Existing profile | Hub `artifacts/woden-f6-runtime-prep-20261015/accepted-controls-source.ini`, exact current DRIVE profile bytes, SHA `F34F7EF757855B1EF7806E045F237FF9AE3A4929AEC4343EFA7ECBF198E6B97E`, schema 1. No Apply or regeneration. |
| Explicit device | Existing profile MOZA instance `d71b8350-61b7-11f1-8001-444553540000`; it must still be in fresh observations or Begin refuses. |

The future candidate directory is this worktree's plugin Release/net6.0 output
with the reviewed shim copied by its existing exact-pin build target. The new
plugin/Core/probe hashes are **pending the affected build**, not the old base
hashes. The final runtime command must include `-ProbeSha256` from that result,
all paths above, `-ShimF6Workflow`, `-NativeProvenancePath`, a new result path
`E:/Source/dbce-project-mgmt/artifacts/woden-f6-runtime-prep-20261015/runtime-01`
and `-Seconds 180`. No runtime handoff exists yet.

## One bounded supervised sequence

Run the existing supervisor once after fresh runtime handoff and owner notice.
Retain its process/session while sending existing `Send-Command.ps1` commands.
Use `-Operation workflow -Arguments "<family> <nonce> <new-step-guid-N> <op> ..."`.
Each completion must match the nonce, sequence and step before the next command.
An unknown completion is never permission to resend.

1. Wait for the existing admitted identity/context within its deadline. Request
   `view ... controls`, then `bindings ... status`. Inspect actual native
   producer/force counters, host access, OS foreground, device identities and
   the PNG produced by the view step. A reply alone does not prove pixels.
2. With no real foreground or no Editable host, record the precise binding
   observation unmet. Passive `view ... ffb` and `view ... cameras` can still
   record rendered guidance/status without editing. Then stop; do not activate,
   fabricate focus, refresh/open a reader or relaunch.
3. If eligible, `bindings ... begin modMenu <explicit-device>` then `cancel`.
   Inspect whether it actually listened/canceled; an unexpected real edge is
   retained as an observation, not synthesized or assumed. `discard` any draft
   before proceeding. No active mapping or file should have changed.
4. Invert the existing steering axis to the opposite of its active inversion
   with `invert steer on|off`. Check Draft versus Active and unchanged file
   bytes. `view ... ffb`, `close`, `keep`, then `cameras`, `close`, `keep`
   observes the global unsaved warning/dialog and its keep-editing behavior.
   View replies include page/dialog/closing state and step-specific screenshots.
5. Return `view ... controls`; obtain a new actual editable status, then `save`.
   This is the production checked Save. Its one saved-profile witness binds
   nonce, step and exact before/startup/saved hashes. Verify Active/native cached
   profile unchanged, Draft clear and Saved-for-next-start shown. Restart
   acceptance is not established in this one launch.
6. Create another inversion draft, `discard`, and verify the saved-next-start
   state remains distinct. `view ... close` uses the existing release barrier;
   original presentation is restored before generic settings Save. Then send
   ordinary `stop` and let the exact supervisor close/restore and release.

Any refused editable step ends editing with normal stop and restoration. The
runner accepts a changed INI only when its one actual checked-Save witness
matches; otherwise startup bytes must match. Native JSON, candidate payloads,
original config values and full final owner restoration retain their existing
checks. A missing/mismatched witness or restoration failure is a failure.

## Mounted FOV is a separate unmet observation

All eleven existing admitted tapes are Stock-only. This admission mode refuses
a player car and has no mounted route. Cameras-page pixels can show the FOV
choices and explanation, but cannot establish a mounted lens/projection change,
three angle-correct rendered views or +/- key delivery. No mounted tape or
gameplay navigator is added for this handoff. Physical force and triple-screen
acceptance remain unqualified.
