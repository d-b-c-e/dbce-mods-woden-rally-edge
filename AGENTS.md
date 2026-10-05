Recording/playback handoff (2026-10-05): owner authorized integration, installation and live recording/playback. Integration 83b72d9 retains both main histories and Claude's feb875b triples. Installed eb02b9c captured 3,601 Kenya SS1 / car 8 poses and original samples; actual ForceSignal reprocess has zero error, zero delivery attempts, normal exit and exact owner restoration. Native cold startup is now a candidate awaiting its first live test. Read docs/2026-10-05-playback-integration.md and docs/STAGE-PLAYBACK.md. The recording was single-screen because Claude left independent monitors active; his accepted triples used Surround. Preserve the active display profile and saved tuning. Do not repeat accepted iRacing launches.

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
