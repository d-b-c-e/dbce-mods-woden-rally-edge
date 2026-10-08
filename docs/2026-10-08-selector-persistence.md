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

Source and fixture validation only; packaging, installation and owner rendering
acceptance are separate. The installed triplesel.1-dev remains the earlier build.
