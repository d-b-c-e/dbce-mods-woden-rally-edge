# Current handoff — 2026-10-13

Owner install: `0.2.14+0ed0292888f29d2152e3a0a77c39e23e43b9d72c`, revision-4 receipt SHA-256 `CC7983451ACF140AC742A648D15253B98D3BFF0993815C38AB1D17D8810304A2`, exact source `0ed0292888f29d2152e3a0a77c39e23e43b9d72c`. The production-native private ZIP SHA-256 is `A97B8E8CBD5B17BCC42D07E16106872A7714F78B615AE4AED2EA85F0A9459DBB`, with 17 receipt-owned payloads matching manifest `10770DE7355F820440FA391303A8EA8DD1C38133B3EED664195B80A186EB63CE`. It was installed with Woden closed under the shared lease; 306 non-owned files in the full inventory, saved config `20C0BC9A...`, and raw Unity preferences `BE7DC331...` remained byte-exact. Saved Backend is Native with the prior tune. Production native `WheelFfb.dll` remains `F8361A08BBE08EAAB936B37800D308CF1FC7EDAB34E2839DB16A447FE34178C5`. Backup: `D:/Program Files (x86)/Steam/steamapps/common/Super Woden Rally Edge/WodenWheelBackups/before-install-20261013-152711-fb8bc483`. See [current STATE](STATE.md) and [install readback](../../../results/hudspeed-owner-install-20261013-01/install-result.json). The previous `a9a255c` receipt `FE2BABFA...` is preserved in that backup.

No game launch or force/device output occurred during installation. Physical rendered scanout and ordinary Shim FFB-open/shared-reader coexistence are still unqualified. The ordinary `Start-RecordedGame.ps1` path captures signals from launch for offline reprocessing; Wheelkit's registered Woden path records a muted stage trajectory. These have different replay claims. The source-only Record/Stop gap review is in the [hub handoff](../../../../../dbce-project-mgmt/artifacts/woden-private-apply-20261013/ordinary-record-stop-gap.md). The sealed Kenya reference is unchanged. All dated handoffs below describe historical checkouts and installs.

Before the lasting install, the same managed HUD-speed payloads ran in one temporary load-muted Kenya replay (internal manifest `21AF5B80A394C48A006921569BB122235DF9B1ECA8DC4506959D4D7BAC2D07AF`). All 3,601 poses passed; runtime logged `game.trueSpeed` for all 3,601 rows, with force/network output muted and a normal game close. Five native 7680x1440 stills show a center-view HUD, without resolving temporal jitter or physical scanout. The temporary wrapper restored 324 inventoried owner files, original `FE2BABFA...` receipt, all 17 payloads, config and raw preferences before releasing the lease. [Replay evidence](../../../results/hudspeed-kenya-20261013-01). The lasting `0ed0292` install above had no game launch. Next work is ordinary full-session reuse; hold StageStartup/playback source for Nene assessment rather than widening this replay test.

October 13 automated ordinary forward/Stop/Saved qualification: clean source `7b3d094` composed an internal stage from frozen `a9a255c` payloads (manifest `09B793AAD2EBC96EEFB562A43B34B8592A22DE152C76015FA270EA56160E6BC0`), retaining all 16 non-native owner files byte-exact and replacing only the qualified load-muted native. One companion-owned muted diagnostic produced three plateaus, 8.133 grounded forward seconds, 376 qualified rows, and a completed 1,311-sample original source with zero drops/errors. The installed companion exited 0 after exact Stop; Wheelkit 0.6.33 showed Saved, analysis only, while the game was alive. After normal game closure, exact owner files (323 inventoried), receipt `FE2BABFA...`, raw preferences, and saved Native backend/tune were restored; the lease was released. [Pinned evidence and limits](../../../docs/ORDINARY-SAVED-FORWARD-REVIEW.md). No owner-driven Saved case, gameplay replay, physical force or HUD visual acceptance is inferred.

The ordinary Record/Stop implementation began at `fcc513e`; the current `0ed0292` owner install retains the self-contained companion/finalizer and revision-4 record-only receipt capability. Exact-source private package and disposable Native/private installer fixtures passed. Earlier isolated Wheelkit installed-path gate and release-installer compatibility tests are in [STATE](STATE.md) and `E:/Source/dbce-project-mgmt/artifacts/woden-ordinary-stop-20261013/installed-companion-fixtures-03`. Shared Wheelkit Woden dispatch is on main (a0e3c15 or later) but not in the owner's installed 0.6.32 (38f599e); do not promote the installed Woden capability to an owner-facing Wheelkit or live ordinary gameplay claim.
The companion may return Saved while Woden remains open. Shared SessionRunner integration must retain the rig lease and wait for the exact launched game process to exit before closing its run; case finalization alone is not permission to release the lease.

October 13 limited ordinary Stop menu smoke: temporary internal package `f53078c` with qualified load-muted native `088F1089...` was installed under one lease, then the owner's `d8df97b` install and receipt `F645FE98...` were restored byte-exact. Request-bound Stop closed the original raw source; live finalization was incomplete, and the same packaged finalizer refused a separate archive copy for exactly zero driving samples. No second launch followed the supervisor's corrected saved-FFB oracle (`4713914`). The exact game process closed by its normal window path; exit code was not observed. See [result and evidence](../../../../../dbce-project-mgmt/artifacts/woden-ordinary-stop-20261013/combined-qualified-01/result.md). This does not make the temporary package an owner candidate or a saved driving case.

The newer `3c174c9` private package passed a temporary, muted applied-profile forward trial and an archive-copy independent analysis export with 376 eligible rows; the original `d8df97b` owner install and complete file/preferences inventory were restored. The test package is not a lasting owner install, and the synthetic force rows do not qualify normalization or physical feel. [Exact evidence](../../../../../dbce-project-mgmt/artifacts/woden-applied-forward-20261013/README.md). Live Grip6 references now exclude reverse travel (`0aca431`); old recorded references remain exact in replay.

Current installed source also corrects the ordinary `status.txt` replacement under a shared reader, bounds the companion's delete-sharing read, and defaults an omitted request ID to empty. Targeted writer, reader and parameter fixtures passed without a game or device. The remaining owner-facing steps are to update the owner's installed Wheelkit from the main branch and observe one attended driving case finalized Saved. The completed automated diagnostic above does not qualify an owner-driven ordinary case.

# Session handoff — 2026-10-04 (Claude → Codex)

**Installed now:** the `claude/triple-screens` build (this worktree,
`E:\Source\_worktrees\woden-triple`), not 0.2.14. Backup of the previous install:
`E:\Source\_archive\2026-10-04\woden-install-before-triple-dev`; registry backup
`E:\Source\_archive\2026-10-04\woden-registry\before-careful-attempt.reg`. Display left on
"Sim Racing" (three independent monitors).

**Branch:** `claude/triple-screens`, 7 commits on `origin/main` (e3d118e), the same base as
`codex/session-playback`. A dry `git merge-tree codex/session-playback claude/triple-screens`
merges cleanly (only `Plugin.cs` touched by both). Not pushed.

What it adds (details in [TRIPLE](TRIPLE.md)):
- Triple screens under Surround, verified by the owner driving (STD-007/008).
- Camera numpad family layout 8/2 forward/back, 9/3 up/down, 4/6 left/right, 7/1 tilt,
  +/- FOV, 5 reset, with migration of untouched old sets (STD-005); seen migrating the
  owner's bindings on 2026-10-04.
- Camera step sizes as settings, default 0.02 m / 1 deg / 2 deg (STD-006), Advanced → Cameras.
- Forza: Speed from the HUD's `TrueSpeed` (about 3x physics m/s, sent as km/h), RPM on a
  nominal 8000 scale from the 0..1 rev fraction, gear (STD-009). Owner still to confirm
  RPM/gear on the dashboard.
- Dev input commands, heartbeat and `tools/dev/Display-Watchdog.ps1`.

Owner feedback 2026-10-04: triple works; FFB felt (it had been saved Off in config,
predating this work; now On); bumper FOV fine with `MatchGameFov = true`; menu black bars
fixed. Installed config: `[Dev] InputCommandFile = false`, `[Triple] Mode = Auto`,
`MatchGameFov = true`. Saved resolution index 13 (valid on both profiles).

Recording/playback (STD-002) is Codex's. Merge or rebase this branch first so playback is
built on the installed behaviour. Standards status: repo root `TOOLKIT-ADOPTION.md`.

---
# Session handoff — 2026-10-03

Read [STATE](STATE.md) first, then [the complete option inventory and acceptance report](UX-OVERNIGHT-2026-09-16.md). The currently installed receipt is **0.2.14 / e3d118e**, with all nine receipt payload hashes matching on October 3; saved FFB is Off. This `codex/ux-simple-advanced-0.2.8` checkout documents the historical 0.2.12 / bf58042 source and attended recording, not the later installed source. Exact evidence and limits are in STATE. Prior dirty work is preserved separately in `9d28b83` and ignored `artifacts/ux-baseline-20260917-001141`. Coordinate a new desktop lease and the owner's attended FFB choice before any further force test.

0.2.12 prepares the next seated owner drive as a GUID-correlated source recording plus device-free model-3 baseline and immutable tuning trials. The command defaults to force suppression; attended physical FFB remains explicit. It never kills Woden on timeout. Offline reprocessing requires complete/drop-free source data, stable tuning, explicit reset/gate semantics and real driving coverage. Trials revalidate the original case/baseline and write a new observation with a separately hashed candidate config. The two historical captures contain zero driving samples and cannot pass. Read [recorded owner-drive boundaries](RECORDED-PLAYBACK.md). Shared contract source is toolkit `87b47d53121c0b3fc8479e622ae8a79c8ca3fa58`; native/Recording pins are unchanged.

The first seated run used `E:\Source\woden-rally-edge-wheel\tools\Start-RecordedGame.ps1 -CaseId corner-unwind-baseline -AttendedFfb` and produced a complete, reprocessed 116-second driving capture. Preserve its source/case/baseline bytes. The owner said force felt good but very weak and asked Kirby to coordinate data driven consistency across rig projects. The saved 25% peak cap was reached in 43.3% of driving samples; pure offline trials of strength, smoothing and a 45% cap are recorded in STATE. No installed tune changed. Any later run needs a fresh case ID and one-launch request created only when invoked.

The earlier force-disabled 0.2.9 run verified IMGUI F6 opening, Advanced rendering, provisional calibration Cancel and release-to-close, with 437,436 actual stock producer reads. It exited normally; capture completed with 2,888 samples and zero drops/errors, force writes stayed zero, and all owner files were restored byte-exact. The initial F6 also advanced the startup message; source analysis then confirmed direct legacy-keyboard consumers bypassing the existing masks. 0.2.10 adds scoped DailyMessage/TitleScreen guards, early Settings observation, a shared poll per frame and legacy keyboard release aggregation. See [startup diagnosis and live plan](STARTUP-INPUT-ISOLATION.md). Broader stock-screen/held-input isolation and physical behavior remain untested. The working IMGUI fallback does not prove the precise cause of 0.2.8's failed InputSystem-only smoke.

Simple is the default; Advanced Driving contains the optional timer assist. Handbrake axis/button contributions are additive. Binding/Clear/default proposals persist before becoming effective; failed saves offer Retry/Cancel. Calibration cannot save a disconnected candidate. Camera numpad defaults follow the family layout (8/2 forward/back, 9/3 up/down, 4/6 left/right, 7/1 tilt, +/− FOV, 5 reset); untouched earlier default sets migrate on load, custom sets stay. Menu buttons target the panel or existing selected Unity UI handlers; native screen coverage remains untested. Agent recording stays external; active captures have Stop and survive UDP/view edits.

The retained 0.2.11 package passed its offline checks but coordinator review held installation because it could only reproduce the original tune and its comparer summarized structurally shifted streams. 0.2.12 addresses both findings. Source `bf58042fbf6ebea9175d8a85f332dd1ffd203ea6`; ZIP SHA-256 `09088883102cc212b8fd0119b09fe1440efb9e1bb2158b3a60666185df7d279c`; plugin `ac669ea81fd9addce3c97228173390a6af4749d511aec23b68bfec41dbd3bd24`; Core `4eb440930a3da061e6c3e77a150c81626fb0bf9ff4f56146dd173720c0bf643c`. Checks pass 37 suites / 965 assertions, 532 UI assertions, 84 installer checks, actual-controller lifecycle capture and a distinct-strength shared-contract trial. Coordinator review passed; the closed-game install verified all payload and owner hashes. Backup `WodenWheelBackups/before-install-20260929-003548-381c44f1`; evidence `artifacts/deployment-0.2.12-preinstall`. Preserve all prior frozen ZIPs. Source-linked fixtures do not establish native/physical acceptance.

The following paragraphs preserve earlier 0.2.5–0.2.7 evidence in chronological order; current installation/configuration is summarized in STATE.

The owner called 0.2.5 a good starting point but reported input/camera blocked before green and possible lingering cornering force. 0.2.6 allows normal calibrated wheel and camera input during WARMING, preserving native start-line physics/lock. Driving/FFB remain restricted to RACE. The force model/tune remains unchanged pending better evidence. The latest owner log ended normally with 5,666 FFB writes and zero failures. The owner also selected countdown/time-limit assistance, not elapsed stage timing: Setup → Difficulty adds a saved Off-by-default assist at 75% speed (25–100% range). It advances the owned single-player CountDown anchor before native expiry, preserving checkpoint additions and lap/stage clocks. Offline hook/config tests pass; gameplay acceptance remains open.

The latest 0.2.6 drive had zero force writes: native initialization replaced its owned HWND with a foreign foreground window and failed exit-guard installation. 0.2.7 supplies a captured/revalidated owned Unity HWND explicitly, waits for window readiness before any device work and keeps every force gate. The toolkit binary pin remains unchanged; the shared source bug is documented in its knowledge base. The owner's bonnet correction is +0.15 m up / +0.05 m forward relative to the second vehicle fit; this now informs fitted defaults, while the exact saved manual view is preserved. Current saved force strength is 75.208336%, cap 25%, Enabled=true. Deployment details and pending acceptance are in STATE.

Latest 0.2.7 run: owned HWND / R12 initialization / 150 ms watchdog / exit guards all succeeded at 23:32:20.715. PanicStop occurred at 23:32:49.644 before racing, saved FFB Off, and the whole drive stayed Off (zero writes/failures). No wheel Panic stop binding exists; F8 or sidebar Stop FFB are possible, but the log lacks trigger attribution. Current strength 49.583332%, cap 25%, Enabled=false; countdown assist On/50%. No settings were changed. Ask the owner to choose F6 → FFB → On and close settings for the next drive; do not silently clear saved panic Off. See STATE for retained evidence.

Newest run supersedes the Off diagnosis above: owner felt improved FFB after selecting On; 6,723 writes / zero failures. Saved strength 49.583332%, cap 25%, smoothing 35 ms, damping 0.05. Native commands show 38.89% of driving time at the cap, longest 4.049 s. Managed-only step experiment shows 200–217 ms capped-force release. These support saturation plus conditioning as plausible contributors to lingering force; no synchronized input/slip capture exists. Read FFB-FEEL-ANALYSIS.md. No tune was changed.

## Owner decisions

FFB is one saved On/Off setting, default On and 50% strength for new settings, with the original output gain restored in 0.2.5. F8/Stop saves Off until On is selected. There is no session-start requirement. Preserve exact GUID, stock ownership, all driving/contact/camera gates, watchdog, ramp and peak cap. Unattended physical output remains prohibited.

Recording belongs to the agent workflow: tools/Start-RecordedGame.ps1 requests one launch and starts Steam; normal exit finalizes the capture. It defaults to diagnostic force suppression and never changes the saved FFB preference. Use -AttendedFfb only for an explicitly requested attended run. -PrepareOnly stages the expiring request without launching. No menu recording controls.

E-Brake accepts independent Button and Axis contributions on the existing Controls pages; the greater contribution wins in 0.2.8. The new analog adaptation scales native rear braking/grip loss; native throttle cut remains binary. Full pull and stock buttons retain stock behavior. Device input bars/calculation tests are not a physical response test.

## Paths and implementation

Game root: D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge. Supported Steam build 21802346 / Unity 6000.3.6f1 / metadata 39. Read DEVELOPMENT-BUILD for full hash and usage.

- Installed plugin/receipt: BepInEx/plugins/WodenRallyEdgeWheel.
- Config/bindings: BepInEx/config/dbce.wodenrallyedgewheel.cfg and wheel-bindings.json.
- Evidence: BepInEx/LogOutput.log, ErrorLog.log, and %LOCALAPPDATA%/DbceWheel/ffb.log.
- Recordings: BepInEx/WodenRecordings. Requests: BepInEx/config/woden-record-next-launch.json, consumed once.
- Backups: WodenWheelBackups for the packaged 0.2.8 installer; historical backups remain under BepInEx/WodenBackups. Both are outside plugin scanning.
- Build/package: classic solution, both executable test harnesses, tools/Package.ps1. Packaged Install.bat updates with backups; Uninstall.bat removes only receipt-owned unchanged payload. Repository Install-Dev.ps1 remains initial-only.
- FFB: ForceController + ToolkitForceDevice; actual controller is linked into the offline tests with fake collaborators. Do not replace toolkit native output.
- Controls: scoped Controls.FixedUpdate action-table lease; boxed action values need indexer writeback. AnalogHandbrake scopes only selected-player native calls and restores game tuning.
- Timing: TimingDiagnostics records frame/poll/control/car/sampler/force durations plus gear/shifting.
- WheelContact uses GC-rooted IL2CPP value storage. NEVER call the generated GetGroundHit(out WheelHit) wrapper.

The ignored artifacts contain native disassembly, offsets, offline audit output and historical deployment helpers. The native handbrake takes a RELEASED boolean, applies 2000 brake torque to rear wheels above 30 rpm, writes configured grip loss, and uses a binary 0.3 acceleration multiplier. The adaptation scales native results, restores the tuning field, and does not change the engine multiplier or game assists. Do not commit proprietary disassembly or generated references.

The toolkit's local UX-1/setup/checklist were updated for persistent FFB and agent-managed captures. That checkout has unrelated uncommitted work: preserve it, do not commit it wholesale, and do not rebuild its moving Recording source into this consumer. Toolkit v0.12.0/native v0.5.0 and the unpublished recording pin remain unchanged.

Next: test pre-green revs/steering/camera and 75% countdown speed, checkpoint bonuses, pause/restart/expiry and persistence; verify handbrake partial/full/release, FFB sign/delivery and pause/focus recovery during an attended drive; use a recording when ready to assess remaining hitches and contact validity. Do not launch a physical force test unattended.
