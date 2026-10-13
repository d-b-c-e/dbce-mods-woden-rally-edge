# S1 ordinary full-session capture: one muted original

This is the review recipe for one short, companion-owned Woden recording. It extends the existing automated forward/Stop/Saved run; it does not use the stage replay runner, install a lasting owner candidate, or claim playback. The ordinary source remains labeled **automated diagnostic**. The shared lease covers temporary installation, the game, archive, close, and exact owner restoration.

## Source and package pins

- Accepted Woden source: clean `main` `1963be45e44e016f5ccb86a93b81b7beff016261`; toolkit timeline `82c12ca`, integrated at toolkit `88d8364`.
- Current owner receipt, to be restored: SHA-256 `CC7983451ACF140AC742A648D15253B98D3BFF0993815C38AB1D17D8810304A2`, revision 4, 17 owned files, version `0.2.14+0ed0292888f29d2152e3a0a77c39e23e43b9d72c`. Saved Native backend, tune, config, raw preferences, and all non-owned files are covered by the existing outer snapshot/readback.
- Internal test-only package: `components/wheel/dist/stage-ceecdff42b54497eb7ac1d276651974d`, manifest SHA-256 `85C9C8FAF35CE0741C898728A7013649DA49C8ECFB99C9667C6FAB4EBE0DCC1D`; private ZIP `WodenRallyEdgeWheel-0.2.14-automation-muted-s1-20261013-01-private-shim-dev.zip`, SHA-256 `03C7636222AAA3B1D89F9389E3A7D70732D1E5F6361A0B502402426AF9A1CE1B`.
- Test package plugin `A96BA20BCC7ADBBF41B5D920735BE818EF59D5E48FDBA7FC66C2D703AD218C02`, Core `B2518F579CC9CF9A913BE5BF56AD9758F2D0944B1CB16E5D3D6BEDC578676A02`, private shim `9B8A036F664CF8F45CCCF82B98E5E4376F93215B60EAC50B6DB965BF498015D8`, qualified load-muted native `088F1089BEB6D18D478F37582C59330C6BDA6BC37D5E365F6BA7EBF80040F567`. The installed companion hash remains `1A2A5F0649BA69FB89AB9A1CCC9F4C0DF5064B4730363BA7C4D98C51B42DF70A`.

`Package.ps1` built this stage directly from the accepted S1 source with the test native and private shim. It passed 71 Core suites / 50,978 assertions, 95 shim, 912 UI, and 3,897 DeviceHub assertions. The private installer fixture passed 31 checks against a pinned 16-file native base stage. Runner source checks passed 139 protocol/workload/coverage, 13 config, 36 trial-result, and 7 companion-guard checks. The read-only `Assert-OrdinaryFullSession.ps1` passed on Nene's actual-writer golden package (75 aligned poses), and the wrapper's `-VerifyOnly` passed the current owner and test-stage pins. Logs are in `E:/Source/dbce-project-mgmt/artifacts/woden-full-session-s1-20261013/{package-01,runner-source-02}`.

## Exact invocation after source review

From the Woden repository root, run `-VerifyOnly` first; remove it only for the single approved capture. Use a new result directory for the real run.

```powershell
pwsh -NoProfile -File tools/controls/Run-TemporaryOrdinaryForward.ps1 `
  -FullSession -VerifyOnly `
  -Result results/full-session-s1-20261013-01 `
  -PackageRoot components/wheel/dist/stage-ceecdff42b54497eb7ac1d276651974d `
  -PackageManifestSha256 85C9C8FAF35CE0741C898728A7013649DA49C8ECFB99C9667C6FAB4EBE0DCC1D `
  -PrivateWriter results/private-applied-car-hpattern-d8df97b-01/harness/ConfigurationQualification.dll `
  -PrivateWriterSha256 E344EA990155AC99A257AD28DA3BA340B484B89710B3E367B2532C63760FBA3A `
  -PrivateCatalog results/private-applied-car-hpattern-d8df97b-01/catalog.json `
  -PrivateCatalogSha256 2D05AF6238503AD8985560DCA32F36E8DFA0CA28929F743E0249E44E85C1628F `
  -ProbeSha256 36A198CA26510B921DBEB4FB2979268EA3498661CCA1FC600BB4D59E02D60E22 `
  -WheelkitCoreDll E:/Source/toolkits/dbce-wheelkit/artifacts/publish-0.6.33/Wheelkit.Core.dll `
  -WheelkitCoreSha256 C9FF238D7FCA4526DF364884AD8E48A1228C45E571C93064764BCD31E31021B9
```

The real run removes `-VerifyOnly`. The outer transaction holds the shared lease, snapshots owner files and raw Unity preferences, installs only the internal load-muted package while the game is closed, and uses the installed companion as the sole launcher and exact-request Stop writer. The same frozen schema-7 forward plateaus are reused; no unchanged-a9 run is repeated. The probe must report three native plateaus, qualified forward rows, and zero physical output. The runner checks the original source footer, a witnessed `Stopping`/inactive status before finalization, completed Saved with the game still alive, and Wheelkit's analysis-only listing. It then checks `session.json` plus both exact seals, timeline stage reference, context build, `physicalOutput=false`, every pose/indexed signal row, recorded HUD speed, and observation scene before stage-open. It archives the entire original capture byte-exact before normal game close. `game-exit.json` records CloseMainWindow request, actual process closure, and numeric exit code separately from the package result. Any nonzero exit remains a shutdown failure even if package evidence passed.

The outer finally waits for the exact companion and game to exit, restores every owner file/preference from the snapshot, verifies current receipt and inventory byte-exact, then releases the lease. If quiescence or restoration cannot be proven it retains the lease and temporary state for attended recovery. No owner package, runtime release, or S2 replay follows from this run alone.
