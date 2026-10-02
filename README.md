# DBCE mods for Super Woden Rally Edge

One game package combines wheel/pedal input, provisional FFB, camera controls and telemetry, retaining existing plugin identity, settings and installer transaction. Private published source is **0bb5784**; the frozen local package candidate is **0.2.14-shutdown.1**. It has not been published as a binary download or installed. The installed build remains **0.2.12 / bf58042**. Read [setup](docs/GAME-SETUP.md), [release policy](docs/RELEASE-STRUCTURE.md) and [exact delivery provenance](docs/DELIVERY.md).

The internal [triple probe](components/triple/README.md) is default off, uninstalled and unverified. It is excluded from the installable candidate; Surround and separate-display presentation remain unimplemented.

Run tools/game/Verify-Source.ps1 for device-free source verification when its private dependencies are available; [asset-free CI](tools/ci/README.md) covers a narrower managed subset. tools/game/Build-ShutdownDelivery.ps1 prepared the frozen **0.2.14-shutdown.1** candidate with consumer **e3d118e**, shared native **a51bed9**, packaging **4286797** and retained installer **14740fd**. Preserve that exact archive; do not regenerate it from current HEAD. tools/game/Package-Delivery.ps1 and Stage-Package.ps1 retain the historical **0.2.13-delivery.1** baseline route. Published source and passing CI do not publish or certify a player binary. Package review fixtures and future attended rig/visual acceptance are distinct.

Delivery metadata does not implement optimizer discovery or dual-renderer migration. Preserve legacy history, settings and frozen releases. The verified renamed repository **d-b-c-e/dbce-mods-woden-rally-edge** remains private; its stable package ID remains **dbce-mods-super-woden-rally-edge**.
