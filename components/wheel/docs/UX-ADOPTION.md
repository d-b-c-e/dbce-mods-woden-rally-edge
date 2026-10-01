# Shared UX adoption — 2026-09-19

Installed **0.2.10 / 25797b3**, installer revision 3 from `e5da2d5`; verified deployment identity is recorded in [STATE](STATE.md). All nine payloads match and all four owner files were preserved byte-exact. The complete option inventory, exact guidance revisions, source/fixture evidence, package identity and remaining acceptance walks are in [UX-OVERNIGHT-2026-09-16](UX-OVERNIGHT-2026-09-16.md).

Guidance `94dc3a1` adds explicit opening-edge and direct-shortcut acceptance. Source audit confirmed direct legacy keyboard consumers on the startup message and title bypass 0.2.9 isolation. Installed 0.2.10 addresses these specific paths; independent source review closed and 532 UI assertions pass. **Live acceptance NOT RUN because the user paused desktop testing.** See [startup input isolation](STARTUP-INPUT-ISOLATION.md) for the evidence and pending walk.

Installer revision 3 passed independent review and 84 PowerShell 5.1 checks, including actual Install.bat with omitted PackageRoot, spaced paths and a different working directory. The real closed-game installation used the corrected omitted-root route. All existing artifacts remain immutable; the delivery ZIP is the distinct installer-r3 package.

**0.2.9:** guidance `12df6b325d770625baffd75b2d2eb74f1fcd0a8c` adds verified-source stock producer suppression and shared close/capture release gating. 506 source-linked UI assertions cover configured action aggregation, existing reference neutralization, held keys/pointer/wheel/Settings/stock controls, fresh/failed/stale reads, F6 fallback/deduplication and effective input cleanup after a primary-device failure. Full game dispatch coverage is still a live check. The 0.2.8 diagnostic launch failed to open F6 through injected keys; zero force writes, normal shutdown and exact config restoration were verified. Installer-r2 passes 81 PowerShell 5.1 assertions and has its own frozen package identity.

The force-disabled **0.2.9 live check** verified IMGUI F6 opening, Simple Setup and Advanced rendering at 4K, provisional steering calibration Cancel, and release-to-close. Stock producer reads reached 437,436; no force writes or plugin input exceptions occurred. Both binding files remained byte-identical during the run; the presentation change was reverted with the complete original configuration after normal exit. Capture completed without drops/errors. This is limited menu acceptance: opening-edge isolation, Tab/Enter/held-controller overlap, selected EventSystem restoration, stock-screen coverage, restart persistence and physical control behavior remain pending.

Implemented for UX-01-S: persistent Simple/Advanced over one settings store; a short Simple Setup; four direct axis rows with full provisional calibration; additive handbrake axis/button input; grouped button bindings; Steering-following/explicit FFB dropdown and saved panic Off; Simple camera adjustment rebinding with conflict/cancel/defaults and release-gated bounded repeats; Advanced force tuning, camera poses, timer assist and diagnostics. Atomic telemetry edits keep the recorder alive; Simple exposes master Off/On and active-capture Stop. Help includes UI scale and a local support summary.

Failed binding saves retain the old effective assignment and exact proposal with Retry/Cancel. Batch camera defaults preflight all conflicts.

Approximate rendered fixtures use actual Panel draw commands and simulated Unity collaborators. They are not game screenshots. Source-linked interaction checks and managed loopback/controller tests are separate from physical acceptance. The owner still starts recordings through the agent workflow; there is no menu Start recording.

| Rule | Remaining gap / acceptance |
|---|---|
| UX-01 / UX-01-S | Simple Setup, Advanced rendering and provisional calibration Cancel verified live; first-use/restart, custom summaries and all edit locks remain Not tested. |
| UX-03 | F6 IMGUI fallback and release-to-close verified live; keyboard focus, text fields, scaling, long names, cursor/pause restoration, held controls and stock-screen dispatch remain Not tested. |
| UX-04 / UX-04-H | Real partial/full/release handbrake, combined axis/button input, disconnect recovery and final game response Not tested. H-pattern and clutch remain Gap. |
| UX-05 / UX-05-D | Strict identity and lifecycle have offline tests. Candidate target switching, sign/load and physical stop/recovery Not tested. Force-feel analysis is retained; no retune. |
| UX-06 / UX-06-K | Saved shortcuts/default scope and held-release behavior have source-linked tests. Actual directions, camera handoff, lens restoration, cross-car hood framing and split-screen remain Not tested. |
| UX-07 / UX-08 | Real loopback/capture preservation and save-error fixtures pass. Receiver integration and actual local support action need a live walk. Support is a summary, not a complete log/config archive; optional markers/open-folder tools remain Gap. |
| UX-09 / UX-10 | Packaged install/update/uninstall passes Windows PowerShell 5.1 fixtures, including replacement rollback, ownership checks and preserved settings/recordings/other mods. Real Steam auto-discovery/folder-picker interaction remains Not tested. |
| UX-11 | An attended first-drive walkthrough remains Not tested. |

Settings view is presentation only. Native HWND ownership, watchdog, exit guards, gates, peak cap, ramp and diagnostic no-force policy are independent of it. No toolkit binary pin changed.
