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
| STD-007 | Triple screens in one wide window | adopted | b89ce73, retained in 83b72d9; owner drove the earlier triple build under Surround 2026-10-04; combined playback accepted single-screen; combined triple presentation still pending |
| STD-008 | Display changes: game applies once | adopted | 0b0f844 DesktopResolution and feb875b index repair, retained in 83b72d9 |
| STD-009 | Dashboard telemetry matches the HUD | adopted | 0b0f844 retained in 83b72d9; owner dashboard RPM/gear confirmation remains separate |
| STD-010 | Install the latest build for testing | adopted | owner Stream Deck target; 05837ee/session-focus.1 installed 21:08 UTC, 13 payloads verified, 18 protected files retained; full cold replay and 111 PS5.1 installer checks pass |
| STD-011 | Work lands on main | adopted | Integrated both main histories at 83b72d9; land tested recording/playback work on main in this session, preserve unrelated active checkouts and private evidence. |
| STD-012 | Reproduce the route and preserve original signals | adopted | Integrated 83b72d9; native cold startup and 3,601 poses passed; owner accepted route/cameras. Original signal data and sealed reference preserved under LocalAppData/Dbce/StagePlayback/references/woden-kenya-20261005. Runner enforces a hash-pinned shared rig lease. Candidate now pins playback core 0.2.1 / af4d20d for explicit armed-replay identity and restricts background execution to supervised, validated, muted replay. Fifty new policy checks pass; live focus-fix/combined-triple completion and broader matrix remain open. See docs/2026-10-05-background-replay.md. |
| STD-013 | The installed build launches plainly | partial | Latest testable 05837ee runtime is installed at the Steam target; 13 payloads freshly verified. Its cold replay used the retained 7680 settings. Ordinary owner launch and the broader final release checks remain separate from supervised replay qualification. |
| STD-014 | Request reciprocal review when progress stalls | adopted | Claude's failed combined replay was reviewed against runtime logs; Codex provided the scoped focus fix and a specific background-acquisition check. Delayed pre-fix failures are retained separately from the later focused pass. No new background success is claimed. |
| STD-018 | Handling changes never reach online scores | adopted | `LeaderboardGuard` (2026-10-06): once the default-off countdown assist adjusts the time limit in a race, that race's `SteamLeaderBoard.UpdateScore` is refused; a new local car clears it. Input mapping and the analog handbrake (native grip-loss branch scaled) do not block uploads. |
