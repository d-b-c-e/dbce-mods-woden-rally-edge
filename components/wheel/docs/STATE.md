# State — 2026-09-30

## 0.2.12 — installed; first attended owner recording completed

The installed build adds a bounded one-command owner capture and `signal-reprocess` adapter for the shared recorded-playback v1 contract. Each invoked request receives a safe case ID and GUID-correlated directory. Runtime metadata binds the request, supported game hash, plugin/runtime identity, attended/no-force mode, effective force config and capture profile. Samples record model validity/reason, reset epochs and output gates in order. After normal exit the command validates the source, requires at least 50 driving/model samples over one second, reruns actual `ForceSignal`, writes an exact case manifest and device-free baseline, and reports paths/hashes. It refuses dropped/incomplete/limited/contended/missing-channel/idle-only captures. This is not deterministic Unity replay or physical acceptance.

0.2.12 adds the missing iteration path identified in review: it verifies the immutable source, manifest and baseline, reruns the original model, then hashes a separate candidate force config into a new observation bound to the original `caseSha256`. It never requires candidate preview values to match the recording and never overwrites source/case/baseline/config/profile. Comparison now rejects invalid identifiers, negative ticks, missing footers, count differences and shifted timelines before numeric magnitude reporting. Actual-controller fixtures cover active, pause, camera, discontinuity, recovery and diagnostic no-force gates.

Offline evidence is complete: zero-warning build, **37 suites / 965 assertions**, **532 UI assertions**, **84 Windows PowerShell 5.1 installer checks**, the v2 no-launch request fixture, a 100-request baseline plus 20%-strength trial accepted by the shared Python validator/comparer, and exact preservation of all original artifact hashes. The shared comparison reports 77 expected changed magnitudes, maximum delta 0.1484078541, with matching case/timeline.

The retained 0.2.11 candidate is immutable at source `fea6c996d89ed09a125e71524d4335a994078afc`, ZIP SHA-256 `7600587155f9d45e16dc7c402127b3531017864213a991fff47c412f23d48e29`; coordinator review held its installation for the tuning-trial and structural-validation gaps above.

- 0.2.12 source `bf58042fbf6ebea9175d8a85f332dd1ffd203ea6`; product version `0.2.12+bf58042fbf6ebea9175d8a85f332dd1ffd203ea6`.
- Delivery ZIP `dist/WodenRallyEdgeWheel-0.2.12-dev.zip`, SHA-256 `09088883102cc212b8fd0119b09fe1440efb9e1bb2158b3a60666185df7d279c`; stage `dist/stage-d1fbafdd2cd14693999c394f82bb46ed`. All 15 allowlisted files / 14 manifest entries verify.
- Plugin SHA-256 `ac669ea81fd9addce3c97228173390a6af4749d511aec23b68bfec41dbd3bd24`; Core `4eb440930a3da061e6c3e77a150c81626fb0bf9ff4f56146dd173720c0bf643c`.
- Coordinator review cleared the exact package. Installed closed-game at **2026-09-29T05:35:48Z** using transactional `Manage-Install.ps1`; backup `WodenWheelBackups/before-install-20260929-003548-381c44f1`. Receipt version `0.2.12+bf58042fbf6ebea9175d8a85f332dd1ffd203ea6`, installer revision 3. All nine deployed payloads match, all four owner-setting hashes are byte-identical, no capture request exists and Woden remained closed. Private receipt/evidence: `artifacts/deployment-0.2.12-preinstall`.

**First attended 0.2.12 recording completed on 2026-09-30 local.** The owner resumed desktop testing from the wheel; `Start-RecordedGame.ps1 -CaseId corner-unwind-baseline -AttendedFfb` launched one session with the saved FFB tune. The game exited normally. Private source/case/baseline are under the game's `BepInEx/WodenRecordings/request-ea1e228595484dcfab283ed440182b82`; source SHA-256 `785d8d679c9dc654ea860212074b920b59944d0c6315ad40a2e9f26bd83d23b8`, baseline SHA-256 `719cf89c266dff71ea626a2cd6257b01fd9225627ab961d2eab3ebcbf3760232`. The completed source has 11,961 samples, 266 markers, 6,981 driving samples over 116.34 seconds, and zero drops/errors/limits. Reprocessing succeeded against the exact case and reported 8,251 model samples. The log was retained privately at `artifacts/owner-0.2.12-first-drive/LogOutput.log` (SHA-256 `86bca30b888439a3f226cd97de7c6b5374867eb213096e03ddf7397bc6bd5d12`).

Live logs show F6 opening, DailyMessage guard execution, countdown wheel route and assist hook execution, player/contact discovery, a Bonnet camera cycle, and 6,922 FFB writes/attempts with zero failures. The recording reports one connection attempt, zero delivery failures, and saved tuning of 49.583332% strength, 25% cap, 35 ms smoothing and 0.05 damping. The pure model/sent telemetry reached the 25% cap on 3,020 of 6,981 driving samples (43.3%); this is evidence of software saturation, not measured wheel torque. Woden is closed, the one-launch request is consumed, and all nine installed payloads still match the receipt. **Owner report:** force felt good but very weak; increase overall strength substantially. Because the saved 25% peak already clips 43.3% of driving samples, increasing only model strength would leave the maximum output unchanged and increase clipping. A stronger physical trial needs a reviewed peak-cap change as well as gain calibration. Physical sign/load/lifecycle, bonnet framing, analog handbrake and timer behavior still need focused checks. Do not retune from the aggregate cap statistic alone.

Three separate device-free trials preserved the original source, case, config, profile and baseline. At 40% strength with 35 ms smoothing, 2,459 of 8,251 model observations were capped (29.8%), versus baseline 3,020 (36.6%); maximum pointwise signal difference was 0.0478583. At unchanged 49.583332% strength with 20 ms smoothing, 3,005 observations were capped (36.4%); maximum difference was 0.0318849. At unchanged strength with a 45% peak cap, 1,540 observations were capped (18.7%); mean absolute normalized signal rose from 0.1386 to 0.1922 and maximum pointwise difference was 0.2. All retained the exact case/timeline and wrote separate hashed observations beside the private recording. These are software-signal comparisons, not physical FFB trials; review cross-project output scale and stage any installed tune for short attended acceptance.

## 0.2.10 — installed; startup/title live check pending

A bounded source audit confirmed direct legacy-keyboard routes in DailyMessage and TitleScreen bypass 0.2.9's stock producer/EventSystem suppression. Message dismissal is input-triggered; title attract is separately timed. The successor adds specific consumer guards and early Settings intent, shares one existing reader poll per frame, and includes legacy input in the close release check even with InputSystem present. No force, physics, device infrastructure or owner settings change. Zero-warning build, 35/942 regressions and **532 UI assertions** pass. Independent review closed without a concrete blocker on the nine-file manifest SHA-256 `81a13d919817a941bebf9f6a37b47e7057c6e7a98bb1736622d12f8edc016dae`. Native method identities and the legacy GetKeyDown wrapper were checked; actual Harmony execution remains a live check. Read [the diagnosis, implementation and live test plan](STARTUP-INPUT-ISOLATION.md).

Installed with Woden closed at **2026-09-19T21:35:30.6297779Z**, runtime source **`25797b3f3cacb4a2ddaf671a7165af097abf4633`**, installer source **`e5da2d5b1c48cb6e9bf3adbece9ea9d57bd54858`**. Installer revision 3 resolves its package folder in the script body because Windows PowerShell 5.1 advanced-script parameter binding left the earlier default empty. The first 0.2.10 install attempt failed before writes; 0.2.9 payloads/settings were verified unchanged. The corrected installer passed independent review and **84 checks**, including actual Install.bat → PowerShell 5.1 with omitted PackageRoot, spaced package/game paths and a different working directory. The real deployment also omitted PackageRoot.

- Delivery ZIP: `dist/WodenRallyEdgeWheel-0.2.10-installer-r3-e5da2d5.zip`, SHA-256 `06976b0f9d06caded3faace123fe76342e4f3aa971db4cbee25a278129dfdee8`; stage `dist/stage-installer3-086c962ffc364bbfa2f9b0e7f9db8666`. All 15 allowlisted files / 14 manifest entries verified in stage and ZIP.
- Plugin SHA-256 `bea8ff1fda8ad2cd7410515b9396a8b1234c4c62e282a9137371d300eb063aec`; Core `bbd569559dd0fb5c9e56f6aea604a5d15924257be2fcafab4a0e4d973d3eeae5`. All nine installed payloads match the frozen runtime, version `0.2.10+25797b3f3cacb4a2ddaf671a7165af097abf4633`.
- All four owner config/binding files remain byte-identical: FFB On / 49.583332%, cap 25%, smoothing 35 ms, damping 0.05; exact manual bonnet view and countdown assist On / 50% preserved.
- Backup `<game>/WodenWheelBackups/before-install-20260919-163528-d7fe2d2b`; receipt `<game>/BepInEx/WodenWheel-install.json`, installerRevision 3. Private package/receipt/config verification: `artifacts/deployment-0.2.10`.
- The original 0.2.10 ZIP, SHA-256 `3d0ddf86469325239394faca77e94e43bbf148cac423289a771816efe20bf09c`, remains immutable with the old installer; use the r3 delivery above. Both 0.2.8 ZIPs and the 0.2.9 ZIP also remain unchanged.

**0.2.10 live check: NOT RUN.** The user paused desktop testing; no game was launched for this deployment. Resume startup/title checks only after the user resumes desktop work and a serialized lease is granted. The earlier 0.2.9 observations below are historical, limited UI evidence. Physical controls/force/camera/timer acceptance and the lingering-force investigation remain separate.

## Historical 0.2.9 — focused menu check completed

0.2.9 adds stock input ownership and a shared release gate for closing/capture, plus bounded F6 diagnostics and an IMGUI F6 fallback. Its native hooks observe configured `GamePadSystem.ReadInputs` results before neutralizing the existing `Game_Pad`, raw float and boxed action arrays. Keyboard, pointer, stock and bound menu/Settings controls must remain neutral for 100 ms with fresh stock reads before handoff. Unknown/stale/failed reads retain ownership; Keep settings open cancels a stalled close. Native input is not scanned or changed during normal driving. Source-linked fixtures cover aggregation, retained references, configured actions, held controls, stale/skipped reads, fallback keyboard, duplicate F6 delivery and shared-primary disconnect cleanup. Build: zero warnings/errors; **506 UI assertions**, **35 suites / 942 regression assertions**. The bounded live menu results below establish limited UI acceptance, not physical input/force acceptance.

The separately frozen installer-r2 successor is `dist/WodenRallyEdgeWheel-0.2.8-installer-r2-9b4d75e.zip`, SHA-256 `1992551438aa7e5d46c66500f53d3189f5552bbcb815ec169b5c04ca976dcbb5`. Installer source `9b4d75e37568e674274aeee0fc8719db164dd22f`; runtime stays `df0e4dc`. **81 PowerShell 5.1 assertions** cover safe rollback, game-start recovery-required and external replacement before/after a write. All 15 files / 14 manifest entries verified. The original ZIP below remains unchanged. The 0.2.9 installer drops the hard-coded version from backup folder names; recovery policy is unchanged.

0.2.9 was installed closed-game at **2026-09-19T19:59:28.0743626Z**, source **`141bbab73740feadc8d35e038a1bebc7f821622d`**. Independent source review closed the capture readiness finding at that exact commit. Full package validation reran 35/942 regression and 506 UI assertions, and the Windows PowerShell 5.1 installer passed **81 checks** on a disposable package copy (`artifacts/managed-installer-test-14e4fc19401c4169a21f81d1cb7d8fb1`). Initial installer and recorded-launch PrepareOnly fixtures also passed. No push.

- ZIP `dist/WodenRallyEdgeWheel-0.2.9-dev.zip`, SHA-256 `961fc545f3ac5e9821518154f0a1580d99e57644815fdd92306d5dec50367438`; stage `dist/stage-426f78c093294fc2baf4a7c029a3f8bf`. Exactly 15 files / 14 manifest hashes verified in stage and ZIP.
- Plugin SHA-256 `2f4fd3e0a054b7e082a571aa7e14974a7d1ec5bdcd7aa7d9abf9b50c5ff1dbca`; Core `26c6d7c59ba79daf867e0c32a3b2978d35c1ee9f04046bce108bef69e745ef34`.
- All nine installed payload hashes match. All four owner configuration/binding files remain byte-identical to the fresh deployment snapshot.
- Backup `<game>/WodenWheelBackups/before-install-20260919-145924-b22eb10f`; receipt `<game>/BepInEx/WodenWheel-install.json`; private copies and pre-smoke settings at `artifacts/deployment-0.2.9`.
- Installation launched no game. The subsequent coordinated diagnostic run used one-launch force suppression; physical driving/force acceptance remains separate.

### Live 0.2.9 menu check — 2026-09-19

The authorized diagnostic run started at 20:01:50Z, with request `03190a06-1fb6-49ec-a688-513fa030f29d` and `physical FFB suppressed=True`. F6 opened the actual 3840×2160 Unity panel through the logged **IMGUI F6** route. Simple Setup and Advanced navigation rendered legibly. Bind Steering opened provisional calibration; Cancel returned to Controls with the prior assignment displayed. F6 close showed the release prompt, then closed normally to the stock title screen. Native stock producer reads reached **437,436**. No plugin hotkey/ownership exception appeared; all force writes/failures remained zero. The saved wheel device was shown as disconnected, so no physical control behavior was exercised.

Normal Alt-F4 exit completed with `outputWorkerStopped=True`. Recording `20260919-200203-435-cede36c987224d4d89641b47acac24ae.jsonl` completed with **2,888 samples / 3 markers**, no drops/errors, elapsed **297.6805 s**. All four owner configuration/binding files were restored byte-for-byte from the pre-run snapshot, all nine installed payload hashes were reverified, and the consumed request/logs were archived privately at `artifacts/deployment-0.2.9/live-smoke`. The game exited before the slot deadline; final restoration verification/release completed at 20:07:39Z, 15 seconds after the coordinator's stated deadline. No further launch is planned without a new lease.

Remaining UI checks: Tab/Enter and held controller overlap, stock menu dispatch across screens, EventSystem restoration with active selection, restart persistence and 720p/scaled native rendering. The initial F6 also advanced the message screen to the title, and stock attract mode ran behind the open panel; full opening-edge and stock-screen isolation is **not established**. This run proves the IMGUI fallback works, not the precise cause of the earlier InputSystem-only failure. Physical wheel/handbrake/camera/timer/FFB acceptance remains pending; force tuning is unchanged.

Private `live-smoke/verification.json` records final configuration/payload hashes and the recording hash/footer. `after-panel-close.jpg` and `screenshot-receipt.json` retain the actual 3840×2160 final title image. Earlier Simple/Advanced/calibration screenshots remain inline tool evidence only; no separate image files were retained.

## Historical 0.2.8 — installed and smoke-tested before 0.2.9

The owner-authorized cross-product UX work adds persistent Simple/Advanced, provisional calibration, additive handbrake axis/button input, strict Steering-following or explicit FFB selection, camera rebinding/repeats, and telemetry edits that preserve active capture. Optional countdown assist moves to Advanced → Driving without changing saved values or the timer hook. Wheel Confirm/Back/directions now navigate the panel and compatible Unity stock menus. See the [complete inventory and evidence](UX-OVERNIGHT-2026-09-16.md).

Binding saves, Clear and default resets persist before replacing effective assignments. Failures retain the old binding and exact proposal with Retry/Cancel; disconnected calibration candidates cannot be saved. Camera default batches preflight conflicts. The Windows player installer now supports install/update/uninstall with pinned prerequisites, backup, rollback, owned-file hashes and preserved settings/recordings/other mods.

Offline checks: zero-warning build; **35 regression suites / 942 assertions**, **459 source-linked UI assertions**, and **59 standalone installer checks under Windows PowerShell 5.1**. Approximate 720p/4K draw-command fixtures are separate from actual Unity rendering. Native menu coverage and physical controls/FFB/camera acceptance remain pending.

Branch `codex/ux-simple-advanced-0.2.8`; baseline checkpoint `9d28b83` preserves prior 0.2.3–0.2.7 work separately. Verified source/package/deployment identities follow. The owner explicitly authorized completion, documentation, build, commit and deployment, followed by a serialized force-disabled menu smoke. Original force model, 50% default, HWND/exit guards, owner bonnet correction and timer eligibility remain unchanged.

## Verified 0.2.8 deployment — 2026-09-19

Installed with Woden closed at **2026-09-19T19:15:05.3124346Z**. Runtime source commit **`df0e4dc323cf39266ebc553d637638cc416d650c`**, branch `codex/ux-simple-advanced-0.2.8`; previous development work is separately preserved in `9d28b83`. No push performed.

- ZIP: `dist/WodenRallyEdgeWheel-0.2.8-dev.zip`; SHA-256 `914657af4dfeb080782724853d2936fdee486388b58078f7a1620af3c8784ac1`.
- Final stage: `dist/stage-a20cfa038b0346ed91296bc33aa0d779`.
- Installed plugin SHA-256 `1f5e178258b60cea3383f3692c35e08c0f37b9b93d1857366528130b3b24f961`; Core `7091a100a9c81153fe75227f7fe0b564407739c54eaff5bc3fa316e466f90c63`.
- All **9 installed payload hashes** match; all **4 existing configuration/binding files** are byte-identical before/after installation. FFB On at 49.583332%, cap 25%, smoothing 35 ms and damping 0.05; manual bonnet height 0.7367809 / forward 0.9380049; countdown assist On/50% preserved.
- Backup: `D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge\WodenWheelBackups\before-install-0.2.8-20260919-141503-904bcc76`.
- Receipt: `<game>/BepInEx/WodenWheel-install.json`; private copy and pre-install config hashes at `artifacts/deployment-0.2.8`.
- Final package verified exactly 15 allowlisted files and all 14 manifest entries. Schema: 215 definitions.
- Final PowerShell 5.1 player installer fixture: `artifacts/managed-installer-test-caff41af997847ddb8725f4e89a7baa3`, **59 checks passed**. Repository initial-only installer fixture: `artifacts/installer-test-b23f351d1fc04d55af8aecac1f2341ef`, passed layout/refusal/preservation.

Deployment itself launched no game. A later authorized diagnostic launch ran from 19:25:33Z to normal exit at 19:31:23Z, with one-launch physical FFB suppression, zero force writes/failures and no driving input. F6 injections did not open the panel; final screen was stock attract-mode DEMO PLAY. The lifecycle update ran and focus was observed, but the log cannot distinguish a missing InputSystem edge from a silent hotkey exception. This remains unresolved, not UI acceptance. Recording completed with 3,206 samples and zero dropped/errors. All four owner configs were restored byte-exact, all nine installed payloads reverified, and the consumed launch request archived privately at `artifacts/deployment-0.2.8/live-smoke`. Desktop lease released; no further launch without a new grant. Actual UI/physical acceptance remains separate.

## Latest 0.2.7 drive — force felt; lingering-force analysis

The owner reports force is much better after selecting On. The run (PID 50964) logs **9,315 local ticks, 7,160 wheel ticks, 6,723 force writes and zero failures**, then normal shutdown. Saved FFB On, strength 49.583332%, cap 25%, smoothing 35 ms and damping 0.05. This confirms felt delivery after the window repair; formal sign/load/lifecycle acceptance remains separate.

[Force-feel analysis](FFB-FEEL-ANALYSIS.md): reconstructed change-only native commands spend **38.89%** of the 113.365 s driving-output window at the ±25% cap (longest hold **4.049 s**). This supports clipping as a contributor to force failing to lighten. A managed-only experiment with the actual shipped model shows **200–217 ms** to release capped force after an instantaneous synthetic slip-to-zero step; instant reversal takes about **317 ms** to reach 90% opposite force. Those timings are synthetic, not measured driver latency. The source also remains based on front sideways slip, which may persist during an unwind.

No synchronized gameplay recording exists, so saturation, signal persistence and damping cannot yet be aligned to the owner's unwind. No tune/settings/code/deployment/game launch or physical test was performed. Evidence remains private under ignored `artifacts/owner-0.2.7-force-feel` and `artifacts/force-release-analysis`. Next useful work: a short attended capture of turns/unwinding/reversals to separate source behavior from shaping, then a targeted A/B test of clipping and release speed.

## Previous run — saved Off

## Latest 0.2.7 drive — initialization repaired; saved FFB Off

The owner again reported zero force. The 23:32–23:34 local run loaded 0.2.7. It captured owned Unity HWND **0x001820E4**, opened the exact R12, initialized successfully, set the 150 ms watchdog, and **installed exit guards successfully at 23:32:20.715**. It prepared at zero with no initialization failures. This is runtime confirmation of the window-selection repair, not physical force acceptance.

At **23:32:49.644**, before the first local car sample, native `PanicStop()` ran, then output closed and readers reopened. The game log changes to `FFB off` while F6 is open and remains Off throughout driving: **2,762 last-reported local/applied ticks, zero force writes, zero failed calls**. Normal shutdown follows. Saved `[ForceFeedback] Enabled = false`, strength **49.583332%**, cap 25%, same R12 GUID. Countdown assist is now saved On / 50% and logs hook execution.

No Panic stop wheel button is bound. The implemented direct panic triggers are keyboard F8 and the F6 sidebar Stop FFB button; the logs do not distinguish them. Do not attribute the press to the owner or claim a particular trigger without evidence. The source confirms these triggers intentionally save Off until explicit On; a normal pause/panel gate never saves Off. No automatic re-enable or configuration change was performed. The next step is F6 → FFB → On, then close settings and drive. That choice persists for normal launches.

Private evidence retained in ignored `artifacts/owner-0.2.7-saved-off`. No new build/deployment/game launch or physical test. Installed version remains 0.2.7; camera settings and all saved preferences remain untouched.

## Prior 0.2.7 diagnosis and deployment

## 0.2.7 — confirmed FFB startup failure and corrected bonnet default

The owner felt no FFB on the 0.2.6 drive. The log shows **7,538 local ticks, 7,411 applied wheel ticks, zero FFB writes and one initialization failure**, then normal shutdown. Saved FFB remains On, exact R12 GUID, strength **75.208336%**, peak cap 25%. No recording was requested. Countdown input and the optional 75% timer hook both logged execution; this does not prove physical input or clock-rate acceptance. Retained private evidence: ignored `artifacts/owner-0.2.6-no-ffb`.

Native evidence at 23:14:11: InitDirectInput(hwnd=0) first found owned window 0x00992384, then replaced it with foreground window 0x002610E4. It initialized the R12 successfully but InstallExitGuards failed with Win32 error 5. Output was stopped and closed; the consumer latched the failure instead of reconnecting repeatedly. The toolkit v0.12.0 tag/native 0.5 source confirms the defect: InitDirectInput assigns ResolveGameWindow then overwrites it with ResolveHwnd, whose zero-handle path uses the foreground window. This was a window/lifecycle failure, not insufficient force gain or the pre-race driving gate.

Fix: capture a visible owned UnityWndClass window before closing readers or enumerating hardware, revalidate its ownership, and pass the explicit handle through the existing toolkit API. If no eligible foreground game window exists, wait without closing readers, enumerating or latching a fault. Preserve guard failures through cleanup. Exit guards, exact GUID, constant-force watchdog, pause/focus/camera/contact gates, ramp and peak cap remain mandatory. The native/device implementation and dependency pins are unchanged. The shared defect and follow-up are recorded in the toolkit's `knowledge/games/woden-rally-edge.md`.

Bonnet evidence: second vehicle fitted height 0.58678085 / forward 0.8880049; saved manual height **0.7367809** / forward **0.9380049**, side 0.000008761883, pitch 8°, FOV 70°. The requested new fitted default adds **0.15 m up and 0.05 m forward** to each car's body-relative fit. Its no-mesh fallback uses the observed height/forward (centered side). Existing manual views, including the owner's exact saved view, stay unchanged. Reset/Fit to car uses the corrected default. Bumper defaults are untouched; cross-car framing still needs visual acceptance.

Validation: release build zero warnings/errors; **32 suites / 906 assertions**. Tests exercise the actual toolkit adapter with fake API calls, including a foreground change after capture, strict HWND/GUID forwarding, mandatory guard/150 ms watchdog/initial zero, invalid/lost window refusal and failure cleanup. The actual controller waits without reader churn or a latched fault. Camera tests reproduce the owner's correction from recorded bounds and preserve saved manual views through migration/reload. Schema remains **215 definitions**. No game launch or physical output test.

Installed with the game closed at **2026-09-17T04:27:43.6817986Z** (September 16 local). All **9 payload hashes** matched and every existing configuration/binding hash was preserved, including the exact manual bonnet view, strength 75.208336%, cap 25%, saved FFB On and countdown assist On/75%. Backup: `D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge\BepInEx\WodenBackups\before-0.2.7-20260916-232742`. Package: `dist/WodenRallyEdgeWheel-0.2.7-dev.zip`, SHA-256 `36cbebd26823665cf0cb04e1d73e1f82e0999c6de9f076b17fef22b31b99481d`. Stage: `dist/stage-528d35b6cf3c4079bf9ed1a11a63de36`; reviewed updater: ignored `artifacts/update-0.2.7.ps1`. Initial installer fixture passed (`artifacts/installer-test-2cee3c628052426ea326cd92ade2fa66`). No game launched or physical forces tested.

Next attended check: confirm normal force returns and startup log records the owned Unity HWND, successful exit guards and nonzero write count. Saved strength is 75.208336%, not the 50% new-config default. Check the current manual bonnet view, then Reset/Fit to car on both vehicles to assess the corrected default. Capture corner/unwind behavior later if the lingering-force concern remains.

## Historical 0.2.6 update

## 0.2.6 — countdown controls, camera availability and optional time-limit assist

The owner tested 0.2.5, called it a good starting point, and reported camera changes and engine revs unavailable before green. They clarified that wheel controls should remain available at the start line, including steering and throttle. They also reported a subjective feeling that cornering force lasts too long. No force model/tune change is made on that observation alone; the next attended recording should compare slip/alignment, shaped output, steering and exit/unwind timing.

The 0.2.5 log finishes normally after **7,078 local ticks, 5,700 applied wheel ticks and 5,666 FFB writes with zero failures**. It records a fitted bonnet pose from body bounds and active bonnet driving. This proves code execution, not final hood placement or force lifecycle acceptance. The owner then closed the game.

Diagnosis: the mod's Runtime.Driving guard required RACE and unlocked status for all wheel/camera input. This suppressed throttle and steering as well as camera bindings in WARMING. The native Controls.FixedUpdate dispatches Camera without a RACE check; Car_Cam.ChangeCamera checks mode/latch. Native MainCar.FixedUpdate's WARMING branch includes throttle-dependent RPM calculation and separate start-line wheel braking. The native game therefore has a rev path; lack of wheel revs cannot be dismissed as game design.

Change: separate wheel/camera availability from driving/FFB permission. WARMING accepts the normal calibrated action-table override, including throttle, brake, steering and bound buttons, and mounted camera cycling/tuning. This input change does not modify car status, lock flags, physics or driving assists. Racing retains its existing lock guard; pause, settings, focus, replay, respawn, photo, non-selected player, finish and destroyed exclusions remain. Camera transition/foreign writer checks remain. FFB still requires a driving sample in RACE; a pre-race player camera cannot enable force.

The owner also requested an easier experience and explicitly chose the countdown/time limit rather than elapsed stage time. Setup → Difficulty now provides a saved optional countdown assist, default Off with 75% speed, bounded to 25–100%. At 75%, a 60-second countdown allows about 80 driving seconds. The native CountDown.Update subtracts Time.time minus TimerControl before checking expiry; the hook advances only that timer anchor before the subtraction, restoring an unconsumed anchor on early return/exception. Scope requires the selected racing player, one PlayerCarList entry and the same GameMaster as the timer. Paused, inactive, infinite, replay and unrelated timers are untouched. Native elapsed lap/stage clocks, vehicle physics and checkpoint additions are unchanged. No timeout is refunded.

Added `wheelInput.preRaceTicks`, a one-time countdown input route log, and three `assist.countdown.*` channels. Schema now has **215 definitions**. Build: zero warnings/errors; **28 suites / 871 assertions**, including the actual boxed action-table lease, interrupted-write restoration, countdown/race gate checks, actual ForceController zero-output checks until green, actual countdown hook against a native-behavior fixture (expiry, ownership, bonuses, cleanup), and real settings persistence/bounds. No new runtime or physical acceptance is claimed.

Installed with the game closed at **2026-09-17T04:10:34.7032035Z** (September 16 local). All **9 payload hashes** matched; all pre-existing configuration and binding hashes were preserved, including strength 50% / peak cap 25%. The new assist remains Off until enabled in F6 → Setup → Difficulty. Backup: `D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge\BepInEx\WodenBackups\before-0.2.6-20260916-231033`. Package: `dist/WodenRallyEdgeWheel-0.2.6-dev.zip`, SHA-256 `59b2a0b31ce31e79913ac0ad9d5f6c2c02abcd14315c06630f0846fe77dc4018`. Stage: `dist/stage-04fce5f458bd46e98c129772696db2f7`; reviewed updater: ignored `artifacts/update-0.2.6.ps1`. Initial installer fixture passed layout, repeat-install refusal and preservation (`artifacts/installer-test-83f7b275032047e49300c5acebfb6652`). No game launched or physical forces tested.

Next attended check: at the start line, rev and steer, change to bonnet/bumper and try tuning keys; confirm the car remains held until green. Confirm steering/input continues at green and FFB ramps only during racing. Enable Setup → Difficulty → Countdown assist at 75% for a time-limited single-player run; compare elapsed driving time to the countdown, cross a checkpoint, pause/resume and verify eventual timeout/restart and saved On/Off. Record an attended corner/unwind session when the owner is ready to investigate lingering force.

## Historical 0.2.5 update

## 0.2.5 — original FFB gain restored

The owner corrected the previous feedback: the wheelbase strength was set too high, so the mod's 25% reduction overcompensates. The requested correction removes model 2's final 0.75 multiplier and restores the original 0.2.3 output curve at the same strength setting. The default remains 50%; model version 3 identifies this restoration in telemetry. The configured peak cap once again directly limits delivered output (25% by default).

Camera/E-Brake changes and all FFB lifecycle/gates remain. Deployment preserves all existing configuration and bindings. No physical force test or game launch is part of this update.

Release build: zero warnings/errors. **22 suites / 765 assertions passed**, including an exact comparison to the original force waveform at 50% through capped peaks, reversal and low-speed fade. Dependency pins and the initial installer fixture passed; schema remains 211 definitions.

Installed with the game closed at **2026-09-15T04:34:58.9618412Z**. All nine payload hashes matched; every existing configuration/binding file was preserved, including strength 50. Backup: `D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge\BepInEx\WodenBackups\before-0.2.5-20260914-233458`. Package: `dist/WodenRallyEdgeWheel-0.2.5-dev.zip`, SHA-256 `af7b2742279839bf4ac868d318e392fa4f4fa1205578b4ea332979eef96e11a1`. Stage: `dist/stage-768627f8b03d4c24b6f10841b1df0c23`; reviewed update helper: ignored `artifacts/update-0.2.5.ps1`. No game launched or physical output tested.

## Historical 0.2.4 update

## 0.2.4 — owner feedback and current update

The owner drove 0.2.3 and reported it was better: force was felt, but 50% was too strong; the bonnet showed no hood. They requested a 25% output reduction with a 50% default, E-Brake under Axes/Buttons rather than its own tab, and customizable/rebindable camera adjustment.

The retained 0.2.3 game log reaches **4,797 local ticks, 3,707 applied input ticks and 3,683 FFB writes, with zero failed output calls**, then normal shutdown. Bonnet/bumper cycling appears in the log. There is one 180.4 ms frame gap around initial level entry, reporting one FFB open. This supports restored force delivery and removes the prior zero-write symptom; it does not establish every stutter resolved or the cause of any shift-associated hitch. Saved owner strength was 50.208336%; the handbrake axis was calibrated and selected. No bounded gameplay recording was created.

0.2.4 changes:

- New-config strength 50%. Force model 2 multiplies the complete conditioned waveform by 0.75, including capped peaks. Existing output lifecycle/gates remain. With the 25% configured cap, the resulting maximum is 18.75% of device range.
- E-Brake is the fourth Axes row and appears under Buttons. Binding either selects that mode without deleting the other saved binding. The stored axis remains usable; the standalone tab is removed.
- Bonnet defaults fit to body-local mesh bounds near the windscreen with downward pitch and 70° FOV; fallback position is (0, 1.1, 0.1), pitch 8°. Mounted views use a 0.03 m near clip. Custom offsets survive migration; untouched old defaults switch to fitted positioning. New framing is a candidate pending a drive.
- Cameras → Position provides side/height/forward/pitch/FOV, fitted/manual selection and reset. Cameras → Bindings accepts keys or wheel buttons for cycle, rear view and all tuning. Defaults match the sibling mods' numpad keys. Tuning saves automatically and only affects an active mounted view.
- Stock lens and pose restore on release. A foreign writer's changed lens values are preserved. Woden strips the managed near-clip setter; CameraNative resolves its named UnityPlayer binding using the native-self/float ABI confirmed against the local far-clip wrapper. Body bounds are read once per car, not each physics tick.

Validation: release build has zero warnings/errors; **22 suites / 765 assertions** cover exact 0.75 waveform scaling through caps/reversals/fades, real config migration/persistence, fitted/tuned camera math and key binding persistence alongside the prior FFB/input/recording regressions. This is offline evidence; 0.2.4 UI/hood framing/lens restoration/physical feel are not yet accepted.

Installed with Woden closed at **2026-09-15 04:25:41 UTC** (September 14, 23:25 local). All nine payload hashes verified. Backup: `BepInEx/WodenBackups/before-0.2.4-20260914-232540`, including the 0.2.3 plugin, configuration, game logs and native FFB log. Saved strength changed from 50.208336 to exactly 50 as requested; every other config/binding file remained unchanged. Untouched bonnet defaults migrate on the next plugin start. No game launched.

Package: `dist/WodenRallyEdgeWheel-0.2.4-dev.zip`, SHA-256 `b88655c9164295af5b00513d38d751be56369ebb0a82eb4b8965316ca2cd892e`. Stage: `dist/stage-f55242b3b1094c03a639e218e859858e`. Reviewed update helper: ignored `artifacts/update-0.2.4.ps1`; installed `update-receipt.json` records verification and the strength change. Initial installer and one-launch recording fixtures passed without launching a game (`artifacts/installer-test-67e978944daf4b5fb8f0956e3fcd27f5`). Schema remains 211 definitions, not a live coverage count.

Next attended check: drive at 50%, confirm feel and stop/recovery; check the E-Brake axis already bound, then partial/full/released response. Cycle to Bonnet and confirm visible hood. Adjust via numpad or Cameras → Position, rebind controls under Cameras → Bindings, and verify stock/rear/bumper handback. The agent can prepare a recording once the owner is ready. No unattended physical force test.

## Historical 0.2.3 implementation and installation

**0.2.3 installed for the next attended test.** The owner's 0.2.2 drive reported no felt FFB, hard stutters (possibly near gear shifts), and inability to bind an axis handbrake. The owner requested one saved FFB On/Off choice (default On), no per-session start, and agent-managed recordings prepared before launch.

## New runtime evidence and diagnosis

The latest 0.2.2 log reached 1,616 car ticks and 1,295 applied input ticks. It logged the correct action names and a normal shutdown. Effective control direction/ranges and camera behavior still need explicit acceptance. FFB delivery counters remained **writes=0, failures=0** despite the saved Enabled=true and exact R12 GUID.

The native log confirms repeated successful FFB opens followed immediately by shutdown/re-enumeration. At 21:35:18 local, reader closure began at .097, FFB initialized at .386, input reopening completed at .521, and StopEffect followed at .525; another enumeration completed at .695. The code tore down the device whenever a contact/time/camera gate failed. Initialization itself delayed samples/camera observations, triggering another teardown before ordinary force writes. This establishes a reconnect stall and explains the zero-write behavior; no recording establishes that every perceived hitch was this cause or that shifting caused it.

## 0.2.3 changes

- FFB defaults On for new configs and honors saved On/Off on normal launches. There is no per-session arm action. F8/Stop saves Off; explicit On resumes. Existing owner tune/GUID are preserved.
- Transient gates zero/stop without releasing or enumerating devices. Zero-only preparation runs in Update, not the physics sampler. Focus recovery can reacquire through a toolkit zero write when the shared input reader is unavailable. Init/write failures latch instead of repeatedly reconnecting.
- Controls → Handbrake offers Button/Axis with calibrated rest/full pull, inversion/deadzone and a live bar. Existing button bindings remain stored. Axis amount scales native rear brake torque and grip loss; full pull uses stock behavior. Native power cut remains binary and the game's rpm threshold is preserved. This game-side adaptation has not been driven.
- Recording start/stop controls removed from F6. Start-RecordedGame.ps1 prepares an expiring one-launch request and launches through Steam. Normal exit finalizes a bounded capture. Unattended diagnostic launches suppress force without changing saved FFB On; -AttendedFfb is for explicitly requested attended testing.
- Added frame/poll/control/car/sampler/force timing, FFB connection counters and status markers, raw/applied handbrake. The signal model and force safety gates remain intact.

Release build: zero warnings/errors; **19 suites / 441 assertions**. Tests include the actual ForceController compiled with fake game/device collaborators, transient recovery without re-enumeration, persistent panic Off, failed-init/write latching, zero-only reader recovery, handbrake mapping/persistence and expiring one-shot diagnostic requests. No physical output or live 0.2.3 acceptance is claimed.

Toolkit UX-1, setup guidance and checklist were updated locally for the owner's saved On/Off and agent-recording workflow. Other pre-existing toolkit work and pinned binaries were not changed.


Installed with Woden closed at **2026-09-15 03:20 UTC** (September 14 local).
All nine payload hashes matched; every pre-existing config hash was preserved.
Backup: `BepInEx/WodenBackups/before-0.2.3-20260914-222021`, including previous
plugin/config, game logs and native-ffb.log. ZIP SHA-256:
`599fef80add0bb8313f225b68b3dc842020f9ffaf6711aa7fed806b0a09cd6d5`.
Installer and recording-launch fixtures passed without starting a game. Schema:
211 definitions, not live coverage. No game was launched after this update.
## Next attended check

After installation, bind/calibrate the handbrake axis and check released/partial/full response. Use normal saved FFB On at the preserved 10% strength / 25% cap; F8 saves Off if needed. Test pause/focus/camera recovery and compare stutters. Once ready for diagnostics, the agent can launch a bounded recording; the owner need not start/stop it in the menu. Capture normal driving, shifts, partial/full handbrake, pause and normal exit. Inspect FFB writes/connection counts, timing and actual contact values. Physical sign/feel, signal scales, SimHub interpretation and camera acceptance remain open.

The older state below is retained as history. Its session-arming and menu-recording instructions are superseded by the owner request and 0.2.3 above.

## Historical state before the 0.2.3 changes

Handoff audit: [HANDOFF.md](HANDOFF.md) now records source/installation paths,
retained evidence and the next test. Read-only checks on 2026-09-14 matched all
nine installed 0.2.2 payload files and the existing package hash, and verified
dependency pins. No new game run or physical test occurred; code test results
below remain from 2026-09-13. Implementation baseline is `f3b5c0a` on `main`.

**0.2.2 development build installed for the next attended test.** 0.2.1's F6 UI, loader boot, device reading, stock Logitech ownership transfer and sustained car/contact sampling ran. The owner confirmed level entry after the WheelHit crash fix, then reported no effective throttle/brake/steering/camera response. 0.2.2 corrects the input boundary and builds camera cycling/handoff. No effective driving, new camera behavior or physical FFB is claimed verified yet.

## 0.2.2 changes and next test

Installed 2026-09-13 05:18 UTC with Woden closed. All nine payload files verified;
all pre-existing config hashes unchanged. Backup:
`BepInEx/WodenBackups/before-0.2.2-20260913-001820`.
Package SHA-256 `cfc0218c1a0d8a9f3fb9b518c546b077b11df26f8389c1d116fc5f7a1dea1554`.
The game was left closed; recording preference and owner bindings were preserved.

Native Controls.FixedUpdate reads the selected Game_Pad.PadActions, copies axes into MainCar's raw fields and dispatches buttons. The previous override of Controls fields around MainCar.FixedUpdate was too late. 0.2.2 temporarily writes the same selected action table around Controls.FixedUpdate, then restores it. Actions is a boxed value array: modified values must be assigned back through its indexer. Inputs now have applied-tick counters and the first action-route log identifies the expected axes.

Bonnet and bumper extend the native camera cycle without saving illegal stock preset indices. Look behind is a held camera action, not the game's unrelated L3 slot. Replay/photo/finish/inactive camera state, native transitions, changed native presets and external transform writers release the mounted camera and gate FFB. This is source/policy-tested behavior pending a real drive.

F6 now follows UX-1: Setup, Controls, FFB, Cameras, Telemetry, Help. Stop FFB remains visible; its latched status survives normal update gating. Existing config and binding keys remain compatible. UI layout changes have not yet been visually accepted. See [UX adoption](UX-ADOPTION.md).

Release build: zero warnings/errors; **14 suites / 405 assertions**, including native-cycle extension/skip/handoff policy checks. Schema: **199 definitions**, not a live coverage count. Added camera mode/view/ownership/transition/preset and applied-input tick channels. Raw native disassembly and owner data remain in ignored artifacts.

Next attended test: use Setup to check device bars, then start a short recording in Telemetry with FFB unstarted. Enter the same level; test all axes, Change camera and Look behind. Check `wheelTicks` in the log and camera/FFB state in the capture. Stop the recording before exiting. Only then perform a low-gain attended FFB test. The owner is away; no unattended force test or control injection substitutes for that check.

## Earlier 0.2.0/0.2.1 implementation and checks

This section preserves earlier evidence. The current 0.2.2 totals above are
14 suites / 405 assertions and 199 definitions; the older counts below do not
describe the current package.

- Private repository; supported Steam build 21802346 / Unity 6000.3.6f1 / metadata 39, guarded by exact GameAssembly SHA-256.
- Pinned BepInEx #788; 71 game-specific interop assemblies generated offline, and runtime generation completed on first actual launch. Empty UnityBaseLibrariesSource remains required.
- F6 panel: wheel/pedal calibration, inversion/deadzone, physical button capture, manual reconnect/raw diagnostics, FFB settings, bonnet offsets, telemetry ports/rate and live capture. F8 panic disarms output.
- Owner requested UI consistency: 0.2.1 replaces `ON Allow FFB` and similar text with labeled Off/On choices. No GameObject UI framework or input backend switch was introduced.
- Provisional front load/slip alignment estimate and calibrated steering damping. Shared toolkit shaping/output, exact FFB GUID, session arming, pause/panel/focus/stale/source gates, 150 ms hold watchdog, exit guards, zero-before-shutdown and ramp-in.
- 193 schema definitions, including reserved/unavailable channels. FFB preview/delivery/tuning and raw/calibrated physical inputs are retained. This count is not live channel coverage.
- Release build passes with warnings as errors. **13 executable suites / 396 assertions passed**, covering capture/persistence, reversed pedals, force symmetry/inversion/gain/cap/ramp/fade/invalid sources, motion, UDP, finite values, stale idle and complete recording. Late-start captures now use a capture-local time origin.
- Initial installer verified in a disposable fixture, then deployed to the actual game. 0.2.1 was deployed with the game closed, per-file payload hash checks and unchanged config hashes.

## Actual runtime evidence and crash

First actual launch loaded 0.2.0 and injected Lifecycle successfully. F6 opened and rendered at 1920x1080, showed R12 X/Z/Rz axis bindings and live centered/released readings; button page navigation and saved-setting changes were observed. No UI draw errors were logged. All four optional named UI engine bindings resolved, including stripped style/cursor setters.

The seeded local axes came from the owner's installed Art of Sim Rally settings: R12 GUID `d71b8350-61b7-11f1-8001-444553540000`, steering X with centre 32919, throttle Z and brake Rz with 0..65535 endpoints. These were imported values, not newly measured endpoints. Subsequent owner edits are preserved.

The log reported `Mod owns wheel route; 1 stock reader(s) suspended`. FFB remained disarmed during agent UI testing. The owner then tested the UI and changed settings; those preferences were preserved during the fix.

At 23:34 local, entering a level discovered player index 0 and crashed in `UnityEngine.WheelCollider.GetGroundHit`, followed by `Il2CppException.BuildMessage` access violation. The BE #788 wrapper generates WheelHit as a boxed value class containing a Collider reference, but offers an IntPtr-sized stack slot for its by-reference native result. Native code overwrites that slot. 0.2.1 `WheelContact.Read` allocates an actual IL2CPP WheelHit value, passes its unboxed storage directly to `il2cpp_runtime_invoke`, and retains its GC root. It does not use the broken generated out wrapper or guessed struct offsets.

0.2.1 boot and level entry are confirmed. The post-fix first-contact log reports **72-byte value storage, alignment 8**. Local ticks increased through 619, 1130 and 1640 over approximately 20 seconds at about 51 Hz; sample age remained 0.00–0.01 seconds. No sample/native-output errors were logged. The process then exited with `Stopped; outputWorkerStopped=True, recording=Disabled, drops=0`, without a new ErrorLog crash. A later UDP listener ran after that exit and received no packets, so no live payload/coverage claim is made from that attempt.

Previous plugin, configuration and crash logs were backed up under `BepInEx/WodenBackups/before-0.2.1-20260912-233855`. `update-receipt.json` records the package hash and backup. Raw game logs, personal bindings, references, caches and recordings stay out of Git.

## Remaining live gates

1. Record a short drive, pause, respawn and stage restart. Corrected level entry and sustained car/contact sampling are established, but the first run was not recorded.
2. Confirm the corrected Controls.FixedUpdate action-table route feeds the car and validate steering direction/travel, pedals, paddle actions and reconnect.
3. Validate contact-corner mapping, units, RPM/gear/clock scales, packet rates, capture coverage and SimHub interpretation. Sampling remains pre-physics-solve; no calibrated rack torque or slip-angle claims.
4. Attended low-gain FFB sign/feel, actual driver delivery and physical stop/resume behavior. Synthetic force tests and successful UI initialization do not transfer physical verification.
5. Bonnet/bumper position, clipping, restoration, native switching and camera-takeover FFB stop, including split-screen. H-pattern, analog handbrake, stock menu navigation, FOV/per-car views remain future work.

Toolkit v0.12.0 is still pinned, native component v0.5.0. Recording is a separately pinned unpublished extension, not part of that release. No public release or change to sibling repositories has been made.

## Isolated shutdown successor candidate (2026-10-02)

Source candidate 0.2.14, based on canonical main 4e9d2c4, adds callback draining and serialized terminal force shutdown with explicit restart. Offline validation: 38 suites / 994 assertions, UI 556, triple geometry 22, normalized force envelope 10836; zero failures. No game, devices, displays or installed files changed. Corrected shared native a51bed9 preserves HAT39 and separately fixes low-demand restart and atomic watchdog readiness; held dd45f4f is excluded. Metadata-only package preparation remains gated on independent native and consumer reviews, delivery integrity/provenance updates and final installer/rollback tests. This entry does not supersede historical runtime evidence or claim rig verification.

## Frozen isolated shutdown ZIP (2026-10-02)

Exact consumer e3d118e and native a51bed9 approved for source/isolated package preparation; package aggregate review remains pending. Packaging source 4286797 produces 0.2.14-shutdown.1 ZIP SHA256 b31bc2ccde9aa6530b200b5a608d9554b2828dd81ca5c368127f2ad30fa80d64, manifest 416ddbf1f8bc644d2578be9b10f2c5525b31b0f136158f30d5d2681c4c221680. Final ZIP passed 39 delivery/mutation checks and 99 installer/rollback/preservation checks in marked disposable fixtures; nine installed payload files retain legacy ownership. Native/header/catalog pins and HAT39 preserved; private assets/settings/recordings excluded. Fresh batch test required restoring Windows PowerShell built-in module lookup inherited from pwsh; unchanged installer then passed. Exact frozen package remains unpublished; no live install/default promotion/game/devices/displays. Actual Unity/driver/physical acceptance is separate. External evidence is retained under task-5/evidence/woden-shutdown-delivery-final and woden-shutdown-*-final logs.

## Isolated FFB regression reference v1 (2026-10-02)

Based on canonical baeb286; no production source/settings/dependency/native/package changes. The existing actual-controller test harness adds 20 versioned synthetic summaries across strengths0/25/50/100 at unchanged25%cap, with repeat/mirror/golden comparison, normalized envelope/cap occupancy, unconditioned demand exceedance, request duty/cadence and terminal zero-before-close. Fixture options are test-only. Required offline checks pass:39suites/37657assertions;UI556;triplegeometry22;legacy shaper10836. Altered metrics and existing output overwrite are rejected with verified messages/byte preservation. ReferenceSHA2562bfe9b4e731394cdc281edbc26b238e4e91570813b00b7211a0fe66c70ca584f. Two oscillating cases have small negative higher-strength RMS deltas, reported rather than treated as gain equivalence. Native effect cadence/collision/physical calibration remain unavailable; no hardware/game/display/privatecapture reads or changes. Separate opt-in cadence11f780a remains excluded. This source-only candidate needs independent review before publication.

## Bounded reference-writer review correction (2026-10-02)

Held0506fcc remains clean/frozen in its original checkout. Corrected successor changes only test writer/race coverage, metric definitions and evidence docs. Final write now uses atomic FileMode.CreateNew; existence precheck is advisory. A separate task creates an eight-byte binary artifact while generation is paused inside its first sample check; final writer refuses it and exact bytes/hash survive. Required offline validation40suites/37661assertions,UI556,triplegeometry22,legacy envelope10836; no failures or build warnings/errors. Definitions: observed slew includes 300-sample in-envelope gate zeros and excludes later shutdown; sign changes retain last above-threshold sign across zero intervals; output-cap tolerance inclusive1e-6 and duty threshold strict>0.0015. Golden JSON Gitblob353e8f remains unchanged; emitted canonical summary SHA2562bfe9b4e731394cdc281edbc26b238e4e91570813b00b7211a0fe66c70ca584f. Windows clone checkout JSON uses CRLF and thus has different raw file hash; semantic/canonical summary data unchanged. Production code/gains/caps/runtime, held artifact and private datasets unchanged. No publication/hardware.

## Cadence command provenance assessment (2026-10-02)

Docs-only isolated successor of95d1ff7. Cadence11f780a and pure sharedsidecar84a106a remain separate/excluded; ancestry verified. Signal-reprocess outputs structuralpreview at recordedelapsed ticks, notcandidate commands. Missing exactadapter clock reads, separatecadence/reset/cappriming epochs, attempted command onfailedwrite and completeout-of-bandzero/close order prevent truthfulcommandreplay. Numericgate9 cannot distinguish conditioning-invalid frommodel-invalid withoutsemantic tags. Versioned engineering proposal lists minimumclock/settings/reset/gate/output identity, observed-vs-injected provenance and futurefake-device conformance; no producer/analyzer/runtime observer implemented because data prerequisite is absent. JSON/sources/identity checks only; completed20summaries/40suites notrerun. No privatecapture reads, gain/cap/HAT/native/runtime/package/sharedowner changes, Unity/device/screens orpublication. Independentreview required before any nextobserverimplementation.

## Bounded command-provenance proposal correction (2026-10-02)

Docs/schema-only successor adds ordered preparation guards/targetresolver and targetchange/CanOpen/IsReading/Open outcome/CloseReaders/finallyRefresh branch observations, including returnedfalse/exceptions/zero-reacquire failure and state before/after. Short-circuit not-evaluated is distinct from observedfalse; remembered target token alone does not prove Open success. Session-local d1..d4096 tokens, no persisted mapping/deviceGUID/name/privatepath/rawerror; finite boundederror set withunknown fallback. Explicitcanonical95 coverage separatefromcadence11f (notancestor); shared84 arithmetic remains separate. JSON/privacy/source validation maps11pinned preparation branches inbothcontroller snapshots; no runtimetests or20summaries/40suites rerun. Held25f assessment/source retained; no implementation/recording/pin/package/runtime/ABI/publication/hardware changes. Independentreview remains gate.

## 2026-10-02 isolated canonical command capture slice

The default-off internal capture candidate is based on canonical 7e806bb. It observes the actual production ForceController with injected fake clocks and devices into a bounded software call-sequence schema. See [COMMAND-CAPTURE-SLICE.md](COMMAND-CAPTURE-SLICE.md) for exact scope, separate observation/model/source clocks, complete/incomplete footer semantics and omitted provenance. Tests pass: 41 suites / 37,691 assertions, 556 UI assertions, 22 offline geometry assertions and 10,836 normalized envelope checks; zero-warning builds. Record/validate/pure-fake call projection and privacy/truncation/overflow/exception tests pass. This is distinct from excluded cadence 11f; it does not enable live capture, establish full algorithm replay or verify physical/visual acceptance. No packaging, deployment, remote publication or device/game/display work occurred. Existing installed builds, settings, freezes and evidence above remain unchanged.

## 2026-10-02 command capture held-candidate successor

Review held f7756acf for malformed call outcomes, a one-event false completion, missing exceptional IsReading outcome and nested shutdown falsely marked complete. The preserved held checkout and artifacts remain unchanged. Its isolated successor uses capture schema @2, validates typed payloads and start/prepare/shutdown/reset order, records exceptional reader call begin/end, and marks nested observed calls/invocations or an audit-invalid trace incomplete while preserving controller behavior. No activation or native/model/pin/gain changes. Production-linked fixtures add 37 adversarial assertions to the prior 30 capture assertions. Full source verification passes 41 suites / 37,728 assertions, 556 UI assertions, 22 offline geometry assertions and 10,836 normalized envelope checks with zero-warning builds. Reviewer's separate full-suite NuGet configuration access blocker remains unresolved; these available source checks used this worker's existing authorized offline build configuration. No hardware, publication, release, archive or installation. Independent successor review remains the next milestone.
