# Shared UX adoption — 2026-09-14

UX baseline: toolkit UX-1, with the owner's September 14 persistent-FFB and agent-recording clarification. Source: 0.2.7 working tree; package/installation evidence is in STATE. This is not visual or physical acceptance.

Implemented: optional countdown/time-limit assist under Setup → Difficulty, default Off with saved speed and an eligibility status; countdown wheel/camera access independent of FFB permission; six standard pages; saved FFB On/Off (new default On and 50% strength with original output gain), no session arming; F8/Stop saves Off; normal output suppression retains the device; E-Brake within Axes and Buttons with independent calibration; external one-launch recording request with automatic finalization. Existing bindings and owner tuning are retained.

| Rule | Remaining gap / acceptance |
|---|---|
| UX-03 | Mouse-first UI; keyboard/wheel navigation, user scale, long names and 720p/4K acceptance remain. |
| UX-04 | Handbrake axis calibration and proportional rear brake/grip-loss adaptation are compiled/tested offline, not driven. The stock power cut remains binary. H-pattern, menu bindings, conflict handling and final-game-input UI bars remain. |
| UX-05 | Existing output picker still cycles wheels; the newer steering-derived dropdown standard is not yet adopted. Physical selection, sign, stop and recovery still require acceptance. |
| UX-06 | Fitted bonnet, manual offsets/FOV, near-clip restore and camera key/button rebinding implemented. 0.2.7 fitted default includes the owner's +0.15 m up / +0.05 m forward correction. Cross-car hood framing, camera handoff and split-screen checks remain; manual per-car presets remain. |
| UX-07 | Recording is agent-managed per owner preference, not a missing menu workflow. Master telemetry toggle, markers/support bundle/open-folder UI remain optional/future; no automatic upload. |
| UX-08 | Endpoint restart failures still need transactional recovery; binding save status remains separate. |
| UX-09/10 | Reviewed closed-game development updates; complete player ZIP/update/uninstall remains unfinished. |
| UX-11 | Walk through this build at the rig and record acceptance. |

Normal game launches honor the saved FFB preference through all driving gates. An unattended diagnostic-launch request temporarily suppresses physical force without changing that saved preference. This is test policy, not a second player-facing enable switch.
