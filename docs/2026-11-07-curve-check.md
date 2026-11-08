# Curved-screen correction checked on the rig

2026-11-07, lane-a. Installed build 0.2.16 at main `da0ccca` (already installed). The owner's three 2560x1440 screens
as one span window, `[Triple] CurveRadiusMm = 1500` as saved, force feedback off, Kenya SS1 from Arcade.

| Check | Result |
|---|---|
| Curve on at stage start | Log: `Curved triples on: targets 2560x1440 x3`, then `presenter camera rendered`. Picture 97.6 / 99.0 / 99.3% |
| Every camera, curved and flat | C cycled Chase 1 to 5, Bonnet (Game) and Bumper (Realistic). Each was captured curved, then with Shift+F7 at "Angle correction" (flat), then back to curved. All 14 captures passed `Test-GamePicture` on every screen |
| Curved vs flat | The curved picture is re-mapped near the seams (fence and figures shift as each panel is bent); the HUD timer, fuel bar and gauges sit in the same place in both, so the HUD is not warped |
| F11 off and on | Single view, then three views with the curve rebuilt; picture passes |
| Stage end | The arcade timer ran out: `Triple screens off (camera destroyed)`, then the route-select menu fills the screens. For under three seconds during that transition the side screens held the last frame and the center was black; it cleared by itself |
| Second stage | `Curved triples on` again and the picture passes on all three screens |

Not checked by an agent: whether lines run straight across the seams from the driving seat. That is the owner's check.

The radius is set by Wheelkit ("Screen curve radius (mm)") or `[Triple] CurveRadiusMm` in the mod's settings file.
There is no F6 control for it; F6 controls wait for the shared panel.

Saves, Unity preferences and `BepInEx\config` were restored exactly. The run's arcade save had uploaded to Steam Cloud,
so the owner install was launched once through Steam and closed at its first screen; the cloud `Supersave0.sav` is the
owner's own save again. Captures, logs and scripts: `E:\Source\temp\woden-curve` (`run\run.txt` is the timeline,
`run\seams-bumper.png` the curved/flat seam crops).
