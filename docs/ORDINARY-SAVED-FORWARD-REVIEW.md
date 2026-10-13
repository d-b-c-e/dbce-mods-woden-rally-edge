# Automated ordinary forward Saved case — source review recipe

This is a source and pure-fixture checkpoint. No automation package has been built from this change and no Woden game, owner install, device or physical force run has occurred. The current production-native owner install remains `0.2.14+a9a255c`, revision 4 with 17 receipt-owned files and saved Backend Native. Its receipt SHA-256 is `FE2BABFA962467EC82C31C0D677476828AAA4C1125FB4882DB1AE8892D9BA440`.

The explicit `-ShimAppliedForwardCapture -OrdinarySavedForward -PrivateApply` branch reuses the qualified schema-7 native forward profile and three plateaus. `Run-Controls.ps1` no longer writes the ordinary one-shot request in that branch. The **installed** `Start-RecordedGame.ps1` owns the only Steam launch and writes a force-disabled request with the same GUID as the cold probe. The probe reports `car-forward-ready` and leaves the ordinary source recording. The runner then uses the installed companion's `-Stop -RequestId` path, reads Completed/source/finalization, checks Wheelkit 0.6.33 capability, finalized status and analysis-only Saved library entry, and proves the exact game PID and start time are still alive. It archives the original recording byte-exact before normal game closure and owner restore. The case ID and trial result say `automated diagnostic`; no Human origin is supplied to Wheelkit's session runner.

The outer transaction is `tools/controls/Run-TemporaryOrdinaryForward.ps1`. It admits only a manifest-covered revision-4 private package with the exact 17 owned paths, the qualified x64 load-muted `WheelFfb.dll` (`088F1089BEB6D18D478F37582C59330C6BDA6BC37D5E365F6BA7EBF80040F567`), `AUTOMATION-ONLY.txt` and `automation-native-provenance.json`. Before temporary installation it pins the current owner receipt and every owned file, rejects collisions with pre-existing unknown plugin files, snapshots all owner plugin/config/save/recording/loader files and raw Unity preferences, and takes the shared rig lease. The inner runner writes a pending-launch guard before starting the companion, then records its exact PID, UTC start ticks, executable/script paths, and whether the game appeared. Both inner and outer restoration refuse while the companion is active or a launched game was never observed; the outer retains the lease and temporary state for attended recovery. A successful path restores and verifies exact owner inventory, hashes, receipt and preferences before releasing the lease. The installer-created backup folder is retained as labeled evidence.

Do not rebuild the managed package from this later source commit: the SDK embeds its source revision, which would change plugin/Core bytes and fail the exact owner pin. After source review, use `Compose-TemporaryOrdinaryPackage.ps1` under the shared lease to copy frozen production stage `stage-d797e6972c8248d7aa1a56559cab78f1` (manifest SHA-256 `60397A0FDF7E26BF56A5ECE2F166A19FF5C94BEFCBBE914EB8B4527091CF1C2C`) into a fresh internal stage. The composer pins the current owner receipt and all 17 base payloads, substitutes only the qualified load-muted native, updates the private capability's native hash, and creates truthful automation provenance and a new manifest. All 16 non-native owner payloads, including managed assemblies and installed companion, remain byte-identical with their embedded `a9a255c` source revision. The provenance names `a9a255c` as the managed payload source and the later clean commit as the composition script source. The composer does not call `Package.ps1`, rebuild or publish anything. Its invocation is:

```powershell
pwsh -NoProfile -File tools/controls/Compose-TemporaryOrdinaryPackage.ps1 `
  -BaseStage components/wheel/dist/stage-d797e6972c8248d7aa1a56559cab78f1 `
  -ResultStage components/wheel/dist/stage-automation-ordinary-forward-01 `
  -OwnerReceiptPath 'D:/Program Files (x86)/Steam/steamapps/common/Super Woden Rally Edge/BepInEx/WodenWheel-install.json'
```

Adding `-VerifyOnly` to that command checks the frozen stage and all 17 owner payload pins without creating a stage. The full composition is held for review.

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
  -WheelkitCoreDll E:/Source/toolkits/dbce-wheelkit/artifacts/publish-0.6.33/Wheelkit.Core.dll `
  -WheelkitCoreSha256 C9FF238D7FCA4526DF364884AD8E48A1228C45E571C93064764BCD31E31021B9
```

The source/pure fixture gate is `dotnet run --project tools/controls/ProtocolTests/ProtocolTests.csproj -c Release`, `dotnet build tools/controls/Probe/Woden.ControlsProbe.csproj -c Release -warnaserror`, `pwsh -NoProfile -File tools/controls/Test-TrialResult.ps1`, and `pwsh -NoProfile -File tools/controls/Test-OrdinaryCompanionGuard.ps1`. The last fixture launches only a disposable delayed PowerShell child: pending/running, premature-exited and exited-without-game states all refuse restoration. The one game result must show three native plateaus, at least two measured grounded forward seconds, qualified production analysis rows, zero output and zero recorder drops/errors, companion exit 0, `saved` finalization, Wheelkit Saved/analysis-only listing, game alive at Saved, byte-exact archive, normal closure and verified full owner restoration. A Saved case here is signal analysis, not gameplay playback, force normalization, physical torque or owner feel acceptance.
