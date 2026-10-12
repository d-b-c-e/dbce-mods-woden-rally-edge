using Dbce.Wheel.Recording;

namespace WodenRallyEdge.Core;

/// <summary>Capture-local steering/damping history. No device, output gate or crash stage participates.</summary>
public sealed class ForceAnalysisHistory
{
    public const string Contract = "woden.force-analysis@1";
    public const string Scope = "Independent capture-local steering+damping history; recorded production front-load reference; no crash cues, delivery gates or device writes";
    private readonly ForceSignal _classic = new();
    private readonly GripSignal _grip = new();
    private ForceOptions? _options;
    private long _car, _epoch, _tuneEpoch;
    private double? _time;

    /// <summary>Records only analysis.*. Existing sample state, ffb.* and crash.* remain untouched.</summary>
    public ForceResult Record(TelemetrySample sample, ForceOptions options)
    {
        long car = Integer(sample.Channels, "ffb.grip.carEpoch");
        var reference = ReadReference(sample.Channels);
        _ = ForceObservationSemantics.ModelId(options.Model);
        bool first = _options == null, tune = !first && _options != options;
        // Boundary bits: capture start, new car, tune/model, simulation gap, source discontinuity, non-driving.
        int boundary = (first ? 1 : 0) | (!first && car != _car ? 2 : 0) | (tune ? 4 : 0);
        bool timeGap = !double.IsFinite(sample.SimulationSeconds) || _time.HasValue &&
            (sample.SimulationSeconds <= _time.Value || sample.SimulationSeconds - _time.Value > FrontLoadReference.MaxGapSeconds);
        if (timeGap) boundary |= 8;
        if (sample.Discontinuity != null) boundary |= 16;
        if (!sample.Driving) boundary |= 32;
        if (first || tune) _tuneEpoch++;
        if (boundary != 0) { _classic.Reset(); _grip.Reset(); _epoch++; }
        _car = car; _options = options; _time = double.IsFinite(sample.SimulationSeconds) ? sample.SimulationSeconds : null;
        var input = sample;
        if (timeGap && sample.Discontinuity == null)
        { input = CopySample(sample); input.Discontinuity = "force sample gap"; }
        long before = options.Grip ? _grip.ResetCount : _classic.ResetCount;
        var result = options.Grip ? _grip.Evaluate(input, options, reference) : _classic.Evaluate(input, options);
        long after = options.Grip ? _grip.ResetCount : _classic.ResetCount;
        if (boundary == 0 && after != before) _epoch++; // invalid contact/reverse also breaks shaping history
        foreach (string key in sample.Channels.Keys.Where(k => k.StartsWith("analysis.force.", StringComparison.Ordinal)).ToArray()) sample.Channels.Remove(key);
        sample.Add("analysis.force.contractVersion", 1);
        sample.Add("analysis.force.epoch", _epoch); sample.Add("analysis.force.carEpoch", car);
        sample.Add("analysis.force.tuneEpoch", _tuneEpoch); sample.Add("analysis.force.boundary", boundary);
        sample.Add("analysis.force.preview", result.Preview); sample.Add("analysis.force.valid", result.Valid ? 1 : 0);
        sample.Add("analysis.force.reset", after); sample.Add("analysis.force.modelVersion", options.Model);
        sample.Add("analysis.force.reason", !sample.Driving ? 10 : sample.Discontinuity != null ? 11 : ForceObservationSemantics.ModelReason(result.Reason));
        if (options.Grip) { sample.Add("analysis.force.steering", _grip.LastSteering); sample.Add("analysis.force.dampingTerm", _grip.LastDamping); }
        return result;
    }

    /// <summary>Stage recording owns a bounded numeric snapshot, never the ordinary publisher's mutable sample.</summary>
    public static TelemetrySample CopySample(TelemetrySample s)
    {
        var copy = new TelemetrySample { SessionId = s.SessionId, Sequence = s.Sequence, ElapsedSeconds = s.ElapsedSeconds,
            SimulationSeconds = s.SimulationSeconds, CarInstanceId = s.CarInstanceId, State = s.State,
            Discontinuity = s.Discontinuity, ForceStatus = s.ForceStatus, ForceGate = s.ForceGate, ForceModelReason = s.ForceModelReason };
        foreach (var p in s.Channels) copy.Channels.Add(p.Key, p.Value);
        copy.Unavailable.AddRange(s.Unavailable);
        return copy;
    }

    public static bool Declared(IReadOnlyList<SessionRecord> records)
    {
        var metadata = records.SingleOrDefault(r => r.Kind == SessionRecordKind.Metadata)?.Metadata;
        if (metadata == null) throw new IOException("Analysis metadata unavailable");
        if (!metadata.Properties.TryGetValue("analysisForceContract", out string? contract))
        {
            if (records.Any(r => r.Kind == SessionRecordKind.Sample && r.Sample.Channels.ContainsKey("analysis.force.contractVersion")))
                throw new IOException("Analysis rows lack a declared contract");
            return false;
        }
        if (contract != Contract || !metadata.Properties.TryGetValue("analysisForce", out string? scope) || scope != Scope)
            throw new IOException("Unsupported force analysis contract/scope");
        return true;
    }

    /// <summary>Caller verifies the complete source's seal/case identity and original actual-force or legacy stage evidence first.</summary>
    public static IReadOnlyList<RecordedForceTrialSample> Replay(IReadOnlyList<SessionRecord> records, ForceOptions original, ForceOptions? candidate = null)
    {
        if (!Declared(records)) throw new IOException("Independent analysis unavailable: capture has no " + Contract);
        if (records.Any(r => r.Kind == SessionRecordKind.Marker && r.Marker.Name == "capture-failed"))
            throw new IOException("Independent analysis unavailable: capture failed");
        var verify = new ForceAnalysisHistory(); var trial = new ForceAnalysisHistory();
        var rows = new List<RecordedForceTrialSample>();
        foreach (var record in records)
        {
            if (record.Kind != SessionRecordKind.Sample) continue;
            var row = record.Sample; var c = row.Channels;
            bool force = c.ContainsKey("ffb.tuning.modelVersion");
            if (!force)
            {
                if (c.Keys.Any(k => k.StartsWith("analysis.force.", StringComparison.Ordinal)) || Required(c, "sample.driving") != 0)
                    throw new IOException("Analysis row lacks production force inputs");
                continue;
            }
            if (Required(c, "sample.driving") is not (0 or 1) || Required(c, "sample.discontinuity") is not (0 or 1))
                throw new IOException("Invalid analysis source state");
            var sample = new TelemetrySample { Sequence = row.Sequence, ElapsedSeconds = row.ElapsedSeconds,
                SimulationSeconds = Required(c, "sample.simulationSeconds"), State = c["sample.driving"] == 1 ? "driving" : "inactive",
                Discontinuity = c["sample.discontinuity"] == 1 ? "recorded-discontinuity" : null };
            foreach (var p in c) sample.Channels.Add(p.Key, p.Value);
            verify.Record(sample, original);
            var keys = sample.Channels.Keys.Where(k => k.StartsWith("analysis.force.", StringComparison.Ordinal)).ToArray();
            if (c.Keys.Count(k => k.StartsWith("analysis.force.", StringComparison.Ordinal)) != keys.Length)
                throw new IOException("Missing/unknown analysis fields at sample " + row.Sequence);
            foreach (string key in keys)
            {
                double expected = sample.Channels[key], actual = Required(c, key);
                double tolerance = key is "analysis.force.preview" or "analysis.force.steering" or "analysis.force.dampingTerm" ? .000002 : 0;
                if (Math.Abs(expected - actual) > tolerance) throw new IOException($"Recorded {key} differs at sample {row.Sequence}");
            }
            var options = candidate ?? original;
            var result = trial.Record(sample, options);
            var reference = ReadReference(c);
            string exclusion = !sample.Driving ? "not driving" : !result.Valid ? "model invalid" :
                options.Grip && reference.Kind == FrontLoadReferenceKind.None ? "no front-load reference" :
                options.Grip && reference.Kind != FrontLoadReferenceKind.Driving ? "provisional reference" : "";
            double? speed = c.TryGetValue("motion.speed", out double ms) && double.IsFinite(ms * 3.6) ? Math.Abs(ms * 3.6) : null;
            if (sample.Driving && !speed.HasValue) throw new IOException("Analysis driving speed unavailable");
            rows.Add(new(row.ElapsedSeconds, (long)sample.Channels["analysis.force.epoch"], result.Valid, speed, result.Preview,
                options.Model, exclusion.Length == 0, exclusion, options.Grip ? (float)sample.Channels["analysis.force.steering"] : null,
                options.Grip ? (float)sample.Channels["analysis.force.dampingTerm"] : null));
        }
        if (rows.Count == 0) throw new IOException("No force analysis samples");
        return rows;
    }
    private static FrontLoadReference ReadReference(IReadOnlyDictionary<string, double> c)
    {
        var r = new FrontLoadReference();
        long kind = Integer(c, "ffb.grip.referenceKind");
        if (kind > 2) throw new IOException("Invalid ffb.grip.referenceKind");
        r.Restore(Required(c, "ffb.grip.reference"), (FrontLoadReferenceKind)kind);
        return r;
    }
    private static long Integer(IReadOnlyDictionary<string, double> c, string key)
    {
        double n = Required(c, key);
        if (n < 0 || n > 9_007_199_254_740_991 || Math.Truncate(n) != n) throw new IOException("Invalid " + key);
        return checked((long)n);
    }
    private static double Required(IReadOnlyDictionary<string, double> c, string key) => c.TryGetValue(key, out double n) && double.IsFinite(n) ? n : throw new IOException("Missing/nonfinite " + key);
}
