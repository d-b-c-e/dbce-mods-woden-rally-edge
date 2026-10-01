# DBCE mods for Super Woden Rally Edge

The [wheel component](components/wheel/README.md) contains wheel/pedal input, provisional force feedback, camera controls and telemetry. Its version remains **0.2.12**. Read the shared [game setup](docs/GAME-SETUP.md) and [release policy](docs/RELEASE-STRUCTURE.md).

The [triple component](components/triple/README.md) preserves the independent **0.0.1** default-off private-target probe from research source `d4ce04a`. It has not been installed, run or visually accepted. Separate-display and Surround presentation remain unimplemented.

Build and package wheel from `components/wheel`; root `tools/game/Verify-Source.ps1` verifies both components without devices. Triple rendering remains blocked pending runtime binding validation and presentation implementation; widescreen or camera FOV alone is insufficient.

This source reorganization preserves component IDs, version, installed packages and historical archives. It does not install anything or establish wheel/FFB, telemetry or rendered acceptance. The repository remains private.
