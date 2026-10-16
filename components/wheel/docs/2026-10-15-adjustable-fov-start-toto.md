# Woden bonnet FOV closeout: Physical lock, stale lens and an Adjustable start at the shown angle (toto, sana 166662)

Source and retained evidence only. Nothing was built, run or launched, and no owner config or install was touched. The
branch is based on Livo `c433e10`, which puts the camera hint on the center screen.

## What the owner saw, traced through the code (installed `d45e2ce`, current `007bc39`; the camera code is identical)

The owner's config `97BBB732` has these values:
- `[Triple] MatchGameFov = false`, which is Physical rig.
- `ChaseUsesGameFov = true`, and the panels are 708.4 × 398.5 mm with the eye at 660 mm.
- `[Camera] Fov = 30` for the bonnet lens. That is the 30–110 floor. `AutoFitBonnet = false`.

**Consumption.**
- `MountedCamera` (Car_Cam.LateUpdate postfix) writes `pose.Fov` to `Car_Cam.field_Private_Camera_0`.
- `TripleView.AfterCarCamera` takes that same camera as the source, so the right camera is updated.
- In Physical mode with a mounted view, `TripleGeometry.EyeDistanceForView` returns the measured 660 mm eye. The center
  projection is therefore always 33.6° vertical, and the lens value is not used.

**Keys.**
- Since `641914e` (10-15 00:29), `CameraShortcuts` refuses FOV +/- under `MountedFovLocked` and shows the lock message.
- In installed d45, that message and the first-view hint are drawn in the bottom-left of the **left** monitor. The fix is
  c433e10, which is not installed. So "no visible change" also came with no explanation where the owner was looking.

**Why the saved lens is stale.**
- Before `641914e`, FOV keys in Physical mode still saved the lens value, and any key also switched off Fit to car. No
  projection changed, so nothing was visible.
- The owner's bonnet lens went from 70 (10-08, `20C0BC9A`) to the 30 floor (10-14, `A9D5CEB9`) and is still 30 in
  `97BBB`.
- The 42/44/42 runtime checks used trial configs (`Fov = 42`). They are not the owner's state.

**The latent bug this fixes.** "Use Adjustable FOV" (d45) and the Triple FOV choice adopted that stale 30° lens. The
bonnet would therefore get **narrower** (30° against the 33.6° shown) exactly when the owner asks to adjust a view they
already find too narrow.

## The fix: one branch, three files

**`Core/FovModeSwitch.AdjustableStart(centerVertical, mountedOnScreen, savedLensInUse)`** returns the shown angle,
rounded to 0.1° and clamped to 30–110. It returns null for a chase view, a fitted bonnet or no measured angle.

The view is kept only in the case that applies to the owner: a measured angle inside the lens bounds, a saved bonnet
lens with Fit to car off, and the view on screen. In the other cases the switch can still change the view:
- a shown angle outside 30–110 starts at the nearest bound;
- with no start, the saved or fitted lens applies.

The log line names the start value, the shown angle and the previous saved value, so a clamped start is visible.

**No overwrite in the same draw.** In `CameraPage` the Advanced editor reads `pose` and `fov` before the locked row's
button. It saves only `if (edited != pose)`. After the click both still hold the old lens, so nothing is saved and the
seeded value stands. The next pass reads the new lens. The Simple Triple FOV choice runs before the Advanced editor, so
there the editor reads the seeded pose.

**`TripleView.MatchGameFov` setter.**
- Only on a change false → true, and before the switch, it sets the on-screen mounted view's saved lens to that angle.
- It marks settings dirty, which saves them through the existing Panel path, and logs
  `Adjustable FOV: bonnet lens starts at the shown 33.6° (saved lens was 30.0°)`.
- It covers both F6 routes: the Triple FOV choice and the locked row's button.
- Nothing else changes: the other view's lens, Fit to car, height, forward and tilt are untouched. Switching back to
  Physical changes no value.

**`Tests/Program.cs`** adds pure bounds checks, plus an owner-rig geometry check:
- the switch keeps the 660 mm projection, within 1.5 mm;
- adopting the stale 30° lens would have moved the eye past 700 mm, which is narrower.

Panel, Settings, CameraShortcuts, MountedCamera, input and FFB are untouched. No Unity API was added, which matters
because Woden is IL2CPP.

## Focused check plan

1. **Assigned build slot.**
   - Build Core and the plugin.
   - `WodenRallyEdge.Tests --only "camera fit, manual tuning, bounds and key bindings survive legacy and new settings"`.
   - The existing UiTests run unchanged. Panel uses the TripleView stub, so this is a sanity run only.
2. **Install with the closed-game route.** Include c433e10 and this commit.
3. **One attended owner check** in bonnet view, triples on:
   - The first-view hint and the +/- message appear on the **center** screen and name 33.6°.
   - With the owner's state (33.6° shown, Fit to car off), "Use Adjustable FOV" causes no visible jump. The log shows
     `Adjustable FOV: bonnet lens starts at 33.6° (shown 33.6°, saved lens was 30.0°)`, and the center vfov stays
     33.6.
   - Each FOV + or − press visibly changes the view by 2°.
   - Restoring Physical is the owner's choice. The lens keeps whatever they set.

There is no automated click route; the DevInput mailbox is not a click substitute (sana 160835).

**Gap after this fix.** In Physical mode the bonnet is 33.6° by design. To make it wider while staying physical, the
owner has to change the measured eye distance; otherwise they use Adjustable. Owner values stay as they are until the
owner clicks.
