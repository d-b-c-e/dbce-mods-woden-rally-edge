# Woden Rally Edge Wheel

A Windows mod for **Super Woden Rally Edge** that connects the game's vehicle simulation to a racing rig. Detailed, trustworthy telemetry is the first milestone; direct wheel controls, force feedback and bonnet/bumper views are the feature targets.

**0.1.0 development foundation — compiled and tested offline, never run in Woden yet.** This is a private development repository, not a finished wheel-support release. Read [the evidence and next steps](docs/STATE.md) before treating an integration as verified.

| Area | Current implementation |
|---|---|
| Telemetry | Game sampler, versioned channel dictionary, standard 324-byte Forza output, detailed JSON UDP, bounded numeric recordings and an offline inspector. Real game sampling still needs validation. |
| Wheel input | Opt-in direct physical-device axes, exact GUID selection, per-axis calibration, temporary override around the car update and stock-input restoration. Compiled; not driven. |
| Camera | Opt-in bonnet transform override with restoration. Compiled; placement, clipping, culling and split-screen behavior untested. |
| Force feedback | Toolkit device/force library pinned. **No force model or torque output implemented.** First validate wheel contact signals and stock Logitech ownership. |
| Setup UI | Config files and a device-list command. Interactive binding/calibration/settings UI is planned. |

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
