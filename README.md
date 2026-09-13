# Woden Rally Edge Wheel

A Windows mod for **Super Woden Rally Edge** that connects the game's vehicle simulation to a racing rig. Detailed, trustworthy telemetry is the first milestone; direct wheel controls, force feedback and bonnet/bumper views are the feature targets.

**0.2.2 development build.** The 0.2.1 level-entry crash fix ran successfully; the owner then reported no driving response. 0.2.2 moves input to the game's action-reading boundary, adds the normal bonnet/bumper cycle and camera/FFB handoff, and starts adopting the shared UX standard. Effective wheel handling, new views and physical FFB still need an attended retest. Read [the evidence and next steps](docs/STATE.md).

| Area | Current implementation |
|---|---|
| Telemetry | Game sampler, versioned channels, 324-byte Forza output, detailed JSON UDP and live bounded capture. Sustained real car/contact sampling observed; signal scales/coverage still need a recording. |
| Wheel input | Exact-GUID devices, interactive steering/pedal calibration, inversion/deadzone, button capture and reconnect. R12 axes read live in F6; effective driving input still needs verification. |
| Camera | Bonnet and bumper extend the normal camera-button cycle; held Look behind and game-camera handoff stop/restore behavior. Compiled; placement, clipping, transitions and split-screen behavior untested. |
| Force feedback | Provisional front-contact/slip alignment estimate and steering damping through the toolkit. Session arming, exact FFB wheel, watchdog, exit guards, ramp, peak cap and driving gates. Physical direction/load normalization unverified. |
| Setup UI | **F6**: Setup, Controls, FFB, Cameras, Telemetry, Help. **F8**: Stop FFB, latched until manually started again. Settings and bindings save live. See [UX adoption gaps](docs/UX-ADOPTION.md). |

The initial build supports Steam app **3218630**, build **21802346**, Unity **6000.3.6f1**, x64 IL2CPP metadata **39**. The plugin checks the exact `GameAssembly.dll` SHA-256 before patching.

## Build and inspect

Requires the .NET 10 SDK, the installed Windows game, and PowerShell 7. The plugin targets .NET 6 for BepInEx's bundled runtime; the core has no Unity dependencies.

```powershell
.\tools\Initialize-Dependencies.ps1
dotnet build WodenRallyEdgeWheel.sln -c Release -warnaserror
dotnet run --project tests\WodenRallyEdge.Tests -c Release
.\tools\Package.ps1
```

Initialization downloads the pinned BepInEx archive and generates **this game's** references offline. It does not install anything in the game or launch it. `lib/core`, `lib/interop` and loader caches are ignored by Git. Our separately pinned toolkit binaries are committed, so building does not depend on dirty sibling checkouts.

```powershell
dotnet run --project tools\TelemetryInspector -c Release -- schema artifacts\telemetry-schema.json
dotnet run --project tools\TelemetryInspector -c Release -- inspect path\to\session.jsonl
dotnet run --project tools\TelemetryInspector -c Release -- devices lib\toolkit\native
```

The device command only enumerates; it does not create force effects. The session inspector validates the complete file and reports channel coverage, ranges and sample gaps. A successful replay validates a recording's structure, not physical handling or force feel.

Installation is a separate deliberate step: [development build instructions](docs/DEVELOPMENT-BUILD.md). The initial installer refuses a running game, unsupported builds, conflicting loaders and existing Woden plugin installations.

## Design references

- [dbce-wheel-mod-toolkit](https://github.com/d-b-c-e/dbce-wheel-mod-toolkit): native device/force lifecycle, Forza encoder, signal conventions and diagnostic recording.
- [art-of-sim-rally](https://github.com/d-b-c-e/art-of-sim-rally): calibrated physical devices, measured contact signals, lifecycle cleanup, recording/evidence discipline and mounted-camera restoration.
- [iracing-arcade-wheel](https://github.com/d-b-c-e/iracing-arcade-wheel): Unity 6 IL2CPP/BepInEx setup, stripped Unity API handling, real input bindings and owner-facing feature baseline.

See [telemetry semantics](docs/TELEMETRY.md), [feature plan](docs/ROADMAP.md), [build findings](docs/RESEARCH.md) and [dependency provenance](docs/DEPENDENCIES.md).
