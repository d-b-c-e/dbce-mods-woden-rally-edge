# Toolkit standards adoption

Which entries of the wheel toolkit's standards ledger
(`E:\Source\toolkits\dbce-wheel-mod-toolkit\STANDARDS.md`) this mod has brought in.
Update a row in the same commit that adopts it. Statuses: `adopted`, `partial`,
`pending`, `n/a` (say why), `unchecked` (nobody has looked yet).
Seeded 2026-10-04 from what was verified that day; `unchecked` rows need a look.

| Standard | Title | Status | Notes |
|---|---|---|---|
| STD-001 | One mod per game | adopted | one combined mod |
| STD-002 | Recording and playback from launch | n/a | Superseded by STD-012. |
| STD-003 | Normalized FFB strength | partial | Original analysis model and offline strength/cap trials validated; 25% cap occupied 46.46% of valid intervals. Physical normalization and defaults remain pending. See docs/FFB-COMPARISON.md. |
| STD-004 | Consistent settings UX | adopted | F6 Simple/Advanced |
| STD-005 | Camera numpad layout 8/2 9/3 4/6 7/1 +/- 5 | adopted | 580a907, retained in merged source 83b72d9 |
| STD-006 | Camera step sizes are settings | adopted | 2e32e46, retained in merged source 83b72d9 |
| STD-007 | Triple screens in one wide window | adopted | Owner drove the earlier triple build under Surround 2026-10-04; October 6 combined Kenya playback passed all 3,601 poses at 7680x1440 on separate monitors, frames 01/03 inspected by Claude and Codex with bonnet across three views and centre HUD. |
| STD-008 | Display changes: game applies once | adopted | 0b0f844 DesktopResolution and feb875b index repair, retained in 83b72d9 |
| STD-009 | Dashboard telemetry matches the HUD | adopted | 0b0f844 retained in 83b72d9; owner dashboard RPM/gear confirmation remains separate |
| STD-010 | Install the latest build for testing | adopted | 8678f06/session-span.2 installed October 6 07:10 UTC after span qualification; 13 current payloads, 60 owner files and 25 raw preferences independently verified. All 111 PS5.1 package checks reported passed. See docs/2026-10-06-span-qualification.md. |
| STD-011 | Work lands on main | adopted | Integrated both main histories at 83b72d9; land tested recording/playback work on main in this session, preserve unrelated active checkouts and private evidence. |
| STD-012 | Reproduce the route and preserve original signals | adopted | Native cold startup and 3,601 poses passed; owner accepted the original route/cameras. Sealed signals/reference remain outside worktrees. Playback core 0.2.1 / af4d20d restricts background execution to supervised, validated, muted replay. October 6 run acquired naturally unfocused and completed all 3,601 steps with inspected triples, zero force/network delivery and exact restoration. Broader transitions/interruption matrix remains open. See docs/2026-10-06-span-qualification.md. |
| STD-013 | The installed build launches plainly | adopted | Installed 8678f06/session-span.2 passed plain Steam launches on separate monitors (03:24) and the Surround round trip (11:34); borderless 7680 at (0,0) on Surround, then (-2560,0) on separate monitors. All 13 payloads and original owner state independently verified. This startup pass does not replace the remaining owner drive/HUD/force checks. |
| STD-014 | Request reciprocal review when progress stalls | adopted | Claude's failed combined replay led to Codex's scoped focus fix. The naturally unfocused October 6 replay passed; Claude and Codex inspected frames and exact evidence. Historical pre-fix failures and the earlier focused-only pass remain distinct. |
| STD-015 | Triples on Surround and separate monitors | adopted | Surround: owner-driven 2026-10-04. Separate monitors: span on by default (2026-10-06) after Astra's 02:05 cold Kenya replay on "Sim Racing" passed (3,601 poses, 0.06 mm; five 7680x1440 frames, triples on, seams continuous; Claude reviewed the frames). See docs/2026-10-06-span-qualification.md. Per-display rendering (Display.Activate) not attempted. |
| STD-016 | Telemetry: Forza Horizon layout, on by default | unchecked | Added from the October 6 ledger reconciliation. Existing telemetry implementation/owner settings are preserved; default and complete layout/HUD audit not performed in this muted replay check. |
| STD-017 | Hide settings pages with nothing to offer | unchecked | Added from the October 6 ledger reconciliation; panel inventory audit remains. |
| STD-018 | Handling changes never reach online scores | adopted | `LeaderboardGuard` (2026-10-06): once the default-off countdown assist adjusts the time limit in a race, that race's `SteamLeaderBoard.UpdateScore` is refused; a new local car clears it. Input mapping and the analog handbrake (native grip-loss branch scaled) do not block uploads. |
| STD-019 | Menus on the centre screen; side screens only in gameplay | partial | October 6 replay frames establish centre gameplay HUD. The 11:34 plain-launch frames show startup line backgrounds extending onto both sides; centre-only loading/front-end/mod-panel coverage remains incomplete. |
| STD-020 | Standard feature checklist per game | unchecked | Added from the October 6 ledger reconciliation; reconcile the portfolio feature row with current scoped evidence before release. |
| STD-021 | art of rally is the FFB reference | partial | Grip v4 uses Strength / 50 without the Classic25% ceiling; Art remains fixed. Three source/fake-sink findings are closed at8bc3444 (independent59/37973). Stage/export dispatch and original reference-state validation are closed atab7f1fa; complete synthetic Grip and original Kenya Classic pass. Fresh original Grip capture, crash qualification and attended calibration remain open. See docs/2026-10-07-grip-stage-followup.md. |
| STD-022 | One triple-screen selector: Off / Surround / Separate monitors | adopted (source, 2026-10-08) | F6 Cameras selector drives Mode and SpanSeparateMonitors. Persistence follow-up schedules the normal timed save for both entries; actual panel clicks and reopened config values pass in the 705-assertion fixture, with a failing pre-fix control. Window-layout changes are labelled next-start. See docs/2026-10-08-selector-persistence.md; owner rendering acceptance remains open. |
| STD-023 | Frame-rate readout in the settings panel and log | partial | eef5672 (fps.1 installed): F6 Cameras 10 s summary and 30 s log line via vendored toolkit FrameRateMonitor; owner acceptance pending. |
| STD-024 | Optional on-screen frame-rate counter | partial | eef5672: [Display] ShowFrameRate, default Off, top right of the centre screen; UI fixture covers the toggle; rendered acceptance pending. |
| STD-025 | One toolkit force model for tyre games | partial | Grip v4 uses AxleForceCurve on a per-wheel force estimate and driving-load mean. Existing configs retain Classic. Low-speed damping, load/curve guards and stage replay/export fixes pass independent production-path tests; missing/provisional references are excluded. CrashStage remains sampled fallback, not Art native delivery. Original live Grip evidence and owner feel remain pending; see docs/2026-10-07-grip-stage-followup.md. |
| STD-026 | One force model for players, no other-mod names | adopted in source (2026-10-08, Grip v5 commit; awaiting Astra review and install) | F6 FFB shows one model, no selector, and no other-mod names in F6 or config text. A saved Classic choice is kept and F6 says so until Grip is qualified for Woden; the owner's install moves to Grip by the owner's request. Previous note: F6 FFB still offered Grip/Classic and its text named art of rally. Existing installs (owner included) run Classic and Grip v4 is under Astra's review; drop the selector and move Classic configs to Grip once Grip v4 is qualified here (owner decision 2026-10-07). |
| STD-027 | Independent steering and effect strengths | adopted in source (2026-10-08, Grip v5; awaiting review and install) | Grip v5: Steering strength scales the tyre force only; damping has its own gain; crash strength separate; both on the Simple FFB page. Classic still scales its damping with strength (kept for comparisons). Previous note: Crash strength is separate, but Strength also scales damping in the current models. Audit/isolate steering-only adjustment with versioned replay before claiming full centering-only independence; preserve the owner-selected model and saved tune. Toolkit docs/FFB-STRENGTH-CONTROLS.md. |
| STD-033 | Rig-profile controls contract | partial | Installer declares woden-bindings-1 for the existing F6 JSON v1 store; Wheelkit candidate translates axes/buttons and clears obsolete gates. Independent file/consumer qualification is separate from live input. Native transmission, clutch/select/neutral and gears above six remain gaps. See docs/2026-10-10-profile-controls.md. |
| STD-034 | Closed-loop Apply qualification | partial | controls.6 / raw-controls-20261010-06: production Apply repairs staged wrong indexes with fixture/live parity; raw profile inputs navigate from cold launch to Spain SS1 and reach the real car consumer (steer +/-0.5, throttle 0.5/1, brake/handbrake 0.5). Exact owner restoration passed. Aux shifters, complete menu/interruption matrix and physical feel remain open. See docs/2026-10-10-profile-controls.md. |
October 10 STD-033 correction: profile hats explicitly use the shared circular
4500-unit rule; old F6 eight-way bindings stay exact. `HatNeighbours` is per
binding and the installer declares its capability. Vendored input source
83b3f93/58651c0 uses the exact resident module's coherent POV export on the
existing read slot; native/managed binary pins are unchanged. Production
DeviceHub passes 3,896 fake-reader checks; actual post-Apply raw input remains
unqualified. See docs/2026-10-10-profile-controls.md.

October 10: the developer-only raw qualification addon is a source candidate, not a player/native pin update. 31 protocol checks, 40 compiled metadata seams, 97 actual-addon callback assertions, 26 file-recovery and 50 production runner recovery checks pass. Claude reviewed the callback lifecycle; force-open guarding now precedes game-specific reflection. Runner review and live input remain open. See tools/controls/README.md. Shared rig-lease helper advances to a484cb6 for borrowed-token validation; 059da7 Enter/Exit behavior is unchanged.

October 10 follow-up: STD-034 runner now freezes reviewed Wheelkit 8c7243e
from a git archive, stages wrong steering/Confirm bindings, compares fixture
and live previews/applied bytes and independently checks their repair. Its
synthetic production Apply check, 28 file-recovery and 50 production recovery
assertions pass. Claude's runner review prompted these additions. Live input
and owner acceptance remain separate and pending.

First raw-addon run refused on BepInEx's startup config rewrite before any raw
command; normal close/exact restoration passed. Schema 2 now checks unchanged
original values plus four explicitly allowed missing defaults, using one shared
addon/offline verifier; 13 negative/positive cases and the actual rewritten
config pass. This remains input qualification pending, not observed controls.

Run03 subsequently admitted and observed raw Confirm through the actual saved
binding after fixture/live production Apply repaired deliberately wrong indexes.
It exposed a native startup screen which bypasses the wheel menu dispatcher.
The startup bridge source passes 759 production UI checks; its runtime and the
remaining driving controls remain pending. Exact run03 restoration passed.
The bounded developer observer passes 108 callback lifecycle assertions.

October 10 run07 supersedes those pending runtime notes for its bounded scope:
installed controls.6/ac308a6, frozen Wheelkit 8c7243e, production fixture/live
preview and byte parity after staged wrong steering/Confirm bindings. Cold
native menus reached Spain SS1. Observer 45772f8 reads actual Controls/MainCar
fields rather than desired wheelInput values. Revised verdict f86b79e requires
a preceding eligible baseline and full sustained sampling: five changed scalar
responses observed, zero mismatches, eleven unknown (including first half-throttle
with no prior baseline). The older six matching plateaus remain in evidence.
Normal exit, exact owner files and raw registry restoration at 07:25:40 CT.
No physical force or output telemetry was delivered. Native Boolean handbrake
does not establish proportional braking; clutch, auxiliary gearbox, full menu
navigation and interruption/hardware acceptance remain open.

STD-035 adoption is partial: writer capability/identity and original-profile
fixture/live checks are in place; the developer addon has no-force request and
runtime lifecycle guards and production callback tests. This is not a claim that
every fleet injection mechanism matches the standard. The exact-restore Woden
runner is authorized; per-game cloud convergence and full lifecycle matrix remain
explicit separate checks. See docs/2026-10-10-profile-controls.md and
tools/controls/README.md for scope and receipts.
