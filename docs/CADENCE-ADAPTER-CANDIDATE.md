# Offline cadence adapter candidate

The earlier dependency gate is resolved in isolated source with a **shared-owned pure sidecar project**, `Dbce.Wheel.Ffb.Cadence`. It compiles the reviewed toolkit arithmetic directly; no shared algorithms are copied into Woden, no live toolkit WIP is used, and no pinned input/native/telemetry binary is replaced.

`CadenceForceAdapter` calls that shared API using an injected monotonic output clock, the current saved peak percentage and the existing 1.5/s limit. It reserves zero collision headroom and always supplies `impactInputsVerified: false`. There is no Trigger route. First call and cap changes prime at zero using a genuine timestamp, with no invented interval. Rejected intervals and nonfinite signals return failure; the actual controller immediately invokes its guarded `ZeroAndStop` route rather than merely resetting arithmetic state. Existing pause/focus/camera/model/write-failure gates continue to apply.

Normal builds and normal controller construction retain legacy output. The shared dependency property compiles the adapter for tests; it does **not** activate runtime cadence. An explicit source build with `WodenCadenceEnabled=true` and `SharedCadenceProject` selects the candidate at the production seam, using `Runtime.Clock.Elapsed.TotalSeconds`. No user setting, default, gain, cap, watchdog or native API changes. Do not install or package that build under the approved release identity.

Telemetry keeps `ffb.preview` as the legacy model preview and `ffb.sent` as the accepted candidate command. Conditioning rejection retains numeric invalid-output gate 9 but uses the distinct `ForceGate` string `conditioning-invalid`; it does not falsely mark a valid structural model invalid. Existing `signal-reprocess` remains structural-preview analysis, not reconstruction of candidate cadence output.

## Device-free verification

- Actual plugin candidate compiles with zero warnings/errors alongside the legacy FFB assembly.
- Actual controller tests cover cap, output-clock slew, zero priming, duplicate/rewound/stale/nonfinite clocks, nonfinite contacts, pause/resume and failed output writes.
- The immutable original source SHA-256 is checked before reading: `785d8d679c9dc654ea860212074b920b59944d0c6315ad40a2e9f26bd83d23b8`. All 8,251 recorded model rows traverse actual legacy/candidate controllers. Original recorded eligibility gates are applied through the fixture's gate suppression; this is a controlled comparison, not reproduction of every historical runtime lifecycle.
- Both routes produce 6,922 fake-device writes. 192 outputs differ above 0.000001; maximum normalized difference is `0.008741524070501328`. Candidate controller stops 41 times. First/cap-change zero priming accounts for differences beyond the previously reviewed standalone limiter comparison.
- Candidate suite: 38 suites, 17,640 assertions, zero failures. Recorded sample elapsed time is an injected software timing scenario, **not** evidence of actual native call cadence. Synthetic clock tests independently exercise that seam.

Reproduce from the isolated checkout with `dotnet run --project components/wheel/tests/WodenRallyEdge.Tests -c Release -p:SharedCadenceProject=<shared-checkout>/dotnet/Dbce.Wheel.Ffb.Cadence/Dbce.Wheel.Ffb.Cadence.csproj`, using the offline restore configuration. Set `WODEN_CADENCE_SOURCE` to the immutable original source JSONL for the recorded comparison; omission runs only synthetic timing fixtures. Production compile additionally uses `-p:WodenCadenceEnabled=true`.

Collision activation remains blocked by the missing verified production event producer. Physical feel, force calibration, Unity runtime loading and display acceptance remain unverified. The approved delivery ZIP and its identity remain unchanged. These two source candidates require independent review; no publication, live incorporation or deployment was performed.
