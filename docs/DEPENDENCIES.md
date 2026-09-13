# Dependency provenance

`lib/toolkit` contains the published **v0.12.0** artifacts from d-b-c-e/dbce-wheel-mod-toolkit: x64 native WheelFfb, managed FFB/Telemetry and force profiles. VERSION and MANIFEST.txt pin every selected file. `tools/Sync-Toolkit.ps1` is copied from that toolkit's current transactional vendor script; its upstream copyright/license is MIT. [dbce-wheel-mod-toolkit, 2026-09-12]

`lib/recording` contains an **unpublished extension** built on toolkit base revision `6f9c662e330af1ab790ca291a799265e0a50ef8a`, from that checkout's dirty recording changes. It is not part of the v0.12.0 download. `provenance.json` pins the individual source-file and built DLL SHA-256 hashes. The consumer commits the built DLL so future Woden builds use that exact artifact without rebuilding a sibling's changing checkout. `tools/Sync-Recording.ps1` is an explicit update operation; inspect the source hashes before committing an updated pin. [woden-rally-edge-wheel, 2026-09-12]

The local loader cache is **BepInEx 6.0.0-be.788+5b766a3, Unity.IL2CPP win-x64**, archive SHA-256 `f4cc496bd098a0df4164b81e3737297707f13a47c2478dba2f60eefab784817a`. The initial bootstrap reused the exact archive cached by iracing-arcade-wheel; fresh machines download the identical official build and verify that hash. Loader files are not committed or included in the plugin ZIP. See [BepInEx source and license](https://github.com/BepInEx/BepInEx).

`lib/core`, `lib/generator-runtime`, `lib/loader` and `lib/interop` are local build/runtime tooling. Woden interop was generated from Woden's own native binary and metadata, with Unity base-library unstripping disabled. No iRacing game assemblies are used. `lib/local-dependencies.json` records local build identity and hashes but is not committed because it describes locally generated proprietary references.

The development ZIP uses an explicit allowlist of six DLLs: our plugin/core, toolkit FFB/Telemetry/Recording and WheelFfb. No Assembly-CSharp, UnityEngine modules, Il2Cpp shims, decompiled game code, game assets, test fixtures or owner recordings are distributed.
