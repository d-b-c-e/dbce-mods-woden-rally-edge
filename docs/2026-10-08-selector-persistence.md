# Triple selector persistence follow-up

The first STD-022 selector (`d5c50e1`) changed the live Mode and
SpanSeparateMonitors entries without marking the panel dirty. Settings disables
BepInEx's save-on-assignment, so the usual 700 ms save did not occur unless a
different control changed or the panel closed. Each selector change now marks
the panel dirty after both entries are set. The save status and ordinary timed
save follow the same path as other controls.

Off now describes a single view and explains that the window size changes at
the next start. Surround also states that its window layout applies next start.
This does not add display transitions or alter the renderer, input or force tune.

The production Panel/Settings fixture clicks all three choices, confirms both
live entries, unchanged disk bytes before the debounce, the pending-save text,
and both values after reopening the saved configuration while the panel remains
open. Real BepInEx entries replace the earlier plain fields in the camera fixture.
All 705 UI assertions pass. Removing just the new Dirty call makes the new
pending-save assertion fail, as expected; the production fix was restored before
the zero-warning solution build. Private negative-control log:
`artifacts/selector-negative-control.log`. No game or device is used.

The recording metadata's analysisForce description is also corrected: its
preview combines steering and damping, excluding crash and delivery gates.
The old text incorrectly said steering only. Signal arithmetic, channels and
recording format remain unchanged. `Compare-FfbToArt -Component steering` uses
the explicit steering component described in FFB-COMPARISON.md.

Claude independently reviewed `4645318`: all 705 panel assertions and 52 other
suites (37,987 assertions) passed, with no build warnings. He installed that exact
runtime as **0.2.14-triplesel.2-dev** at 07:53 CT on October 8. ZIP SHA-256:
`72C4E198D39CBBFF68CFFCE9C74900A98017A2D211A7665484A6465CDA6E1089`.

Independent readback verified all 13 installed payload hashes and the five
backed-up owner files against the deployment backup; settings were unchanged.
The exact package also passed all 111 installer fixture checks under Windows
PowerShell 5.1. Private evidence: `artifacts/selector-independent-install.json`
and `artifacts/selector-install-ps51.log`. No game or force device was used for
these checks. Owner rendering acceptance remains pending; the earlier muted
Kenya replay qualifies the underlying playback/native change, not this new UI.

Low follow-up from review: the next-start help should say that a changed span
choice may resize the window, rather than implying every choice always resizes.
