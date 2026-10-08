using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dbce.Wheel.Recording;

namespace WodenRallyEdge.Core;

public sealed record RecordedForceReplayResult(string CasePath, string ObservationPath, string SourceSha256,
    string ObservationSha256, string CaseId, long SourceSamples, long ModelSamples, long DrivingSamples, double DrivingSeconds);
public sealed record RecordedForceTrialResult(string ObservationPath, string ObservationSha256, string CandidateConfigSha256,
    long ModelSamples, long DrivingSamples, double DrivingSeconds, ForceObservationComparison Comparison);
public sealed record ForceObservationComparison(bool Equal, int Differences, double MaximumMagnitudeError,
    string BaselineModel, string CandidateModel, string BaselineConfigSha256, string CandidateConfigSha256,
    string BaselineProfileSha256, string CandidateProfileSha256);

public static class RecordedForceReplay
{
    private const double Tolerance = .000002;
    private const long MaxObservationBytes = 256L * 1024 * 1024;
    private const int MaxObservationLineBytes = 16 * 1024;
    private const int MaxObservationRecords = 2_000_000;
    private static readonly string[] Tuning = { "strengthPercent", "peakPercent", "loadReference", "slipScale", "smoothingMs", "damping", "invert", "modelVersion" };
    // Force model versions 4 and 5 (GripSignal) add these per-row inputs (force config v2 and v3).
    private static readonly string[] GripTuning = { "loadRatio", "gripSmoothing" };
    private static readonly string[] FrictionFields = { "extremumSlip", "extremumValue", "asymptoteSlip", "asymptoteValue", "stiffness" };
    private static string ModelId(ForceOptions o) => ForceObservationSemantics.ModelId(o.Model);
    private static string ConfigFormat(ForceOptions o) => "woden.force-config@" + ForceObservationSemantics.ConfigVersion(o.Model);

    private sealed record Observation(long Tick, double Magnitude);
    private sealed record ObservationFile(string CaseId, string CaseSha256, string Model, string Config, string Profile, List<Observation> Requests);
    private sealed record CaptureIdentity(string Directory, string SourcePath, string ConfigPath, string ProfilePath, string CaseId,
        string PluginVersion, string SourceRevision, string ExecutableHash, string SourceHash, string ConfigHash, string ProfileHash, ForceOptions OriginalOptions);
    private sealed record ReplayRun(List<Observation> Observations, long SourceSamples, long DrivingSamples, double DrivingSeconds);
    private sealed record ManifestIdentity(string CaseId, string CaseSha256, string SourceHash, string ConfigHash, string ProfileHash);

    public static RecordedForceReplayResult Reprocess(string caseDirectory)
    {
        string directory = RequireDirectory(caseDirectory);
        string observation = Path.Combine(directory, "force-observation.jsonl"), manifest = Path.Combine(directory, "case.json");
        foreach (string path in new[] { observation, manifest }) if (File.Exists(path)) throw new IOException("Refusing to replace existing replay artifact: " + path);

        CaptureIdentity capture = ReadCapture(directory);
        ReplayRun run = RunSource(capture, capture.OriginalOptions, verifyRecordedOutputs: true);
        EnsureCaptureUnchanged(capture);
        WriteManifest(manifest, capture.CaseId, capture.PluginVersion, capture.SourceRevision, capture.ExecutableHash,
            capture.SourceHash, capture.ConfigHash, capture.ProfileHash, ConfigFormat(capture.OriginalOptions));
        string caseHash = RecordingArtifacts.Sha256(manifest);
        WriteObservation(observation, capture.CaseId, caseHash, capture.ConfigHash, capture.ProfileHash, run.Observations, ModelId(capture.OriginalOptions));
        ObservationFile written = ReadObservation(observation);
        RequireObservationIdentity(written, capture.CaseId, caseHash, capture.ConfigHash, capture.ProfileHash, ModelId(capture.OriginalOptions));
        EnsureCaptureUnchanged(capture);
        return new(manifest, observation, capture.SourceHash, RecordingArtifacts.Sha256(observation), capture.CaseId,
            run.SourceSamples, run.Observations.Count, run.DrivingSamples, run.DrivingSeconds);
    }

    public static RecordedForceTrialResult Trial(string caseDirectory, string candidateConfigPath, string outputPath, double tolerance = .000001)
    {
        string directory = RequireDirectory(caseDirectory);
        string manifestPath = Path.Combine(directory, "case.json"), baselinePath = Path.Combine(directory, "force-observation.jsonl");
        if (!File.Exists(manifestPath) || !File.Exists(baselinePath)) throw new IOException("Verified case.json and baseline force-observation.jsonl are required before a tuning trial");
        string candidatePath = Path.GetFullPath(candidateConfigPath), candidateOutput = Path.GetFullPath(outputPath);
        CaptureIdentity capture = ReadCapture(directory);
        ManifestIdentity manifest = ReadManifest(manifestPath, capture);
        string baselineHash = RecordingArtifacts.Sha256(baselinePath), manifestHash = RecordingArtifacts.Sha256(manifestPath);
        ObservationFile baseline = ReadObservation(baselinePath);
        RequireObservationIdentity(baseline, capture.CaseId, manifest.CaseSha256, capture.ConfigHash, capture.ProfileHash, ModelId(capture.OriginalOptions));

        ReplayRun verified = RunSource(capture, capture.OriginalOptions, verifyRecordedOutputs: true);
        RequireMatchingTimelineAndMagnitudes(verified.Observations, baseline.Requests, Tolerance, "Baseline observation differs from the verified original model run");
        EnsureProtectedHashes(capture, manifestPath, manifestHash, baselinePath, baselineHash);

        if (!File.Exists(candidatePath)) throw new IOException("Candidate force config missing: " + candidatePath);
        if (SamePath(candidatePath, capture.ConfigPath)) throw new IOException("Candidate force config must be a separate artifact");
        foreach (string protectedPath in new[] { capture.SourcePath, capture.ConfigPath, capture.ProfilePath, manifestPath, baselinePath, candidatePath })
            if (SamePath(candidateOutput, protectedPath)) throw new IOException("Trial output must be a new observation path");
        string candidateHash = RecordingArtifacts.Sha256(candidatePath);
        ForceOptions candidate = ReadForceConfig(candidatePath);
        ReplayRun trial = RunSource(capture, candidate, verifyRecordedOutputs: false);
        if (RecordingArtifacts.Sha256(candidatePath) != candidateHash) throw new IOException("Candidate force config changed during trial");
        EnsureProtectedHashes(capture, manifestPath, manifestHash, baselinePath, baselineHash);

        WriteObservation(candidateOutput, capture.CaseId, manifest.CaseSha256, candidateHash, capture.ProfileHash, trial.Observations, ModelId(candidate));
        if (RecordingArtifacts.Sha256(candidatePath) != candidateHash) throw new IOException("Candidate force config changed while writing trial output");
        EnsureProtectedHashes(capture, manifestPath, manifestHash, baselinePath, baselineHash);
        ForceObservationComparison comparison = Compare(baselinePath, candidateOutput, tolerance);
        return new(candidateOutput, RecordingArtifacts.Sha256(candidateOutput), candidateHash,
            trial.Observations.Count, trial.DrivingSamples, trial.DrivingSeconds, comparison);
    }

    public static ForceObservationComparison Compare(string baselinePath, string candidatePath, double tolerance = .000001)
    {
        if (!double.IsFinite(tolerance) || tolerance < 0 || tolerance > 1) throw new ArgumentOutOfRangeException(nameof(tolerance));
        ObservationFile a = ReadObservation(baselinePath), b = ReadObservation(candidatePath);
        if (a.CaseId != b.CaseId || a.CaseSha256 != b.CaseSha256) throw new IOException("Different case identity; comparison unavailable");
        if (a.Requests.Count != b.Requests.Count) throw new IOException("Different observation count; comparison unavailable");
        for (int i = 0; i < a.Requests.Count; i++)
            if (a.Requests[i].Tick != b.Requests[i].Tick) throw new IOException($"Different observation timeline at sequence {i}; comparison unavailable");
        int differences = 0; double maximum = 0;
        for (int i = 0; i < a.Requests.Count; i++)
        {
            double error = Math.Abs(a.Requests[i].Magnitude - b.Requests[i].Magnitude); maximum = Math.Max(maximum, error);
            if (error > tolerance) differences++;
        }
        return new(differences == 0, differences, maximum, a.Model, b.Model, a.Config, b.Config, a.Profile, b.Profile);
    }

    private static string RequireDirectory(string path)
    {
        string directory = Path.GetFullPath(path);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
        return directory;
    }

    private static CaptureIdentity ReadCapture(string directory)
    {
        string source = Path.Combine(directory, "source.jsonl"), config = Path.Combine(directory, "force-config.json"), profile = Path.Combine(directory, "capture-profile.json");
        foreach (string path in new[] { source, config, profile }) if (!File.Exists(path)) throw new IOException("Missing capture artifact: " + Path.GetFileName(path));
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
        if (!ValidIdentifier(caseId)) throw new IOException("Invalid recorded caseId");
        if (Property("capability") != "signal-reprocess" || Property("recordingContract") != "dbce.wheel.replay-case@1")
            throw new IOException("Unsupported recording capability/contract");
        string sourceRevision = Property("runtimeSource"), executableHash = Property("pluginSha256");
        if (!ValidIdentifier(metadata.PluginVersion)) throw new IOException("Invalid plugin version identifier");
        if (!ValidIdentifier(sourceRevision)) throw new IOException("Invalid runtime source identifier");
        ValidateHash(executableHash, "pluginSha256");
        string sourceHash = RecordingArtifacts.Sha256(source), configHash = RecordingArtifacts.Sha256(config), profileHash = RecordingArtifacts.Sha256(profile);
        if (configHash != Property("forceConfigSha256") || profileHash != Property("captureProfileSha256"))
            throw new IOException("Capture config/profile identity does not match recording metadata");
        return new(directory, source, config, profile, caseId, metadata.PluginVersion, sourceRevision, executableHash,
            sourceHash, configHash, profileHash, ReadForceConfig(config));
    }

    private static ReplayRun RunSource(CaptureIdentity capture, ForceOptions runOptions, bool verifyRecordedOutputs)
    {
        ValidateForceOptions(runOptions);
        CaptureIdentity current = ReadCapture(capture.Directory);
        if (current != capture) throw new IOException("Capture identity changed before model run");
        var records = SessionReader.Read(capture.SourcePath).ToArray();
        // Dispatch by the run's model version: v3 classic ForceSignal, v4/v5 GripSignal against the recorded reference.
        bool useGrip = runOptions.Grip;
        // A trial of the other model reruns it on the same inputs; the recorded model's validity, reasons and reset
        // counts describe the recorded model only, so they are checked when the run uses it.
        bool sameModel = runOptions.Model == capture.OriginalOptions.Model;
        var signal = new ForceSignal(); var grip = new GripSignal(); var recordedReference = new FrontLoadReference(); var observations = new List<Observation>();
        var crash = new CrashReplay();
        long ModelResets() => useGrip ? grip.ResetCount : signal.ResetCount;
        void ResetModel() { if (useGrip) grip.Reset(); else signal.Reset(); }
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
            if (previousResetAfter.HasValue && before != previousResetAfter.Value) ResetModel();
            var sample = new TelemetrySample { Sequence = row.Sequence, ElapsedSeconds = row.ElapsedSeconds,
                SimulationSeconds = Required(c, "sample.simulationSeconds"), State = isDriving ? "driving" : "inactive",
                Discontinuity = Required(c, "sample.discontinuity") == 1 ? "recorded-discontinuity" : null };
            foreach (var pair in c) sample.Channels[pair.Key] = pair.Value;
            if (Options(c) != capture.OriginalOptions) throw new IOException("Force tuning changed during capture; split into stable-config cases before reprocessing");
            if (isDriving) ValidateDrivingInputs(c, row.Sequence, useGrip);
            if (useGrip) recordedReference.Restore(Required(c, "ffb.grip.reference"), (FrontLoadReferenceKind)(int)Integer(c, "ffb.grip.referenceKind"));
            long runnerBefore = ModelResets();
            ForceResult result = useGrip ? grip.Evaluate(sample, runOptions, recordedReference) : signal.Evaluate(sample, runOptions);
            long evaluateResets = ModelResets() - runnerBefore;
            int gate = (int)Integer(c, "ffb.gate");
            if (gate is < 0 or > 11) throw new IOException("Unsupported force gate code: " + gate);
            if (gate != 0) ResetModel();
            long expectedResetDelta = evaluateResets + (gate == 0 ? 0 : 1);
            if (sameModel)
            {
                if (after - before != expectedResetDelta) throw new IOException($"Force reset semantics differ at source sample {row.Sequence}");
                CompareValue(result.Valid ? 1 : 0, Required(c, "ffb.modelValid"), "model validity", row.Sequence);
                int reason = sample.Discontinuity != null ? 11 : ForceObservationSemantics.ModelReason(result.Reason);
                CompareValue(reason, Required(c, "ffb.modelReason"), "model reason", row.Sequence);
            }
            if (verifyRecordedOutputs)
            {
                CompareValue(result.FrontLoad, Required(c, "ffb.frontLoad"), "front load", row.Sequence);
                CompareValue(result.Alignment, Required(c, "ffb.alignmentEstimate"), "alignment", row.Sequence);
                CompareValue(result.Damping, Required(c, "ffb.dampingEstimate"), "damping", row.Sequence);
                CompareValue(result.Preview, Required(c, "ffb.preview"), "preview", row.Sequence);
                // The grip model's two pre-clamp terms: required on v5 rows, checked on older rows that carry them (Codex
                // review of 2d5ab07: otherwise a tampered term would pass while the composed preview still matched).
                if (useGrip && sameModel && (runOptions.Model == GripSignal.ModelVersion || c.ContainsKey("ffb.grip.steering")))
                {
                    CompareValue(grip.LastSteering, Required(c, "ffb.grip.steering"), "grip steering term", row.Sequence);
                    CompareValue(grip.LastDamping, Required(c, "ffb.grip.dampingTerm"), "grip damping term", row.Sequence);
                }
            }
            if (verifyRecordedOutputs && sameModel) crash.Check(sample, c, gate, runOptions.Invert, row.Sequence);
            long tick = checked((long)Math.Round(row.ElapsedSeconds * ForceObservationSemantics.TicksPerSecond, MidpointRounding.AwayFromZero));
            if (tick < 0 || tick < lastTick) throw new IOException("Recording model timeline is not monotonic");
            lastTick = tick; observations.Add(new(tick, result.Preview)); previousResetAfter = after;
            if (isDriving) { driving++; firstDriving ??= row.ElapsedSeconds; lastDriving = row.ElapsedSeconds; }
        }
        double drivingSeconds = firstDriving.HasValue && lastDriving.HasValue ? lastDriving.Value - firstDriving.Value : 0;
        if (driving < 50 || drivingSeconds < 1) throw new IOException($"Insufficient driving coverage: {driving} samples over {drivingSeconds:F3}s; need at least 50 samples and 1 second");
        if (observations.Count == 0) throw new IOException("No reproducible force-model samples");
        EnsureCaptureUnchanged(capture);
        return new(observations, sourceSamples, driving, drivingSeconds);
    }

    /// <summary>
    /// Replays the crash stage (Core <see cref="CrashStage"/>, crash-constant-fallback@2) from each row's recorded contacts,
    /// motion, settings and delivery path, as iRacing Arcade's force contract 2 does. Seeds at the first row whose stage
    /// state is known (the car not live, or a new detector epoch); a cue before that cannot be replayed and is refused.
    /// Woden composes in floats: the written force is the steering preview plus the delivered cue, clamped.
    /// </summary>
    private sealed class CrashReplay
    {
        private CrashStage? _stage;
        private long _epochOffset, _playedOffset;

        internal void Check(TelemetrySample sample, IReadOnlyDictionary<string, double> c, int gate, bool invert, long sequence)
        {
            if (!c.ContainsKey("crash.modelVersion")) { if (c.Keys.Any(k => k.StartsWith("crash.", StringComparison.Ordinal))) throw new IOException("Crash channels without a model version at sample " + sequence); return; }
            if (Required(c, "crash.modelVersion") != CrashStage.ModelVersion) throw new IOException("Unsupported crash model version at sample " + sequence);
            bool enabled = Required(c, "crash.enabled") == 1, delivered = Required(c, "crash.delivered") == 1, discontinuous = Required(c, "crash.discontinuous") == 1;
            double cue = Required(c, "crash.cue"); long played = Integer(c, "crash.count"), epoch = Integer(c, "crash.epoch");
            long contacts = Integer(c, "crash.contacts"), dropped = Integer(c, "crash.contactsDropped");
            if (cue is < -1 or > 1 || delivered && cue == 0 || contacts > CrashStage.MaxContactsPerTick) throw new IOException("Invalid recorded crash cue or contacts at sample " + sequence);
            if (dropped > 0) throw new IOException($"{dropped} crash contacts were not recorded at sample {sequence}; the cue cannot be replayed");
            // Live as production: the selected model's validity, both motion vectors and a running physics clock.
            double time = sample.SimulationSeconds;
            bool motion = sample.TryVector("motion.position.world", out var p) & sample.TryVector("motion.velocity.world", out var v);
            bool live = Required(c, "ffb.modelValid") == 1 && motion && double.IsFinite(time) && time > 0;
            bool deliver = gate is 0 or 11;   // the active path (11 = the write was attempted and failed)
            bool seed = false;
            if (_stage == null)
            {
                if (live && !discontinuous)
                {
                    if (cue != 0 || delivered) throw new IOException($"Crash cue at sample {sequence} comes before a row with a known crash state; it cannot be replayed");
                    return;
                }
                _stage = new CrashStage(); seed = true;
            }
            for (int i = 0; i < contacts; i++)
            {
                string k = "crash.contact" + i + ".";
                double at = Required(c, k + "time"); float speed = (float)Required(c, k + "speed"), share = (float)Required(c, k + "share"), strength = (float)Required(c, k + "strength");
                bool road = Required(c, k + "road") == 1, classified = Required(c, k + "classified") == 1;
                if (!seed) _stage.Observe(at, speed, share, road, classified, strength, out _);
            }
            float steering = (float)Required(c, "ffb.preview");
            var m = new Dbce.Wheel.Ffb.MotionSample { Time = time, X = p.X, Y = p.Y, Z = p.Z, Vx = v.X, Vy = v.Y, Vz = v.Z };
            float output = _stage.Mix(live, m, enabled, deliver, invert, steering);
            if (seed) { _epochOffset = epoch - _stage.Epoch; _playedOffset = played - _stage.Played; }
            else
            {
                for (int i = 0; i < contacts; i++) CompareValue(_stage.TickContact(i).Intensity, Required(c, "crash.contact" + i + ".intensity"), "crash contact intensity", sequence);
                if (_stage.Epoch + _epochOffset != epoch || _stage.Played + _playedOffset != played || _stage.Discontinuous != discontinuous)
                    throw new IOException("Recorded crash epoch/count/discontinuity differs from the replayed motion and contacts at sample " + sequence);
            }
            CompareValue(_stage.Cue, cue, "crash cue", sequence);
            if (_stage.Delivered != delivered) throw new IOException("Recorded crash delivery differs from the replay at sample " + sequence);
            if (deliver && c.TryGetValue("ffb.deliveredOutput", out double written)) CompareValue(output, written, "written force (steering + crash cue)", sequence);
        }
    }

    private static void ValidateDrivingInputs(IReadOnlyDictionary<string, double> c, long sequence, bool grip)
    {
        foreach (string key in new[] { "motion.speed", "motion.velocity.local.z", "wheel.fl.grounded", "wheel.fr.grounded" }) _ = Required(c, key);
        foreach (string corner in new[] { "fl", "fr" }) if (Required(c, "wheel." + corner + ".grounded") == 1)
        {
            foreach (string field in new[] { "contactForce", "sidewaysSlip" }) _ = Required(c, "wheel." + corner + "." + field);
            // A v4 run needs the live friction curve the production model read; captures from before f1ee7af lack it.
            if (grip) foreach (string field in FrictionFields) _ = Required(c, "wheel." + corner + ".sideFriction." + field);
        }
        foreach (string name in Tuning) _ = Required(c, "ffb.tuning." + name);
        if (grip) foreach (string key in new[] { "ffb.grip.reference", "ffb.grip.referenceKind" }) _ = Required(c, key);
    }

    private static ForceOptions Options(IReadOnlyDictionary<string, double> c)
    {
        double version = Required(c, "ffb.tuning.modelVersion");
        if (version is not (3 or GripSignal.CoupledDampingVersion or GripSignal.ModelVersion)) throw new IOException("Unsupported force model version");
        var options = new ForceOptions((float)Required(c, "ffb.tuning.strengthPercent"), (float)Required(c, "ffb.tuning.peakPercent"),
            (float)Required(c, "ffb.tuning.loadReference"), (float)Required(c, "ffb.tuning.slipScale"),
            (float)Required(c, "ffb.tuning.smoothingMs"), (float)Required(c, "ffb.tuning.damping"), Required(c, "ffb.tuning.invert") == 1);
        // Each grip row keeps its recorded version: v4 rows replay v4 arithmetic, v5 rows v5.
        return version == 3 ? options : options with { Model = (int)version, LoadRatio = (float)Required(c, "ffb.tuning.loadRatio"), GripSmoothing = (float)Required(c, "ffb.tuning.gripSmoothing") };
    }

    private static ForceOptions ReadForceConfig(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path)); var root = doc.RootElement;
        int version = root.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
        string[] common = { "schema", "version", "model", "strengthPercent", "peakPercent", "loadReference", "slipScale", "smoothingMs", "damping", "invert" };
        if (version is not (1 or 2 or 3)) throw new IOException("Unsupported force config artifact");
        int model = ForceObservationSemantics.ModelForConfigVersion(version);
        if (version >= 2) Strict(root, common.Concat(GripTuning).ToArray()); else Strict(root, common);
        if (root.GetProperty("schema").GetString() != "woden.force-config" || root.GetProperty("model").GetString() != ForceObservationSemantics.ModelId(model))
            throw new IOException("Unsupported force config artifact");
        var options = new ForceOptions(root.GetProperty("strengthPercent").GetSingle(), root.GetProperty("peakPercent").GetSingle(), root.GetProperty("loadReference").GetSingle(),
            root.GetProperty("slipScale").GetSingle(), root.GetProperty("smoothingMs").GetSingle(), root.GetProperty("damping").GetSingle(), root.GetProperty("invert").GetBoolean());
        if (version >= 2) options = options with { Model = model, LoadRatio = root.GetProperty("loadRatio").GetSingle(), GripSmoothing = root.GetProperty("gripSmoothing").GetSingle() };
        ValidateForceOptions(options); return options;
    }

    private static void ValidateForceOptions(ForceOptions options)
    {
        float[] values = { options.Strength, options.PeakPercent, options.LoadReference, options.SlipScale, options.SmoothingMs, options.Damping };
        if (values.Any(x => !float.IsFinite(x)) || options.LoadReference <= 0 || options.SlipScale <= 0 ||
            options.Grip && (!float.IsFinite(options.LoadRatio) || options.LoadRatio <= 0 || !float.IsFinite(options.GripSmoothing) || options.GripSmoothing is < 0 or > .95f))
            throw new IOException("Candidate force config contains invalid model tuning");
    }

    private static ManifestIdentity ReadManifest(string path, CaptureIdentity capture)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length > 64 * 1024) throw new IOException("Case manifest too large");
        using var doc = JsonDocument.Parse(bytes); JsonElement root = doc.RootElement;
        Strict(root, "schema", "version", "caseId", "game", "adapter", "capability", "clock", "source", "initialState", "artifacts", "provenance");
        if (root.GetProperty("schema").GetString() != "dbce.wheel.replay-case" || root.GetProperty("version").GetInt32() != 1 ||
            root.GetProperty("capability").GetString() != "signal-reprocess" || root.GetProperty("initialState").ValueKind != JsonValueKind.Null)
            throw new IOException("Unsupported Woden replay manifest");
        string caseId = Identifier(root, "caseId"); _ = Identifier(root, "game"); _ = Identifier(root, "adapter");
        if (caseId != capture.CaseId) throw new IOException("Manifest case identity differs from source metadata");
        JsonElement clock = root.GetProperty("clock"); Strict(clock, "domain", "ticksPerSecond");
        if (clock.GetProperty("domain").GetString() != "monotonic" || clock.GetProperty("ticksPerSecond").GetInt64() != ForceObservationSemantics.TicksPerSecond)
            throw new IOException("Manifest clock mismatch");
        string sourceHash = Artifact(root.GetProperty("source"), "source.jsonl", "dbce.wheel.session@1", capture.SourcePath);
        JsonElement artifacts = root.GetProperty("artifacts"); Strict(artifacts, "config", "profile");
        string configHash = Artifact(artifacts.GetProperty("config"), "force-config.json", ConfigFormat(capture.OriginalOptions), capture.ConfigPath);
        string profileHash = Artifact(artifacts.GetProperty("profile"), "capture-profile.json", "woden.capture-profile@1", capture.ProfilePath);
        JsonElement provenance = root.GetProperty("provenance"); Strict(provenance, "sourceRevision", "dirty", "executableSha256");
        if (Identifier(provenance, "sourceRevision") != capture.SourceRevision || provenance.GetProperty("dirty").GetBoolean() ||
            Hash(provenance, "executableSha256") != capture.ExecutableHash) throw new IOException("Manifest provenance mismatch");
        if (sourceHash != capture.SourceHash || configHash != capture.ConfigHash || profileHash != capture.ProfileHash)
            throw new IOException("Manifest artifact identity differs from capture metadata");
        return new(caseId, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), sourceHash, configHash, profileHash);
    }

    private static string Artifact(JsonElement element, string requiredPath, string requiredFormat, string actualPath)
    {
        Strict(element, "path", "sha256", "format");
        string path = element.GetProperty("path").GetString() ?? "", format = Identifier(element, "format"), hash = Hash(element, "sha256");
        if (path != requiredPath || format != requiredFormat || RecordingArtifacts.Sha256(actualPath) != hash)
            throw new IOException("Manifest artifact mismatch: " + requiredPath);
        return hash;
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
    private static void CompareValue(double actual, double recorded, string name, long sequence)
    {
        if (Math.Abs(actual - recorded) > Tolerance) throw new IOException($"Recorded {name} differs from the actual force model at sample {sequence}: {recorded} vs {actual}");
    }

    private static void RequireMatchingTimelineAndMagnitudes(IReadOnlyList<Observation> expected, IReadOnlyList<Observation> actual, double tolerance, string message)
    {
        if (expected.Count != actual.Count) throw new IOException(message + ": count mismatch");
        for (int i = 0; i < expected.Count; i++)
            if (expected[i].Tick != actual[i].Tick || Math.Abs(expected[i].Magnitude - actual[i].Magnitude) > tolerance)
                throw new IOException(message + $" at sequence {i}");
    }

    private static void RequireObservationIdentity(ObservationFile file, string caseId, string caseHash, string configHash, string profileHash, string model)
    {
        if (file.CaseId != caseId || file.CaseSha256 != caseHash || file.Model != model ||
            file.Config != configHash || file.Profile != profileHash) throw new IOException("Observation identity differs from verified case artifacts");
    }
    private static void EnsureCaptureUnchanged(CaptureIdentity capture)
    {
        if (RecordingArtifacts.Sha256(capture.SourcePath) != capture.SourceHash || RecordingArtifacts.Sha256(capture.ConfigPath) != capture.ConfigHash ||
            RecordingArtifacts.Sha256(capture.ProfilePath) != capture.ProfileHash) throw new IOException("Capture artifact changed during offline model run");
    }
    private static void EnsureProtectedHashes(CaptureIdentity capture, string manifestPath, string manifestHash, string baselinePath, string baselineHash)
    {
        EnsureCaptureUnchanged(capture);
        if (RecordingArtifacts.Sha256(manifestPath) != manifestHash || RecordingArtifacts.Sha256(baselinePath) != baselineHash)
            throw new IOException("Case manifest or baseline observation changed during tuning trial");
    }
    private static bool SamePath(string left, string right) => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static void WriteObservation(string path, string caseId, string caseHash, string configHash, string profileHash, IReadOnlyList<Observation> observations, string model)
    {
        if (observations.Count > MaxObservationRecords) throw new IOException("Observation record bound exceeded");
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); long bytesWritten = 0;
        void WriteLine(object value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value) + "\n");
            if (bytes.Length > MaxObservationLineBytes) throw new IOException("Observation line bound exceeded");
            if (bytesWritten + bytes.Length > MaxObservationBytes) throw new IOException("Observation file bound exceeded");
            output.Write(bytes, 0, bytes.Length); bytesWritten += bytes.Length;
        }
        WriteLine(new { kind = "header", schema = "dbce.wheel.force-observation", version = 1, caseId, caseSha256 = caseHash,
            clock = new { domain = "monotonic", ticksPerSecond = ForceObservationSemantics.TicksPerSecond }, model,
            configSha256 = configHash, profileSha256 = profileHash, output = "observe", physicalOutput = false });
        for (int i = 0; i < observations.Count; i++) WriteLine(new { kind = "force", sequence = i, tick = observations[i].Tick,
            frame = (long?)null, effect = "steering", family = "constant", operation = "set", magnitude = observations[i].Magnitude,
            frequencyHz = 0.0, durationMs = (double?)null });
        WriteLine(new { kind = "footer", complete = true, count = observations.Count }); output.Flush(true);
    }

    private static void WriteManifest(string path, string caseId, string pluginVersion, string sourceRevision, string executableHash,
        string sourceHash, string configHash, string profileHash, string configFormat)
    {
        var value = new { schema = "dbce.wheel.replay-case", version = 1, caseId, game = "super-woden-rally-edge",
            adapter = "woden-rally-edge-wheel@" + pluginVersion, capability = "signal-reprocess",
            clock = new { domain = "monotonic", ticksPerSecond = ForceObservationSemantics.TicksPerSecond },
            source = new { path = "source.jsonl", sha256 = sourceHash, format = "dbce.wheel.session@1" }, initialState = (object?)null,
            artifacts = new Dictionary<string, object> {
                ["config"] = new { path = "force-config.json", sha256 = configHash, format = configFormat },
                ["profile"] = new { path = "capture-profile.json", sha256 = profileHash, format = "woden.capture-profile@1" } },
            provenance = new { sourceRevision, dirty = false, executableSha256 = executableHash } };
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(output, value, new JsonSerializerOptions { WriteIndented = true }); output.WriteByte((byte)'\n'); output.Flush(true);
    }

    private static ObservationFile ReadObservation(string path)
    {
        if (!File.Exists(path)) throw new IOException("Observation missing: " + path);
        long length = new FileInfo(path).Length;
        if (length > MaxObservationBytes) throw new IOException("Observation file bound exceeded");
        if (length == 0) throw new IOException("Incomplete observation file");
        using (var ending = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        { ending.Seek(-1, SeekOrigin.End); if (ending.ReadByte() != '\n') throw new IOException("Truncated observation line"); }
        string? caseId = null, caseHash = null, model = null, config = null, profile = null; var requests = new List<Observation>(); bool footer = false; long previousTick = 0;
        foreach (string line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) throw new IOException("Blank observation line");
            if (Encoding.UTF8.GetByteCount(line) + 1 > MaxObservationLineBytes) throw new IOException("Observation line bound exceeded");
            using var doc = JsonDocument.Parse(line); var root = doc.RootElement; string kind = root.GetProperty("kind").GetString() ?? "";
            if (kind == "header")
            {
                if (caseId != null || requests.Count != 0 || footer) throw new IOException("Observation header is not first");
                Strict(root, "kind", "schema", "version", "caseId", "caseSha256", "clock", "model", "configSha256", "profileSha256", "output", "physicalOutput");
                if (root.GetProperty("schema").GetString() != "dbce.wheel.force-observation" || root.GetProperty("version").GetInt32() != 1 ||
                    root.GetProperty("output").GetString() != "observe" || root.GetProperty("physicalOutput").GetBoolean()) throw new IOException("Unsupported/unsafe observation header");
                var clock = root.GetProperty("clock"); Strict(clock, "domain", "ticksPerSecond");
                if (clock.GetProperty("domain").GetString() != "monotonic" || clock.GetProperty("ticksPerSecond").GetInt64() != ForceObservationSemantics.TicksPerSecond) throw new IOException("Observation clock mismatch");
                caseId = Identifier(root, "caseId"); caseHash = Hash(root, "caseSha256"); model = Identifier(root, "model"); config = Hash(root, "configSha256"); profile = Hash(root, "profileSha256");
            }
            else if (kind == "force")
            {
                if (caseId == null || footer) throw new IOException("Observation request outside stream");
                Strict(root, "kind", "sequence", "tick", "frame", "effect", "family", "operation", "magnitude", "frequencyHz", "durationMs");
                if (root.GetProperty("sequence").GetInt32() != requests.Count || Identifier(root, "effect") != "steering" ||
                    root.GetProperty("family").GetString() != "constant" || root.GetProperty("operation").GetString() != "set" ||
                    root.GetProperty("frame").ValueKind != JsonValueKind.Null || root.GetProperty("durationMs").ValueKind != JsonValueKind.Null || root.GetProperty("frequencyHz").GetDouble() != 0) throw new IOException("Unsupported observation request");
                long tick = root.GetProperty("tick").GetInt64(); double magnitude = root.GetProperty("magnitude").GetDouble();
                if (tick < 0 || tick < previousTick || !double.IsFinite(magnitude) || magnitude is < -1 or > 1) throw new IOException("Invalid observation timeline/magnitude");
                previousTick = tick; requests.Add(new(tick, magnitude));
                if (requests.Count > MaxObservationRecords) throw new IOException("Observation record bound exceeded");
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

    private static bool ValidIdentifier(string? value) => value is { Length: > 0 and <= 128 } &&
        IsAlphanumeric(value[0]) && value.All(c => IsAlphanumeric(c) || c is '.' or '_' or ':' or '@' or '-');
    private static bool IsAlphanumeric(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';
    private static string Identifier(JsonElement root, string name)
    {
        string value = root.GetProperty(name).GetString() ?? "";
        if (!ValidIdentifier(value)) throw new IOException("Invalid identifier: " + name);
        return value;
    }
    private static string Hash(JsonElement root, string name)
    {
        string value = root.GetProperty(name).GetString() ?? ""; ValidateHash(value, name); return value;
    }
    private static void ValidateHash(string value, string name)
    {
        if (value.Length != 64 || value.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))) throw new IOException("Invalid lowercase SHA-256: " + name);
    }
    private static void Strict(JsonElement element, params string[] names)
    {
        var allowed = names.ToHashSet(StringComparer.Ordinal); int count = 0;
        foreach (var property in element.EnumerateObject()) { count++; if (!allowed.Contains(property.Name)) throw new IOException("Unknown JSON key: " + property.Name); }
        if (count != allowed.Count || names.Any(x => !element.TryGetProperty(x, out _))) throw new IOException("Missing/duplicate JSON key");
    }
}
