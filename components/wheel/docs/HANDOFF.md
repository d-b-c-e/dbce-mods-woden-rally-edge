# October 29, 01:00-01:30: first curved runtime evidence; presenter replaced; installed 7125a55

Muted Kenya replay (Run-StageSession, outputs muted, cfg CurveRadiusMm = 1500 for the run only, restored byte-exact) on
941b78a: all 3601 poses passed; the curved path built its targets but the injected OnPostRender message never fired, so the
presenter never drew and the mod latched flat after 120 source frames (flat views rendered normally, captures kept); the
texture size bucket also flapped between two values every frame (229 rebuilds in 7 s, a speed-varying chase FOV).
Fix (a66d15b, 7125a55, 208974c): the presenter draws through a CommandBuffer at AfterEverything (explicit full-window
viewport, 0..1 ortho, three grid meshes with warp UVs, one material per target), presentation evidence from the static
Camera.onPostRender for the presenter camera (camera completion only; captures show visible success), textures sized by a
session high-water scale, our callback removed on Reset. Installed closed-game as 0.2.16+7125a55 (gates: Package.ps1
103 suites / UI 7033 / DeviceHub 3897, Test-PrivateShimInstall 38; rollback before-install-20261029-011736-4328e4fa).
The follow-up replays on 7125a55 (01:17, 02:35) did not launch the game: Steam console_log shows LaunchApp waiting for a user response to KickingOtherSession (the owner account playing Star of Providence on another computer); the harness answered nothing, as required, and the later launch stayed behind that pending action;
the owner cleared it at 07:57. 08:38 muted replay on 7125a55: Curved triples on (2560x1440 x3, then the high-water 2880x1620 x3,
no flapping), presenter camera rendered every frame (Camera.onPostRender), no curve-off for the whole stage, 3601 poses passed,
normal exit, cfg restored byte-exact. Harness frames (hub artifacts run-01/visual) show the three warped panels drawn with
correct orientation and the HUD unwarped on the centre third: first visual curved-output evidence. Open: seated acceptance
(seams on the glass, F7 feel). Evidence:
dbce-project-mgmt/artifacts/woden-curved-replay-dome-20261029 (run-01-gl-presenter, run-02-not-launched).

# October 29: clean-install lifecycle on a pristine copy (dome; file level, no launch)

Owner request (relayed by lulu): test the ordinary player install from a game copy with no mod files. Owner-derived, stock-shaped copy of the
installed game (E:/Source/temp/woden-clean/game: 744 files, 4.499 GB, no BepInEx/dotnet/doorstop/winhttp/backups,
GameAssembly F422894D...; not depot-verified against a Steam manifest), the exact public WodenRallyEdge-0.2.16.zip (7EA01BD3...) run through its own Manage-Install.ps1
(Install.bat without the pause; the installer downloaded BepInEx be.788 against its pinned hash). PASS end to end: fresh
install (22 owned files, rev 5), a plugin-shaped seeded config, update to the owner private candidate 941b78a (23 files,
curvedTriples), rollback to the public package, uninstall with user data kept (shared loader retained, config kept),
reinstall; receipts and payloads match the manifests and every pristine game file is byte-identical after every step.
Evidence: dbce-project-mgmt/artifacts/woden-clean-install-dome-20261029 (result.json, README). Receipt capability added
this night: `curvedTriples` (b297f1d), installed on the real rig as 0.2.16+941b78a.

Runtime gates still open (need the real install through guna with muted outputs; a copied Steam exe relaunches the installed
game): first launch writes dbce.wodenrallyedgewheel.cfg and wheel-bindings.json; F6 opens; [Input] Backend choice; forces
(saved tune, never unattended); triples on the span; curved panels when CurveRadiusMm > 0 (presenter bindings, seams, HUD
order); FovKey F7 session A/B. Fixture or upgrade success is not a clean-gameplay pass.

# October 29: curved panels and FovKey session A/B (dome; reviewed by lulu; installed closed-game)

Branch dome/woden-curvature (worktree E:/Source/_work/dbce-mods-woden-rally-edge-curvature). Lulu reviewed a4a6b71 and the
fixes (d9b33fc, 47bab9b: resource key with output size, target ownership, presenter order, source-frame presentation
evidence with bounded fail-to-flat, shown views for the status, texture size bucket so a speed-varying FOV never rebuilds
targets). Installed closed-game under the shared lease: 0.2.16+47bab9b (private shim package, receipt rev 5, 23 owned
files hash-matched, configs and shim byte-exact, Package.ps1 gates + Test-PrivateShimInstall 38 checks), rollback
`WodenWheelBackups/before-install-20261028-235615-6df7452d`. The receipt now also declares `curvedTriples: true` (b297f1d) so
Wheelkit shows and writes `[Triple] CurveRadiusMm` only to this build. No launch, device or force run: GPU output, seams,
HUD order and the resolved engine bindings still need a rig check. Owner request relayed through lulu: curvature for the
Unity triples and a session-only measured/game A/B key.

- `[Triple] CurveRadiusMm` (0 = flat, unchanged output). `CurvedRig` builds the three views through the vendored shared
  toolkit core (`src/WodenRallyEdge.Plugin/Toolkit/Triple`, toolkit b4b7120: eye distance is to the middle of the center
  glass, a curved chord sits one sagitta closer, hinges at the chord ends). Radius 0 reproduces `TripleGeometry.Compute`
  to 1e-9 (new suite in WodenRallyEdge.Tests), so flat rigs render exactly as before.
- Curved output (`TripleCurve`): source and side cameras render full-rect into three textures sized for their widened
  source surfaces (targetTexture through a newly resolved engine binding), and an injected presenter camera at depth
  source+5 redraws the 64x36 warp grids over the window with the copy shader; the HUD camera at +10 stays unwarped.
  GL PushMatrix/LoadOrtho/PopMatrix wrappers are stripped from the interop, so they are resolved like the camera setters.
  Any failure (binding, shader, render target, pass, invalid radius) latches flat output with the original flat frusta
  and the reason in Status/F6 until the rig changes; Stop releases everything.
- `[Triple] FovKey` (F7; Input System name; None disables): this session only, never saved. Measured shows the
  physical eye for every view, chase included; game shows the lens (saved mounted pose or game FOV) for every view; a
  saved Physical/Adjustable choice in F6 ends the session state. F6 Cameras shows the active session text.
- Not done here: GPU output, seams, HUD order and the stripped-binding resolution at runtime need a rig check; the
  manual TripleFovTests harness (not a Verify-Source gate) does not model the new fields; Wheelkit catalog row for
  Woden CurveRadiusMm is on the Wheelkit side. FFB, physics, inputs and chase policy untouched.

# October 27: 0.2.16 released; side branches retired

Public release [v0.2.16](https://github.com/d-b-c-e/dbce-mods-woden-rally-edge/releases/tag/v0.2.16)
(not a pre-release) from main `9cb382e`: `WodenRallyEdge-0.2.16.zip`, SHA-256
`7EA01BD3F32CCAA7FEFE38A1F6B47B0EAF85E92A66A29DB487EE8CDF1F34EDEC`, public download re-hashed and matched.
Gates: Verify-Source 102 suites / 52,345 assertions, 7,033 UI, zero warnings; 163 PS 5.1 exact-package
installer checks. Owner install is the same source as the private-shim variant (`-ArtifactLabel owner`,
shim `9B8A036F…` kept, Backend Shim and all settings kept; backup `WodenWheelBackups/before-install-20261027-225704-c09bd943`).

Merged for the release: TripleView FOV preservation (d6488dd + fixture), side cameras share the source
post-process volume trigger (seam grading), Help-report token redaction, guided-setup Ready predicate, F6 rig
geometry line, held-joystick close blocker, all-gates test. New: a 3% pedal rest floor on both input routes (the
"no drive" cause: Shim had no pedal deadzone and Woden accelerates only at exactly zero brake) and Forza
lap/position/lap and race times from CarClock (seconds proven on two finished Kenya stages; DistanceTraveled 0).
Menu prompts: every A/B/Start prompt is already relabelled; remaining glyphs are X/Y-only actions with no wheel
binding (listed as a known limit). All 106 side branches (local and origin) were deleted after a verified bundle at
`E:\Source\_archive\2026-10-27\woden-side-branches.bundle` (includes the preserved cadence adapter work).

Open, owner at the rig: first launch shows the grip v8 migration (Strength 74.75 -> 50, same feel); FOV on
bumper/chase/photo with triples on and off; seams; H-pattern N/R on dashboards; photo-mode Pause. Earlier
sections below are historical.

# October 22: Partial native prompt source handoff

Continuation lale: source implementation is still incomplete. Additional
metadata/direct-call evidence is recorded in WHEEL-PROMPTS; unresolved virtual
calls and scene references prevent a supported prompt target from being named.
Source coordination must supply that association or scope a separate bounded
runtime survey before another implementation offer. Preserve the existing
menu/layout/input-switching runtime checklist and all unqualified gates.

No GLY implementation or new feature fixture is ready. Existing source passed
Verify-Source; the successful log hash, prerequisite failure and interrupted
unleased attempt are preserved in [WHEEL-PROMPTS](WHEEL-PROMPTS.md). The corrected
run released its exact lease. Native selector/menu dispatch evidence does not
establish which Text/Image draws the label, so no guessed mutation was added.

Next bounded task: inspect existing static scene/widget assets to identify that
object and its update consumer; if insufficient, explicitly scope a runtime
survey separately. Then implement the real hook and source-linked fixtures for
active Native/Shim identity, switching, fallback and exact restoration. Preserve
the merged 15bc663 safety baseline, 2d1c8cf/7b6808d camera commits and 11a4534
clutch contract. Candidate evidence is the ordinary commit carrying this note,
reported by full hash to Fleet; source support is incomplete, with no install,
publication, rendered or owner acceptance. The precise future runtime checklist
is in the linked trace. Do not repeat unrelated runtime suites.

# October 16: Candidate forward source handoff

Deadzone productcf655d45 is qualified offline: Plugin1D9741CC/retained8c84Core7ADA,
55 focused UI/28 calibration/6500 required UI+95 managed shim checks; exact actual
FDF29060/independent66C9D375 closure. Current child changes test tools only. Schema11
pins private04EB80F8 (EC81 with only BrakeDZ03) for one4s throttle-only window,
normal expiry and actual motion/brake-release evidence. Existing reader/native
client/launch/restore stay in use; no P/Z/getter baseline, physics or force retune.
Focused protocol mode is authored/unrun; build new Probe only, retain product.
Fresh package/runtime handoff and owner notice remain pending. See controls README.

# October 16: Pedal deadzone implementation pending qualification

Source adds explicit Native existing-binding deadzone editing and visible3%
new-pedal capture defaults using the existing normalization. OwnerEC81 remains
unchanged. R9 actual A/B data supports a nonzero-brake diagnosis; its original
interval FAIL is retained. Focused calibration and --deadzone-only UI checks
are authored/unrun; build/UAT need fresh coordinator handoffs. Suggest Brake3%
explicitly during candidate/UAT notice, preserving Rest/End/Centre/Invert. No
physics/native-DeadZone/force retune. See [the scoped handoff](2026-10-16-pedal-deadzone.md).

**Current owner install - October 15, 17:08 CT:** private UX/status candidate `0.2.16+d45e2ce2b92fe0335c43e28ae57b93dfc101a76f`, revision-5 receipt `A93D0E5661E4826F7F5CBDF0C2410769AF81F5B7E72BCE41CE228B606777315E`. Exact ZIP `147DA6551672FF660AFEDC496735014C025B1C2FACD3E3F023BBB19EA3BD91FE` / manifest `BAF90F90EF886D7C56BEF99B43E425BEA666AB55FA94E3EB52C4463A1D5C640E`. Closed-game update verified all23 payloads twice; only Plugin533D217E and Core0F99A847 changed, other21 remain exact. All five configs including A9D5, Native backend/native bindings, force tune,305 protected nonowned/save/loader files,10 directory entries and raw typed preferences stayed byte-exact. Automatic rollback of old422F/23payloads/configs remains at `WodenWheelBackups/before-install-20261015-170817-df15496a`. The original functionalPASS retained its lease because its own console host was still alive; authorized post-host cleanup separately released that exact lease, and independent22:10:15Z readback confirmed both original identities absent, scope0, lease absent and small installed pins exact. Evidence: hub `artifacts/woden-fov-ux-status-20261015/owner-ux-update-01` (original2FABCDE6, cleanupDE3865D3, independentC3665E3F), closure161523. No game/helper Apply/native/device/force run or public push accompanied installation. **Genuine focused F6/button/FOV validation and owner preferred FOV/weak-force acceptance remain pending.** Keep installed Physical config and existing tune; no new input helper or idle game launch. Older installed-state headers below are historical.

**Current owner install - October 15, 07:37 CT:** private `0.2.16+fe5828d68e17dce02e8641c2dee5825cabd38b20`, revision-5 receipt `422F8E52578FD2B81B404398D0DE068FADB4D76228B9BC71A5FCFD4BC62F46CE`. Exact ZIP `0DDECAC4A0AD9AFA45E3CCE306B8FBB45AE4F1F637D39809C767ADBB54B9789A` / manifest `995B1D1E55ED8F18F2FA7AA6F2EE672876A844ECDBD8001D205D998EEFD773CC`; packaging source `2fb68a2`, compiled source `fe5828d` (UI source qualified at `92794b8`). Closed-game update passed all 23 payloads twice, exact automatic backup of the prior 857F receipt/23 payloads/five configs, and byte-exact preservation of all five configs, native bindings, force tune, raw typed preferences and 305 protected nonowned/save/loader files plus 10 directory entries. Only Plugin/Core/case finalizer and the already-qualified request-ID runner DF140 changed; other 19 payloads including normal F836 force native and 4AF shim remain unchanged. The one bff4ce86 setup helper is package-only, never installed or applied to owner. No game/native/device/force test or public push accompanied installation. Evidence: hub `artifacts/woden-ui-0.2.16-20261015`, owner result `4AA681E0E793D4C7EF0A72BD9770DE9EB54F77E3A041C560EC073D98FDA45108`; backup `WodenWheelBackups/before-install-20261015-073749-8603e4cc`. Dine owns the separate volume-trigger/seam source candidate; it is not installed and seam improvement remains unproven. Earlier checkpoints below are historical.

# Current handoff — 2026-10-14

**Installed now (2026-10-14, 15:21 CT, romi for sana 106683):** exact private save-guard/gearbox candidate
`0.2.14+24d7203e684e45b1a69ba6a6db3ba2b1d0b28179` (ZIP `4FFB64F2…`, manifest `242843BA…`), revision-5 receipt
`D50B71A24BDD1ACD7654026DF3FC35BC8013B1096A4736408118D032CE3AA50D` with `nativeGearboxPreference=true`.
- Installed closed-game through the packaged `Manage-Install.ps1` under the shared lease. All 23 owned payloads match,
  with shim `9B8A036F…` and the production `WheelFfb.dll` `F8361A08…` (not the load-muted test native).
- Backup `WodenWheelBackups/before-install-20261014-152117-cd7626c5` holds the `f252deb` receipt `3041965C…`, its 23
  payloads and the config.
- Unchanged: config `20C0BC9A…` (Native default, Gearbox at start Keep, tune), 305 non-owned files, raw Unity
  preferences `A9BB495D…`.
- Wheelkit 0.6.42 Core accepts controls and playback. Its gearbox rule offered nothing before the install and now gives
  H-pattern → Manual and Automatic → Automatic.
- Displays were recorded, not gated, because the layout was changing during the slot (sana 106683): Surround
  7680x1440 before and after. This install makes no span or render claim.
- No launch, Shim opt-in, device open or force.
- Evidence: [install result](../../../../../dbce-project-mgmt/artifacts/woden-private-owner-install-24d7203-20261014/README.md).
- Main `7393748` differs from `24d7203` only in the repo-root runner and docs, which are not packaged.

What remains is attended: [READY-TO-TEST](../../../docs/READY-TO-TEST.md) A (Native, force feel, HUD), B (Shim opt-in,
and whether input keeps responding once force starts) and C (gearbox at start).

**Native gearbox at start (2026-10-14, now installed):**
- One ordinary load-muted launch set Manual over the owner's saved Automatic, with readback; local owner files were
  restored exactly.
- That run's MapScreen save was uploaded to Steam Cloud (UploadOK 06:29:57). We did no remote restore.
- Run 03 (08:10) used the save-guard candidate from `24d7203`:
  - The central Save was skipped, Manual was set and read back, and the local save and backups stayed byte-identical.
  - Steam re-uploaded the unchanged local original on its own (ChangeNumber 74), so the cloud record now matches it.
  - The runner exited 1 because its check counts any remotecache change as a save.
- See [the qualification note](../../../docs/2026-10-14-native-gearbox-qualification.md) for the hashes, the cloud log
  and the private archive path.

**Ready to test (2026-10-14):** the owner-attended checklist is [READY-TO-TEST](../../../docs/READY-TO-TEST.md). The software and package side is complete for the installed 24d7203 private package; what remains is attended.

**Previous install (2026-10-13, 19:17 CT):** exact optional private-shim package `0.2.14+f252debc54159d101b344abf0f5631b503b64c16`, revision-5 receipt SHA-256 `3041965CC31751B0F4F04A248932E31CB549A4F48208A63129C1A8C0CACCC516`. All 23 owned payloads and the pinned shim match the manifest; backup `WodenWheelBackups/before-install-20261013-191730-fa5b2c2f` retains the prior 7c receipt/config and all 22 payloads. The saved Native default and tune, 305 nonowned files, raw Unity preferences, and four displays passed readback. Wheelkit 0.6.34 recognizes ordinary/playback/native controls capabilities. Woden remained closed; no Shim opt-in, physical-device open, force or game test. [Private install evidence](../../../../../dbce-project-mgmt/artifacts/woden-private-owner-install-20261013/README.md). Ordinary Shim FFB-open/shared-reader coexistence remains unqualified.

**Earlier install (2026-10-13, 18:23 CT):** exact production-native `0.2.14+7c1e2870c47e6100e666ad6a4ecd0d6a3f23824e`, revision-5 receipt SHA-256 `66EC5A30D348B7F6086F56A39219B7196E8EDD4876716100A1606BF2C9155239`. All 22 receipt-owned files match package manifest `0F296EA0...` and installed bytes. Packaged `Manage-Install.ps1` completed closed-game under the shared rig lease; backup `WodenWheelBackups/before-install-20261013-182323-2d7e7447` contains the exact former revision-4 receipt, prior shim, and saved config. The old receipt-owned `dbce_shim.dll` was intentionally retired, and six runner/capability files were added. A wrapper inventory count initially misclassified that removal as non-owned; corrected read-only verification passed 302/302 non-owned files, saved config, raw preferences, four displays, and shipped Wheelkit 0.6.34 ordinary/playback/controls capabilities. [Exact reconciliation](../../../../../dbce-project-mgmt/artifacts/woden-owner-install-7c-20261013/README.md). Woden stayed closed; no physical force or input was sent. Native production does not claim shared-shim input.

The row selector changes window placement, while the prior triple-camera implementation and its qualification carry forward unchanged. The current bounds observer's `tripleRenderedQualified=false` is its own limited measurement; this row fix needs no new optical/scanout gate. Sampled row images do not resolve temporal jitter or physical feel. Wheelkit's revision-5 public release-installer support is in source; the private package was installed through Woden's own installer and no public release followed.

Wheelkit 0.6.34 is published and running on the owner's PC. Its revision-5 Woden
session contract, generic receipt discovery and native settings/controls support
the qualified `7c1e287` private package; the exact read-only package comparison
and separate public-installer limitation are in the
[hub checkpoint](../../../../../dbce-project-mgmt/artifacts/wheelkit-0.6.34-20261013/README.md).
The mod's own packaged installer is the qualified private delivery route.

Prior owner install (now superseded): `0.2.14+0ed0292888f29d2152e3a0a77c39e23e43b9d72c`, revision-4 receipt SHA-256 `CC7983451ACF140AC742A648D15253B98D3BFF0993815C38AB1D17D8810304A2`, exact source `0ed0292888f29d2152e3a0a77c39e23e43b9d72c`. The production-native private ZIP SHA-256 was `A97B8E8CBD5B17BCC42D07E16106872A7714F78B615AE4AED2EA85F0A9459DBB`, with 17 receipt-owned payloads matching manifest `10770DE7355F820440FA391303A8EA8DD1C38133B3EED664195B80A186EB63CE`. It was installed with Woden closed under the shared lease; 306 non-owned files in the full inventory, saved config `20C0BC9A...`, and raw Unity preferences `BE7DC331...` remained byte-exact. Saved Backend was Native with the prior tune. Production native `WheelFfb.dll` was `F8361A08BBE08EAAB936B37800D308CF1FC7EDAB34E2839DB16A447FE34178C5`. Backup: `D:/Program Files (x86)/Steam/steamapps/common/Super Woden Rally Edge/WodenWheelBackups/before-install-20261013-152711-fb8bc483`. See [STATE](STATE.md) and [historical install readback](../../../results/hudspeed-owner-install-20261013-01/install-result.json). The previous `a9a255c` receipt `FE2BABFA...` is preserved in that backup.

No game launch or force/device output occurred during installation. Physical rendered scanout and ordinary Shim FFB-open/shared-reader coexistence are still unqualified. The ordinary `Start-RecordedGame.ps1` path captures signals from launch for offline reprocessing; Wheelkit's registered Woden path records a muted stage trajectory. These have different replay claims. The source-only Record/Stop gap review is in the [hub handoff](../../../../../dbce-project-mgmt/artifacts/woden-private-apply-20261013/ordinary-record-stop-gap.md). The sealed Kenya reference is unchanged. All dated handoffs below describe historical checkouts and installs.

Before the lasting install, the same managed HUD-speed payloads ran in one temporary load-muted Kenya replay (internal manifest `21AF5B80A394C48A006921569BB122235DF9B1ECA8DC4506959D4D7BAC2D07AF`). All 3,601 poses passed; runtime logged `game.trueSpeed` for all 3,601 rows, with force/network output muted and a normal game close. Five native 7680x1440 stills show a center-view HUD, without resolving temporal jitter or physical scanout. The temporary wrapper restored 324 inventoried owner files, original `FE2BABFA...` receipt, all 17 payloads, config and raw preferences before releasing the lease. [Replay evidence](../../../results/hudspeed-kenya-20261013-01). The lasting `0ed0292` install above had no game launch. Next work is ordinary full-session reuse; hold StageStartup/playback source for Nene assessment rather than widening this replay test.

October 13 automated ordinary forward/Stop/Saved qualification: clean source `7b3d094` composed an internal stage from frozen `a9a255c` payloads (manifest `09B793AAD2EBC96EEFB562A43B34B8592A22DE152C76015FA270EA56160E6BC0`), retaining all 16 non-native owner files byte-exact and replacing only the qualified load-muted native. One companion-owned muted diagnostic produced three plateaus, 8.133 grounded forward seconds, 376 qualified rows, and a completed 1,311-sample original source with zero drops/errors. The installed companion exited 0 after exact Stop; Wheelkit 0.6.33 showed Saved, analysis only, while the game was alive. After normal game closure, exact owner files (323 inventoried), receipt `FE2BABFA...`, raw preferences, and saved Native backend/tune were restored; the lease was released. [Pinned evidence and limits](../../../docs/ORDINARY-SAVED-FORWARD-REVIEW.md). No owner-driven Saved case, gameplay replay, physical force or HUD visual acceptance is inferred.

The ordinary Record/Stop implementation began at `fcc513e`; the current `0ed0292` owner install retains the self-contained companion/finalizer and revision-4 record-only receipt capability. Exact-source private package and disposable Native/private installer fixtures passed. Earlier isolated Wheelkit installed-path gate and release-installer compatibility tests are in [STATE](STATE.md) and `E:/Source/dbce-project-mgmt/artifacts/woden-ordinary-stop-20261013/installed-companion-fixtures-03`. Shared Wheelkit Woden dispatch is on main (a0e3c15 or later) but not in the owner's installed 0.6.32 (38f599e); do not promote the installed Woden capability to an owner-facing Wheelkit or live ordinary gameplay claim.
The companion may return Saved while Woden remains open. Shared SessionRunner integration must retain the rig lease and wait for the exact launched game process to exit before closing its run; case finalization alone is not permission to release the lease.

October 13 limited ordinary Stop menu smoke: temporary internal package `f53078c` with qualified load-muted native `088F1089...` was installed under one lease, then the owner's `d8df97b` install and receipt `F645FE98...` were restored byte-exact. Request-bound Stop closed the original raw source; live finalization was incomplete, and the same packaged finalizer refused a separate archive copy for exactly zero driving samples. No second launch followed the supervisor's corrected saved-FFB oracle (`4713914`). The exact game process closed by its normal window path; exit code was not observed. See [result and evidence](../../../../../dbce-project-mgmt/artifacts/woden-ordinary-stop-20261013/combined-qualified-01/result.md). This does not make the temporary package an owner candidate or a saved driving case.

The newer `3c174c9` private package passed a temporary, muted applied-profile forward trial and an archive-copy independent analysis export with 376 eligible rows; the original `d8df97b` owner install and complete file/preferences inventory were restored. The test package is not a lasting owner install, and the synthetic force rows do not qualify normalization or physical feel. [Exact evidence](../../../../../dbce-project-mgmt/artifacts/woden-applied-forward-20261013/README.md). Live Grip6 references now exclude reverse travel (`0aca431`); old recorded references remain exact in replay.

Current installed source also corrects the ordinary `status.txt` replacement under a shared reader, bounds the companion's delete-sharing read, and defaults an omitted request ID to empty. Targeted writer, reader and parameter fixtures passed without a game or device. The remaining owner-facing steps are to update the owner's installed Wheelkit from the main branch and observe one attended driving case finalized Saved. The completed automated diagnostic above does not qualify an owner-driven ordinary case.

# Current playback handoff — 2026-10-13

Clean combined source `7c1e287` passed 85 suites / 51,353 assertions and disposable installer checks, then one temporary load-muted installed-runner cold replay: 749 poses, numeric game exit 0, exact owner restoration. The row selector placed the full mapped client across the three racing panels for 171/188 samples, including 102 pre-stage and 37 in-stage, with zero diagnostic intersections. Two in-stage screenshots show gameplay across the racing row and changing scene imagery in all thirds. This sampled visual result is not continuous scanout or independent angle-correct projection proof; `tripleRenderedQualified=false` stays. The prior revision-4 owner package remains installed, and lasting delivery awaits Wheelkit 0.6.34 capability support. [Changed-build adjudication](2026-10-13-row-cold-replay.md). The frozen `0e21fcc` functional replay settled on the right panel alone; [its separate evidence](2026-10-13-portable-cold-replay.md) remains valid. The older dated handoffs below describe historical checkouts.

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
