# Explicit Native observation entry - source handoff

Supersedes the scene-entry gap in the prepared capture plan. Based on reviewed
f61 close-release source, merged into the isolated diagnosis branch. No launch,
device access, config write or build occurred while authoring this adapter.

## Exact entry

The existing companion accepts optional `-NativeObservationSource <sealed Kenya
reference>` and `-NativeObservationSeconds 10` together with `-InputOrigin automated`
and no `-AttendedFfb`. Ordinary requests without those flags retain their path.
The consumed version-2 request bounds observation to5..15seconds and five-minute
expiry, requires force suppression and automated origin, and claims once.

`Runtime.Start -> NativeObservation.Begin -> StagePlayback.MuteNativeObservation
-> StageStartup.InitializeNativeObservation(reference)`. Normal frame ticks call
`StageStartup.TickNativeObservation(now) -> NativeStep(now)`; existing scene,
sealed context, round/rules, player/car and 55-second per-scene checks stay in
charge. Total entry/observation deadline is180seconds. There is no replay,
ConfigureCarTrial, input injection, device discovery or extra poll.

The observation branch preserves live Arcade transmission slots, honoring the
saved NativeGearbox choice for player0; it verifies the actual car agrees. It
does not enter the input-trial Manual path or Shim trial gate. Native backend,
bindings/calibration and force tune remain unchanged. Network delivery must be
disabled before startup; the existing stage output mute remains latched.

Only the existing GameSampler result after publication is observed. The first
eligible, validated car starts the current-physical-reader interval, with raw,
normalized and delivered throttle/brake/handbrake plus steering required. The
first sequence and cached bound handbrake button are logged; no rest or historical
reproduction is inferred. The last sequence/count and fresh completion are
logged. Missing required values end unavailable. Then the original recorder
stops and the game exits normally after8seconds. No synthetic driving phase is
part of this first run. MainCar is required; no front-end-only launch is proposed.

## Focused build/check commands (after assigned shared-lease entry)

Use the existing affected Plugin/Core build plus Core executable once. Its exact
focused command is:

```powershell
dotnet exec <checks>/WodenRallyEdge.Tests.dll --only 'native observation launch requires bounded automated no-force identity'
```

The authored suite checks bounded identity/expiry, positive admission and unsafe
no-force/origin/path/duration/legacy/incomplete refusals. It is not runtime proof.
Combined Help host fixtures run `WodenRallyEdge.UiTests.dll --help-report-only`;
do not repeat the already-qualified close suite or the common reporter baseline.
Both changed PowerShell scripts parse without errors; `git diff --check` is clean.

## Concrete supervisor command (not executed)

Reuse `tools/controls/Run-OrdinaryStopSmoke.ps1`'s exact package/native pins,
owner-file/raw-preference snapshot, exact companion/game identity, normal exit
and restoration. Its optional observation flag changes only the bounded wait
and verdict from the existing menu-only smoke. It does not call PrivateApply,
switch backend or submit driving input.

```powershell
pwsh -NoProfile -File tools/controls/Run-OrdinaryStopSmoke.ps1 `
  -PackageRoot <exact-private-muted-candidate> -PackageManifestSha256 <manifest-pin> `
  -NativeCandidatePath <qualified-088F1089-native> `
  -NativeSha256 088F1089BEB6D18D478F37582C59330C6BDA6BC37D5E365F6BA7EBF80040F567 `
  -NativeProvenancePath <qualified-provenance> `
  -NativeProvenanceSha256 6068B7A848F85FE42AE88EE8A86B10F1EF4C4E307B9C4235BE44C1A84BBEA8E5 `
  -OriginalReceiptSha256 A93D0E5661E4826F7F5CBDF0C2410769AF81F5B7E72BCE41CE228B606777315E `
  -GameDir 'D:/Program Files (x86)/Steam/steamapps/common/Super Woden Rally Edge' `
  -Result <new-private-run-directory> -RigLeaseToken <exact-caller-token> `
  -NativeObservationSource 'C:/Users/antho/AppData/Local/Dbce/StagePlayback/references/woden-kenya-20261005' `
  -NativeObservationSeconds 10
```

Package/output pins are populated only after the one combined candidate compile
and exact private mute package composition; prior f61 output pins are not pins
for these source changes. No production install or launch is granted by this
document. Preserve UAT snapshot run-01 and take a fresh owner snapshot at slot
entry: restore the current post-UAT config/tune/preferences, not an older A9D5
baseline. Idle force-case refusal may coexist with valid original input evidence;
source identity/footer, raw/normalized/delivered coverage and explicit observation
completion remain required. Exact rollback/process/lease closure precedes handback.

Supervisor-only successor: revision4/5 admission now checks the exact set/count
of manifest-owned plugin payloads, keeping per-file, capability and native pins.
This admits the current revision5/23-file package without changing the frozen
combined4f153 managed source or repeating its build (sana164624).
