# Woden Rally Edge Wheel

A Windows mod for **Super Woden Rally Edge** that connects the game's vehicle simulation to a racing rig. Detailed, trustworthy telemetry is the first milestone; direct wheel controls, force feedback and bonnet/bumper views are the feature targets.

**0.2.9 private development build installed.** Simple/Advanced settings separate basic setup from detailed tuning, with direct axis calibration, additive handbrake axis/button input, a Steering-following FFB dropdown, camera shortcut handling and recording-preserving telemetry edits. The successor adds stock input suppression while settings owns input, release checks before closing/capture, and F6 diagnostics/fallback. A force-disabled live check verified F6 opening, Advanced rendering, calibration Cancel and release-to-close; broader menu isolation and physical gameplay acceptance remain pending. See the [option inventory and evidence](docs/UX-OVERNIGHT-2026-09-16.md).

 Wheel controls and camera changes are now allowed during the start-line countdown, including throttle/revs and steering. The game retains its start lock and FFB still requires racing. Advanced → Driving provides an optional single-player countdown/time-limit assist (Off by default, saved speed 75%); lap/stage timing remains normal. Original FFB gain and the 50% default remain; the owner described the current feel as a good starting point but noted possible lingering force in turns. Read [the evidence and next steps](docs/STATE.md).

| Area | Current implementation |
|---|---|
| Telemetry | Game sampler, versioned channels, 324-byte Forza output, detailed JSON UDP and live bounded capture. Sustained real car/contact sampling observed; signal scales/coverage still need a recording. |
| Wheel input | Exact-GUID calibration, inversion/deadzone, buttons and reconnect. Bindable panel/Unity menu navigation; screens without compatible handlers retain keyboard/controller fallback. Handbrake accepts independent additive button and calibrated axis bindings; proportional rear braking/grip-loss adaptation is awaiting a drive. |
| Camera | Bonnet and bumper extend the normal camera-button cycle; held Look behind and game-camera handoff stop/restore behavior. Body-fitted bonnet, manual offsets/FOV, shorter near clip and rebindable keys/buttons. New framing, transitions and split-screen behavior await acceptance. |
| Force feedback | Provisional contact/slip estimate and damping through the toolkit. Saved On/Off, default On for new settings; exact wheel, watchdog, exit guards, ramp, peak cap and driving gates. Physical direction/load normalization unverified. |
| Setup UI | **F6**: Setup, Controls, FFB, Cameras, Telemetry, Help. **F8** saves FFB Off until On is selected. Recording is prepared outside the menu. See [UX adoption gaps](docs/UX-ADOPTION.md). |

The initial build supports Steam app **3218630**, build **21802346**, Unity **6000.3.6f1**, x64 IL2CPP metadata **39**. The plugin checks the exact `GameAssembly.dll` SHA-256 before patching.

## Build and inspect

Continuing in another session? Read [STATE](docs/STATE.md), then the
[handoff guide](docs/HANDOFF.md) for installed-build evidence, source entry
points, retained local artifacts and the next attended test.

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

Player install/update and receipt-based uninstall are available through `Install.bat` / `Uninstall.bat` in the ZIP; they need no SDK. Updates back up the existing plugin/configuration and verify preserved settings. See [installation and recovery details](docs/DEVELOPMENT-BUILD.md). The repository-only `Install-Dev.ps1` remains an initial-install helper.

## Design references

- [dbce-wheel-mod-toolkit](https://github.com/d-b-c-e/dbce-wheel-mod-toolkit): native device/force lifecycle, Forza encoder, signal conventions and diagnostic recording.
- [art-of-sim-rally](https://github.com/d-b-c-e/art-of-sim-rally): calibrated physical devices, measured contact signals, lifecycle cleanup, recording/evidence discipline and mounted-camera restoration.
- [iracing-arcade-wheel](https://github.com/d-b-c-e/iracing-arcade-wheel): Unity 6 IL2CPP/BepInEx setup, stripped Unity API handling, real input bindings and owner-facing feature baseline.

See [telemetry semantics](docs/TELEMETRY.md), [feature plan](docs/ROADMAP.md), [build findings](docs/RESEARCH.md) and [dependency provenance](docs/DEPENDENCIES.md).
