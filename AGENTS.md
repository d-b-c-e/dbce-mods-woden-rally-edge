**Current owner install (2026-10-14, 15:21 CT):** Private save-guard/gearbox candidate from exact source `24d7203e684e45b1a69ba6a6db3ba2b1d0b28179` is installed with revision-5 receipt `D50B71A24BDD1ACD7654026DF3FC35BC8013B1096A4736408118D032CE3AA50D` (`nativeGearboxPreference=true`). All 23 payloads, the backup of the `f252deb` install, saved Native default, Gearbox at start Keep, tune, 305 nonowned files and raw preferences passed readback; displays were recorded only. No launch, opt-in, device or force test. See `components/wheel/docs/HANDOFF.md`, `docs/READY-TO-TEST.md` and hub `artifacts/woden-private-owner-install-24d7203-20261014/README.md`.

**Previous owner install (2026-10-13, 19:17 CT):** Optional private-shim package from exact source `f252debc54159d101b344abf0f5631b503b64c16` is installed with revision-5 receipt `3041965CC31751B0F4F04A248932E31CB549A4F48208A63129C1A8C0CACCC516`. All 23 payloads, backup of prior 7c package, saved Native default/tune, 305 nonowned files, raw preferences and four displays passed readback. No launch, opt-in, device or force test. See `components/wheel/docs/STATE.md` and hub `artifacts/woden-private-owner-install-20261013/README.md` for exact evidence; ordinary Shim FFB-open/shared-reader coexistence remains unqualified.

**Earlier owner install (2026-10-13, 15:27 CT):** production-native private HUD-speed candidate source `0ed0292888f29d2152e3a0a77c39e23e43b9d72c`, version `0.2.14+0ed0292888f29d2152e3a0a77c39e23e43b9d72c`, revision-4 receipt SHA-256 `CC7983451ACF140AC742A648D15253B98D3BFF0993815C38AB1D17D8810304A2`. Exact private ZIP SHA-256 `A97B8E8CBD5B17BCC42D07E16106872A7714F78B615AE4AED2EA85F0A9459DBB`; all 17 owned payloads matched manifest `10770DE7...`. The closed-game install retained 306 non-owned files, raw Unity preferences, saved Backend Native and the prior tune byte-exact; no game/force run accompanied installation. A separate temporary load-muted Kenya replay passed 3,601 poses and selected recorded `game.trueSpeed` for all rows, but still frames do not close temporal HUD jitter or physical scanout. Owner-driven ordinary Saved, Shim FFB-open/shared-reader coexistence and physical force remain open. Read `components/wheel/docs/STATE.md` and `HANDOFF.md` before using older installed-state notes below.

**Span and background qualification (2026-10-06):** the 02:05 session-span.1 replay passed all 3,601 poses while naturally unfocused, zero force writes/Forza sends, normal exit and exact restoration. Frames 01/03 show the bonnet drive across three views with centre HUD. Claude promoted the new-config span default to true (explicit false retained) and installed `8678f06/session-span.2`. Plain Steam launch and Surround return now pass: borderless 7680x1440 at (0,0) on Surround, then (-2560,0) on separate monitors. Independent readback verifies all 13 current payloads, 55 original LocalLow files and byte-exact pre/post/current registry exports. Focus-transition/interruption coverage remains open; startup backgrounds still extend onto the side screens (STD-019), and the broader public-release checklist remains. Read docs/2026-10-06-span-qualification.md. Saved FFB remains On; supervised tests must mute outputs independently.

**FFB comparison (2026-10-06):** `docs/FFB-COMPARISON.md` documents the new offline tools and remaining physical acceptance. No installed tune/default was changed. Coordinate normalization with the shared toolkit findings before changing gains.

Release/readiness (2026-10-05): read docs/PUBLIC-RELEASE.md, docs/STAGE-PLAYBACK.md and docs/2026-10-05-background-replay.md. Installed 05837ee/session-focus.1 passed all 3,601 Kenya poses, normal quit/exact restoration, zero physical writes, 111 PS5.1 installer checks. It scopes background execution to validated supervised muted replay; this run remained focused, so background qualification awaits a naturally unfocused coordinated run. Do not move focus or inject OS input to manufacture coverage. The log reports triples at the retained 7680 config; new rendered acceptance is separate. Prior a628e04 owner acceptance and exact backup remain. Private reference is sealed outside worktrees under LocalAppData/Dbce/StagePlayback/references/woden-kenya-20261005. Do not delete results when consolidating source.

# Super Woden Rally Edge coordination

Owner decision, October 5 23:35 CT: publish the whole repository and history
with the final release. This supersedes older private-only instructions once
the checks in docs/PUBLIC-RELEASE.md are complete. Keep visibility private during
preparation; preserve historical tags/artifacts and unmerged cadence work.

Read components/wheel/AGENTS.md and its STATE and HANDOFF before feature work. Their safety, ownership and fixture requirements remain in force.

One game repository, one installable package, one setup and release version. Source folders are internal features, not separately released products. Preserve legacy plugin/adapter IDs, settings, receipts, histories and frozen archives. The shared feature contract remains under review; package metadata is not installed optimizer discovery.

Run tools/game/Verify-Source.ps1 for device-free verification. Stage-Package.ps1 retains source-stage preparation; Package-Delivery.ps1 wraps the exact reviewed baseline into the player ZIP, without installation. Keep the generic tools/delivery/v1 files byte-pinned; only Woden integrity/semantics belong in Woden-Delivery.ps1. Keep proprietary dependencies, recordings and owner settings out of Git/packages. Keep private visibility. Publication, live incorporation and game/device/display work require coordinated owner handoff.

## Toolkit standards

At the start of every session, compare the wheel toolkit's ledger
(`E:\Source\toolkits\dbce-wheel-mod-toolkit\STANDARDS.md`) with this repo's
`TOOLKIT-ADOPTION.md`. Report any entry that is `pending`, `unchecked` or missing
from the adoption file, and bring it in when your work touches that area. When you
adopt one (or find it does not apply), update `TOOLKIT-ADOPTION.md` in the same
commit. When you set a new family-wide standard, append it to the toolkit ledger
and commit it in the same turn; do not leave it only in an uncommitted file.
