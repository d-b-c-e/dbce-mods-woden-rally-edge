using System.Globalization;
using System.Text.Json;
using Dbce.Wheel.Playback;
using Dbce.Wheel.Recording;
using WodenRallyEdge.Core;

// Complete, explicitly synthetic stages through the actual writers and validator/exporter.
// No game libraries, controller, network sender, native sink or physical output.
string root = args.Length == 1 ? Path.GetFullPath(args[0]) : Path.Combine(Path.GetTempPath(), "woden-stage-review-" + Guid.NewGuid().ToString("N"));
if (Directory.Exists(root)) throw new IOException("Fixture destination already exists");
Directory.CreateDirectory(root);
int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
void Refuses(Action action, string reason)
{
    try { action(); } catch (IOException ex) when (ex.Message.Contains(reason, StringComparison.Ordinal)) { checks++; return; }
    throw new Exception("Expected refusal: " + reason);
}

string Capture(string name, bool gripModel, Action<int, Dictionary<string, double>>? change = null, bool legacy = false)
{
    string directory = Path.Combine(root, name);
    var options = new ForceOptions(Model: gripModel ? 4 : 3);
    using var tape = new TrajectoryRecorder(directory, new Dictionary<string, string>
    {
        ["contract"] = "dbce.trajectory@1", ["game"] = "Super Woden Rally Edge", ["gameSha256"] = new string('0', 64),
        ["scenario"] = "synthetic-model-fixture", ["fixedDeltaTime"] = ".02", ["captureSource"] = "synthetic-test",
        ["physicalOutput"] = "false"
    });
    string configHash = RecordingArtifacts.WriteForceConfig(Path.Combine(directory, "force-config.json"), options);
    File.WriteAllText(Path.Combine(directory, "stage-context.json"), "{\"synthetic\":true}");
    File.WriteAllText(Path.Combine(directory, "channels.json"), "[]");
    using var recorder = new SessionRecorder(Path.Combine(directory, "source.jsonl"), new SessionMetadata
    {
        Game = "Super Woden Rally Edge", PluginVersion = "synthetic-test", ToolkitVersion = "vendored",
        Properties = new() { ["forceConfigSha256"] = configHash, ["captureSource"] = "synthetic-test", ["physicalOutput"] = "false" }
    });
    var classic = new ForceSignal(); classic.Reset();
    var grip = new GripSignal(); grip.NewCar();
    var reference = new FrontLoadReference();
    for (int i = 0; i < 160; i++)
    {
        var sample = new TelemetrySample { Sequence = i, ElapsedSeconds = i * .02, SimulationSeconds = 100 + i * .02,
            State = "driving", Discontinuity = i == 90 ? "recorded" : null };
        var c = sample.Channels;
        c["capture.trajectoryIndex"] = i; c["sample.simulationSeconds"] = sample.SimulationSeconds;
        c["sample.driving"] = 1; c["sample.discontinuity"] = i == 90 ? 1 : 0;
        c["ffb.sent"] = c["ffb.connectionAttempts"] = c["ffb.deliveryAttempts"] = 0;
        c["ffb.tuning.strengthPercent"] = options.Strength; c["ffb.tuning.peakPercent"] = options.PeakPercent;
        c["ffb.tuning.loadReference"] = options.LoadReference; c["ffb.tuning.slipScale"] = options.SlipScale;
        c["ffb.tuning.smoothingMs"] = options.SmoothingMs; c["ffb.tuning.damping"] = options.Damping;
        c["ffb.tuning.invert"] = options.Invert ? 1 : 0; c["ffb.tuning.modelVersion"] = options.Model;
        c["ffb.tuning.loadRatio"] = options.LoadRatio; c["ffb.tuning.gripSmoothing"] = options.GripSmoothing;
        c["motion.speed"] = c["motion.velocity.local.z"] = 20;
        c["wheelInput.steer"] = Math.Sin(i * .02) * .2;
        foreach (string corner in new[] { "fl", "fr" })
        {
            string p = "wheel." + corner + ".";
            c[p + "grounded"] = 1; c[p + "contactForce"] = 5000;
            c[p + "sidewaysSlip"] = i < 80 ? .15 : -.13;
            if (!legacy)
            {
                c[p + "sideFriction.extremumSlip"] = .2; c[p + "sideFriction.extremumValue"] = 1;
                c[p + "sideFriction.asymptoteSlip"] = .4; c[p + "sideFriction.asymptoteValue"] = .6;
                c[p + "sideFriction.stiffness"] = i < 80 ? 1 : .8;
            }
        }
        // Reference differs from the observed 10,000 N sum: it represents history before this tape.
        // Known-zero -> provisional -> qualified, then a distinct recorded load; never relearn from row zero.
        reference.Restore(i < 5 ? 0 : i < 80 ? 8000 : 15000,
            i < 5 ? FrontLoadReferenceKind.None : i < 20 ? FrontLoadReferenceKind.Provisional : FrontLoadReferenceKind.Driving);
        if (!legacy)
        {
            c["ffb.grip.reference"] = reference.Load; c["ffb.grip.referenceKind"] = (int)reference.Kind;
            c["ffb.grip.carEpoch"] = 7; c["ffb.grip.curveEstimateVersion"] = 1;
            c["analysis.force.modelVersion"] = options.Model;
        }
        var result = gripModel ? grip.Evaluate(sample, options, reference) : classic.Evaluate(sample, options);
        c["analysis.force.preview"] = result.Preview; c["analysis.force.valid"] = result.Valid ? 1 : 0;
        c["analysis.force.reset"] = gripModel ? grip.ResetCount : classic.ResetCount;
        change?.Invoke(i, c);
        Check(recorder.TryRecord(i * .02, c), "Synthetic recorder dropped a row");
        var frame = new TrajectoryFrame { Time = i * .02 }; frame.Values[6] = 1; frame.Values[2] = i * .4f; frame.Values[9] = 20;
        tape.Write(frame);
    }
    Check(recorder.Stop(TimeSpan.FromSeconds(10)), "Recorder did not complete");
    tape.Complete("source.jsonl", "force-config.json", "stage-context.json", "channels.json");
    return directory;
}

string classicPath = Capture("classic-legacy", false, legacy: true), gripPath = Capture("grip", true);
foreach (string path in new[] { classicPath, gripPath })
{
    var report = JsonSerializer.SerializeToElement(StageCaptureReview.Run(path));
    Check(report.GetProperty("passed").GetBoolean(), "Stage validation failed");
    Check(report.GetProperty("source").GetString() == "synthetic-test", "Synthetic capture was labelled live");
    Check(report.GetProperty("maximumForceReprocessError").GetDouble() == 0, "Reprocessing differed");
    Check(report.GetProperty("forceScope").GetString()!.StartsWith("steering analysis only"), "Claimed full force qualification");
    File.WriteAllText(Path.Combine(root, Path.GetFileName(path) + "-review.json"), report.ToString());
}
var originalHashes = Directory.GetFiles(gripPath).ToDictionary(p => p, ArtifactSeal.Hash);
string csvPath = Path.Combine(root, "grip.csv");
ForceExport.Run(gripPath, csvPath, 50, 25, 2);
var rows = File.ReadAllLines(csvPath).Skip(1).Select(line => line.Split(',')).ToArray();
var source = SessionReader.Read(Path.Combine(gripPath, "source.jsonl")).Where(r => r.Kind == SessionRecordKind.Sample).ToArray();
Check(rows.Length == 160, "Export lost rows");
for (int i = 0; i < rows.Length; i++)
{
    Check(float.Parse(rows[i][4], CultureInfo.InvariantCulture) == (float)source[i].Sample.Channels["analysis.force.preview"], "Trial changed the original Grip recurrence");
    Check(rows[i][1] == source[i].Sample.Channels["analysis.force.reset"].ToString(CultureInfo.InvariantCulture), "Trial reset epoch differs");
    Check(rows[i][6] == (i >= 20 && i != 90 ? "1" : "0"), "Eligibility included unqualified reference or discontinuity");
}
Check(rows[0][7] == "no front-load reference" && rows[5][7] == "provisional reference", "Missing and provisional states were conflated");
Refuses(() => ForceExport.Run(gripPath, csvPath, 50, 25, 2), "already exists");
ForceExport.Run(gripPath, Path.Combine(root, "grip-to-classic.csv"), 50, 25);
ForceExport.Run(classicPath, Path.Combine(root, "classic.csv"), 50, 25);
Refuses(() => ForceExport.Run(classicPath, Path.Combine(root, "unavailable.csv"), 50, 25, 2), "no recorded friction curves");
Check(!File.Exists(Path.Combine(root, "unavailable.csv")), "Failed export left a result");
foreach (var entry in originalHashes) Check(ArtifactSeal.Hash(entry.Key) == entry.Value, "Export changed capture bytes");

foreach (var mutation in new (string Name, string Key, double? Value, string Reason)[]
{
    ("unknown-model", "ffb.tuning.modelVersion", 5, "Capture tune changed"),
    ("mixed-model", "ffb.tuning.modelVersion", 3, "Capture tune changed"),
    ("changed-ratio", "ffb.tuning.loadRatio", 1, "Capture tune changed"),
    ("changed-smoothing", "ffb.tuning.gripSmoothing", .7, "Capture tune changed"),
    ("analysis-model", "analysis.force.modelVersion", 3, "Analysis force model"),
    ("missing-reference", "ffb.grip.reference", null, "recorded grip reference"),
    ("fractional-reference-kind", "ffb.grip.referenceKind", 1.5, "recorded grip reference"),
    ("invalid-reference", "ffb.grip.reference", -1, "recorded grip reference"),
    ("unknown-curve", "ffb.grip.curveEstimateVersion", 2, "Unsupported grip curve"),
    ("car-change", "ffb.grip.carEpoch", 8, "Capture car changed"),
    ("preview", "analysis.force.preview", .91, "Model preview differs"),
    ("reset", "analysis.force.reset", 900, "validity/reset differs"),
    ("curve-missing", "wheel.fl.sideFriction.stiffness", null, "validity/reset differs"),
    ("delivery", "ffb.deliveryAttempts", 1, "physically muted"),
    ("alignment", "capture.trajectoryIndex", 1, "row alignment")
})
{
    string fixture = Capture(mutation.Name, true, (i, c) =>
    {
        if (i != 55) return;
        if (mutation.Value.HasValue) c[mutation.Key] = mutation.Value.Value; else c.Remove(mutation.Key);
    });
    Refuses(() => StageCaptureReview.Run(fixture), mutation.Reason);
    Refuses(() => ForceExport.Run(fixture, Path.Combine(root, mutation.Name + ".csv"), 50, 25, 2), mutation.Reason);
}
Console.WriteLine($"PASS stage analysis/export: {checks} checks; Classic and Grip, pre-capture reference, eligibility, reset, refusal and immutable-source cases. Evidence: {root}");
