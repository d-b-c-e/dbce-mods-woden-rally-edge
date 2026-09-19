# Shared UX adoption — 2026-09-19

Source **0.2.8**; final deployment identity is recorded in [STATE](STATE.md). The complete option inventory, exact guidance revisions, source/fixture evidence, package identity and remaining acceptance walks are in [UX-OVERNIGHT-2026-09-16](UX-OVERNIGHT-2026-09-16.md).

Implemented for UX-01-S: persistent Simple/Advanced over one settings store; a short Simple Setup; four direct axis rows with full provisional calibration; additive handbrake axis/button input; grouped button bindings; Steering-following/explicit FFB dropdown and saved panic Off; Simple camera adjustment rebinding with conflict/cancel/defaults and release-gated bounded repeats; Advanced force tuning, camera poses, timer assist and diagnostics. Atomic telemetry edits keep the recorder alive; Simple exposes master Off/On and active-capture Stop. Help includes UI scale and a local support summary.

Failed binding saves retain the old effective assignment and exact proposal with Retry/Cancel. Batch camera defaults preflight all conflicts.

Approximate rendered fixtures use actual Panel draw commands and simulated Unity collaborators. They are not game screenshots. Source-linked interaction checks and managed loopback/controller tests are separate from physical acceptance. The owner still starts recordings through the agent workflow; there is no menu Start recording.

| Rule | Remaining gap / acceptance |
|---|---|
| UX-01 / UX-01-S | In-game Simple first use, restart, fallback, custom summaries and editing locks Not tested on candidate. |
| UX-03 | Actual stripped-Unity keyboard focus, text fields, scaling, glyphs, long device names and cursor/pause restoration Not tested; bound panel/Unity menu navigation is implemented and source-tested; live coverage remains Not tested. |
| UX-04 / UX-04-H | Real partial/full/release handbrake, combined axis/button input, disconnect recovery and final game response Not tested. H-pattern and clutch remain Gap. |
| UX-05 / UX-05-D | Strict identity and lifecycle have offline tests. Candidate target switching, sign/load and physical stop/recovery Not tested. Force-feel analysis is retained; no retune. |
| UX-06 / UX-06-K | Saved shortcuts/default scope and held-release behavior have source-linked tests. Actual directions, camera handoff, lens restoration, cross-car hood framing and split-screen remain Not tested. |
| UX-07 / UX-08 | Real loopback/capture preservation and save-error fixtures pass. Receiver integration and actual local support action need a live walk. Support is a summary, not a complete log/config archive; optional markers/open-folder tools remain Gap. |
| UX-09 / UX-10 | Packaged install/update/uninstall passes Windows PowerShell 5.1 fixtures, including replacement rollback, ownership checks and preserved settings/recordings/other mods. Real Steam auto-discovery/folder-picker interaction remains Not tested. |
| UX-11 | An attended first-drive walkthrough remains Not tested. |

Settings view is presentation only. Native HWND ownership, watchdog, exit guards, gates, peak cap, ramp and diagnostic no-force policy are independent of it. No toolkit binary pin changed.
