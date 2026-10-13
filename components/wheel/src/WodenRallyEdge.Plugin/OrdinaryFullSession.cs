using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dbce.Wheel.Playback;
using Dbce.Wheel.Recording;
using Dbce.Wheel.Sessions;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// One ordinary launch's first driven stage, in DRIVE's full-session package layout with Woden schema ids. Engine-free:
// the plugin feeds observed scenes, native loads, prefix poses and postfix samples. This class owns only its new files
// beside the ordinary capture. It never mutes, resets force state, changes input/physics/progress, or touches the
// ordinary signal recorder or its case files. A failure ends only this capture and leaves full-session-incomplete.txt.
// A valid package is session.json plus its verified outer seal; this class writes session.json only after the stage,
// timeline and completed source verified, and removes it if the seal then fails. No playback adapter is declared here.
internal sealed class OrdinaryFullSession
{
    internal const string TimelineSchema = "woden.full-session@1", PackageSchema = "woden.full-session-package@1",
        ObservationSchema = "woden.full-session-observation@1", AdapterVersion = "woden.observed-stage@1", Game = "Super Woden Rally Edge";
    internal const string IncompleteFile = "full-session-incomplete.txt";
    internal const string NoPlayback = "Ordinary Woden playback is not admitted yet: the cold-start route for recorded menu choices is still under review.";
    private const int Pending = 0, Committing = 1, Done = 2;
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private readonly string _root;
    private readonly FullSessionPackageNames.Names _names;
    private readonly StageSignalFiles.Identity _id;
    private readonly string _origin;
    private readonly double _clockOrigin;
    private readonly FullSessionCapture _capture;
    private readonly List<JsonObject> _journal = new();
    private TrajectoryRecorder? _tape;
    private TelemetryOutput? _signals;
    private ForceAnalysisHistory? _analysis;
    private FullSessionSelection? _selection;
    private FullSessionArtifact? _artifact;
    private FullSessionTimeline? _timeline;
    private string? _scenario;
    private double _fixedDeltaTime, _poseOrigin = double.NaN, _lastPoseTime = double.NaN, _lastSeconds;
    private int _poses, _stagePoses, _signalRows, _state = Pending;
    private bool _stageUsed, _paused, _stopped;
    private volatile string? _outcome;

    internal string? Failure { get; private set; }
    /// <summary>"package" once session.json is sealed and verified; "incomplete" once full-session-incomplete.txt exists.</summary>
    internal string? Outcome => _outcome;
    internal bool Terminal => _outcome != null;
    internal bool Observing => !_stopped && Failure == null;
    internal bool Stopped => _stopped || Failure != null;
    internal bool StageOpen => _tape != null;
    internal bool CanOpenStage => Observing && !_stageUsed;
    internal int Poses => _poses;
    internal string Root => _root;

    /// <param name="origin">"diagnostic" unless the producer has explicit evidence of an owner session (DRIVE's rule).</param>
    internal OrdinaryFullSession(string root, string signalName, StageSignalFiles.Identity id, string origin, double now)
    {
        if (origin is not ("owner" or "diagnostic")) throw new ArgumentException("Unknown recording origin");
        _root = Path.GetFullPath(root); _names = FullSessionPackageNames.FromSignal(signalName); _id = id; _origin = origin; _clockOrigin = now;
        if (!File.Exists(Path.Combine(_root, _names.Signals))) throw new IOException("The ordinary signal file is not in this capture");
        foreach (string name in new[] { _names.Observation, _names.Timeline, _names.Stage, FullSessionPackageNames.Descriptor, "complete.tsv", IncompleteFile })
            if (File.Exists(Path.Combine(_root, name)) || Directory.Exists(Path.Combine(_root, name))) throw new IOException("Full-session output already exists: " + name);
        _capture = new FullSessionCapture(TimelineSchema, id.GameHash, AdapterVersion);
        _capture.Observe(new FullSessionState { Phase = FullSessionPhase.Boot, Marker = "recording-start" }, 0, 0);
        Journal("header", 0, new JsonObject { ["schema"] = ObservationSchema, ["gameBuild"] = id.GameHash, ["bootRequested"] = true,
            ["source"] = _names.Signals, ["diagnostic"] = origin == "diagnostic", ["capture"] = Path.GetFileName(_root) });
    }

    internal void Scene(string name, bool loading, double now) => Guard(() =>
    {
        double s = Seconds(now);
        if (_tape != null) SealCore("scene changed to " + name, false, s);
        Journal("scene", s, new JsonObject { ["name"] = name });
        _capture.Observe(new FullSessionState { Phase = loading ? FullSessionPhase.Loading : FullSessionPhase.Menu, Marker = "scene:" + name }, _poses, s);
    });

    internal void Load(string from, string to, string method, JsonNode? selection, double now) => Guard(() =>
    {
        double s = Seconds(now);
        if (_tape != null) SealCore("native load to " + to, false, s);
        Journal("load", s, new JsonObject { ["from"] = from, ["to"] = to, ["method"] = method, ["selection"] = Clone(selection) });
        _capture.Observe(new FullSessionState { Phase = FullSessionPhase.Loading, Marker = "load:" + to }, _poses, s);
    });

    /// <summary>Opens the single stage on the first eligible tick; the caller writes its pose in the same tick.</summary>
    internal void OpenStage(FullSessionSelection selection, string scenario, double fixedDeltaTime, bool physicalOutput,
        Action<string> writeContext, ForceOptions options, double now) => Guard(() =>
    {
        if (!CanOpenStage) return;
        selection.Validate();
        _stageUsed = true;
        double s = Seconds(now);
        string directory = Path.Combine(_root, _names.Stage);
        // This flag describes the stage-open configuration. Per-row force history records later delivery changes.
        var metadata = new Dictionary<string, string> {
            ["contract"] = "dbce.trajectory@1", ["game"] = Game, ["gameSha256"] = _id.GameHash, ["scenario"] = scenario,
            ["fixedDeltaTime"] = fixedDeltaTime.ToString("R", CultureInfo.InvariantCulture), ["captureSource"] = "live-physics",
            ["physicalOutput"] = physicalOutput ? "true" : "false", ["startedUtc"] = DateTime.UtcNow.ToString("O"),
            ["scope"] = "observed ordinary stage; configured outputs retained; original force state not reset" };
        _tape = new TrajectoryRecorder(directory, metadata);
        _selection = selection.Copy(); _scenario = scenario; _fixedDeltaTime = fixedDeltaTime;
        _stagePoses = 0; _signalRows = 0; _paused = false;
        writeContext(directory);
        _signals = StageSignalFiles.Open(directory, _id, options, physicalOutput, RecordingProfiles.Ordinary());
        _analysis = new ForceAnalysisHistory();
        _capture.Observe(new FullSessionState { Phase = FullSessionPhase.Driving, Marker = "stage.driving", Selection = _selection.Copy() }, _poses, s);
        Journal("stage-open", s, new JsonObject { ["stage"] = _names.Stage, ["scenario"] = scenario, ["fixedDeltaTime"] = fixedDeltaTime,
            ["physicalOutput"] = physicalOutput, ["selection"] = JsonSerializer.SerializeToNode(_selection) });
    });

    /// <summary>One MainCar.FixedUpdate prefix pose. A gap, scene/car or timestep change seals the stage before writing.</summary>
    internal void Pose(TrajectoryFrame frame, string scenario, double fixedDeltaTime, double now) => Guard(() =>
    {
        if (_tape == null) return;
        double s = Seconds(now);
        if (scenario != _scenario) { SealCore("stage or vehicle changed", false, s); return; }
        if (fixedDeltaTime != _fixedDeltaTime) { SealCore("physics timestep changed", false, s); return; }
        if (_poses >= FullSessionTimeline.MaxPoses) { SealCore("pose limit", false, s); return; }
        double time = double.IsNaN(_poseOrigin) ? 0 : frame.Time - _poseOrigin;
        // The tape refuses a missing physics step; keep every pose already written by sealing first.
        if (!double.IsNaN(_lastPoseTime) && Math.Abs(time - _lastPoseTime - _fixedDeltaTime) > .0001) { SealCore("physics step gap", false, s); return; }
        if (_signalRows != _stagePoses) throw new InvalidDataException("The previous pose has no postfix signal row");
        if (_paused)
        {
            _paused = false;
            _capture.Observe(new FullSessionState { Phase = FullSessionPhase.Driving, Marker = "stage.driving", Selection = _selection!.Copy() }, _poses, s);
            Journal("resume", s, new JsonObject { ["poses"] = _stagePoses });
        }
        if (double.IsNaN(_poseOrigin)) _poseOrigin = frame.Time;
        frame.Time = time;
        _tape.Write(frame);
        _lastPoseTime = time; _stagePoses++; _poses++;
    });

    /// <summary>The same tick's postfix sample, aligned to its pose by capture.trajectoryIndex. A tick without a pose has no row.</summary>
    internal void Signal(TelemetrySample sample, ForceOptions options) => Guard(() =>
    {
        if (_tape == null || _signals == null || _analysis == null || _signalRows == _stagePoses) return;
        if (_signalRows != _stagePoses - 1) throw new InvalidDataException("Source sample does not match the next recorded pose");
        var copy = ForceAnalysisHistory.CopySample(sample);
        copy.Add("capture.trajectoryIndex", _signalRows);
        _analysis.Record(copy, options);
        _signals.Publish(copy);
        _signalRows++;
    });

    internal void Pause(double now) => Guard(() =>
    {
        if (_tape == null || _paused) return;
        double s = Seconds(now);
        _paused = true;
        _capture.Observe(new FullSessionState { Phase = FullSessionPhase.Paused, Marker = "stage.paused", Selection = _selection!.Copy() }, _poses, s);
        Journal("pause", s, new JsonObject { ["poses"] = _stagePoses });
    });

    internal void Seal(string reason, bool finished, double now) => Guard(() => SealCore(reason, finished, Seconds(now)));

    internal void Stop(double now)
    {
        if (!Observing) return;
        try
        {
            double s = Seconds(now);
            if (_tape != null) SealCore("ordinary recording stopped", false, s);
            Journal("stop", s, new JsonObject { ["poses"] = _poses });
            _timeline = _capture.Stop(_poses, s, _artifact);
            WriteNew(Path.Combine(_root, _names.Timeline), JsonSerializer.SerializeToUtf8Bytes(_timeline, Indented));
            _stopped = true;
        }
        catch (Exception ex) { Fail("stop: " + ex.Message); }
    }

    internal void Fail(string reason)
    {
        // After Stop only Package decides the outcome, so a late failure cannot race its commit.
        if (Failure != null || Terminal || _stopped) return;
        Failure = reason.Length > 1000 ? reason[..1000] : reason;
        var tape = _tape; var signals = _signals;
        _tape = null; _signals = null; _analysis = null;
        try { tape?.Dispose(); } catch { /* evidence is retained as written */ }
        try { signals?.Dispose(); } catch { /* evidence is retained as written */ }
        if (tape != null) TryWrite(Path.Combine(_root, _names.Stage, "incomplete.txt"), "Stage capture failed: " + Failure);
        Incomplete("Full-session capture failed: " + Failure);
    }

    /// <summary>Fixture seam: runs on the package worker once the commit has begun (null in production).</summary>
    internal Action? CommitStarted { get; set; }
    internal const double MaxHoldSeconds = 60, MaxCommitHoldSeconds = 300;
    /// <summary>Whether the ordinary status should still read Stopping. Plugin close always releases it, so the last
    /// published status is the recorder's real state even if a slow commit is still running; readers require the
    /// outer seal, so a descriptor left without it is never a package. Past 60 s after packaging starts it abandons
    /// a package not yet committing; a commit already running is released at 300 s, like close.</summary>
    internal bool HoldStatus(bool closing, double packageStartedAt, double now)
    {
        if (Terminal || closing) return false;
        if (double.IsNaN(packageStartedAt) || now - packageStartedAt <= MaxHoldSeconds) return true;
        if (Abandon("Package verification exceeded 60 seconds; the original capture files are retained.")) return false;
        return now - packageStartedAt <= MaxCommitHoldSeconds;
    }

    /// <summary>Gives up before the package commit begins (game closing, or a stalled verification). False only while a
    /// commit is running; true once nothing further will be written (the marker is attempted if still pending).</summary>
    internal bool Abandon(string reason)
    {
        int previous = Interlocked.CompareExchange(ref _state, Done, Pending);
        if (previous == Committing) return false;
        if (previous == Pending) Incomplete(reason);
        return true;
    }

    /// <summary>Worker-side, after Stop and after the ordinary recorder closed its file.</summary>
    internal void Package()
    {
        if (Terminal || Failure != null || Volatile.Read(ref _state) != Pending) return;
        bool descriptor = false, seal = false;
        try
        {
            if (!_stopped || _timeline == null) throw new InvalidDataException("the capture did not stop cleanly");
            var (metadata, footer) = ReadSource(Path.Combine(_root, _names.Signals), _id.GameHash);
            var counts = footer.Counts;
            Journal("end", _timeline.DurationSeconds, new JsonObject { ["sourceStatus"] = footer.Completed ? "Completed" : "LimitReached",
                ["sourceStop"] = footer.StopReason, ["complete"] = footer.Completed, ["sourceCounters"] = JsonSerializer.SerializeToNode(counts) });
            WriteJournal();
            if (_timeline.Stage == null)
            {
                if (Interlocked.CompareExchange(ref _state, Done, Pending) == Pending)
                    Incomplete("No sealed driven stage was recorded; menu-only, too-short and interrupted stages stay evidence only.");
                return;
            }
            _timeline.Validate(TimelineSchema, forPlayback: true);
            VerifyStage(_timeline.Stage);
            var selection = _timeline.Steps.First(s => s.State?.Phase == FullSessionPhase.Driving).State!.Selection!;
            var started = metadata.StartedUtc.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(metadata.StartedUtc, DateTimeKind.Utc) : metadata.StartedUtc;
            var document = new JsonObject {
                ["schema"] = PackageSchema, ["game"] = Game, ["gameBuildSha256"] = _id.GameHash,
                ["recordedUtc"] = started.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), ["origin"] = _origin,
                ["outcome"] = _timeline.Outcome == FullSessionOutcome.CompletedStage ? "completed-stage" : "stopped",
                ["durationSeconds"] = _timeline.DurationSeconds, ["car"] = selection.Car, ["biome"] = selection.Biome,
                ["signals"] = _names.Signals, ["observation"] = _names.Observation, ["timeline"] = _names.Timeline, ["stage"] = _names.Stage,
                ["playbackUnavailableReason"] = FullSessionPackageNames.UnavailableReason(NoPlayback) };
            if (Interlocked.CompareExchange(ref _state, Committing, Pending) != Pending) return; // abandoned meanwhile
            CommitStarted?.Invoke();
            WriteNew(Path.Combine(_root, FullSessionPackageNames.Descriptor), System.Text.Encoding.UTF8.GetBytes(document.ToJsonString(Indented)));
            descriptor = true;
            ArtifactSeal.Complete(_root, _names.OuterSealFiles());
            seal = true;
            ArtifactSeal.Verify(_root, _names.OuterSealFiles());
            _outcome = "package";
            Volatile.Write(ref _state, Done);
        }
        catch (Exception ex)
        {
            // Only files this call created are removed, so no descriptor can outlive a failed seal.
            if (seal) TryDelete(Path.Combine(_root, "complete.tsv"));
            if (descriptor) TryDelete(Path.Combine(_root, FullSessionPackageNames.Descriptor));
            Volatile.Write(ref _state, Done);
            Incomplete("Full-session package was not sealed: " + ex.Message);
        }
    }

    private static (SessionMetadata Metadata, SessionFooter Footer) ReadSource(string path, string gameHash)
    {
        SessionMetadata? metadata = null; SessionFooter? footer = null;
        foreach (var row in SessionReader.Read(path))
        {
            if (row.Metadata != null) metadata = row.Metadata;
            if (row.Footer != null) footer = row.Footer;
            if (row.Marker?.Name == "capture-failed") throw new InvalidDataException("the ordinary source records a capture failure");
        }
        if (metadata == null || footer == null) throw new InvalidDataException("the ordinary source has no metadata or footer");
        // As the case finalizer (RecordedForceReplay): a limit-reached or otherwise unfinished source is never packaged.
        if (!footer.Completed) throw new InvalidDataException("the ordinary source did not complete (" + footer.StopReason + ")");
        if (!metadata.Properties.TryGetValue("gameAssemblySha256", out string? build) || build != gameHash)
            throw new InvalidDataException("the ordinary source names a different game build");
        var c = footer.Counts;
        if (c.DroppedSamples != 0 || c.DroppedMarkers != 0 || c.ErrorCount != 0 || c.InvalidCount != 0 || c.RejectedAfterStop != 0)
            throw new InvalidDataException("the ordinary source dropped, rejected or failed records");
        return (metadata, footer);
    }

    private void VerifyStage(FullSessionArtifact stage)
    {
        string directory = Path.Combine(_root, stage.Directory);
        var tape = TrajectoryTape.Read(directory); // verifies the nested seal
        ArtifactSeal.Verify(directory, new[] { "trajectory.meta", "trajectory.tsv" }.Concat(StageSignalFiles.Files).ToArray());
        if (ArtifactSeal.Hash(Path.Combine(directory, "complete.tsv")) != stage.SealSha256 || tape.Frames.Length != stage.PoseCount ||
            tape.Metadata["gameSha256"] != _id.GameHash || tape.Metadata["game"] != Game)
            throw new InvalidDataException("the sealed stage differs from its timeline reference");
        int row = 0; bool closed = false;
        foreach (var record in SessionReader.Read(Path.Combine(directory, "source.jsonl")))
        {
            if (record.Footer != null) closed = record.Footer.Completed;
            if (record.Sample == null) continue;
            if (!record.Sample.Channels.TryGetValue("capture.trajectoryIndex", out double index) || index != row)
                throw new InvalidDataException("stage signal row " + row + " is not aligned to its pose");
            row++;
        }
        if (!closed || row != stage.PoseCount) throw new InvalidDataException("stage signal rows differ from the recorded poses");
    }

    private void SealCore(string reason, bool finished, double s)
    {
        var tape = _tape; var signals = _signals;
        if (tape == null) return;
        _tape = null; _signals = null; _analysis = null;
        string directory = Path.Combine(_root, _names.Stage);
        try
        {
            StageSignalFiles.Finish(signals!, _signalRows, _stagePoses);
            tape.Complete(StageSignalFiles.Files);
            _artifact = new FullSessionArtifact { Directory = _names.Stage, SealSha256 = ArtifactSeal.Hash(Path.Combine(directory, "complete.tsv")), PoseCount = _stagePoses };
        }
        catch (Exception ex)
        {
            tape.Dispose();
            try { signals?.Dispose(); } catch { /* already reported below */ }
            TryWrite(Path.Combine(directory, "incomplete.txt"), "Stage not sealed (" + reason + "): " + ex.Message);
            Journal("stage-incomplete", s, new JsonObject { ["reason"] = reason, ["error"] = ex.Message, ["poses"] = _stagePoses });
            return;
        }
        Journal("stage-sealed", s, new JsonObject { ["reason"] = reason, ["poses"] = _stagePoses, ["finished"] = finished && !_paused });
        if (finished && !_paused)
            _capture.Observe(new FullSessionState { Phase = FullSessionPhase.Finish, Marker = "stage.finish", Selection = _selection!.Copy() }, _poses, s);
    }

    private void Guard(Action action)
    {
        if (!Observing) return;
        try { action(); }
        catch (Exception ex) { Fail(ex.Message); }
    }
    private double Seconds(double now) => _lastSeconds = Math.Max(_lastSeconds, now - _clockOrigin);
    // net6's System.Text.Json has no JsonNode.DeepClone; a node can have only one parent.
    private static JsonNode? Clone(JsonNode? node) => node == null ? null : JsonNode.Parse(node.ToJsonString());
    private void Journal(string kind, double seconds, JsonObject detail)
    {
        // Reserve the end row; a longer observation is incomplete rather than silently truncated.
        if (_journal.Count >= FullSessionTimeline.MaxSteps - (kind == "end" ? 0 : 1)) throw new InvalidDataException("Observation row limit reached");
        _journal.Add(new JsonObject { ["seconds"] = seconds, ["kind"] = kind, ["detail"] = detail });
    }
    private void WriteJournal()
    {
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < _journal.Count; i++)
        {
            var row = new JsonObject { ["seq"] = i };
            foreach (var pair in _journal[i]) row[pair.Key] = Clone(pair.Value);
            text.Append(row.ToJsonString()).Append('\n');
        }
        WriteNew(Path.Combine(_root, _names.Observation), System.Text.Encoding.UTF8.GetBytes(text.ToString()));
    }
    // Terminal only once the marker really exists. If it cannot be written, nothing claims an outcome: the status hold
    // stays bounded (60/300 s, or plugin close) and readers see neither a valid package nor a marker.
    private void Incomplete(string reason)
    {
        string marker = Path.Combine(_root, IncompleteFile);
        if (TryWrite(marker, reason.Replace('\r', ' ').Replace('\n', ' ') + "\n") || File.Exists(marker)) _outcome = "incomplete";
    }
    private static void WriteNew(string path, byte[] bytes)
    {
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(bytes); output.Flush(true);
    }
    private static bool TryWrite(string path, string text)
    {
        try
        {
            if (!Directory.Exists(Path.GetDirectoryName(path))) return false;
            WriteNew(path, System.Text.Encoding.UTF8.GetBytes(text));
            return true;
        }
        catch (IOException) { return false; } // an existing marker is kept
        catch (UnauthorizedAccessException) { return false; }
    }
    private static void TryDelete(string path) { try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}
