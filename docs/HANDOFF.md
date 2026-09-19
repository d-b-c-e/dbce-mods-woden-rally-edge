# Session handoff — 2026-09-19

Read [STATE](STATE.md) first, then [the complete option inventory and acceptance report](UX-OVERNIGHT-2026-09-16.md). Current work is **0.2.8**, on `codex/ux-simple-advanced-0.2.8`; prior dirty work is preserved separately in baseline commit `9d28b83` and ignored `artifacts/ux-baseline-20260917-001141`. The owner authorized completion, build, commit and closed-game deployment. Final identity/receipt is recorded in STATE when verified.

Simple is the default; Advanced Driving contains the optional timer assist. Handbrake axis/button contributions are additive. Binding/Clear/default proposals persist before becoming effective; failed saves offer Retry/Cancel. Calibration cannot save a disconnected candidate. New numpad 1/3 tilt down/up defaults preserve existing saved/implicit mappings until explicitly reset. Menu buttons target the panel or existing selected Unity UI handlers; native screen coverage remains untested. Agent recording stays external; active captures have Stop and survive UDP/view edits.

Offline checks: 35 regression suites / 942 assertions, 459 source-linked UI assertions and 59 Windows PowerShell 5.1 player-installer checks. Source-linked fixtures and approximate renders do not establish native/physical acceptance. Preserve original FFB tuning, HWND/watchdog/exit guards, owner camera view and countdown restrictions. No game was launched for this work.

The following paragraphs preserve earlier 0.2.5–0.2.7 evidence in chronological order; current installation/configuration is summarized in STATE.

The owner called 0.2.5 a good starting point but reported input/camera blocked before green and possible lingering cornering force. 0.2.6 allows normal calibrated wheel and camera input during WARMING, preserving native start-line physics/lock. Driving/FFB remain restricted to RACE. The force model/tune remains unchanged pending better evidence. The latest owner log ended normally with 5,666 FFB writes and zero failures. The owner also selected countdown/time-limit assistance, not elapsed stage timing: Setup → Difficulty adds a saved Off-by-default assist at 75% speed (25–100% range). It advances the owned single-player CountDown anchor before native expiry, preserving checkpoint additions and lap/stage clocks. Offline hook/config tests pass; gameplay acceptance remains open.

The latest 0.2.6 drive had zero force writes: native initialization replaced its owned HWND with a foreign foreground window and failed exit-guard installation. 0.2.7 supplies a captured/revalidated owned Unity HWND explicitly, waits for window readiness before any device work and keeps every force gate. The toolkit binary pin remains unchanged; the shared source bug is documented in its knowledge base. The owner's bonnet correction is +0.15 m up / +0.05 m forward relative to the second vehicle fit; this now informs fitted defaults, while the exact saved manual view is preserved. Current saved force strength is 75.208336%, cap 25%, Enabled=true. Deployment details and pending acceptance are in STATE.

Latest 0.2.7 run: owned HWND / R12 initialization / 150 ms watchdog / exit guards all succeeded at 23:32:20.715. PanicStop occurred at 23:32:49.644 before racing, saved FFB Off, and the whole drive stayed Off (zero writes/failures). No wheel Panic stop binding exists; F8 or sidebar Stop FFB are possible, but the log lacks trigger attribution. Current strength 49.583332%, cap 25%, Enabled=false; countdown assist On/50%. No settings were changed. Ask the owner to choose F6 → FFB → On and close settings for the next drive; do not silently clear saved panic Off. See STATE for retained evidence.

Newest run supersedes the Off diagnosis above: owner felt improved FFB after selecting On; 6,723 writes / zero failures. Saved strength 49.583332%, cap 25%, smoothing 35 ms, damping 0.05. Native commands show 38.89% of driving time at the cap, longest 4.049 s. Managed-only step experiment shows 200–217 ms capped-force release. These support saturation plus conditioning as plausible contributors to lingering force; no synchronized input/slip capture exists. Read FFB-FEEL-ANALYSIS.md. No tune was changed.

## Owner decisions

FFB is one saved On/Off setting, default On and 50% strength for new settings, with the original output gain restored in 0.2.5. F8/Stop saves Off until On is selected. There is no session-start requirement. Preserve exact GUID, stock ownership, all driving/contact/camera gates, watchdog, ramp and peak cap. Unattended physical output remains prohibited.

Recording belongs to the agent workflow: tools/Start-RecordedGame.ps1 requests one launch and starts Steam; normal exit finalizes the capture. It defaults to diagnostic force suppression and never changes the saved FFB preference. Use -AttendedFfb only for an explicitly requested attended run. -PrepareOnly stages the expiring request without launching. No menu recording controls.

E-Brake accepts independent Button and Axis contributions on the existing Controls pages; the greater contribution wins in 0.2.8. The new analog adaptation scales native rear braking/grip loss; native throttle cut remains binary. Full pull and stock buttons retain stock behavior. Device input bars/calculation tests are not a physical response test.

## Paths and implementation

Game root: D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge. Supported Steam build 21802346 / Unity 6000.3.6f1 / metadata 39. Read DEVELOPMENT-BUILD for full hash and usage.

- Installed plugin/receipt: BepInEx/plugins/WodenRallyEdgeWheel.
- Config/bindings: BepInEx/config/dbce.wodenrallyedgewheel.cfg and wheel-bindings.json.
- Evidence: BepInEx/LogOutput.log, ErrorLog.log, and %LOCALAPPDATA%/DbceWheel/ffb.log.
- Recordings: BepInEx/WodenRecordings. Requests: BepInEx/config/woden-record-next-launch.json, consumed once.
- Backups: WodenWheelBackups for the packaged 0.2.8 installer; historical backups remain under BepInEx/WodenBackups. Both are outside plugin scanning.
- Build/package: classic solution, both executable test harnesses, tools/Package.ps1. Packaged Install.bat updates with backups; Uninstall.bat removes only receipt-owned unchanged payload. Repository Install-Dev.ps1 remains initial-only.
- FFB: ForceController + ToolkitForceDevice; actual controller is linked into the offline tests with fake collaborators. Do not replace toolkit native output.
- Controls: scoped Controls.FixedUpdate action-table lease; boxed action values need indexer writeback. AnalogHandbrake scopes only selected-player native calls and restores game tuning.
- Timing: TimingDiagnostics records frame/poll/control/car/sampler/force durations plus gear/shifting.
- WheelContact uses GC-rooted IL2CPP value storage. NEVER call the generated GetGroundHit(out WheelHit) wrapper.

The ignored artifacts contain native disassembly, offsets, offline audit output and historical deployment helpers. The native handbrake takes a RELEASED boolean, applies 2000 brake torque to rear wheels above 30 rpm, writes configured grip loss, and uses a binary 0.3 acceleration multiplier. The adaptation scales native results, restores the tuning field, and does not change the engine multiplier or game assists. Do not commit proprietary disassembly or generated references.

The toolkit's local UX-1/setup/checklist were updated for persistent FFB and agent-managed captures. That checkout has unrelated uncommitted work: preserve it, do not commit it wholesale, and do not rebuild its moving Recording source into this consumer. Toolkit v0.12.0/native v0.5.0 and the unpublished recording pin remain unchanged.

Next: test pre-green revs/steering/camera and 75% countdown speed, checkpoint bonuses, pause/restart/expiry and persistence; verify handbrake partial/full/release, FFB sign/delivery and pause/focus recovery during an attended drive; use a recording when ready to assess remaining hitches and contact validity. Do not launch a physical force test unattended.
