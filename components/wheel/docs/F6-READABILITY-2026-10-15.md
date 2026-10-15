# Shared Controls initial viewport - October 15, 2026

Sana139375/139616 assigned a small presentation-only continuation from clean
`fc0df220239be7858499bd9309ec75fb07fa6862`, isolated on
`hera/woden-f6-readability-20261015`. Dine139421 confirmed Panel is outside his
private projection witness/runtime lane. Installed 0.2.15 and its frozen runtime
tuple are outside this change.

## Finding and change

At 1280x720 and 100% UI scale the logical body ends at y578. For an editable
shared profile with one observed device and no draft, the previous header put
the first binding label at y580. The initial Controls view therefore contained
setup/status guidance, the toolbar and device choice, but no actual binding.

`SharedBindingPage` now uses the existing F6 row gap and toolbar row height and
less reserved height around ordinary help text. The same fixture case places
the first label at y490 and its Active value at y524..566. Both fit initially;
Bind/Clear/Invert remain reachable through the existing scroll controls. The
source/profile, standalone setup route, opened-device limitation, draft/restart
instruction and capture hint stay above the binding list. Read-only access
reasons and active capture instructions retain their 68-unit text blocks.
Draft and saved-next-start warnings, missing-device warnings and row notes
retain their existing text and blocks.

This uses vendored `F6Metrics` and the existing `Choice`/`F6RowLayout`/`F6Flow`
renderer. The common Unity `F6Widgets` requires GUILayout, which this stripped
host does not expose; no widget framework, generated interop or toolkit change
is introduced. Existing shared palette and binding contract remain pinned.

Only Panel presentation and source-linked UI fixtures change. Binding host,
reader/capture policy, missing-device selection, unsupported row guards,
persistence, camera Physical/Adjustable semantics, force/tune and all defaults
are retained. Multiple device lines or draft/pending rows can still require
scrolling; this is not an initial-fit promise for every profile/scale.

## Focused checks and execution

The existing executable UI harness now checks the actual Panel at 1280x720 and
7680x1440 / 100%, with no body scroll before the initial-view assertions:
source/setup guidance, Save/Discard, device choice, first row and Active value,
draft/restart guidance and capture hint. It saves both draw-command snapshots.
A focused capture-state check retains visible instructions and Cancel. Existing
cross-page draft/footer/discard, row policy, native UI, camera, keyboard,
scrolling and large-scale checks continue in that same executable.

Fixture draw rectangles do not establish real Unity glyph wrapping, runtime
visual appearance, hardware capture or physical force. Those remain separate
observations; accepted force/performance do not need repetition for this patch.

After a fresh coordinator offline handoff and actual shared rig lease:

1. Populate only the isolated checkout's ignored `components/wheel/lib/core`,
   `interop`, `loader` and `local-dependencies.json` from the canonical Woden
   dependency cache; verify using its production `Verify-Dependencies.ps1`.
   Do not generate new references, copy owner settings or use sibling interop.
2. From `components/wheel`, build
   `dotnet build src/WodenRallyEdge.Plugin/WodenRallyEdge.Plugin.csproj -c Release -warnaserror`.
3. Run the source-linked executable
   `dotnet run --project tests/WodenRallyEdge.UiTests -c Release`
   and the required pure regression executable
   `dotnet run --project tests/WodenRallyEdge.Tests -c Release`.
   No `PrivateShimPath` is supplied; no real-native ABI fixture is requested.
4. Retain logs, command exit codes, source/diff identities and initial-view
   snapshots, release the lease and independently check it absent.

At preparation, no executable had run and `git diff --check` passed. The
subsequent named offline execution below qualifies these source/fixture checks;
runtime visual evidence remains separate.

## Offline qualification complete

Sana139777 granted WODEN-F6-READABILITY-OFFLINE after Kiki's actual R2 exit139831.
Exact wrapper SHA256
`4D2C4A2E84090F53F368401A5F49749E88B0FD31BB2749D02BFD06FCEA01B512`
ran once against clean runtime source `92794b8b5cade2658e0857dc54e80f887bafdad2`,
11:44:16.071..11:44:52.719 UTC. Production dependency verification passed.
Actual Plugin Release warnaserror passed with zero warnings/errors; source-linked
UI passed 5,370 assertions (including 95 managed shim/lease checks), and the
required pure harness passed 88 suites / 51,501 assertions / zero failures.
Native ABI was explicitly NOT RUN. Scoped node/compiler reuse was disabled and
remaining owned/scoped build processes were empty before exact lease release.
Release and independent lease absence both passed. Source remained clean.

Hub evidence: `artifacts/woden-f6-readability-20261015/run-01/result.json`, SHA256
`6364A24E4710158EAEC3305D5F6FDE2FE1E9FF05ACE47431A23341434A6D4760`.
Initial-view draw snapshots are `shared-controls-initial-1280x720.json`, SHA256
`862FA296B5D9EDB0A98887B79B7DE4F79365ACBE178B8C82E63F00D4C3A44ED2`,
and `shared-controls-initial-7680x1440.json`, SHA256
`59BCAFB8BB815BE1E35CEB3B8C2A67BF18584C08BA52E55D661BA2D938F49A13`.
At 720p, the actual draw records put Steering at screen y506..534 and its Active
value at y540..582, within the body's y160..594 bounds (window top16).
These are fixture rectangles, not a real Unity screenshot or glyph-fit claim.

No package, game, input, device probe or installation ran. Installed0.2.15,
owner configuration, force tune and frozen projection tuple were not changed.
No ledger adoption/runtime status is promoted by these offline results.
