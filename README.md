# DBCE mods for Super Woden Rally Edge

One game package combines wheel/pedal input, provisional FFB, camera controls and telemetry. The next isolated source candidate is **0.2.13**, retaining existing plugin identity, settings and installer transaction. Read [setup](docs/GAME-SETUP.md) and [release policy](docs/RELEASE-STRUCTURE.md).

The internal [triple probe](components/triple/README.md) is default off, uninstalled and unverified. It is excluded from the installable candidate; Surround and separate-display presentation remain unimplemented.

Run tools/game/Verify-Source.ps1 for device-free source verification. tools/game/Stage-Package.ps1 retains the historical source-stage route. The player delivery authority is tools/game/Package-Delivery.ps1: it wraps the reviewed 14740fd stage into separately versioned **0.2.13-delivery.1**, with pinned common delivery contract v1, package-local instructions and a frozen ZIP. It retains every runtime/installer payload byte and records runtime versus packaging provenance separately. tools/game/Test-Delivery.ps1 verifies real ZIP fixtures. See [delivery and provenance](docs/DELIVERY.md).

Delivery metadata does not implement optimizer discovery or dual-renderer migration. Preserve legacy history, settings and frozen releases. This repository remains private; intended name dbce-mods-woden-rally-edge differs explicitly from stable package ID dbce-mods-super-woden-rally-edge.
