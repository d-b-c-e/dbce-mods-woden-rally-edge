# Native gearbox at start: ordinary Manual qualification (2026-10-14)

deli, for sana 96307/96352/97799. One temporary, load-muted, ordinary launch to MapScreen per run. No stage, replay
or Phase B. Evidence is local and private, archived in `E:\Source\_archive\2026-10-14\woden-gearbox-check-20261014`
(`gearbox-check-manual-20261014-01` and `-02`; it holds owner snapshots and is never committed).

**Result: `[Input] NativeGearbox = Manual` set the game's saved choice, which was read back, in an ordinary startup.**
The owner's local files were restored exactly. **Steam Cloud now holds the run's save; a remote restore was NOT done.**

## Candidate

- **Source and builds.** Main `b3e008c`, with the UiTests fixture that b015bdb lacked.
  - Automation ZIP `A4D84606828C4458E73FA8F3D09CF33C07FBE18345E6A89CD5D3689E98637E98`, stage manifest
    `89CF0D88AFB14F9EDE3320743DBDD9248A809CFF115E1CF9BBE5B039856F6B2E`, plugin
    `30BEAAD007ECFCB45FE6FA87796F43A74B09DE30A851242B36520F8A6E9BFD4E`.
  - Load-muted native `088F1089…` with the reviewed shim `9B8A036F…`.
- **Package-embedded tests.** Core 86 suites / 51,388 assertions; UI 912 + 95 shim; Input 3,897.
- **Installer and runner checks.** Test-AutomationMutedPackage PASS.
  - Test-ManageInstall on the real native b3e008c stage: 163/163, including `nativeGearboxPreference=true`.
  - Test-PrivateShimInstall native + private: 33/33.
  - Runner `-VerifyOnly` PASS.
  - Test-ManageInstall refuses the private automation stage by design: it is the native-only fixture.
- **Runner.** `tools/controls/Run-TemporaryGearboxCheck.ps1`: 66e7bf6 for run 01's fixes, b8576d0 for run 02. Offline
  fixtures `Test-GearboxCheck.ps1`: 31/31.

## Runs

| Run | Navigation | Result |
| --- | --- | --- |
| 01 (06:14 CT) | 15 x `pad A 150` | Stopped at the "PRESS START" title. A dismissed "Message of the day"; the title ignored 11 A presses. No gearbox line, no write, the save unchanged. Focus was not recorded. |
| 02 (06:29 CT) | `pad A` / `pad Start` alternating | Step 4 (Start) reached MapScreen. Line: "Native gearbox Manual: set Auto_Transmission[0], Arcade_Auto_Transmission[0] for this start (Auto_Transmission[0] Automatic -> Manual, Arcade_Auto_Transmission[0] Automatic -> Manual)". Outcome `set`. |

Run 02 details:

- **Write and readback.** The owner's saved choice was Automatic in both arrays. The plugin wrote both elements and read
  each one back before logging `set`.
- **Foreground.** At every step, the foreground sampled just after the command write was pid 4788 (Code), not the game.
  These are single samples, not a continuous focus record.
- **Exit.** `exitCode=0` after the runner's own CloseMainWindow, so the runner initiated the close.
- **Restore.** 329 owner files restored and inventory-checked, raw preferences equal, receipt
  `3041965CC31751B0F4F04A248932E31CB549A4F48208A63129C1A8C0CACCC516` re-pinned, no cleanup notes.

## Save and Steam Cloud (run 02)

| `Supersave0.sav` | SHA-256 | SHA-1 |
| --- | --- | --- |
| Owner original (archived `owner-before\tree\save\Save`); the current local file equals it | `EBFA4FF628B22456C142E8236C0AC984B17EC7A3D4775FAB1CE8FF6D3C8F3290` | `688b5551f127d5a07b842d16df8ee19d5c44f31c` |
| Run save, written 11:29:43Z on reaching MapScreen; archived in `candidate-after` | `34C1110468C2CD91302C01B911B542FD521466AEC6F94D00498F44003126AB84` | `0a4b556f162f9559e5a19119a39b646f2ce8f6b6` |

Both files are 7,136 bytes. The BackUp count stayed at 50.

Steam `logs\cloud_log.txt` excerpt (AppID 3218630):

```
[2026-10-14 06:29:55] Persisting file ...\Save\Supersave0.sav to the cloud
[2026-10-14 06:29:55] Need to upload file ViJuDa/Super Woden Rally Edge/Save/Supersave0.sav
[2026-10-14 06:29:55] Upload batch initiated, ID: 386515784912421199, ChangeNumber: 73, App Build ID: 21802346
[2026-10-14 06:29:57] Upload OK for file ViJuDa/Super Woden Rally Edge/Save/Supersave0.sav
[2026-10-14 06:29:57] Upload complete, result OK
```

`userdata\22623167\3218630\remotecache.vdf` now records Supersave0.sav as SHA-1 `0a4b556f…` (the run save), with
`syncstate 1` and `time = remotetime = 1791977381` (11:29:41Z).

- **Remote restore NOT done.** Steam and its cache were left alone (sana 97799), and sana told the owner.
- **Next launch.** Do not assume how Steam reconciles the restored local file against the cloud record at the owner's
  next launch.
- **Unknown.** Whether the run save holds Manual is unknown: the file is encrypted, and the log has no timestamps to
  order the MapScreen save against the in-memory write.

## Remote state: open, not restored (assessment 07:05 CT)

Read-only; Steam, its cache and the owner's files are left untouched (sana 98767/98855).

**Exact state at 07:05:**

| Where | Supersave0.sav |
| --- | --- |
| Local file (the owner original) | SHA-1 `688b5551f127d5a07b842d16df8ee19d5c44f31c`, SHA-256 `EBFA4FF6…3290`, 7,136 bytes, mtime 2026-10-07T01:55:53Z |
| `remotecache.vdf` (ChangeNumber 73) | SHA-1 `0a4b556f162f9559e5a19119a39b646f2ce8f6b6` (the 06:29 run save); `time = remotetime = 1791977381`, `localtime 1791977383`, `syncstate 1`, `persiststate 0` |
| `cloud_log.txt` | no AppID 3218630 activity after 06:29:57 |

**Observed in this PC's cloud log (not an official description):**
- Every logged Woden launch begins "Starting sync (AC Launch,down,)", then "Currently already synced to global change
  number 'N', should be nothing to download" (10-13 18:07, 10-14 06:14). The 06:29 launch did a full sync from local
  change 0 to 72.
- At exit, AutoCloud looks for new and updated files, and uploaded Supersave0.sav when it differed (06:29:55).

**Likely reconciliation (predicted, not proven):** the owner's next ordinary Woden session on this PC.
- At launch the local and global change numbers are both 73, so no download is expected, and the game loads the local
  original.
- At that session's exit, the local save differs from the cached run-save SHA-1, so an upload is expected.
- This prediction comes from the log pattern above, not from an official source. A conflict prompt cannot be ruled out.

**For the owner:**
1. Launch Woden on this PC first. A first launch on another computer would be expected to download the run save there.
2. If Steam shows a cloud conflict prompt, keep the local copy.
3. Afterwards check, read-only:
   - `cloud_log.txt` shows "Need to upload file ViJuDa/Super Woden Rally Edge/Save/Supersave0.sav" and "Upload OK";
   - the `remotecache.vdf` Supersave0 SHA-1 equals the local file's.

The run save is encrypted and was not decoded, so how its contents differ from the original is unknown. The original
local backup stays in the private archive:
`E:\Source\_archive\2026-10-14\woden-gearbox-check-20261014\gearbox-check-manual-20261014-02\owner-before\tree\save\Save\Supersave0.sav`.

The save-suppression source for a later candidate is on main `463cb09`. It skips the game's central Save, which
MapScreen calls directly, for a confirmed runner request or for muted stage playback.

## Not covered

- Automatic, and an "already matched" start.
- The car's `Aids.Automatic` at a stage (Phase B).
- Any change to the Steam remote state.
