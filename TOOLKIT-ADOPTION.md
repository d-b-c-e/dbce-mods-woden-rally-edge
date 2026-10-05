# Toolkit standards adoption

Which entries of the wheel toolkit's standards ledger
(`E:\Source\toolkits\dbce-wheel-mod-toolkit\STANDARDS.md`) this mod has brought in.
Update a row in the same commit that adopts it. Statuses: `adopted`, `partial`,
`pending`, `n/a` (say why), `unchecked` (nobody has looked yet).
Seeded 2026-10-04 from what was verified that day; `unchecked` rows need a look.

| Standard | Title | Status | Notes |
|---|---|---|---|
| STD-001 | One mod per game | adopted | one combined mod |
| STD-002 | Recording and playback from launch | partial | Integrated 83b72d9; supervised real recording and original signal capture; trajectory playback is distinct from deterministic input resimulation. Cold startup/live acceptance pending. |
| STD-003 | Normalized FFB strength | unchecked | Original signal/model data preserved; physical cross-game normalization remains unverified and saved tuning is unchanged. |
| STD-004 | Consistent settings UX | adopted | F6 Simple/Advanced |
| STD-005 | Camera numpad layout 8/2 9/3 4/6 7/1 +/- 5 | adopted | 580a907, retained in merged source 83b72d9 |
| STD-006 | Camera step sizes are settings | adopted | 2e32e46, retained in merged source 83b72d9 |
| STD-007 | Triple screens in one wide window | adopted | b89ce73, retained in 83b72d9; owner drove the earlier triple build under Surround 2026-10-04; combined runtime check pending |
| STD-008 | Display changes: game applies once | adopted | 0b0f844 DesktopResolution and feb875b index repair, retained in 83b72d9 |
| STD-009 | Dashboard telemetry matches the HUD | adopted | 0b0f844 retained in 83b72d9; owner dashboard RPM/gear confirmation remains separate |
| STD-010 | Install the latest build for testing | adopted | owner Stream Deck target |
