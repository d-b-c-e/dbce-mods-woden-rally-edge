# Binding file publication — source/fixture increment

Assignment509956. Starting mainf69aa69; production/fixture source
`9750a81f596be5236e90fd23d010e4309b817e79` in isolated tree
`E:\Source\_work\napo-woden-bindings-publication-20261103`.

Full Bindings.Save previously used WriteAllText on an unreserved temp name and
overwrote the same .bak on every replacement. The publisher now uses an exclusive
CreateNew sibling, writes UTF-8 without BOM as before, Flush(true), then atomic
File.Replace or non-overwriting File.Move. Cleanup touches only a temp actually
created by this call and does not replace the primary exception.

The first replacement still creates the legacy .bak; an existing backup is
retained and a new suffix used. Repeated same-value full Save still backs up,
retaining its prior contract. Read-only/reparse targets refuse. This is not a
complete binding-format preservation repair: full existing-file Save still
serializes the entire object and can rewrite formatting/unknown data. That
existing gap remains explicitly open.

An internal creation-only SaveNew route is used by SaveDeadzone's missing-file
fallback. It refuses an existing file/directory and always publishes with the
non-overwrite move. The general publisher records existence before staging, so
a target appearing during preparation is not silently adopted for replacement.
External-editor transaction/admission remains unqualified; do not infer a
cross-process concurrency guarantee beyond the non-overwrite creation primitive.

The existing-file deadzone token implementation, plugin/capture, calibration math,
accepted force tune/defaults, cameras, native libraries, installers/packages and
owner files are untouched. No game, Steam, real device/input/force, UI, plugin or
native action, installation or public push. STD-044 stays partial.

## Offline evidence

CPU510030 per510033, plan
`46CF1C6EB9FEDA2AFE670B7E0BD71F27F362872B4EE30BD50F81BF5BB2988A5E`,
runner `3CF1870AF6A366062D0FDBABABCB199224BBAE4692C1736DBAE617065EC06DF1`.
90 pins,180seconds/30reserve/one compiler; actual **8.7120423seconds**.
NuGet sources cleared, existing local net6 cache only, server/node reuse disabled.

- Actual original full Save:3 checks/1 expected failure, proving prior-backup loss.
- Fixed actual Bindings/AxisCalibration: **30/30 checks PASS**, net6 target/runtime.
  Creation-only rival refusal, missing-file deadzone fallback, legacy repeated
  save behavior, backup retention, locked destination/backup, readonly and owned
  temp cleanup. Only unrelated default-camera/digital-index boundaries stubbed.
- Production Core net6 build: **0 warnings/0 errors**, never loaded. SHA256
  `5EE8E5500FD7E8247C6F59A057E6AEFAF50AF564EC5FCB4D46EACC7FD35715CA`.
- Result SHA256 `C24446690CEE827A8267C43D4E30E55223E2C4BF2FEE2AF8775E1E70A5329DA0`.
- Independent ALL-image scoped identity census before exact lease exit:
  `0A5446FAB1CA6393BBC05D396D091084A8E579A7CAFA792293373EED1DFBF556`.

Seven explicit held roots/20 observed identities, normal exits/no force, empty
job, retained handles0, audit clear, exact lease released. No custody. Retain
`bld/bindings-publication-qualification/cpu01` and its frozen inputs/baseline/logs.
Prior44 existing-file token checks retained without replay: that path is unchanged.
No broad Core/UI suite, plugin build, package or live acceptance claim.

## Next slices sized

1. Full existing-file Bindings.Save needs an owned-delta plan rather than whole
   object serialization. Start with one actual consumer edit (for example a camera
   key), compare its saved expected value, patch that member and atomically publish;
   retain unknown bytes and refuse ambiguous paths. Keep full initialization distinct.
2. BepInEx Settings.SetPresentation writes just Interface View/Page but calls the
   full ConfigFile.Save. A small first config slice can batch those two keys through
   the existing ConfigLine byte editor and publish once; do not loop two independent
   writes. Preserve save failure feedback and the in-memory BepInEx entries.
3. General Settings.Save still Validate/Syncs all entries then calls ConfigFile.Save;
   it needs an owned dirty-entry plan. SaveOnConfigSet is already false; do not
   rediscover or change accepted force migration/defaults while repairing persistence.

Art full-object XML serialization and FH2 full-writer candidates should be traced
after these Woden increments; neither is implemented or qualified by this change.
