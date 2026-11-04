# Woden presentation preference transaction

Assignment510104; clean baseline d059a1be41e780662f0818267779187460987af4.
Production79d5ba0af770e9e69eb5f826f77b84dcfc61c5a0 plus shared ConfigLine writer lock at a6985cc4383f111385268065fc8ba044c172b142. SetPresentation no longer
calls whole ConfigFile.Save for two strings. It normalizes View/Page as before,
prepares both values against one file snapshot and publishes once. Only after
successful disk publication are UiView/UiPage and their BepInEx entries updated.
Existing Panel.Navigate exception feedback remains; failure keeps last committed
model/cache values. Parent creation preserves the former first-file behavior.

ConfigLine's existing parser is extracted as a pure in-memory edit. New WriteBatch
validates the request and applies each edit in memory; a late second-key refusal
cannot publish the first. The exclusive-temp/flush/replace publisher runs once.
Existing single-key Write delegates to the same path. Both share an in-process writer lock so F7 and presentation cannot lose each other's file snapshot. BOM, each original line
separator, whitespace and unowned data/EOF remain. Missing keys/section retain
the existing insertion layout. This is not concurrent external-edit admission.

Only this presentation writer changes; general Settings.Save, migrations/tune,
binding writers, capture/input, cameras, packages/installers and owner state are
outside the slice. STD-044 remains partial. No public push/install/runtime rig.

Fixture uses actual Settings.cs, production Core as ProjectReference and actual
BepInEx.Core ConfigFile (DLL1D492EEECD40CD4FEB7DC35AFF7F08839928AC1637D4993BBDE0060DE36B7E0A,
copied read-only from canonical dependency into this tree's ignored lib/core).
The canonical SemanticVersioning2.0.2 dependency is also explicitly referenced (34816B, SHA256 A5B57607225A0EBDE049E93469F49E5B2D07A7D602A03864CE3A72CCF57103A2). No fake ConfigFile. Managed Core/Bep libraries load inside the inert fixture;
the actual Woden plugin/game/native/device/UI never loads or runs. All file paths
are disposable project evidence. No owner config/Steam/device/FFB operation.

Planned original SetPresentation3checks/1 exact-byte regression, fixed focused
suite covering four byte variants, actual cold ConfigFile readback, missing key/
section, late Page duplicate, invalid batch requests, locked/readonly, unchanged
failure model/cache, malformed/encoding refusal, first-file/normalization and
the existing seven actual ConfigLine checks. No broad Core/UI suite claim.

## Qualification and retained attempts

The original real Settings.SetPresentation+Bep ConfigFile whole-save exposed
**3 checks/1 expected exact-byte failure** in cpu02. Original source was linked
as Settings.baseline.cs; Core was the current dependency, which the old method
did not use to write. This is an actual managed dependency/consumer regression,
not a fake ConfigFile serializer. No game/plugin/native behavior is implied.

- cpu01/GO510337: setup refusal,8.3158095s. Baseline/Core built; actual ConfigFile.Save
  could not resolve SemanticVersioning. Dependency added only to the fixture;
  product code unchanged. Result AD1186DD911BBDD97029C908C1692984F1C84B45219BA9933A5147A5E40D291D,
  pre-release audit F25281D521DC3758960584A1A1F3E20E65597E6B54461AD9E2336406B59C6266.
- cpu02/GO510508: genuine original RED, then fixed build/first20 assertions passed;
  expected duplicate Page threw InvalidDataException, which the fixture's refusal
  catcher omitted.11.1132158s. Raw result20640958A1E54DD573489B6B32EA6F28502E78A24ABC52CFFBB127695BB9D665,
  pre-release auditDE6BD86A0DDABAC90B9F913F6012C99C363A7EB6A87E0F9CF85941FB9DA795CE.
  Only the fixture catch changed atdea9b20181ffc3148f7375f7213958d26c984e79.
- cpu03/GO510604 per510607: **58/58 PASS in5.052293s**, original RED admitted
  from pinned cpu02 result/DLL/stdout/identity, no baseline replay. Actual Settings
  and Core compile against net6.0 with real Bep/Sem libraries;0warnings/0errors.
  Core SHA256 D3B43B098C9638CC62FEA40B40196E93FE98125360B6DDB8988F44551C59E01A,
  built and exercised inside the managed fixture (not an un-loaded claim).

Final source dea9b201; product code identical toa6985cc. Final frozen plan
4D42B4895A62C213399D0CB4DB5AD736F7E1535F3D0A55E53C08455B2EA4C2C1,
runner125EAE2B8494F39E6FCF947175883563795CFD0D9DA3B35BDF3BCC8A6B0639C9;
90seconds/15reserve/onecompiler, TWO held dotnet roots, offline NuGet/cache and
server/node reuse disabled. Final result SHA256
7882AA5824936C9E884B9B1F7C6DE6905DEA0D30390EEFF38FABE647CB0B435C;
independent ALL-image scoped identity audit
501E2C745528662C4616C38003CF51B191F0FC378D05241220116BBA082592DE before
exact lease exit. Two held roots/eight observed identities, normal exits/no force,
jobempty/handles0, audit clear and lease released. All three intervals released;
no CPU/live/custody hold. Preserve bld/presentation-qualification cpu01..03/raw inputs.

The suite includes the seven unchanged actual ConfigLine checks, exact real-Bep
cold readback, late-second-key refusal and unchanged model/cache, real locks,
missing fields/section, malformed/encoding refusal, first-file normalization,
unrelated-temp retention and two-thread/different-key serialization. Broad Core/UI
suites and actual Woden plugin DLL build/load were not performed. No runtime UI,
physical input/force, installed state or owner acceptance claim.

General Settings.Save/ConfigFile.Save and full JSON binding serialization remain
open; those writers do not gain this helper's serialization/admission claim.
Later docs/ledger changes need no test replay. Public push remains unperformed.