# DBCE mods for Super Woden Rally Edge

Wheel and pedal controls, force feedback, telemetry, mounted cameras and triple-screen views in one mod. Public-release preparation is in progress; the current installed 0.2.14 candidate includes the combined recording/playback and triple-screen work.

Extract the complete player ZIP and run **Install.bat** with the game closed. The installer finds Steam, verifies files, backs up replacements and preserves settings. First install downloads the exact supported BepInEx loader. Launch from Steam and use **F6** for setup. **F8** saves feedback Off. See [setup](docs/GAME-SETUP.md) and [public-release checklist](docs/PUBLIC-RELEASE.md).

The owner recorded and accepted a one-minute Kenya SS1 playback, including camera changes. All 3,601 poses passed; original force samples reprocess with zero error while physical wheel/network outputs stay muted. The [operator guide](docs/STAGE-PLAYBACK.md) documents unattended startup, Stop, normal exit and restoration.

Triple views were accepted in the earlier Surround drive. The combined runtime subsequently completed all 3,601 replay poses at 7680×1440 with triples active. Separate monitors currently need the developer Launch-Wide script; automatic startup spanning is being implemented. A final plain-launch owner drive and dashboard RPM/gear check remain.

One repository and package contain the plugin. Game-specific code is under `components/wheel`, shared playback artifacts under `vendor/playback`. Proprietary game files and private recordings are excluded from releases.
