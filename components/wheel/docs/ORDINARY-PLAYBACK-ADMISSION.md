# Ordinary playback admission (S2)

S2 lets an S1 original ([ORDINARY-FULL-SESSION.md](ORDINARY-FULL-SESSION.md)) be played back cold through the
existing `native-arcade-v1` startup. That startup restores the **recorded stage choice** (Arcade route, car,
skin, transmission, camera) through Woden's own native loads, then plays the recorded stage. It does not replay
every menu input, and it presses no menus.

## Route gate (`ArcadeRouteProof`, used by `StageStartup`)

The sealed `stage-context.json` must show all of the following:
- single-player Arcade/Arcade, round 0 of its route;
- no rivals, ghost or rolling start;
- the selected Arcade car equal to the driven car;
- an observed route **ending** Arcade map → (loading)* → stage presentation → (loading)* → stage.

Scenes before the map are evidence only, so a launch without the daily message, or with menu wandering, is
accepted.

Where a native load into the presentation was observed:
- it must come from the map;
- its selection must equal the context's on every field the startup writes;
- later native loads may go only to loading scenes or the stage.

Contexts that observed no native loads (the archived Kenya original) report `absent` load evidence. Truncated
scene or load lists are refused.

This replaces only the former exact-path check. Every other `StageStartup` check, the muted replay and the
supervised runner's muted-capture review are unchanged.

## Derived package (`FullSessionAdmission`, `TelemetryInspector admit`)

```
TelemetryInspector admit <original-package> <derived-package> <gameAssemblySha256>
```

The command verifies the original:
- the outer seal and its exact inventory;
- the S1 descriptor (no adapter, no markers);
- the timeline for playback;
- the nested stage seal, every aligned signal row, and the completed source footer;
- the context build;
- the route gate;
- one stage and car across the timeline, tape and context.

It then writes a **sibling** package and never changes the original (hashed before and after):
- the four payloads copied byte-identical;
- `admission.json` (`woden.full-session-admission@1`): `evidence: route-compatible`,
  `coldReplay: not-qualified`, the adapter `woden.native-arcade-v1@1`, scope, route, load evidence, restored
  fields, pose and pause counts, and every source hash;
- a `session.json` with the original fields, plus `playbackAdapter`, `admissionEvidence` and
  `derivedFrom {sessionSha256, packageSealSha256}`;
- an outer seal over all of them.

`route-compatible` is the compiler's proof only. A passed muted cold replay of the derived package is separate
evidence, recorded by that run against the derived seal; the admission never claims it.

Playback stays unavailable in Wheelkit until a receipt-owned capability declares the adapter.

## Ordinary review of an admitted package (S2b)

An owner drive may have had forces on. The supervised runner's `stage-review` requires a muted capture, and it
stays the default for raw `-Recording` stages, unchanged in behavior and output. An admitted package instead
gets:

```
TelemetryInspector ordinary-stage-review <derived-package>
Run-StageSession.ps1 -AdmittedPackage <derived-package> -Result <new folder>
```

Before reviewing, the command checks the derived package:
- the exact derived seal;
- `playbackAdapter` and `admissionEvidence: route-compatible`;
- that the admission's source hashes match `derivedFrom` and the nested stage seal.

It then runs the same stage review under the explicit `ordinary-output-retained` policy, with two differences:
- **`physicalOutput`** may be `true`, but the tape and the source metadata must agree. When `false`, the
  delivery counters must still be zero.
- **Delivery counters,** when output was allowed, must be truthful cumulative counts: nonnegative, whole and
  non-decreasing. Their deltas are reported, and nothing replays them.

Every tune, alignment, clock, coverage and force-reprocess check is unchanged, so the ordinary force history
(`analysis.force.*` from the stage's own history, and the recorded `ffb.*`) is proven replayable byte for byte.
The cold replay itself is always muted. The review claims `coldReplay: not-qualified`; a passed replay of the
derived package is separate evidence.

**Not yet admitted:**
- later rounds and other modes;
- results observation.
