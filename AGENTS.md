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
