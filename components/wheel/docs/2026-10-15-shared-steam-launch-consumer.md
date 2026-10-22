# Woden shared Steam launch consumer

Source-only successor to the three retained failed activation attempts. It
consumes shared helper commit af22ae39624f13b377830dc5d046fe8da55c08c6,
SHA256 8CE6326E1A67207E1EE56418AA02BADF5D76F9457CA0532B19B1ED8A3266A2DE.
Shared qualification168570 (actual DDE7B711 / independent2F571C58) is reused.
Do not rerun its46 cases, declaration compile, camera22 cases, existing close33
cases, owned-window native checks or prior package/helper checks.

The baseline is the independently closed168891 owner install
0.2.16+3eb6bbade862debb3270eda6d80f9a0429a5764a, receipt
A7511081F4917FA1B9E75661EAE07EBAF52F39860CD31CC5973137DF06177149.
Plugin0D4B23B1/Core55D0AD7F, config97BBB732, bindingsEC81BEB0, normal forceF836
and shim4AF2 are retained. Owner Physical FOV, Native Automatic and tuning are
unchanged. D45/A93 describes historical runs only.

## Consumer boundary

`Start-RecordedGame.ps1 -ExternalLaunch` is restricted to the explicit automated
Native observation with forces suppressed. It creates the same one-shot
request/nonce and performs the same status wait, finalization and original-source
handling. It issues no launch. PrepareOnly and ordinary interactive use keep
their existing separate behavior. This script change is a private candidate,
not an unreviewed replacement of the installed companion.

`Run-OrdinaryStopSmoke.ps1` retains the existing private load-muted native,
installer admission, source identity, saved files/preferences and restore gates.
Its explicit successor requires the pinned shared helper and exact Steam image.
It starts the external-launch companion, verifies the prepared nonce/mute, then
uses the shared helper's observed direct/broker decision with exact Woden image,
working directory and AppID3218630. Child DBCE_FFB_MUTE=1 and Steam IDs come from
the helper. No ordinary foreground-request fallback or focus-gate relaxation
is added. Token match is launch admission only, not SteamAPI/focus proof.

Shell-request and game Process objects/handles are retained; PID/full100ns
start/path enter an atomic launch-custody receipt before token/readiness checks.
The parent imports this receipt into canonical E58A/BE13 custody independently
of recorder status. The actual helper's Complete-Owned census/lineage gate also
runs before the leaf restores owner files. An attempted launch without an exact
adopted game stays unknown even with an empty current scope. A timeout never
means absence. Existing Explorer/Steam are never owned or stopped. Both exact
game/companion closure and all scoped descendants must be safe before restore;
parent release and separate independent inspection remain required afterward.

## Valve restart applicability

[Valve's Steam API contract](https://partner.steamgames.com/doc/api/steam_api#SteamAPI_RestartAppIfNecessary)
says a restart may select the installed library executable and that an AppID
file suppresses that restart. This Woden consumer deliberately launches the
actual installed library image, not a disposable executable copy. It verifies
appmanifest3218630's appid/installdir resolve to the supplied game directory,
plus the existing guarded GameAssembly hash. Thus the copied-image escape
problem from other consumers does not justify a new owner steam_appid.txt.
No AppID file is created. An existing file must match3218630 and its exact
presence/bytes join the existing owner inventory. A different/restarted PID
cannot inherit recorder admission; observed exact path/PID/start must match.
The local manifest confirms this installed path; actual SteamAPI initialization
and any restart behavior remain runtime observations, not source promises.

## Focused checks and the one queued run

Only parser checks have run during preparation. The new source-linked
`tools/controls/Test-OrdinarySteamConsumer.ps1` is authored, unexecuted: exact
Woden specification, attempt latch, early shell/game publication and handle
order, token-refusal custody, pre/post-request distinction, unknown and live
scope refusal, and separate ExternalLaunch/PrepareOnly modes. Its process/shell
seams are fake and it does not execute the common qualification again. Run it
once in the next named batch; no C# build or hardware/native fixture is needed.

Private composition starts from the already-qualified3eb camera package. Only
the companion script and existing qualified no-force native088F are replaced;
normal nativeF836 is restored afterward. Managed3eb DLLs/finalizer/helper are
copied byte-exact and their passed checks inherited. No E9 owned-window probe
DLL enters this package, and no public or installed package is published here.

The one later named run retains the existing recorder/GameSampler and force,
state, source completion and real OS focus gates. Correlate matched eligible
raw/normalized/controls/game brake and throttle with speed, grounded, fuel/oil,
status/lock, actualPower and final wheel motor/brake torques. Preserve sample
phase: car postfix occurs before Unity's next solve. F6 attribution uses
installed3eb's named stock-blocker logs if exercised in the coordinated run;
absence of a blocker observation is not proof of neutral input. No second
observer, raw input injection, physical rest claim or deadzone/tune change.
The static trace is isolated commit3849d9e, observed-small-brake-source-trace.md.
If no positive-throttle or close samples occur, report those gates unobserved.

Steam runtime stays queued during owner Steam use and Mudo's build. Before any
launch/screens/input, require coordinator owner notice and a fresh actual
handoff/predecessor/lease. This source packet does not grant that slot.

## Post-failure consumer successor (source only)

The one171cc/508281 run passed30 new consumer assertions and composition but
stopped before shell/game request: shared ShellId collision and consumer strict
CIM/native tick equality. Owner recovery is independently closedE2680C0F;
original29853475/CLEANUP_PENDING is retained at hubf1f227a3. No game samples.
Beni's shared helper successorbb8faaf/ECD5703B passed the actual High->Medium
shell-context branch read-only; no common46 rerun is needed.

WodenLaunchCustody.ps1 uses canonical BE13 conversion/disposition for the
existing E58A map plus explicitly labeled leaf records. Canonical native roots
keep100ns precision; E58A-admitted descendants have CIM microsecond provenance.
A compatible coarse record can refine to native100ns; a later coarse record
cannot erase it. Two disagreeing native records, real PID reuse, unknown/malformed
precision or failed census refuse the entire receipt without partial map changes.
No elapsed-time tolerance, new OS query, PID cleanup authority or separate
identity framework is added. The leaf now publishes the actual producer
precision for its lineage, retaining native registration/handles before token
checks. New source-linked pure cases cover only that changed precision seam.
Reuse the passed30 consumer cases and exact D0F4D785 composition; do not rerun
or recompose unchanged payloads. The separate corrected packet must have fresh
pins, handoff/notice and lease before one later observation.
