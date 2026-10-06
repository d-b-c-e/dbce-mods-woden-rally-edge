# Offline FFB comparison

The shared [normalization runbook](https://github.com/d-b-c-e/dbce-wheel-mod-toolkit/blob/master/docs/FFB-NORMALIZATION.md)
defines the comparison manifest and duration-weighted statistics. Woden owns
its actual ForceSignal model and the channel mapping.

From this repository root, with the original sealed stage directory:

```powershell
dotnet run --project components/wheel/tools/TelemetryInspector -c Release -- ffb-export $reference $newCsv 35 50
```

The last arguments are Strength and PeakPercent. The command validates original
artifact hashes, tune, row alignment and the original force replay before running
the production model with those two overrides. Other saved force parameters and
reset boundaries are preserved. Existing output files are refused. No native
device is opened and no game, profile or installed setting changes.

For direct baseline analysis use `analysis.force.preview`, `.valid`, `.reset`
and `motion.speed` (m/s). `ffb.preview` represents the actual output lifecycle and
is zero in the muted Kenya reference; it is not the continuous analysis model.

October 6: 3,601 original rows reprocess with zero model error; the baseline CSV
agrees within 7.5e-9. The saved 49.583332 / peak25 tune occupies its cap for 46.46%
of valid interval time. The offline 35 / peak 50 trial reduces cap occupancy to
zero, RMS 22.29%, peak 40.03%. That higher peak is **not installed or physically
accepted**. The owner will compare it explicitly before any tune/default change.
Physical cross-game normalization (STD-003) remains pending.
