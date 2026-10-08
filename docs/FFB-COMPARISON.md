# Offline FFB comparison

The shared [normalization runbook](https://github.com/d-b-c-e/dbce-wheel-mod-toolkit/blob/master/docs/FFB-NORMALIZATION.md)
defines the comparison manifest and duration-weighted statistics. Woden owns
its Classic ForceSignal / experimental GripSignal adapters and channel mapping.

From this repository root, with the original sealed stage directory:

```powershell
dotnet run --project components/wheel/tools/TelemetryInspector -c Release -- ffb-export $reference $newCsv 35 50
```

This command selects **Classic v3**, even if the source was recorded with Grip.
The last arguments are Strength and PeakPercent. The command validates original
artifact hashes, tune, row alignment and the original force replay before running
the production model with those two overrides. Other saved force parameters and
reset boundaries are preserved. Existing output files are refused. No native
device is opened and no game, profile or installed setting changes.

For a Grip trial, supply its reference ratio explicitly:

```powershell
dotnet run --project components/wheel/tools/TelemetryInspector -c Release -- stage-review $reference
dotnet run --project components/wheel/tools/TelemetryInspector -c Release -- ffb-export $reference $newCsv 50 25 grip 2
```

Here `2` is the trial load ratio, not a calibrated recommendation. Grip ignores
the Classic peak argument. Recorded smoothing/damping remain in effect; a
Classic-to-Grip trial uses Grip smoothing 0.2 and the latest Grip model (currently
v5). A Grip capture keeps its recorded version: v4 remains coupled-damping v4;
v5 keeps independent steering strength. New v5 stages use force-config v3 /
model5; v4 stages keep force-config v2 / model4. Both restore the production
front-load reference on every row, and check analysis model, tune, car, curve
version and reset identity. Existing Classic stages retain v1 / model3 behavior.
Never replace unavailable historical friction curves with current defaults:
the original Kenya tape cannot support a Grip trial.

Use this mapping with the shared normalization tool:

```json
{"time":"time_s","value":"request","valid":"eligible","epoch":"epoch","speed":"speed_kmh"}
```

`valid` describes the model calculation; `eligible` also excludes missing or
provisional Grip references. The CSV explains each exclusion. A discontinuity
starts a new force epoch. A changed car or tune requires a separate stage.
Known-zero references stay zero; invalid/fractional reference kinds are refused.
Synthetic fixtures remain labelled `synthetic-test` in the stage report.

The CSV `request` is the selected model's combined steering/damping preview.
For Grip, `steering` and `damping` are that trial's own pre-clamp terms, not values
copied from the original tape. In v5, changing Strength changes only steering;
v4 intentionally retains its old coupled damping law. `model_version` identifies
which law ran. The hub's `Compare-FfbToArt.ps1 -Component steering` selects the
explicit steering term for comparison to Art. It refuses a Classic export that
does not expose that term; it never labels the combined request tyre-only.

This validates **software model analysis**, not the crash cue or wheel torque. Original
`ffb.*` delivery and `crash.*` replay remain separate. A kinematic route playback
cannot produce a replacement live-physics force baseline. Collect a new muted
original drive with real friction curves before calibrating Grip. See the
[October 7 follow-up](2026-10-07-grip-stage-followup.md) for verified scope.

For direct baseline analysis use `analysis.force.preview`, `.valid`, `.reset`
and `motion.speed` (m/s). `ffb.preview` represents the actual output lifecycle and
is zero in the muted Kenya reference; it is not the continuous analysis model.

October 6: 3,601 original rows reprocess with zero model error; the baseline CSV
agrees within 7.5e-9. The saved 49.583332 / peak25 tune occupies its cap for 46.46%
of valid interval time. The offline 35 / peak 50 trial reduces cap occupancy to
zero, RMS 22.29%, peak 40.03%. That higher peak is **not installed or physically
accepted**. The owner will compare it explicitly before any tune/default change.
Physical cross-game normalization (STD-003) remains pending.
