using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dbce.Wheel.Recording;

namespace WodenRallyEdge.Core;

public sealed record RecordedForceReplayResult(string CasePath, string ObservationPath, string SourceSha256,
    string ObservationSha256, string CaseId, long SourceSamples, long ModelSamples, long DrivingSamples, double DrivingSeconds);
public sealed record ForceObservationComparison(bool Equal, int Differences, double MaximumMagnitudeError,
    string BaselineModel, string CandidateModel, string BaselineConfigSha256, string CandidateConfigSha256,
    string BaselineProfileSha256, string CandidateProfileSha256);

public static class RecordedForceReplay
{
    private const double Tolerance = .000002;
    private static readonly string[] Tuning = { "strengthPercent", "peakPercent", "loadReference", "slipScale", "smoothingMs", "damping", "invert", "modelVersion" };

    private sealed record Observation(long Tick, double Magnitude);
    private sealed record ObservationFile(string CaseId, string CaseSha256, string Model, string Config, string Profile, List<Observation> Requests);

    public static RecordedForceReplayResult Reprocess(string caseDirectory)
    {
        string directory = Path.GetFullPath(caseDirectory);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
        string source = Path.Combine(directory, "source.jsonl"), config = Path.Combine(directory, "force-config.json"), profile = Path.Combine(directory, "capture-profile.json");
        string observation = Path.Combine(directory, "force-observation.jsonl"), manifest = Path.Combine(directory, "case.json");
        foreach (string path in new[] { source, config, profile }) if (!File.Exists(path)) throw new IOException("Missing capture artifact: " + Path.GetFileName(path));
        foreach (string path in new[] { observation, manifest }) if (File.Exists(path)) throw new IOException("Refusing to replace existing replay artifact: " + path);

        var records = SessionReader.Read(source).ToArray();
        var metadata = records.SingleOrDefault(x => x.Kind == SessionRecordKind.Metadata)?.Metadata ?? throw new IOException("Recording metadata missing");
        var footer = records.SingleOrDefault(x => x.Kind == SessionRecordKind.Footer)?.Footer ?? throw new IOException("Recording footer missing");
        if (!footer.Completed) throw new IOException("Recording is incomplete: " + footer.StopReason);
        var counts = footer.Counts;
        if (counts.DroppedSamples != 0 || counts.DroppedMarkers != 0 || counts.ErrorCount != 0 || counts.InvalidCount != 0 ||
            counts.LimitCount != 0 || counts.QueueFullCount != 0 || counts.ContentionCount != 0 || counts.RejectedAfterStop != 0)
            throw new IOException("Recording has dropped/rejected/invalid/limited source records and cannot support exact signal reprocessing");

        string Property(string name) => metadata.Properties.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new IOException("Recording metadata property missing: " + name);
        string caseId = Property("caseId");
        if (!DiagnosticLaunch.ValidCaseId(caseId)) throw new IOException("Invalid recorded caseId");
        if (Property("capability") != "signal-reprocess" || Property("recordingContract") != "dbce.wheel.replay-case@1")
            throw new IOException("Unsupported recording capability/contract");
        string sourceRevision = Property("runtimeSource"), executableHash = Property("pluginSha256");
        string configHash = RecordingArtifacts.Sha256(config), profileHash = RecordingArtifacts.Sha256(profile);
        if (configHash != Property("forceConfigSha256") || profileHash != Property("captureProfileSha256"))
            throw new IOException("Capture config/profile identity does not match recording metadata");
        ForceOptions expectedOptions = ReadForceConfig(config);

        var signal = new ForceSignal(); var observations = new List<Observation>();
        long sourceSamples = 0, driving = 0; double? firstDriving = null, lastDriving = null; long? previousResetAfter = null; long lastTick = -1;
        foreach (var record in records)
        {
            if (record.Kind != SessionRecordKind.Sample) continue;
            sourceSamples++; var row = record.Sample; var c = row.Channels;
            bool hasModel = c.ContainsKey("ffb.modelResetBefore");
            bool isDriving = Required(c, "sample.driving") == 1;
            if (isDriving && !hasModel) throw new IOException($"Driving sample {row.Sequence} has no force-model record");
            if (!hasModel) continue;
            long before = Integer(c, "ffb.modelResetBefore"), after = Integer(c, "ffb.modelResetAfter");
            if (after < before || previousResetAfter.HasValue && before < previousResetAfter.Value) throw new IOException("Force reset epochs are not monotonic");
            if (previousResetAfter.HasValue && before != previousResetAfter.Value) signal.Reset();
            var sample = new TelemetrySample { Sequence = row.Sequence, ElapsedSeconds = row.ElapsedSeconds,
                SimulationSeconds = Required(c, "sample.simulationSeconds"), State = isDriving ? "driving" : "inactive",
                Discontinuity = Required(c, "sample.discontinuity") == 1 ? "recorded-discontinuity" : null };
            foreach (var pair in c) sample.Channels[pair.Key] = pair.Value;
            var options = Options(c);
            if (options != expectedOptions) throw new IOException("Force tuning changed during capture; split into stable-config cases before reprocessing");
            if (isDriving) ValidateDrivingInputs(c, row.Sequence);
            long runnerBefore = signal.ResetCount;
            var result = signal.Evaluate(sample, options);
            long evaluateResets = signal.ResetCount - runnerBefore;
            int gate = (int)Integer(c, "ffb.gate");
            if (gate is < 0 or > 11) throw new IOException("Unsupported force gate code: " + gate);
            if (gate != 0) signal.Reset();
            long expectedResetDelta = evaluateResets + (gate == 0 ? 0 : 1);
            if (after - before != expectedResetDelta) throw new IOException($"Force reset semantics differ at source sample {row.Sequence}");
            Compare(result.Valid ? 1 : 0, Required(c, "ffb.modelValid"), "model validity", row.Sequence);
            int reason = sample.Discontinuity != null ? 11 : ForceObservationSemantics.ModelReason(result.Reason);
            Compare(reason, Required(c, "ffb.modelReason"), "model reason", row.Sequence);
            Compare(result.FrontLoad, Required(c, "ffb.frontLoad"), "front load", row.Sequence);
            Compare(result.Alignment, Required(c, "ffb.alignmentEstimate"), "alignment", row.Sequence);
            Compare(result.Damping, Required(c, "ffb.dampingEstimate"), "damping", row.Sequence);
            Compare(result.Preview, Required(c, "ffb.preview"), "preview", row.Sequence);
            long tick = checked((long)Math.Round(row.ElapsedSeconds * ForceObservationSemantics.TicksPerSecond, MidpointRounding.AwayFromZero));
            if (tick < 0 || tick < lastTick) throw new IOException("Recording model timeline is not monotonic");
            lastTick = tick; observations.Add(new(tick, result.Preview)); previousResetAfter = after;
            if (isDriving) { driving++; firstDriving ??= row.ElapsedSeconds; lastDriving = row.ElapsedSeconds; }
        }
        double drivingSeconds = firstDriving.HasValue && lastDriving.HasValue ? lastDriving.Value - firstDriving.Value : 0;
        if (driving < 50 || drivingSeconds < 1) throw new IOException($"Insufficient driving coverage: {driving} samples over {drivingSeconds:F3}s; need at least 50 samples and 1 second");
        if (observations.Count == 0) throw new IOException("No reproducible force-model samples");

        string sourceHash = RecordingArtifacts.Sha256(source);
        WriteManifest(manifest, caseId, metadata.PluginVersion, sourceRevision, executableHash, sourceHash, configHash, profileHash);
        string caseHash = RecordingArtifacts.Sha256(manifest);
        WriteObservation(observation, caseId, caseHash, configHash, profileHash, observations);
        string observationHash = RecordingArtifacts.Sha256(observation);
        _ = ReadObservation(observation);
        return new(manifest, observation, sourceHash, observationHash, caseId, sourceSamples, observations.Count, driving, drivingSeconds);
    }

    public static ForceObservationComparison Compare(string baselinePath, string candidatePath, double tolerance = .000001)
    {
        if (!double.IsFinite(tolerance) || tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance));
        var a = ReadObservation(baselinePath); var b = ReadObservation(candidatePath);
        int differences = 0; double maximum = 0;
        if (a.CaseId != b.CaseId || a.CaseSha256 != b.CaseSha256) throw new IOException("Different case identity; comparison unavailable");
        if (a.Requests.Count != b.Requests.Count) differences++;
        int count = Math.Min(a.Requests.Count, b.Requests.Count);
        for (int i = 0; i < count; i++)
        {
            if (a.Requests[i].Tick != b.Requests[i].Tick) differences++;
            double error = Math.Abs(a.Requests[i].Magnitude - b.Requests[i].Magnitude); maximum = Math.Max(maximum, error);
            if (error > tolerance) differences++;
        }
        return new(differences == 0, differences, maximum, a.Model, b.Model, a.Config, b.Config, a.Profile, b.Profile);
    }

    private static void ValidateDrivingInputs(IReadOnlyDictionary<string, double> c, long sequence)
    {
        foreach (string key in new[] { "motion.speed", "motion.velocity.local.z", "wheel.fl.grounded", "wheel.fr.grounded" }) _ = Required(c, key);
        foreach (string corner in new[] { "fl", "fr" }) if (Required(c, "wheel." + corner + ".grounded") == 1)
            foreach (string field in new[] { "contactForce", "sidewaysSlip" }) _ = Required(c, "wheel." + corner + "." + field);
        foreach (string name in Tuning) _ = Required(c, "ffb.tuning." + name);
    }

    private static ForceOptions Options(IReadOnlyDictionary<string, double> c)
    {
        if (Required(c, "ffb.tuning.modelVersion") != 3) throw new IOException("Unsupported force model version");
        return new((float)Required(c, "ffb.tuning.strengthPercent"), (float)Required(c, "ffb.tuning.peakPercent"),
            (float)Required(c, "ffb.tuning.loadReference"), (float)Required(c, "ffb.tuning.slipScale"),
            (float)Required(c, "ffb.tuning.smoothingMs"), (float)Required(c, "ffb.tuning.damping"), Required(c, "ffb.tuning.invert") == 1);
    }

    private static ForceOptions ReadForceConfig(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path)); var root = doc.RootElement;
        Strict(root, "schema", "version", "model", "strengthPercent", "peakPercent", "loadReference", "slipScale", "smoothingMs", "damping", "invert");
        if (root.GetProperty("schema").GetString() != "woden.force-config" || root.GetProperty("version").GetInt32() != 1 ||
            root.GetProperty("model").GetString() != ForceObservationSemantics.Model) throw new IOException("Unsupported force config artifact");
        return new(root.GetProperty("strengthPercent").GetSingle(), root.GetProperty("peakPercent").GetSingle(), root.GetProperty("loadReference").GetSingle(),
            root.GetProperty("slipScale").GetSingle(), root.GetProperty("smoothingMs").GetSingle(), root.GetProperty("damping").GetSingle(), root.GetProperty("invert").GetBoolean());
    }

    private static double Required(IReadOnlyDictionary<string, double> channels, string key)
    {
        if (!channels.TryGetValue(key, out double value) || !double.IsFinite(value)) throw new IOException("Required recorded channel missing/nonfinite: " + key);
        return value;
    }
    private static long Integer(IReadOnlyDictionary<string, double> channels, string key)
    {
        double value = Required(channels, key); long result = checked((long)value);
        if (result != value || result < 0) throw new IOException("Required nonnegative integer channel invalid: " + key);
        return result;
    }
    private static void Compare(double actual, double recorded, string name, long sequence)
    { if (Math.Abs(actual - recorded) > Tolerance) throw new IOException($"Recorded {name} differs from actual ForceSignal at sample {sequence}: {recorded} vs {actual}"); }

    private static void WriteObservation(string path, string caseId, string caseHash, string configHash, string profileHash, IReadOnlyList<Observation> observations)
    {
        if (observations.Count > 2_000_000) throw new IOException("Observation record bound exceeded");
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        long bytesWritten = 0;
        void WriteLine(object value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value) + "\n");
            if (bytes.Length > 16 * 1024) throw new IOException("Observation line bound exceeded");
            if (bytesWritten + bytes.Length > 256L * 1024 * 1024) throw new IOException("Observation file bound exceeded");
            output.Write(bytes, 0, bytes.Length);
            bytesWritten += bytes.Length;
        }
        WriteLine(new { kind = "header", schema = "dbce.wheel.force-observation", version = 1, caseId, caseSha256 = caseHash,
            clock = new { domain = "monotonic", ticksPerSecond = ForceObservationSemantics.TicksPerSecond }, model = ForceObservationSemantics.Model,
            configSha256 = configHash, profileSha256 = profileHash, output = "observe", physicalOutput = false });
        for (int i = 0; i < observations.Count; i++) WriteLine(new { kind = "force", sequence = i, tick = observations[i].Tick,
            frame = (long?)null, effect = "steering", family = "constant", operation = "set", magnitude = observations[i].Magnitude,
            frequencyHz = 0.0, durationMs = (double?)null });
        WriteLine(new { kind = "footer", complete = true, count = observations.Count });
        output.Flush(true);
    }

    private static void WriteManifest(string path, string caseId, string pluginVersion, string sourceRevision, string executableHash,
        string sourceHash, string configHash, string profileHash)
    {
        var value = new { schema = "dbce.wheel.replay-case", version = 1, caseId, game = "super-woden-rally-edge",
            adapter = "woden-rally-edge-wheel@" + pluginVersion, capability = "signal-reprocess",
            clock = new { domain = "monotonic", ticksPerSecond = ForceObservationSemantics.TicksPerSecond },
            source = new { path = "source.jsonl", sha256 = sourceHash, format = "dbce.wheel.session@1" }, initialState = (object?)null,
            artifacts = new Dictionary<string, object> {
                ["config"] = new { path = "force-config.json", sha256 = configHash, format = "woden.force-config@1" },
                ["profile"] = new { path = "capture-profile.json", sha256 = profileHash, format = "woden.capture-profile@1" } },
            provenance = new { sourceRevision, dirty = false, executableSha256 = executableHash } };
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(output, value, new JsonSerializerOptions { WriteIndented = true }); output.WriteByte((byte)'\n'); output.Flush(true);
    }

    private static ObservationFile ReadObservation(string path)
    {
        if (new FileInfo(path).Length > 256L * 1024 * 1024) throw new IOException("Observation file bound exceeded");
        string? caseId = null, caseHash = null, model = null, config = null, profile = null; var requests = new List<Observation>(); bool footer = false; long previousTick = -1;
        foreach (string line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) throw new IOException("Blank observation line");
            if (Encoding.UTF8.GetByteCount(line) + 1 > 16 * 1024) throw new IOException("Observation line bound exceeded");
            using var doc = JsonDocument.Parse(line); var root = doc.RootElement; string kind = root.GetProperty("kind").GetString() ?? "";
            if (kind == "header")
            {
                if (caseId != null || requests.Count != 0 || footer) throw new IOException("Observation header is not first");
                Strict(root, "kind", "schema", "version", "caseId", "caseSha256", "clock", "model", "configSha256", "profileSha256", "output", "physicalOutput");
                if (root.GetProperty("schema").GetString() != "dbce.wheel.force-observation" || root.GetProperty("version").GetInt32() != 1 ||
                    root.GetProperty("output").GetString() != "observe" || root.GetProperty("physicalOutput").GetBoolean()) throw new IOException("Unsupported/unsafe observation header");
                var clock = root.GetProperty("clock"); Strict(clock, "domain", "ticksPerSecond");
                if (clock.GetProperty("domain").GetString() != "monotonic" || clock.GetProperty("ticksPerSecond").GetInt64() != ForceObservationSemantics.TicksPerSecond) throw new IOException("Observation clock mismatch");
                caseId = root.GetProperty("caseId").GetString(); caseHash = Hash(root, "caseSha256"); model = root.GetProperty("model").GetString(); config = Hash(root, "configSha256"); profile = Hash(root, "profileSha256");
            }
            else if (kind == "force")
            {
                if (caseId == null || footer) throw new IOException("Observation request outside stream");
                Strict(root, "kind", "sequence", "tick", "frame", "effect", "family", "operation", "magnitude", "frequencyHz", "durationMs");
                if (root.GetProperty("sequence").GetInt32() != requests.Count || root.GetProperty("effect").GetString() != "steering" ||
                    root.GetProperty("family").GetString() != "constant" || root.GetProperty("operation").GetString() != "set" ||
                    root.GetProperty("frame").ValueKind != JsonValueKind.Null || root.GetProperty("durationMs").ValueKind != JsonValueKind.Null || root.GetProperty("frequencyHz").GetDouble() != 0) throw new IOException("Unsupported observation request");
                long tick = root.GetProperty("tick").GetInt64(); double magnitude = root.GetProperty("magnitude").GetDouble();
                if (tick < previousTick || !double.IsFinite(magnitude) || magnitude is < -1 or > 1) throw new IOException("Invalid observation timeline/magnitude");
                previousTick = tick; requests.Add(new(tick, magnitude));
                if (requests.Count > 2_000_000) throw new IOException("Observation record bound exceeded");
            }
            else if (kind == "footer")
            {
                Strict(root, "kind", "complete", "count");
                if (caseId == null || footer || !root.GetProperty("complete").GetBoolean() || root.GetProperty("count").GetInt32() != requests.Count) throw new IOException("Invalid observation footer");
                footer = true;
            }
            else throw new IOException("Unknown observation record kind");
        }
        if (!footer || caseId == null || caseHash == null || model == null || config == null || profile == null) throw new IOException("Incomplete observation file");
        return new(caseId, caseHash, model, config, profile, requests);
    }

    private static string Hash(JsonElement root, string name)
    {
        string value = root.GetProperty(name).GetString() ?? "";
        if (value.Length != 64 || value.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))) throw new IOException("Invalid lowercase SHA-256: " + name);
        return value;
    }
    private static void Strict(JsonElement element, params string[] names)
    {
        var allowed = names.ToHashSet(StringComparer.Ordinal); int count = 0;
        foreach (var property in element.EnumerateObject()) { count++; if (!allowed.Contains(property.Name)) throw new IOException("Unknown JSON key: " + property.Name); }
        if (count != allowed.Count || names.Any(x => !element.TryGetProperty(x, out _))) throw new IOException("Missing/duplicate JSON key");
    }
}
