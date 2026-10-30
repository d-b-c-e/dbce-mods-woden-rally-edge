# Clean-install lane, Phase A — 2026-10-30 (vemi)

File level only, on a disposable mod-free copy of the installed game (744 files, 4.5 GB, without `BepInEx`, `dotnet`,
`WodenWheelBackups`, `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`). No game launch; the owner install was
only read. Script: `tools/Test-CleanInstallCopy.ps1`; result: `clean-install-phase-a-20261030.json`.

Candidate `0.2.16+60be56c` (private-shim package `8F47F49C...`), rollback target `0.2.16+e305a76` (`8AE3D0D5...`), loader
from the pinned local archive `lib/loader/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788+5b766a3.zip` (no download).

**17/17 PASS:** install exit 0, loader placed, 23 payloads byte-exact, receipt `curvedTriples` true, no original game
file added-over, removed or changed; uninstall exit 0, original files unchanged, every payload removed, leftovers only the
shared loader (incl. its root `changelog.txt`), backups and receipt, as `Manage-Install` intends (`sharedLoaderRetained`);
reinstall, rollback to e305a76 and forward to 60be56c each exit 0 with byte-exact payloads; original game files unchanged
at the end. The first run's single failure was the script's leftover filter missing the loader's `changelog.txt`; fixed
and re-run on a fresh copy.

Not covered: per-user state isolation tooling for Woden (Art's `ArtUserState.ps1` is Art-specific) and **Phase B**
(first launch of a clean copy), which stays blocked on the owner's Steam Cloud decision.
