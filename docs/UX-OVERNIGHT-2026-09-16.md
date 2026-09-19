# Woden UX delivery — assignment of 2026-09-16

Status as of **2026-09-19**: **0.2.8 implemented, verified offline and installed** under the owner’s explicit completion/build/deploy authorization. This report separates source, managed fixtures, approximate render fixtures, verified deployment and live acceptance.

**0.2.9 successor is under review.** Its source and managed fixtures add stock-input ownership, held-input close/capture gating and bounded F6 diagnostics/fallback. It has not replaced the installed 0.2.8 runtime. The failed injected-F6 smoke and installer-r2 identity are recorded below without claiming live acceptance.

## Baseline and scope

- Guidance: UX-1 revised 2026-09-16 / UX-01-S, published guidance commit `a84bebab5ec2abdcd5140b9c63c139ccff86a7d3`; shared visual reference published at `95cbd89`.
- Read `CONSUMER-UX.md`, `CONSUMER-SETTINGS-VIEWS.md`, `CONSUMER-CONTROLS-CAMERAS.md`, `CONSUMER-SETUP.md`, `CONSUMER-UX-CHECKLIST.md`, and `docs/reference/README.md` in the toolkit. Compared hierarchy, labels, Setup density, direct binding and cancellation with the reference HTML. Reference SHA-256: `cd64239d2a4f092a69d74d05fe8a85e915509fd8686d19a7df2cf18ac8323996`.
- Woden branch `codex/ux-simple-advanced-0.2.8`. Baseline commit `9d28b83` separately checkpoints the prior 0.2.3–0.2.7 work over original HEAD `7fcbbbde049353096e025278cb42ac54399d98cc`. The pre-assignment snapshot remains at ignored `artifacts/ux-baseline-20260917-001141`. No reset or sibling-project commit was made. The owner authorized scoped commits and deployment after the original review-only assignment.
- Reviewed addendum `483bebd4715bac8cc3ccfedeaf6677260310aba8`: full camera-default conflict preflight and persistence-before-effective binding/calibration/Clear/reset. Source-linked fixtures cover individual and batch collisions, save failure, retained proposals, Retry/Cancel and disconnect-before-Save. Earlier guidance/reference identities above remain the adopted baseline.
- Reviewed clarification `12df6b325d770625baffd75b2d2eb74f1fcd0a8c`: stock dispatch isolation, every active keyboard/wheel/pad/Settings control released before handoff, and shared-primary disconnect cleanup. The distinct 0.2.9 source implements these supported routes; actual Unity hook execution and physical device release remain separate acceptance checks.
- Toolkit binary pin remains v0.12.0 / native 0.5.0; recording remains separately pinned and unpublished. No newer native package was adopted for this UI work.
- Preserved owned Unity HWND / mandatory exit guards, watchdog, saved panic Off, exact target identity, suppression without normal reconnect, original force model/gain, 50% default, owner bonnet correction and timer restrictions. Installed config/binding/recording preservation is required and verified separately at deployment. No physical-force test is part of this delivery.
- The coordinator's short rig HOLD interrupted final packaging on September 17. On September 19 the owner resumed, and the coordinator explicitly released the expired hold. A later serialized, force-disabled menu smoke ran and closed normally; no physical driving input or force was sent.

## Resulting workflow

Simple is the default for fresh and legacy configurations; explicit view and page persist. Switching view saves only presentation, uses the same runtime settings, preserves custom tunes and maps Advanced-only Driving to Setup. Capture/calibration, failed binding saves and unapplied connection edits lock view/page changes with a visible instruction. Esc cancels the pending edit. Binding/Clear/default writes are transactional: the exact proposal remains available with Retry/Cancel if saving fails. Axis Save polls and verifies the candidate still exists.

Setup presents three device-input bars, Centre for neutral Steering, and one next step. Four axis rows are visible at 720p, each with direct Bind, Calibrate and Clear. Calibration is provisional and offers normalized preview, inversion, deadzone, endpoints, Save and Cancel in Simple. Calibrate constrains capture to the existing device/axis. Handbrake axis and button contribute independently; the greater contribution wins. The existing proportional rear braking/grip route and binary engine cut remain.

Controls also includes Pause, Confirm, Back and four menu directions. Menu and driving contexts may share a button; global Settings/Stop/Pause assignments remain protected. The mod panel consumes its bound navigation directly. Outside driving, stock menus receive Unity submit/cancel/move events on their selected item, with a visible keyboard/controller fallback when no compatible handler exists. Actual menu coverage is pending a live check.

FFB has Off/On, a direct device dropdown, Strength, status and Refresh in Simple. Fresh selection follows the exact saved Steering GUID; legacy nonempty GUIDs remain explicit overrides. Missing/ambiguous/non-FFB/virtual targets are inactive. Duplicate friendly names receive a GUID suffix. Clearing/changing followed Steering releases the old output before any replacement is opened; target changes cannot enable saved Off.

Cameras keeps Bonnet/Bumper, Change camera, Look behind and on-demand Adjustment bindings in Simple. Camera key/button conflicts are rejected, not silently reassigned; reserved F6/F8 and modifier chords are protected. New numpad 1/3 means tilt down/up; existing explicit and implicit legacy mappings retain their meaning. Restore numpad defaults affects adjustment assignments only. Held adjustments repeat after 350 ms at at most 10 Hz; reset acts once. Panel/focus/camera suppression consumes held shortcuts until release. Pose sliders are Advanced; offsets/default fitting remain unchanged. Live changes briefly show the active mount and values.

Telemetry has a master Off/On, actual loopback destinations, truthful Sending/Off/Unavailable status and active-capture Stop in Simple. Advanced connection edits apply atomically or cancel to the active destination. Network changes keep the existing recorder and worker; Telemetry Off does not stop capture. Recording still starts only through the owner's agent workflow. The panel offers a local support summary, never an upload.

## Complete settings and action placement

S = Simple (also in Advanced); D = Simple on demand; A = Advanced only; Internal = persistence metadata, no player control.

| Saved field / action | Page and placement | Default / unit | Reason, behavior and evidence |
|---|---|---|---|
| Interface.View | Header, S | Simple | Explicit persistent presentation only; actual Settings + Panel fixture |
| Interface.Page | Navigation, S | Setup | Six common pages; Advanced adds Driving between Cameras/Telemetry |
| Interface.ScalePercent | Help, S | 100%, 85–150% | All text/targets scale; Default scale action; capped to viewport width |
| Wheel.Enabled | Setup, S | Off | Three axes required; wheel readiness does not depend on optional hardware |
| Refresh devices | Setup/FFB S; Help A | Action | Explicit zero/release then refresh, saved preference retained |
| Steering / Throttle / Brake / Handbrake identity | Controls, S | Unbound; exact GUID + axis | Direct Bind/Calibrate/Clear; independent devices |
| Axis Calibration.Rest / End / Centre | Controls calibration, D | Measured 0–65535; Steering centre | Provisional full-range capture; no raw manual edit feature |
| Axis Inverted | Controls calibration, D; row summary S | Off | Explicit Off/On applied to calibrated direction; preserves existing signed endpoints |
| Axis Calibration.Deadzone | Controls calibration, D; row summary S | 0%, range 0–25% | Normalized device-input preview; Save/Cancel |
| Bindings.Version / HandbrakeUsesAxis | Internal | Version 1; legacy boolean retained | Old mode no longer disables either binding; compatible JSON |
| Gear up / Gear down | Controls → Driving buttons, D | Unbound | Display Shift up / Shift down; wheel-button capture |
| Handbrake button | Controls → Driving buttons, D | Unbound | Additive with analog axis and stock button; full input when held |
| Camera / Rear view | Controls → Driving buttons or Cameras, D/S | Unbound | Shared stored actions; Change camera initially visible, Look behind on demand |
| Respawn | Controls → Driving buttons, D | Unbound | Display Reset car; preserves native dispatch |
| Pause / Confirm / Back / Menu up/down/left/right | Controls → Menu buttons, D | Unbound | Pause retains native route; mod panel focus or existing selected Unity stock UI handlers receive context-gated events |
| Settings panel / Panic stop | Controls → Mod buttons, D | Unbound; F6/F8 reserved | Saved wheel bindings plus keyboard escape routes; panic saves Off |
| Lights / Horn / Records / Next song | Controls → Extra game buttons, D | Unbound | Native action names/routes retained |
| H-pattern / clutch | Controls groups, D | Unavailable | Visible implementation-gap message; sequential and native keyboard/controller routes remain |
| General.PlayerIndex | Controls, A | 0, range 0–3 | Existing local-player selection; normal first player remains Simple |
| ForceFeedback.Enabled | FFB, S; Stop global S | On | One saved preference; F8/Stop saves Off; view changes cannot enable |
| ForceFeedback.FollowSteering / DeviceGuid | FFB dropdown, S | Follow saved Steering; no GUID fallback | Legacy explicit GUID retained; direct physical-wheel choices |
| ForceFeedback.SelectionVersion | Internal | 1 | Migration marker only |
| ForceFeedback.StrengthPercent | FFB, S | 50%, range 0–100% | Literal existing gain; default action beside control |
| ForceFeedback.PeakPercent | FFB, A | 25%, range 0–50% | Existing hard cap; explanatory help |
| ForceFeedback.SmoothingMs | FFB, A | 35 ms, range 0–200 | Existing filter, unchanged |
| ForceFeedback.Damping | FFB, A | 0.05, range 0–0.5 | Calibrated steering-motion gain, unchanged |
| ForceFeedback.LoadReference | FFB, A | 6000 Unity force units | Existing uncalibrated estimate normalization |
| ForceFeedback.SlipScale | FFB, A | 0.35 game slip units | Existing provisional response scale |
| ForceFeedback.Invert | FFB, A | Off | Output sign; panel already suppresses force |
| Reset FFB tuning | FFB, A | Above defaults | Includes Strength, preserves On/Off and device; no reset on view change |
| FFB output/delivery counters | FFB, A | Read-only | Simple retains actionable inactive/error reason and custom-tune summary |
| Camera.BonnetEnabled / BumperEnabled | Cameras, S | Both On | Native cycle participation; existing ownership rules |
| CameraKeys adjustment entries | Cameras → Adjustment bindings, D | Numpad 8/2, 9/7, 4/6, 3/1 up/down tilt, +/- FOV, 0 reset | Full key/button Bind/Clear/Cancel/timeout/conflict; actual saved assignments displayed |
| Restore numpad defaults | Cameras → Adjustment bindings, D | Family mapping | Only adjustment assignments; rejects conflict with cycle/look-behind keys |
| Camera.AutoFitBonnet | Cameras, A | On for fresh settings | Owner +0.15 m height / +0.05 m forward fitted correction preserved |
| Camera.Side / Height / Forward | Cameras, A | 0 / 0.7367809 / 0.9380049 m fallback | Bonnet selected-mount sliders; bounds −2..2 / 0.1..3 / −2..4 m |
| Camera.PitchDegrees / Fov | Cameras, A | 8° down / 70° | Bonnet tilt −30..30°, FOV 30..110° |
| Camera.BumperSide / BumperHeight / BumperForward | Cameras, A | 0 / 0.35 / 2.2 m | Independent Bumper sliders, same bounds |
| Camera.BumperPitchDegrees / BumperFov | Cameras, A | 0° / 70° | Independent Bumper lens/tilt |
| Reset this view | Cameras, A; bound shortcut D | Mount default | Only selected/active mount; Bonnet resets to fit mode |
| Camera.DefaultsVersion | Internal | 2 | Existing camera migration; manual pose preserved |
| Difficulty.CountdownAssistEnabled | Driving, A | Off | Optional owned single-player time-limit hook; not a setup requirement |
| Difficulty.CountdownSpeedPercent | Driving, A | 75%, range 25–100% | Default speed action; elapsed clocks/physics/checkpoint additions untouched |
| Telemetry.Enabled | Telemetry, S | On, retaining prior stream default | Master dashboard UDP control, independent recording |
| Telemetry.ForzaPort / DetailPort | Telemetry, A; destination summary S | 8000 / 8001, loopback only | 0 disables stream; active ports distinct; atomic Apply/Cancel |
| Telemetry.DetailHz | Telemetry, A | 20 Hz, range 1–60 | Provisional edit until Apply connection |
| Restore connection defaults | Telemetry, A | 8000 / 8001 / 20 Hz | Stages edits; Apply or Cancel required |
| Telemetry counters/error | Telemetry, A; status/error S | Read-only | Sending means recent successful socket send, not receiver acknowledgement |
| Diagnostics.RecordSession / one-launch request | External agent workflow | Off / no request | Legacy setting preserved; no menu Start; expiry and duration/size bounds retained |
| Active recording status / Stop | Telemetry, S | Visible when active | Stop affects recorder only; limit/failure status remains visible |
| Device inventory, GUIDs, raw axes | Help → Details, A | Read-only | Explicit Refresh and next device; full identities available |
| Create support file | Help, S | Local WodenSupport text file | Version/status/counters/device summary; no config reset or upload |
| Saved / save failure / Close / Stop FFB | Global, S | Always visible | Persistence errors retain working values; edit-time Close requires finish/cancel |

## Evidence and acceptance

Final source checks on September 19: release build with warnings as errors, zero warnings/errors; **35 regression suites / 942 assertions**; **459 source-linked UI fixture assertions**. The assertion count includes draw checks and varies with required scrolling; it is not a count of independent workflows. The walk includes visible Save/Cancel beside the 720p calibration preview, live camera-change hint, sticky binding-load errors, exact failed-proposal retry, no effective change on failed Clear/defaults, batch camera-default collision rejection, disconnected-candidate rejection and actual menu dispatch source.

`tests/WodenRallyEdge.UiTests` compiles the actual Panel, WheelInput, CameraShortcuts, MenuNavigation and Settings source against managed fake Unity events/devices. It walks direct axis Bind, provisional Save/Cancel, additive binding retention, camera rebind/conflict/chord/timeout/defaults, view edit locks, keyboard header activation, real config reload, failed save visibility, active-mount adjustment and held-key release after panel/focus. It never loads a native device adapter or game.

Draw-command JSON from that production Panel is rendered offline using an approximate Segoe UI font. Setup, Controls, FFB, Cameras and calibration at 1280×720, Setup at 3840×2160, and enlarged 720p layout were visually inspected. All Simple/Advanced pages keep header/navigation/Close/Stop in the fixture at 720p and 4K. These PNGs establish approximate geometry, **not Unity screenshots, actual glyph/font behavior or physical input acceptance**. Private artifacts: `artifacts/ux-ui-fixture`.

| Check | Source / offline result | Live or product gap |
|---|---|---|
| UX-01 / UX-01-S | Implemented; actual view/capture/edit/persistence fixture passes | First-use and restart walk on installed candidate Not tested |
| UX-02 / UX-03 | Shared hierarchy, labels, opaque layout, scaling, keyboard and scrolling; fixture review | Native IMGUI focus/text-field navigation, long names, cursor and actual 720p/4K render Not tested; wheel menu routing is implemented and source-tested; actual stock-screen coverage Not tested |
| UX-04 / UX-04-H | Independent identity, provisional calibration, additive axis/button, inversion and conflict checks | Partial/full/rest, simultaneous real controls, unplug/reconnect and final game response Not tested; H-pattern/clutch Gap |
| UX-05 / UX-05-D | Strict follow/override policy and actual controller lifecycle tests pass; owner tune unchanged | Low-force sign/load, target changes and physical stop/recovery Not tested in 0.2.8 |
| UX-06 / UX-06-K | Actual shortcut source tested; scoped defaults and legacy meaning preserved | Visible tilt directions, all mount movements, scripted-camera takeover/lens restore, cross-car hood framing and split-screen Not tested |
| UX-07 / UX-08 | Real loopback retarget, invalid apply rollback, UDP Off/capture continuity, completed capture; save-error fixture | SimHub/real receiver integration Not tested; local support is a summary, not a full log/config archive; optional mark/open-folder UI Gap |
| UX-09 / UX-10 | Player install/update/uninstall implemented; 59 Windows PowerShell 5.1 fixture checks pass, including fresh and update rollback, receipt allowlist, package corruption, modified-file refusal, settings/recording/shared-loader/other-mod preservation | Real Steam discovery/folder-picker interaction Not tested; no public release claim |
| UX-11 | Required setup available in Simple in source/fixture | Attended first-drive walkthrough Not tested |

## Changed files and candidate identity

The owner’s later completion/deployment authorization supersedes the original review-only scope. Prior work is isolated in baseline commit `9d28b83`; UX/menu/installer work receives its own scoped commit. Final package/installed hashes and backup receipt are recorded below. No push is needed for this local delivery.

New files: `src/WodenRallyEdge.Core/SettingsPresentation.cs`, `src/WodenRallyEdge.Plugin/MenuNavigation.cs`, `tools/{Manage-Install.ps1,Install.bat,Uninstall.bat,Test-ManageInstall.ps1}`, `tests/WodenRallyEdge.Tests/UxChecks.cs`, `tests/WodenRallyEdge.UiTests/{WodenRallyEdge.UiTests.csproj,Fixtures.cs,Program.cs}`, this report.

Modified by this UX assignment: Core `Bindings.cs`, `CameraTuning.cs`, `HandbrakeInput.cs`, `TelemetryOutput.cs`; Plugin `Panel.cs`, `Settings.cs`, `WheelInput.cs`, `CameraShortcuts.cs`, `DeviceHub.cs`, `ForceController.cs`, `InputLease.cs`, `Plugin.cs`; existing test `Program.cs` and `ForceControllerFixtures.cs`; classic solution; `Directory.Build.props`; `tools/Package.ps1`, `tools/Install-Dev.ps1`; development/build/state/handoff/UX documentation. Original force calculation, native adapter/window fix, game hooks, camera fit/ownership and assist hook are preserved from the baseline.


## Installer verification and delivery identity

The player package contains nine runtime/schema/provenance payloads, LICENSE, README, Install.bat, Uninstall.bat, Manage-Install.ps1 and manifest.json: exactly **15 allowed files**, with **14 manifest entries**. The loader is downloaded separately and hash-pinned; no proprietary game/interop/assets, owner settings/recordings or loader caches enter Git/ZIP.

`tools/Test-ManageInstall.ps1` passed **59 checks under Windows PowerShell 5.1** at ignored `artifacts/managed-installer-test-caff41af997847ddb8725f4e89a7baa3`. It exercises a failed first install with complete new-file rollback, retry, initial install, update with full configuration retention/backup, injected replacement failure restoring different old bytes, corrupted package refusal, modified owned-file refusal, malformed receipt refusal, normal uninstall, reinstall and explicit named-settings removal. Unknown mod files, unrelated configuration, shared loader and recordings remain. Fixtures contain only a private identity DLL copy; no game executable or device use.

The force calculation, native adapter, game-window repair, camera fit/ownership and assist/analog-handbrake hooks remain byte-identical to baseline. A separately coordinated force-disabled Unity menu walk and an attended physical drive remain distinct acceptance steps.

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

Deployment itself launched no game. A later diagnostic run at 19:25:33–19:31:23Z did not open F6 through injected key presses. Lifecycle updates/focus were observed, but missing InputSystem input and a swallowed hotkey exception cannot be distinguished from that log. Attract-mode DEMO PLAY was the final screen; no driving input was sent. This is an unresolved UI check. The run logged zero force writes/failures and normal shutdown. Its bounded capture completed with 3,206 samples, zero dropped/errors. All four owner configurations were restored byte-exact and all nine installed files reverified. The matching consumed request and logs are private in `artifacts/deployment-0.2.8/live-smoke`; the desktop lease was released.

## Separate installer-r2 successor

Source `9b4d75e37568e674274aeee0fc8719db164dd22f` fixes recovery when the game starts or another process changes a file during replacement. Every copy/delete/rollback rechecks closed state and current hashes. Recovery restores only known new bytes from verified originals; unknown bytes remain untouched and `recovery.json` records required review. **81 actual Windows PowerShell 5.1 fixture assertions passed**, including game-start-after-two-writes, external replacement after a write, and a forward write conflict. Independent read-only review closed both installer findings.

Frozen ZIP: `dist/WodenRallyEdgeWheel-0.2.8-installer-r2-9b4d75e.zip`, SHA-256 `1992551438aa7e5d46c66500f53d3189f5552bbcb815ec169b5c04ca976dcbb5`; stage `dist/stage-installer2-8dcc22a2a27146fe8eadde9a61746b01`; script SHA-256 `c1091e39ecd851a6eeec20a7ff8057587139c5f57e7243755cec5dc4f5eeb46c`. Exactly 15 files / 14 manifest hashes verified. The nine runtime payloads remain `df0e4dc` and match installed files. Original ZIP `914657…` is retained unchanged. Fixture: `artifacts/managed-installer-test-03b654917b7e4ffdb82aac643da3ae2e`.

## 0.2.9 input-ownership candidate

Native disassembly of the guarded local game shows `GamePadSystem.Update` (RVA `0xC000A0`) calls `ReadInputs` (`0xBFD6F0`) for each configured pad. The latter writes each existing `PadActions` value/Pressed from its actual AssignedFloat and configured KeyBoard_Key. New postfixes observe that aggregate before neutralizing the same objects, raw float arrays and boxed-value action entries. Retained references therefore see neutral input. No assumed menu action indices, new native device implementation, global key injection or configuration rewrite is used. EventSystem dispatch stays disabled while the panel owns input. Normal driving does not scan or mask the stock route.

Close remains owned until keyboard, pointer, configured wheel menu/Settings controls and fresh stock input stay neutral for 100 ms. Missing/stale/failed observations do not count as neutral. Keep settings open cancels a stalled close. The same policy arms button/key capture after release. Current and previous disconnected-device samples are cleared; a failed input lease invalidates the previous effective handbrake/input telemetry sample.

The F6 source now logs keyboard availability and bounded read failures, isolates F8 errors, and accepts the existing IMGUI key route with same-frame deduplication and held-repeat suppression. Legacy keyboard/pointer reads can establish release when InputSystem devices are absent. This is a supported fallback candidate, **not proof of the earlier failure's cause**.

Zero-warning full build, **35 suites / 942 regression assertions** and **506 source-linked UI assertions** pass. New checks execute the production aggregation/mask/release code against retained arrays, remapped stock controls, keyboard, pointer, configured wheel Confirm/Settings, unknown/stale/skipped reads, failed F8 before F6, missing keyboard fallback, duplicate key delivery, capture release and shared-primary failure. Live hook counters, actual stock-menu isolation, F6 display and physical wheel/pad handoff remain unverified until a new coordinated run.
