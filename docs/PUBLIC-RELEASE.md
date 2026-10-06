# Public release preparation — 2026-10-05

## Whole-repository publication decision

The owner decided on October 5 that the **whole repository, including source
history, becomes public with the release**. Historical private-only instructions
are superseded by that decision; keep visibility private while completing the
release checks below. Publication has not happened.

The October 5 final inventory at `379be9a` covered 81 reachable commits and 651
unique blobs after fetching origin. Private complete-history Git bundles were
verified outside the repository. The six remote `codex/` branches and
`pm/reviewed-woden-unified-20261001` are ancestors of main and can be retired at
release freeze after rechecking their exact OIDs. Preserve
`pm/reviewed-woden-cadence-20261001`: its two unique commits contain an unmerged
experimental force adapter. Do not merge it solely for cleanup. Keep the
historical `v0.2.14-shutdown.1` tag unchanged; its release is a draft, not a
published stable release.

Five path-changing historical commits contain local build paths in vendored
DLL debug metadata; the current FFB DLL still has one. The publication plan
recommends retaining immutable history/pins and generalizing future build paths,
rather than rewriting accepted artifacts for username-only paths. Complete the
historical per-revision license/source/notice matrix, including toolkit overrides
and the separately pinned recorder. Inspect repository content and GitHub logs,
PRs and release assets as well as the final player ZIP. A bounded credential
screen is complete; full license/content closure remains open.

Rebuild a fresh final player archive from final source, verify its standalone
installer and notices, finish the targeted owner drive, then publish the exact
tested archive/checksum and verify its public download. Preserve prior packages.
The shared startup span helper is installed and defaults on in session-span.2
after a complete separate-monitor replay with inspected frames. Explicit saved
opt-outs remain intact. Ordinary Steam startup and the return to Surround passed
on session-span.2 (October 6, 03:24/11:34). The owner's plain-launch drive and
feature checks below remain separate from those startup/window-placement passes.

## Current candidate

The owner requested documentation, simple setup and release readiness. This repository is still private; no new binary has been published. Installed candidate `0.2.14+8678f06fa967e0985d963315b72ef879c79e3235`, player archive `session-span.2`, promotes the new-config span default after session-span.1 passed all 3,601 Kenya poses while naturally unfocused with inspected triple-view frames. All 13 current payloads, 60 owner files and 25 raw preferences match. Claude's package gate passed 42 suites / 37,760 assertions, 630 UI assertions and all 111 PS5.1 exact-package installer checks. See [exact package/install/runtime evidence](2026-10-06-span-qualification.md). Earlier accepted packages remain backed up.

## Passed

- First unattended Kenya SS1 / car 8 playback: 3,601 poses, max position error 0.00006103515625 m and rotation error 0.0000172453823 degrees. Native launch/menu/stage selection, camera changes, normal exit and exact restoration passed. Owner confirmed playback worked.
- Original ForceSignal analysis: 3,553 valid samples, 3,477 nonzero, zero numeric error and zero physical delivery attempts. Pose playback is not physics resimulation.
- Installed runtime package gates: 42 suites / 37,760 assertions, 580 UI assertions, zero compiler warnings/errors. Existing installer passed 111 PowerShell 5.1 checks before the terminal simulation hold change.
- Install.bat and Uninstall.bat already implement Steam discovery, hashes, backups, ownership and settings retention; no new installer framework is needed.

## Final checks

| Item | Status / next action |
|---|---|
| Expected triples on normal launch | Session-span.1 passed all 3,601 poses while naturally unfocused with inspected frames. Session-span.2 plain Steam launch and Surround return passed: borderless at (0,0) on Surround, then (-2560,0) on separate monitors; original settings/saves and 13 payloads independently verified. The owner's plain-launch drive remains; startup side backgrounds keep STD-019 partial. See the span qualification report. |
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

The former playback worktree was archived at `%LOCALAPPDATA%/Dbce/SessionEvidence/woden-20261005/session-playback-worktree.zip` (SHA-256 `423CE47E9731F67E33AB22BDCA50F34D448FEE194818FA8666B3A412BE616A13`), with all 3,902 files byte-verified. Its Git worktree/branch are retired; archived file leftovers remain after automated cleanup was blocked. The player ZIP, checksum, original receipt and both validation logs were also copied unchanged to the canonical checkout paths above. The receipt retains its original build location. The canonical TelemetryInspector successfully reviewed the stable private reference after consolidation.

A bounded reachable-history filename scan found no Assembly-CSharp, GameAssembly, UnityEngine DLL, .env, registry backup or matching save/recording paths. Complete content/license review remains a publication check.

The October 5 content screen at source a10d419 read all 645 reachable Git blobs
(6,724,765 bytes) and all 20 session-focus.1 player ZIP files. Nine historical
PE blobs were the vendored toolkit recording/FFB/telemetry/playback revisions;
no historical ZIP blobs were present. The defined game-assembly/save/recording
path patterns and narrow AWS/GitHub-token/private-key patterns produced no
candidates. Private receipt: results/release-prep-20261005/content-screen.json.
The player archive includes MIT notices and the pinned first-install loader
source/download notice, without bundling the loader. This is a bounded content
screen, not an exhaustive privacy or license certification. Final publication
and downloaded-byte verification remain separate.
