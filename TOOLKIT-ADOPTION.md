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
| STD-013 | The installed build launches plainly | partial | Current 8678f06/session-span.2 runtime is installed at the Steam target; 13 payloads verified. Plain launch with its new span default and return to Surround remain open; supervised replay does not qualify those cases. |
| STD-014 | Request reciprocal review when progress stalls | adopted | Claude's failed combined replay led to Codex's scoped focus fix. The naturally unfocused October 6 replay passed; Claude and Codex inspected frames and exact evidence. Historical pre-fix failures and the earlier focused-only pass remain distinct. |
| STD-015 | Triples on Surround and separate monitors | adopted | Surround: owner-driven 2026-10-04. Separate monitors: span on by default (2026-10-06) after Astra's 02:05 cold Kenya replay on "Sim Racing" passed (3,601 poses, 0.06 mm; five 7680x1440 frames, triples on, seams continuous; Claude reviewed the frames). See docs/2026-10-06-span-qualification.md. Per-display rendering (Display.Activate) not attempted. |
| STD-016 | Telemetry: Forza Horizon layout, on by default | unchecked | Added from the October 6 ledger reconciliation. Existing telemetry implementation/owner settings are preserved; default and complete layout/HUD audit not performed in this muted replay check. |
| STD-017 | Hide settings pages with nothing to offer | unchecked | Added from the October 6 ledger reconciliation; panel inventory audit remains. |
| STD-018 | Handling changes never reach online scores | adopted | `LeaderboardGuard` (2026-10-06): once the default-off countdown assist adjusts the time limit in a race, that race's `SteamLeaderBoard.UpdateScore` is refused; a new local car clears it. Input mapping and the analog handbrake (native grip-loss branch scaled) do not block uploads. |
| STD-019 | Menus on the centre screen; side screens only in gameplay | partial | October 6 replay frames establish centre gameplay HUD; loading, front-end and mod-panel coverage was not captured in that run. |
| STD-020 | Standard feature checklist per game | unchecked | Added from the October 6 ledger reconciliation; reconcile the portfolio feature row with current scoped evidence before release. |
