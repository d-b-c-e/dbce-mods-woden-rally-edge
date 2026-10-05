# Public release preparation — 2026-10-05

The owner requested documentation, simple setup and release readiness. This repository is still private; no new binary has been published. The installed candidate is `0.2.14+a628e04c2101866a2a22335004f4f536aa565d93`, installed at 05:18:06 UTC with existing settings retained.

## Passed

- First unattended Kenya SS1 / car 8 playback: 3,601 poses, max position error 0.00006103515625 m and rotation error 0.0000172453823 degrees. Native launch/menu/stage selection, camera changes, normal exit and exact restoration passed. Owner confirmed playback worked.
- Original ForceSignal analysis: 3,553 valid samples, 3,477 nonzero, zero numeric error and zero physical delivery attempts. Pose playback is not physics resimulation.
- Installed runtime package gates: 42 suites / 37,760 assertions, 580 UI assertions, zero compiler warnings/errors. Existing installer passed 111 PowerShell 5.1 checks before the terminal simulation hold change.
- Install.bat and Uninstall.bat already implement Steam discovery, hashes, backups, ownership and settings retention; no new installer framework is needed.

## Final checks

| Item | Status / next action |
|---|---|
| Expected triples on normal launch | Open: combined playback was single-screen. Claude's prior accepted triples used Surround. Prepare the selected monitor profile and game preferences before launch; use one profile change with settle time and a display watchdog. Preserve the corrected normal-play configuration afterwards. |
| Public player archive | `Prepare-PublicRelease.ps1` builds a local candidate with the current quick-start guide, notices, checksums and existing installer. Run exact-package installer checks, then retain immutable outputs. |
| Current feature coverage | Owner accepted this one reference. Stop/focus-loss/pause and wider car/stage/device coverage remain explicit limits. |
| Repository visibility | Review reachable source history, tracked binaries and dependency notices before making private source public. No game assemblies, generated interop, saves or recordings in public artifacts. |
| Publication | Choose final version, tag clean tested source, publish exact archive/checksum, download and hash-check. Do not retag or overwrite frozen candidates. |

```powershell
./tools/game/Prepare-PublicRelease.ps1 -ArtifactLabel preview.1
```

The older `Package-Delivery.ps1` deliberately repacks a frozen 0.2.13 baseline; do not use it for this newer runtime. `components/wheel/tools/Package.ps1` is the active runtime builder. The new wrapper adds player documentation around that builder's exact payload and existing installer.

The known-good private reference is now outside disposable worktrees at `%LOCALAPPDATA%/Dbce/StagePlayback/references/woden-kenya-20261005`; all seven sealed files were copied and hash-verified. It must remain private. Archive private evidence before removing any finished worktree.

## Prepared player candidate

`WodenRallyEdge-0.2.14-preview.1.zip` was built from clean `e534231fd9ed6b5205164322b8f3ef2510944e16`; SHA-256 `1200E49856C353701ED36221FE506D8C5FE23B50CD0B54DF051B08387D988935`. The exact extracted player package passed all **111 installer checks under stock Windows PowerShell 5.1**. Runtime build passed 42 suites / 37,760 assertions plus 580 UI assertions with zero warnings/errors. The candidate adds the player quick-start and dependency notices. It is neither installed nor published; the accepted installed runtime remains the a628e04 candidate above. Private logs: `components/wheel/artifacts/public-preview-1*.log`. Package receipt and checksum are beside the ZIP under `components/wheel/dist`.
