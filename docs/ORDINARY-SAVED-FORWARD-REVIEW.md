# Automated ordinary forward Saved case — source review recipe

This is a source and pure-fixture checkpoint. No automation package has been built from this change and no Woden game, owner install, device or physical force run has occurred. The current production-native owner install remains `0.2.14+a9a255c`, revision 4 with 17 receipt-owned files and saved Backend Native. Its receipt SHA-256 is `FE2BABFA962467EC82C31C0D677476828AAA4C1125FB4882DB1AE8892D9BA440`.

The explicit `-ShimAppliedForwardCapture -OrdinarySavedForward -PrivateApply` branch reuses the qualified schema-7 native forward profile and three plateaus. `Run-Controls.ps1` no longer writes the ordinary one-shot request in that branch. The **installed** `Start-RecordedGame.ps1` owns the only Steam launch and writes a force-disabled request with the same GUID as the cold probe. The probe reports `car-forward-ready` and leaves the ordinary source recording. The runner then uses the installed companion's `-Stop -RequestId` path, reads Completed/source/finalization, checks Wheelkit 0.6.33 capability, finalized status and analysis-only Saved library entry, and proves the exact game PID and start time are still alive. It archives the original recording byte-exact before normal game closure and owner restore. The case ID and trial result say `automated diagnostic`; no Human origin is supplied to Wheelkit's session runner.

The outer transaction is `tools/controls/Run-TemporaryOrdinaryForward.ps1`. It admits only a manifest-covered revision-4 private package with the exact 17 owned paths, the qualified x64 load-muted `WheelFfb.dll` (`088F1089BEB6D18D478F37582C59330C6BDA6BC37D5E365F6BA7EBF80040F567`), `AUTOMATION-ONLY.txt` and `automation-native-provenance.json`. Before temporary installation it pins the current owner receipt and every owned file, rejects collisions with pre-existing unknown plugin files, snapshots all owner plugin/config/save/recording/loader files and raw Unity preferences, and takes the shared rig lease. It installs the automation package only for the trial, then restores and verifies exact owner inventory, hashes, receipt and preferences before releasing the lease. If the game remains open or restoration cannot be verified, it retains the lease and evidence for recovery. The installer-created backup folder is retained as labeled evidence.

After source review, build the new probe and automation package from a clean committed source checkout **under one shared rig lease**. `Package.ps1` already admits `-AutomationMutedNativePath` and its provenance sidecar and emits the truthful marker, manifest and revision-4 receipt. Use a fresh `automation-muted-...` artifact label and the pinned private shim `9B8A036F...`. The package command runs the existing package suites; do it once at that gate rather than repeating them here. Inspect the generated stage/manifest and require the package plugin, Core and installed companion to match the current owner production package, with only the native automation substitution and corresponding receipt/manifest/capability metadata different.

Only after review of the exact committed diff, script, package pins and one-run recipe, the closed-game invocation is:

```powershell
pwsh -NoProfile -File tools/controls/Run-TemporaryOrdinaryForward.ps1 `
  -Result <new-result-directory> -PackageRoot <reviewed-automation-stage> `
  -PackageManifestSha256 <reviewed-manifest-sha256> `
  -PrivateWriter results/private-applied-car-hpattern-d8df97b-01/harness/ConfigurationQualification.dll `
  -PrivateWriterSha256 E344EA990155AC99A257AD28DA3BA340B484B89710B3E367B2532C63760FBA3A `
  -PrivateCatalog results/private-applied-car-hpattern-d8df97b-01/catalog.json `
  -PrivateCatalogSha256 2D05AF6238503AD8985560DCA32F36E8DFA0CA28929F743E0249E44E85C1628F `
  -ProbeSha256 36A198CA26510B921DBEB4FB2979268EA3498661CCA1FC600BB4D59E02D60E22 `
  -WheelkitCoreDll E:/Source/toolkits/dbce-wheelkit/src/Wheelkit.Core/bin/Release/net10.0-windows/Wheelkit.Core.dll `
  -WheelkitCoreSha256 C9FF238D7FCA4526DF364884AD8E48A1228C45E571C93064764BCD31E31021B9
```

The source/pure fixture gate is `dotnet run --project tools/controls/ProtocolTests/ProtocolTests.csproj -c Release`, `dotnet build tools/controls/Probe/Woden.ControlsProbe.csproj -c Release -warnaserror`, and `pwsh -NoProfile -File tools/controls/Test-TrialResult.ps1`; only the last test is needed before a rig lease. The result must show three native plateaus, at least two measured grounded forward seconds, qualified production analysis rows, zero output and zero recorder drops/errors, companion exit 0, `saved` finalization, Wheelkit Saved/analysis-only listing, game alive at Saved, byte-exact archive, normal closure and verified full owner restoration. A Saved case here is signal analysis, not gameplay playback, force normalization, physical torque or owner feel acceptance.
