# Shared UX adoption — 2026-09-13

Target: toolkit [UX-1](../../dbce-wheel-mod-toolkit/docs/CONSUMER-UX.md).
0.2.2 begins alignment; this is not full UX acceptance. The previous F6 renderer
ran at 1920×1080, but this revised layout still needs visual/interaction checking.

Implemented: standard six page names/order; Setup with device-input bars and
links to existing Controls/FFB values; Controls groups axes and buttons; common
display action names without changing stored binding keys; labeled Off/On
settings; permanent Stop FFB and Close; explicit Save calibration; Saved/error
indicator; model constants under Advanced; help with next actions; normal
stock/bonnet/bumper cycle and camera-control gate for FFB.

Development exception: FFB requires **Start FFB for this session** at each launch.
The initial tune is provisional and physical direction has not been accepted.
F8 latches the stop, including its visible reason. Saved FFB On does not arm it.

Remaining gaps:

| Rule | Gap / next check |
|---|---|
| UX-03 | Mouse-first renderer; no full keyboard/wheel navigation, user scale control or long-name/720p/4K acceptance. F6/Esc/F8 remain keyboard routes. |
| UX-04 | Current axis reversal is an Invert action, not an explicit state switch. Menu bindings, H-pattern, analog handbrake and conflict handling remain unfinished. Setup bars are explicitly device input, not proof that the car received it. |
| UX-05 | Output picker advances through wheels instead of listing them. Attended selection, sign, stop and resume validation still required. |
| UX-06 | Compiled cycle/handoff requires actual native-camera and foreign-writer transition checks; FOV/per-car clipping not implemented. |
| UX-07 | Telemetry uses port 0 to disable; no master Off/On, marker/Open folder/combined support-file action. Recording already bounded and opt-in. |
| UX-08 | Endpoint editor applies together, but output-restart failures still need transactional recovery. Binding-file save status is separate from general settings status. |
| UX-09/10 | Repository-based initial installer and reviewed backup updates; no complete player ZIP with bundled loader/transactional update/uninstall. |
| UX-11 | First-use path built, not yet walked through on this version. |

Do not copy the old Woden page names into another project as the family standard.
The toolkit guide owns that standard; existing game-specific rendering can remain.
