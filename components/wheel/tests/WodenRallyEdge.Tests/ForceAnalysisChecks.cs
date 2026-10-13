using System.Numerics;
using Dbce.Wheel.Recording;
using WodenRallyEdge.Core;

internal static class ForceAnalysisChecks
{
    private static TelemetrySample Sample(double time, int model = 6, long sequence = 0)
    {
        var s = new TelemetrySample { SessionId = "synthetic-analysis", SimulationSeconds = time,
            ElapsedSeconds = time, Sequence = sequence, CarInstanceId = 1, State = "driving" };
        s.Vector("motion.position.world", new Vector3(0, 0, (float)(time * 25)));
        s.Vector("motion.velocity.world", new Vector3(0, 0, 25));
        s.Add("motion.speed", 25); s.Add("motion.velocity.local.z", 25); s.Add("wheelInput.steer", Math.Sin(time) * .2);
        s.Add("ffb.grip.reference", 10000); s.Add("ffb.grip.referenceKind", 2); s.Add("ffb.grip.carEpoch", 1);
        foreach (string corner in new[] { "fl", "fr" })
        {
            string p = "wheel." + corner + ".";
            s.Add(p + "grounded", 1); s.Add(p + "contactForce", 5000); s.Add(p + "sidewaysSlip", .3);
            s.Add(p + "sideFriction.extremumSlip", model == 6 ? 2 : .2);
            s.Add(p + "sideFriction.asymptoteSlip", model == 6 ? 2 : .5);
            s.Add(p + "sideFriction.extremumValue", 1); s.Add(p + "sideFriction.asymptoteValue", model == 6 ? 1 : .75);
            s.Add(p + "sideFriction.stiffness", 1);
        }
        return s;
    }
    private static void Refuses(Action action, string reason, Action<bool, string> check)
    {
        try { action(); } catch (IOException ex) { check(ex.Message.Contains(reason, StringComparison.Ordinal), "specific analysis refusal: " + ex.Message); return; }
        check(false, "Expected analysis refusal: " + reason);
    }

    internal static void Boundaries(Action<bool, string> check)
    {
        foreach (int model in new[] { 3, 4, 5, 6 })
        {
            var options = new ForceOptions(Model: model); var a = new ForceAnalysisHistory(); var b = new ForceAnalysisHistory();
            TelemetrySample current = null!;
            for (int i = 0; i < 60; i++)
            {
                current = Sample(i * .02, model); var other = ForceAnalysisHistory.CopySample(current);
                current.Add("ffb.gate", i % 12); other.Add("ffb.gate", 0);
                a.Record(current, options); b.Record(other, options);
                check(current.Get("analysis.force.preview") == other.Get("analysis.force.preview") &&
                    current.Get("analysis.force.epoch") == other.Get("analysis.force.epoch"), "device gate never affects analysis history");
            }
            check(Math.Abs(current.Get("analysis.force.preview")) > .01, "independent model ramps to nonzero: " + model);
            var ordinary = current.Channels.ToDictionary(p => p.Key, p => p.Value);
            var stage = ForceAnalysisHistory.CopySample(current); stage.Unavailable.Add("stage-only"); stage.Add("capture.trajectoryIndex", 0);
            new ForceAnalysisHistory().Record(stage, options);
            check(stage.Get("analysis.force.preview") == 0 && current.Get("analysis.force.preview") != 0,
                "starting stage has independent cold history, ordinary remains warm");
            check(ordinary.Count == current.Channels.Count && ordinary.All(p => current.Channels[p.Key] == p.Value) &&
                !current.Unavailable.Contains("stage-only"), "stage snapshot never mutates ordinary channels/unavailable list");

            double t = 1.18;
            void Boundary(Action<TelemetrySample> mutate, int bit, bool valid, string label, ForceOptions? tune = null)
            {
                var s = Sample(t += .02, model); mutate(s); var r = a.Record(s, tune ?? options);
                check(((int)s.Get("analysis.force.boundary") & bit) == bit && r.Valid == valid && r.Preview == 0, label);
            }
            Boundary(s => s.Add("ffb.grip.carEpoch", 2), 2, true, "new car resets analysis");
            Boundary(_ => { }, 4, true, "new tune resets analysis", options with { Strength = 60 });
            Boundary(_ => { }, 4, true, "new model resets analysis", options with { Model = model == 3 ? 6 : 3 });
            Boundary(s => s.Discontinuity = "teleport", 16, false, "source discontinuity resets analysis");
            Boundary(s => s.State = "trajectory", 32, false, "non-driving resets analysis");
            foreach (double delta in new[] { 0d, -.02, .2 })
            {
                var s = Sample(t + delta, model); var r = a.Record(s, options);
                check(!r.Valid && r.Preview == 0 && s.Get("analysis.force.reason") == 18 &&
                    ((int)s.Get("analysis.force.boundary") & 8) != 0, "duplicate/backwards/gap refused without mutating sample");
                check(s.Discontinuity == null, "analysis-only time gap never alters production state");
                t += delta; a.Record(Sample(t += .02, model), options);
            }
            var reverse = Sample(t += .02, model); reverse.Add("motion.velocity.local.z", -2);
            check(!a.Record(reverse, options).Valid && reverse.Get("analysis.force.reason") == 13, "reverse remains unqualified");
            var malformed = Sample(t += .02, model); malformed.Add("ffb.grip.referenceKind", 1.5);
            Refuses(() => a.Record(malformed, options), "referenceKind", check);
            var missing = Sample(t += .02, model); missing.Channels.Remove("ffb.grip.reference");
            Refuses(() => a.Record(missing, options), "ffb.grip.reference", check);
            Refuses(() => a.Record(Sample(t += .02, model), options with { Model = 99 }), "Unsupported force model", check);
            var nonfinite = Sample(double.NaN, model);
            check(!a.Record(nonfinite, options).Valid && nonfinite.Get("analysis.force.reason") == 18,
                "nonfinite simulation clock cannot produce analysis force");
        }
    }

    internal static void ProducerReplay(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "woden-analysis-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var options = new ForceOptions(Model: 6);
        string config = RecordingArtifacts.WriteForceConfig(Path.Combine(root, "force-config.json"), options);
        string profile = RecordingArtifacts.WriteCaptureProfile(Path.Combine(root, "capture-profile.json"), new string('1',64), new string('2',64),
            new string('3',64), new string('4',64), true, true, true, "fixture-guid");
        var properties = new Dictionary<string,string> { ["recordingContract"] = "dbce.wheel.replay-case@1", ["capability"] = "signal-reprocess",
            ["requestId"] = Guid.NewGuid().ToString(), ["caseId"] = "independent-analysis", ["disableForces"] = "true",
            ["gameAssemblySha256"] = new string('1',64), ["pluginSha256"] = new string('2',64), ["pluginProductVersion"] = "0.2.14+fixture-source",
            ["runtimeSource"] = "fixture-source", ["forceConfigSha256"] = config, ["captureProfileSha256"] = profile,
            ["analysisForceContract"] = ForceAnalysisHistory.Contract, ["analysisForce"] = ForceAnalysisHistory.Scope };
        var (controller, device) = WodenRallyEdge.ForceControllerChecks.Create();
        WodenRallyEdge.Runtime.Settings.Options = options; WodenRallyEdge.Runtime.DiagnosticNoForce = true;
        var analysis = new ForceAnalysisHistory(); string source = Path.Combine(root, "source.jsonl");
        try
        {
            using (var output = new TelemetryOutput(new(0, 0, 20, source, false), "analysis-fixture", "synthetic", "0.2.14", properties))
            {
                for (int i = 0; i < 260; i++)
                {
                    var s = Sample(i * .02, sequence: i);
                    if (i == 0 || i == 200) s.State = "paused";
                    if (i == 220) s.Discontinuity = "teleport";
                    if (i == 235) s.Add("motion.velocity.local.z", -2);
                    controller.Prepare(); controller.Tick(s);
                    var original = s.Channels.ToDictionary(p => p.Key, p => p.Value);
                    analysis.Record(s, options);
                    check(original.All(p => s.Channels[p.Key] == p.Value), "analysis preserves every actual force/crash/source channel");
                    check(s.Get("ffb.sent") == 0 && s.Get("ffb.deliveryAttempts") == 0 && s.Get("ffb.preview") == 0,
                        "diagnostic physical path and historical actual preview remain zero");
                    output.Publish(s);
                }
            }
            check(device.Opens == 0 && device.Writes.Count == 0 && controller.Attempts == 0, "production-linked mute performs zero native opens/writes");
            var records = SessionReader.Read(source).ToArray();
            var rows = records.Where(r => r.Kind == SessionRecordKind.Sample).Select(r => r.Sample).ToArray();
            check(rows.Length == 260 && rows.Any(r => r.Channels["analysis.force.preview"] != 0 && r.Channels["ffb.grip.referenceKind"] == 2),
                "same source has nonzero independent analysis with production-qualified driving reference");
            check(RecordedForceReplay.Reprocess(root).ModelSamples == 260, "old actual-force/crash baseline still replays exactly");
            var protectedFiles = Directory.GetFiles(root).ToDictionary(p => p, RecordingArtifacts.Sha256);
            var actual = RecordedForceReplay.ExportTrial(root, 50, 25, 2);
            var exported = RecordedForceReplay.ExportAnalysis(root, 50, 25, 2);
            check(actual.All(r => r.Request == 0) && exported.Any(r => r.Eligible && Math.Abs(r.Request) > .01),
                "explicit exporters distinguish zero gated actual from independent nonzero analysis");
            Console.WriteLine($"Independent analysis fixture: {rows.Length} rows; {exported.Count(r => r.Eligible)} eligible; peak {exported.Where(r => r.Eligible).Max(r => Math.Abs(r.Request)):R}; native opens/writes 0/0; actual preview all zero.");
            for (int i = 0; i < rows.Length; i++) check(exported[i].Request == (float)rows[i].Channels["analysis.force.preview"], "exact independent replay row " + i);
            check(exported.Any(r => r.Exclusion == "provisional reference") &&
                exported.Any(r => r.Exclusion == "not driving") && exported.Any(r => r.Exclusion == "model invalid"), "eligibility reasons stay explicit");
            check(RecordedForceReplay.ExportAnalysis(root, 75, 25, 2).Any(r => r.Eligible && Math.Abs(r.Request) > Math.Abs(exported.First(x => x.Time == r.Time).Request)),
                "candidate tune receives its own analysis history");

            // In-memory reprocessing tests deliberately bypass file/case validation to exercise the analysis validator itself.
            // Public export above first verified the actual case, original force/crash recurrence and all immutable hashes.
            SessionRecord[] Mutated(Action<Dictionary<string, double>> change)
            {
                var copy = SessionReader.Read(source).ToArray();
                change(copy.First(r => r.Kind == SessionRecordKind.Sample).Sample.Channels); return copy;
            }
            foreach (string key in new[] { "preview", "epoch", "carEpoch", "tuneEpoch", "boundary", "reason", "modelVersion", "valid", "reset", "contractVersion", "steering", "dampingTerm" })
            {
                string full = "analysis.force." + key;
                var changed = Mutated(c => c[full] += 1);
                Refuses(() => ForceAnalysisHistory.Replay(changed, options), full, check);
            }
            Refuses(() => ForceAnalysisHistory.Replay(Mutated(c => c.Remove("analysis.force.reason")), options), "analysis fields", check);
            Refuses(() => ForceAnalysisHistory.Replay(Mutated(c => c["analysis.force.unknown"] = 1), options), "analysis fields", check);
            var unknown = SessionReader.Read(source).ToArray(); unknown[0].Metadata.Properties["analysisForceContract"] = "woden.force-analysis@2";
            Refuses(() => ForceAnalysisHistory.Replay(unknown, options), "contract/scope", check);
            unknown[0].Metadata.Properties["analysisForceContract"] = ForceAnalysisHistory.Contract; unknown[0].Metadata.Properties.Remove("analysisForce");
            Refuses(() => ForceAnalysisHistory.Replay(unknown, options), "contract/scope", check);
            var stripped = SessionReader.Read(source).ToArray(); stripped[0].Metadata.Properties.Remove("analysisForceContract");
            Refuses(() => ForceAnalysisHistory.Replay(stripped, options), "lack a declared contract", check);
            // Old records are readable but never silently given the new history.
            foreach (var record in stripped.Where(r => r.Kind == SessionRecordKind.Sample))
                foreach (string k in record.Sample.Channels.Keys.Where(k => k.StartsWith("analysis.force.", StringComparison.Ordinal)).ToArray()) record.Sample.Channels.Remove(k);
            Refuses(() => ForceAnalysisHistory.Replay(stripped, options), "Independent analysis unavailable", check);
            string failed = Path.Combine(root, "failed-source.jsonl");
            using (var output = new TelemetryOutput(new(0,0,20,failed,false), "analysis-failure", "synthetic", "0.2.14", properties))
            {
                var s = Sample(1); controller.Tick(s); new ForceAnalysisHistory().Record(s, options); output.Publish(s);
                output.FailRecording(1.02, "synthetic analysis failure");
                check(!output.RecordingActive && output.RecordingStatus == "Faulted" && output.RecordingError == "synthetic analysis failure",
                    "analysis failure stays visible even when source prefix saves cleanly");
            }
            Refuses(() => ForceAnalysisHistory.Replay(SessionReader.Read(failed).ToArray(), options), "capture failed", check);
            check(device.Opens == 0 && device.Writes.Count == 0, "analysis failure never enables physical output");
            foreach (var pin in protectedFiles) check(RecordingArtifacts.Sha256(pin.Key) == pin.Value, "all original capture and actual baseline bytes preserved");
        }
        finally { controller.Shutdown(); WodenRallyEdge.Runtime.DiagnosticNoForce = false; Directory.Delete(root, true); }
    }
}
