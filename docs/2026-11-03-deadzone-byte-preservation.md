# Woden existing-binding deadzone token save

Assignment509525, first bounded binding-writer slice. Baseline canonical main
a3f28c30aa951191baae5e6221a5ba257c6b7fba; source2813a3d3fa3c5ee56b23d4eb346e418e12ed7186.

SaveDeadzone previously guarded the selected saved axis, then serialized the
whole JsonNode document, changing unowned formatting/BOM/EOL/EOF. It now parses
the complete JSON, locates only immediate owned axis/Calibration/Deadzone tokens
with Utf8JsonReader offsets, and replaces only the number. Escaped property names
are decoded for identity; nested lookalikes cannot select the wrong value.
Missing Deadzone is inserted inside the existing calibration without rewriting
other bytes. Duplicate owned path elements, malformed/unsupported/oversized or
stale saved bindings refuse. Existing saved-axis and valid-deadzone guards remain.

The snapshot read handle denies content writers through replacement; a byte
recheck refuses a replaced/changed source. No transaction against independently
renaming editors is claimed; retain existing editor exclusion. Publication
reuses Core's exclusive sibling-temp/Flush(true)/File.Replace pattern, cleaning
only a successfully owned temp. No-op leaves bytes/timestamp/backup unchanged.
First backup remains .bak; an existing backup is retained and a new suffix used.

Only existing-file SaveDeadzone changes. Missing-file initialization still calls
the unchanged full Bindings.Save/SaveJson; general bindings/config saves remain
open. Plugin input/capture, calibration math, accepted force tune/defaults,
camera policy, installers/packages and installed files are untouched. No game,
Steam, plugin/native/device/force/UI or owner data actions; no public push.

Focused fixture links actual Bindings.cs and AxisCalibration.cs against net6.0.
Only unrelated default-camera-key/digital-index boundaries are inert stubs;
none of their semantics or hardware behavior is qualified. Planned original
regression3 checks/1 exact-byte failure, fixed44 checks, and real Core net6 build
(not loaded). NuGet sources cleared, existing local6.0.36 cache/runtime only;
one compiler worker, server/node reuse disabled, seven held dotnet roots.

## Offline qualification

CPU509823 per509827, first tuple2813a3d/DB946F78/CEB376F2, completed7.897s.
The original actual writer exposed the exact-byte regression (3checks/1failure).
Fixed net6 compilation and first13 assertions passed; the fixture then stopped
because Windows wildcard *.bak.* also matched .bak. The original logs/result
remain unchanged. Only the fixture filter changed to an ordinal filename prefix
at7ba6c9506d419926db47c8822d041d788d13bac8; Bindings.cs is identical to2813a3d.

Remaining CPU used preapproval509894 (formal same-tuple509895/per509900 arrived
during execution), freshcpu02,120seconds/30reserve/onecompiler/five held roots.
Plan SHA256 `39F02B9FB1C6FB7A76BAD7F32043A26B079B8A7FE3527ADB39DDBD69CC0E8B20`;
runner SHA256 `8E1C0EB921429AB57C22FB39651E75BFEF40C401EA7B38D686232222B0E9D625`.
Pinned original-regression output/DLL/identity admitted fromcpu01, no replay.

**PASS in6.3697053seconds:**44 focused checks against actual Bindings/AxisCalibration
source, compiled and run on net6.0; real production Core net6 build0warnings/0errors.
Core DLL SHA256 `28950E45BE318D2C9C4E2F5046611D33E1BCE1D99DA0181D94F4448B59450F9C`,
never loaded. Fixture default-key/digital-index stubs do not qualify those unrelated
boundaries. The broad Core/UI suites, plugin build, installer and live behavior
were not rerun and are not claimed from these focused checks.

Evidence retained under this worktree `bld/deadzone-qualification/cpu01` andcpu02.
First result SHA256 `3BAA2564ECF9C242F07DA231350B7016125AAC600A6A0800D507B8D0C8A37527`;
first independent pre-release audit `1EBA6005EBBBDA1C63CD878E24D8EEE2FA4E841BE949A609BFFF3A4AEA6D9F9B`.
Final result SHA256 `B3C6AA97444F1CAA191BF565ADDCB41A4E850E4973304D60FB5256359B7E5D69`;
final independent pre-release ALL-image scoped audit
`40972B7627DD733C212990DD4E56A19528C1917096388C2885340E11FD097A38`.
Five explicit held roots/14 observed final-run job identities, normal exits, no
forced stop, empty job, retainedhandles0; audit passed before exact lease exit.
No CPU/live/lease custody. No public push or installation.

Only documentation/ledger changes follow the tested7ba6c95 scripts/source. Full
settings/binding writers, missing-file initialization, cross-process rename
admission and player/runtime/device acceptance remain separate. STD-044 partial.