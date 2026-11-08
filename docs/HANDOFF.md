# Handoff (2026-11-08, WOD-L legacy removal)

- Installed on the rig: `0.2.16+4041021` (`components/wheel/dist/WodenRallyEdgeWheel-0.2.16-leg-cleanup-private-shim-dev.zip`). Launch-checked into Kenya SS1 from Arcade: all three screens show the picture, shim owns the wheel, FFB was off for the run. Evidence and backups: `E:\Source\temp\woden-leg-cleanup` (`run\run.txt`).
- Gone: recording and playback in the plugin and Core, `[Dev] InternalTools`, `[Diagnostics] RecordSession`, `[Input] Backend`, FFB `Model`/`PeakPercent`, the settings migrations, the old delivery pipeline, `tools/controls`, `tools/ci`, `components/triple`, rig-lease and recorded-launch scripts.
- The shim is the input route whenever the package ships `dbce_shim.dll`; a package without it keeps the standalone bindings.
- Next 1: delete the shim trial seam (`ShimInput.Trial.cs`, `ShimInput.CarTrial.cs`, `ShimNative.OpenLatched`, `Runtime.DiagnosticNoForce`) and its UI tests; only the deleted controls harness drove it.
- Next 2: `MountedCamera.LegacyPose`/`LegacyOffset` still turn the old `[Camera]` pose keys into framing offsets; removing them moves cameras for players with old custom poses, so check with the owner first.
- Next 3: bring the source-vendored copies (`Core/Toolkit`, `Core/Vendor`, `Plugin/Toolkit`, `vendor/dbce-shim`) under the toolkit `Sync-Toolkit.ps1` manifest; several differ from the toolkit. `lib/toolkit` already passes `-Check`.
- Wheelkit (CFG-2) still writes `[Input] Backend = Shim` and `Package.ps1` keeps the capability `optIn` for it; both go when Wheelkit drops the Woden opt-in.
- `Manage-Install.ps1` revision lists stay until the first release on the shared packager (owner call 1).
