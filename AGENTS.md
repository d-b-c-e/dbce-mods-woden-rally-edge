Recording/playback handoff (2026-10-05): owner has ended Claude's triple session and authorized integration, installation and recording/playback work. Local main 95974ec already included feb875b triples and 86cbc6d playback; integration 83b72d9 adds origin/main 16b144e without dropping either history. Read docs/2026-10-05-playback-integration.md and docs/STAGE-PLAYBACK.md. Preserve the existing display profile and saved tuning; output is muted for stage sessions while original telemetry remains available. The 116-second historical drive lacks scene/setup identity. Fresh capture now observes native scene requests/selection and has supervised exit/restoration; cold-launch playback remains under implementation. Do not repeat accepted iRacing launches.

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
