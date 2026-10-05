Recording/playback continuation (2026-10-05): owner is working on Woden triples in a separate session and owns the screen. Keep this work offline; do not launch/deploy or change display/input/settings. Read docs/2026-10-05-playback-offline-review.md and docs/STAGE-PLAYBACK.md. Original owner drive recovered: 6,981 contiguous driving poses, original source hash verified, scene/setup not captured. Source adds sealed read-only stage context, shared driving exclusions and a mode/output banner. Zero-warning build, 38/994 regressions, 556 existing UI assertions and eight inspection tests pass; stage hooks/banner remain unqualified and unpackaged. Native ReplayMovie stores presentation frames but does not supply a proven durable replay/startup workflow. Preserve concurrent triple work before integration. Do not repeat accepted iRacing launches.

# Super Woden Rally Edge coordination

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
