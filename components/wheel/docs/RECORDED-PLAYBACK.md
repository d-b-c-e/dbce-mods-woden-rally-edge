# Recorded owner drive and force-signal reprocessing

Woden supports `signal-reprocess` under the toolkit's recorded-playback v1 contract. The original toolkit Recording JSONL remains the source artifact and retains its separate unpublished pin. The adapter reruns the actual managed `ForceSignal` offline and emits ordered, normalized constant-force observations with `physicalOutput=false`. It never initializes a wheel, launches the game, injects input or claims deterministic Unity physics replay.

## Owner command

Run this only when the owner is seated and ready:

```powershell
.\tools\Start-RecordedGame.ps1 -CaseId corner-unwind-baseline -AttendedFfb
```

Without `-AttendedFfb`, the requested launch records all available model inputs while suppressing physical force and preserving the saved FFB On/Off preference. The request expires after 15 minutes and is consumed once. The command waits a bounded time for Woden to start and exit, but never stops a live process on timeout. Normal exit is required for a completed source footer. It then reruns the model offline, validates driving coverage and prints the exact source, observation, manifest and hashes.

`-PrepareOnly` creates the expiring request and expected path without launching Woden, reading hardware or applying force. It is the fixture/review path. The menu can stop an active recording but intentionally has no Start action.

Each request writes under:

```text
<game>\BepInEx\WodenRecordings\request-<request-guid-n>\
  source.jsonl
  force-config.json
  capture-profile.json
  case.json
  force-observation.jsonl
```

The first three files are created by the installed consumer. `case.json` and `force-observation.jsonl` are produced only after source validation and offline reprocessing. Existing files are never replaced.

## Identity and evidence levels

The source metadata contains the request/case identity, actual plugin version and hash, runtime source revision, supported GameAssembly hash, no-force/attended mode, force-config hash and capture-profile hash. `force-config.json` records model 3's effective tuning. `capture-profile.json` records game/plugin/config/binding identities and the effective saved wheel/FFB selection state without copying owner configuration files into the repository.

The case manifest hashes the exact source/config/profile files. The observation header hashes the exact `case.json` bytes as `caseSha256`; changing the source or manifest therefore creates a different case identity even when the human case name is reused. Original recordings and owner data remain local and excluded from Git/packages. The verified baseline is also immutable: a tuning trial refuses changed source, case or baseline bytes and writes only to a new observation path.

Keep these claims separate:

1. **Structurally valid source:** complete footer and no dropped, rejected, invalid, limited, queue-full or contended records.
2. **Sufficient driving coverage:** at least 50 force-model samples and one second of actual `sample.driving=1`; idle-only historical recordings fail this gate.
3. **Reproducible model signals:** the actual `ForceSignal` reproduces recorded validity, reason, reset, alignment, damping and preview values before a completed observation stream is written.
4. **Game/physical acceptance:** requires an attended run and owner feedback. Equal observation files do not prove deterministic game execution, physical torque or good force feel.

## Stream semantics

Every sampled model evaluation records `ffb.modelValid`, `ffb.modelReason`, `ffb.modelResetBefore`, `ffb.modelResetAfter` and `ffb.gate`. Ordered string markers retain the human-readable model reason and gate transition. `sample.discontinuity` identifies the current sample's reset before serialization.

Model reason codes are: 0 estimated tyre signal, 1 low-speed fade, 10 inactive state, 11 discontinuity, 12 missing motion, 13 reverse, 14 invalid tuning, 15 missing front contact, 16 invalid front contact, 17 front wheels airborne, 18 force sample gap, and 99 unsupported/unknown. Gate codes are: 0 active, 1 diagnostic force suppression, 2 saved Off, 3 faulted, 4 settings open, 5 unfocused, 6 stock owner unresolved, 7 camera not owned, 8 wheel input unavailable, 9 invalid model, 10 wheel not connected, and 11 write failed.

The offline adapter refuses unknown gate/model versions, missing required driving channels, tuning changes within one capture, non-monotonic clocks/reset epochs, mismatched recorded model results and inadequate driving coverage. It emits one `steering` / `constant` / `set` observation for each actual recorded model evaluation. Magnitude is the managed model's normalized preview before the native toolkit, output watchdog and wheel; it is not a delivered command or measured torque.

Trials replay the recorded reset epochs and output gates. This matters for shaping history: the normal attended path can accumulate ramp/smoothing while output is active, while the default force-suppressed capture records a blocked gate that resets shaping after every model sample. Use `-AttendedFfb` for the owner's requested force-feel capture; the no-force mode remains useful for source/channel validation but cannot represent the same continuous shaping history.

## Offline commands

For normalization, `ffb-export` accepts either a sealed stage recording or an
ordinary completed owner case from `Start-RecordedGame.ps1`. The ordinary path
verifies the original source, config, profile, case and baseline observation,
then replays the recorded gate/reset lifecycle before evaluating the trial.
It preserves Grip v4/v5 selection, refuses unavailable friction curves, and
never substitutes the continuously evaluated stage shadow for an attended drive.

```powershell
# From components/wheel; no game or device is opened.
dotnet run --project tools\TelemetryInspector -c Release -- ffb-export '<case-directory>' '<new.csv>' 50 25 grip 1.15
```

The explicit strength and load ratio are counterfactual settings; use the
recorded ratio when comparing the felt tune. Omitting `grip <ratio>` runs
Classic with the explicit peak. CSV `request` includes model damping but no
crash cue; `steering` and `damping` expose the evaluated Grip terms separately.
Map normalization validity to `eligible`, not merely `valid`: inactive,
blocked, invalid and provisional-reference rows remain in the timeline but
are excluded. Unobserved non-driving speed is blank; missing driving speed
is refused. Any stage marker requires a valid stage seal, even if other
stage files are missing.

Woden has no F11 capture-start shortcut. Prepare the one-launch ordinary
recording with the command in Owner command; use `-AttendedFfb` only
for an explicitly requested owner-attended drive. Normal exit completes the
case. A default muted ordinary case preserves the blocked lifecycle and is
not a substitute for that felt drive. See the
[ordinary-export verification](../../../docs/2026-10-08-ordinary-force-export.md).

```powershell
dotnet run --project tools\TelemetryInspector -c Release -- reprocess '<case-directory>'
Copy-Item '<case-directory>\force-config.json' '<case-directory>\candidate-force-config.json'
# Edit only the copied candidate, then write a new observation path:
dotnet run --project tools\TelemetryInspector -c Release -- trial '<case-directory>' '<case-directory>\candidate-force-config.json' '<case-directory>\force-observation.trial.jsonl'
dotnet run --project tools\TelemetryInspector -c Release -- compare '<baseline.jsonl>' '<candidate.jsonl>' 0.000001
```

`trial` first verifies all manifest hashes and independently reruns the original config against the baseline. It then hashes the separate candidate config, reruns actual `ForceSignal` without requiring candidate preview values to equal the recording, writes a new observation with the original `caseSha256`, and compares it. It never rewrites the source, case, baseline, original config or profile. Count or timeline changes make comparison unavailable rather than producing a magnitude summary by index.

The shared reference validator may independently validate the manifest and compare observations. Woden retains its C# adapter because sampling, model/reset interpretation and source readiness are consumer-owned. Toolkit commit `87b47d53121c0b3fc8479e622ae8a79c8ca3fa58` defines the adopted source-only contract; the native toolkit and unpublished Recording binary pins are unchanged.
