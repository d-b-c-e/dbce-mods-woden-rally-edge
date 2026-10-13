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
//
// Ownership of the outcome: the package worker, game close and the status hold race for one state (Pending ->
// Committing or Done). Every package output (timeline, observation, descriptor, seal, the incomplete marker, the
// handoff) is written only by whoever won it; a loser writes nothing. The stage seal runs detached from the game thread.
// At game close nothing parses or hashes a source: an unfinished package is handed to the companion's finalizer
// (full-session-pending.json, then WodenCaseFinalizer package), which runs the same verification and commit.
internal sealed class OrdinaryFullSession
{
    internal const string TimelineSchema = "woden.full-session@1", PackageSchema = "woden.full-session-package@1",
        ObservationSchema = "woden.full-session-observation@1", AdapterVersion = "woden.observed-stage@1", Game = "Super Woden Rally Edge";
    internal const string IncompleteFile = "full-session-incomplete.txt";
    internal const string PendingFile = "full-session-pending.json", PendingSchema = "woden.full-session-pending@1", PackagingLock = "full-session-packaging.lock";
    internal const int MaxPendingBytes = 1024 * 1024;
    internal const string NoPlayback = "Ordinary Woden playback is not admitted yet: the cold-start route for recorded menu choices is still under review.";
    private const string NoStage = "No sealed driven stage was recorded; menu-only, too-short and interrupted stages stay evidence only.";
    private const int Pending = 0, Committing = 1, Done = 2;
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private readonly string _root;
    private readonly FullSessionPackageNames.Names _names;
    private readonly StageSignalFiles.Identity _id;
    private readonly string _origin, _inputOrigin;
    private readonly double _clockOrigin;
    private readonly FullSessionCapture _capture;
    private readonly List<JsonObject> _journal = new();
    private TrajectoryRecorder? _tape;
    private TelemetryOutput? _signals;
    private ForceAnalysisHistory? _analysis;
    private FullSessionSelection? _selection;
    private FullSessionArtifact? _artifact;
    private FullSessionTimeline? _timeline;
    private byte[]? _timelineBytes;
    private string? _timelineError;
    private StageSeal? _seal;
    private readonly object _sync = new();
    private string? _scenario;
    private double _fixedDeltaTime, _poseOrigin = double.NaN, _lastPoseTime = double.NaN, _lastSeconds, _stopSeconds;
    private int _poses, _stagePoses, _signalRows, _stopPoses, _state = Pending;
    private bool _stageUsed, _paused, _stopped;
    private volatile string? _outcome;

    // The detached stage seal. Its journal row is reserved at the stage end, so the observation order is the order of
    // events, and is completed (sealed or incomplete) when the seal resolves.
    private sealed class StageSeal
    {
        internal Task<(string? Hash, string? Error)> Task = null!;
        internal JsonObject Row = null!;
        internal string Reason = "";
        internal int Poses;
        internal bool Resolved;
    }

    internal string? Failure { get; private set; }
    /// <summary>"package" once session.json is sealed and verified; "incomplete" once full-session-incomplete.txt exists;
    /// "pending" once full-session-pending.json hands the package to the companion's finalizer.</summary>
    internal string? Outcome => _outcome;
    internal bool Terminal => _outcome != null;
    internal bool Observing => !_stopped && Failure == null;
    internal bool Stopped => _stopped || Failure != null;
    internal bool StageOpen => _tape != null;
    internal bool CanOpenStage => Observing && !_stageUsed;
    internal int Poses => _poses;
    internal string Root => _root;

    /// <param name="inputOrigin">"human", "automated" or "unknown", exactly as the request stated it (never inferred).
    /// The DRIVE-shaped origin is "owner" only for human input, otherwise "diagnostic".</param>
    internal OrdinaryFullSession(string root, string signalName, StageSignalFiles.Identity id, string inputOrigin, double now)
    {
        if (inputOrigin is not ("human" or "automated" or "unknown")) throw new ArgumentException("Unknown recording input origin");
        string origin = DriveOrigin(inputOrigin);
        _root = Path.GetFullPath(root); _names = FullSessionPackageNames.FromSignal(signalName); _id = id; _origin = origin; _inputOrigin = inputOrigin; _clockOrigin = now;
        if (!File.Exists(Path.Combine(_root, _names.Signals))) throw new IOException("The ordinary signal file is not in this capture");
        foreach (string name in new[] { _names.Observation, _names.Timeline, _names.Stage, FullSessionPackageNames.Descriptor, "complete.tsv", IncompleteFile, PendingFile })
            if (File.Exists(Path.Combine(_root, name)) || Directory.Exists(Path.Combine(_root, name))) throw new IOException("Full-session output already exists: " + name);
        _capture = new FullSessionCapture(TimelineSchema, id.GameHash, AdapterVersion);
        _capture.Observe(new FullSessionState { Phase = FullSessionPhase.Boot, Marker = "recording-start" }, 0, 0);
        Journal("header", 0, new JsonObject { ["schema"] = ObservationSchema, ["gameBuild"] = id.GameHash, ["bootRequested"] = true,
            ["source"] = _names.Signals, ["diagnostic"] = origin == "diagnostic", ["inputOrigin"] = inputOrigin, ["capture"] = Path.GetFileName(_root) });
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

    /// <summary>Game thread. Records the stop; the timeline is completed once the detached stage seal resolves, by
    /// whoever packages (the worker) or hands off (game close).</summary>
    internal void Stop(double now)
    {
        if (!Observing) return;
        try
        {
            double s = Seconds(now);
            if (_tape != null) SealCore("ordinary recording stopped", false, s);
            Journal("stop", s, new JsonObject { ["poses"] = _poses });
            _stopSeconds = s; _stopPoses = _poses;
            _stopped = true;
        }
        catch (Exception ex) { Fail("stop: " + ex.Message); }
    }

    /// <summary>Waits for the detached stage seal and completes its reserved journal row. True once resolved (also when
    /// no stage was opened); false only when the timeout passed first.</summary>
    internal bool WaitStageSeal(TimeSpan timeout)
    {
        var seal = _seal;
        if (seal == null) return true;
        if (!seal.Task.Wait(timeout)) return false;
        lock (_sync)
        {
            if (seal.Resolved) return true;
            var (hash, error) = seal.Task.Result;
            if (hash != null)
                _artifact = new FullSessionArtifact { Directory = _names.Stage, SealSha256 = hash, PoseCount = seal.Poses };
            else
            {
                seal.Row["kind"] = "stage-incomplete";
                seal.Row["detail"] = new JsonObject { ["reason"] = seal.Reason, ["error"] = error, ["poses"] = seal.Poses };
            }
            seal.Resolved = true;
            return true;
        }
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

    /// <summary>Fixture seams (null in production): VerificationStarted runs on the package worker once it is packaging,
    /// before it waits for the stage seal; PrepareFinished after its read-only verification, before it claims the commit;
    /// CommitStarted once the commit has begun; StageSealRunning inside the detached stage seal, before it drains and
    /// hashes.</summary>
    internal Action? CommitStarted { get; set; }
    internal Action? VerificationStarted { get; set; }
    internal Action? PrepareFinished { get; set; }
    internal Action? StageSealRunning { get; set; }
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

    /// <summary>Gives up before the package commit begins (a stalled verification, or a game close before the ordinary
    /// signal file closed). False only while a commit is running; true once nothing further will be written (the marker
    /// is attempted if still pending).</summary>
    internal bool Abandon(string reason)
    {
        int previous = Interlocked.CompareExchange(ref _state, Done, Pending);
        if (previous == Committing) return false;
        if (previous == Pending) Incomplete(reason);
        return true;
    }

    /// <summary>Worker-side, after Stop and after the ordinary recorder closed its file. The verification is read-only;
    /// outputs are written only once this worker has won the commit, so a package handed off at game close meanwhile
    /// never gets a second writer.</summary>
    internal void Package()
    {
        if (Terminal || Failure != null || Volatile.Read(ref _state) != Pending) return;
        VerificationStarted?.Invoke();
        byte[]? timeline = null;
        Plan plan;
        try
        {
            if (!_stopped) throw new InvalidDataException("the capture did not stop cleanly");
            var (parsed, bytes) = FinishTimeline();
            timeline = bytes;
            plan = Prepare(_root, _names, _id.GameHash, _inputOrigin, parsed, bytes, SnapshotJournal());
        }
        catch (Exception ex)
        {
            if (Interlocked.CompareExchange(ref _state, Done, Pending) != Pending) return; // handed off or abandoned: not this worker's outcome
            if (timeline != null) TryWriteNew(Path.Combine(_root, _names.Timeline), timeline);
            Incomplete("Full-session package was not sealed: " + ex.Message);
            return;
        }
        PrepareFinished?.Invoke();
        if (Interlocked.CompareExchange(ref _state, Committing, Pending) != Pending) return; // handed off or abandoned meanwhile
        CommitStarted?.Invoke();
        if (Commit(_root, _names, plan, writeTimeline: true)) _outcome = "package";
        else if (File.Exists(Path.Combine(_root, IncompleteFile))) _outcome = "incomplete";
        Volatile.Write(ref _state, Done);
    }

    /// <summary>Game close (game thread), after Stop. It parses and hashes no source: within the budget it waits only for
    /// the detached stage seal, or for a commit the worker has already begun. Otherwise it claims the outcome, so the
    /// worker can no longer write, completes the timeline and hands the package to the companion's finalizer
    /// (full-session-pending.json). A stage seal still running when the budget ends leaves the capture incomplete.</summary>
    internal void CloseForExit(TimeSpan budget, Task? worker)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        TimeSpan Left() => budget > clock.Elapsed ? budget - clock.Elapsed : TimeSpan.Zero;
        if (Terminal) return;
        if (Interlocked.CompareExchange(ref _state, Done, Pending) != Pending)
        {
            // The worker owns the outcome (a running commit, or its own marker): let it finish, bounded.
            try { worker?.Wait(Left()); } catch (AggregateException) { /* Package reports through its outcome */ }
            return;
        }
        try
        {
            if (!_stopped) throw new InvalidDataException("the capture did not stop cleanly");
            if (!WaitStageSeal(Left())) { Incomplete("Game closed before the stage seal finished; the original capture files are retained."); return; }
            HandOff(_root);
            _outcome = "pending";
        }
        catch (Exception ex) { Incomplete("Full-session package was not handed off: " + ex.Message); }
    }

    /// <summary>Writes the completed timeline and the handoff into <paramref name="root"/>: the capture at game close, or
    /// a copy of it in fixtures that compare the finalizer's package with the worker's. Needs the stopped capture.</summary>
    internal void HandOff(string root)
    {
        var (_, timeline) = FinishTimeline();
        var document = new JsonObject
        {
            ["schema"] = PendingSchema, ["gameBuildSha256"] = _id.GameHash, ["origin"] = _origin, ["inputOrigin"] = _inputOrigin, ["signals"] = _names.Signals,
            ["timelineSha256"] = Sha256(timeline), ["stageSealSha256"] = _artifact?.SealSha256, ["poses"] = _stopPoses,
            ["journal"] = new JsonArray(SnapshotJournal().Select(line => (JsonNode?)JsonValue.Create(line)).ToArray()),
        };
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(document.ToJsonString(Indented));
        if (bytes.Length > MaxPendingBytes) throw new InvalidDataException("the package handoff exceeds 1 MiB");
        WriteNew(Path.Combine(root, _names.Timeline), timeline);
        WriteNew(Path.Combine(root, PendingFile), bytes);
    }

    internal sealed record OfflineOutcome(string Outcome, string Reason);

    /// <summary>The companion's finalizer (WodenCaseFinalizer package): completes a package handed off at game close with
    /// the plugin's own verification and commit, ending in exactly one of a sealed package or the incomplete marker.
    /// "refused", writing nothing, when another finalizer holds the lock, the outcome is already decided, or there is no
    /// readable handoff.</summary>
    internal static OfflineOutcome PackageOffline(string capture)
    {
        string root = Path.GetFullPath(capture);
        FileStream lease;
        try { lease = new FileStream(Path.Combine(root, PackagingLock), FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new("refused", "the package lock is held or unavailable: " + ex.Message); }
        using (lease)
        {
            foreach (string decided in new[] { FullSessionPackageNames.Descriptor, "complete.tsv", IncompleteFile })
                if (File.Exists(Path.Combine(root, decided))) return new("refused", "the package outcome is already decided: " + decided);
            PendingHandoff pending;
            try { pending = ReadPending(root); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or InvalidOperationException or ArgumentException or FormatException)
            { return new("refused", "no readable package handoff: " + ex.Message); }
            try
            {
                string path = Path.Combine(root, pending.Names.Timeline);
                if (new FileInfo(path).Length > FullSessionTimeline.MaxDocumentBytes) throw new InvalidDataException("the timeline is too large");
                byte[] bytes = File.ReadAllBytes(path);
                if (Sha256(bytes) != pending.TimelineSha256) throw new InvalidDataException("the timeline differs from the handoff");
                var timeline = JsonSerializer.Deserialize<FullSessionTimeline>(bytes) ?? throw new InvalidDataException("missing timeline");
                timeline.Validate(TimelineSchema);
                if (timeline.Stage?.SealSha256 != pending.StageSealSha256) throw new InvalidDataException("the stage seal differs from the handoff");
                var plan = Prepare(root, pending.Names, pending.GameHash, pending.InputOrigin, timeline, bytes, pending.Journal);
                return Commit(root, pending.Names, plan, writeTimeline: false) ? new("package", "") : new("incomplete", ReadMarker(root));
            }
            catch (Exception ex)
            {
                WriteIncomplete(root, "Full-session package was not sealed: " + ex.Message);
                return new("incomplete", ex.Message);
            }
        }
    }

    private sealed record PendingHandoff(FullSessionPackageNames.Names Names, string GameHash, string InputOrigin, string TimelineSha256, string? StageSealSha256, string[] Journal);

    private static PendingHandoff ReadPending(string root)
    {
        string path = Path.Combine(root, PendingFile);
        if (new FileInfo(path).Length > MaxPendingBytes) throw new InvalidDataException("the handoff exceeds 1 MiB");
        var o = JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? throw new InvalidDataException("empty handoff");
        if ((string?)o["schema"] != PendingSchema) throw new InvalidDataException("unknown handoff schema");
        string game = (string?)o["gameBuildSha256"] ?? "", timeline = (string?)o["timelineSha256"] ?? "";
        string origin = (string?)o["origin"] ?? "", inputOrigin = (string?)o["inputOrigin"] ?? "";
        string? stage = (string?)o["stageSealSha256"];
        if (!FullSessionTimeline.Hash(game) || !FullSessionTimeline.Hash(timeline) || (stage != null && !FullSessionTimeline.Hash(stage)) ||
            inputOrigin is not ("human" or "automated" or "unknown") || origin != DriveOrigin(inputOrigin))
            throw new InvalidDataException("invalid handoff identity");
        var names = FullSessionPackageNames.FromSignal((string?)o["signals"] ?? "");
        string[] journal = (o["journal"] as JsonArray ?? throw new InvalidDataException("the handoff has no journal"))
            .Select(row => (string?)row ?? throw new InvalidDataException("empty journal row")).ToArray();
        for (int i = 0; i < journal.Length; i++)
            if ((int?)JsonNode.Parse(journal[i])?.AsObject()["seq"] != i) throw new InvalidDataException("the handoff journal is not contiguous");
        return new(names, game, inputOrigin, timeline, stage, journal);
    }

    /// <summary>The DRIVE-shaped origin: "owner" only for human input, otherwise "diagnostic".</summary>
    private static string DriveOrigin(string inputOrigin) => inputOrigin == "human" ? "owner" : "diagnostic";

    /// <summary>Everything a commit writes, computed read-only by the plugin's worker or the companion's finalizer.</summary>
    private sealed record Plan(byte[] Timeline, byte[] Observation, byte[]? Descriptor);

    private static Plan Prepare(string root, FullSessionPackageNames.Names names, string gameHash, string inputOrigin,
        FullSessionTimeline timeline, byte[] timelineBytes, IReadOnlyList<string> journal)
    {
        var (metadata, footer) = ReadSource(Path.Combine(root, names.Signals), gameHash);
        // One fact, read once from the request: the source and the package must state the same input origin.
        string sourceOrigin = metadata.Properties.TryGetValue("inputOrigin", out var stated) ? stated : "unknown";
        if (sourceOrigin != inputOrigin) throw new InvalidDataException("the ordinary source states input origin " + sourceOrigin + ", the package " + inputOrigin);
        if (journal.Count >= FullSessionTimeline.MaxSteps) throw new InvalidDataException("Observation row limit reached");
        var end = new JsonObject { ["seconds"] = timeline.DurationSeconds, ["kind"] = "end", ["detail"] = new JsonObject {
            ["sourceStatus"] = footer.Completed ? "Completed" : "LimitReached", ["sourceStop"] = footer.StopReason,
            ["complete"] = footer.Completed, ["sourceCounters"] = JsonSerializer.SerializeToNode(footer.Counts) } };
        var text = new System.Text.StringBuilder();
        foreach (string line in journal) text.Append(line).Append('\n');
        text.Append(Line(end, journal.Count)).Append('\n');
        byte[] observation = System.Text.Encoding.UTF8.GetBytes(text.ToString());
        if (timeline.Stage == null) return new Plan(timelineBytes, observation, null);
        timeline.Validate(TimelineSchema, forPlayback: true);
        VerifyStage(Path.Combine(root, timeline.Stage.Directory), timeline.Stage, gameHash);
        var selection = timeline.Steps.First(s => s.State?.Phase == FullSessionPhase.Driving).State!.Selection!;
        var started = metadata.StartedUtc.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(metadata.StartedUtc, DateTimeKind.Utc) : metadata.StartedUtc;
        var document = new JsonObject {
            ["schema"] = PackageSchema, ["game"] = Game, ["gameBuildSha256"] = gameHash,
            ["recordedUtc"] = started.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), ["origin"] = DriveOrigin(inputOrigin), ["inputOrigin"] = inputOrigin,
            ["outcome"] = timeline.Outcome == FullSessionOutcome.CompletedStage ? "completed-stage" : "stopped",
            ["durationSeconds"] = timeline.DurationSeconds, ["car"] = selection.Car, ["biome"] = selection.Biome,
            ["signals"] = names.Signals, ["observation"] = names.Observation, ["timeline"] = names.Timeline, ["stage"] = names.Stage,
            ["playbackUnavailableReason"] = FullSessionPackageNames.UnavailableReason(NoPlayback) };
        return new Plan(timelineBytes, observation, System.Text.Encoding.UTF8.GetBytes(document.ToJsonString(Indented)));
    }

    /// <summary>Writes a plan; only the winner of the outcome calls it. True for a sealed, verified package; otherwise the
    /// marker was attempted. Only files this call created are removed, so no descriptor can outlive a failed seal.</summary>
    private static bool Commit(string root, FullSessionPackageNames.Names names, Plan plan, bool writeTimeline)
    {
        bool descriptor = false, seal = false;
        try
        {
            if (writeTimeline) WriteNew(Path.Combine(root, names.Timeline), plan.Timeline);
            WriteNew(Path.Combine(root, names.Observation), plan.Observation);
            if (plan.Descriptor == null) { WriteIncomplete(root, NoStage); return false; }
            WriteNew(Path.Combine(root, FullSessionPackageNames.Descriptor), plan.Descriptor);
            descriptor = true;
            ArtifactSeal.Complete(root, names.OuterSealFiles());
            seal = true;
            ArtifactSeal.Verify(root, names.OuterSealFiles());
            return true;
        }
        catch (Exception ex)
        {
            if (seal) TryDelete(Path.Combine(root, "complete.tsv"));
            if (descriptor) TryDelete(Path.Combine(root, FullSessionPackageNames.Descriptor));
            WriteIncomplete(root, "Full-session package was not sealed: " + ex.Message);
            return false;
        }
    }

    internal static (SessionMetadata Metadata, SessionFooter Footer) ReadSource(string path, string gameHash)
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

    internal static void VerifyStage(string directory, FullSessionArtifact stage, string gameHash)
    {
        var tape = TrajectoryTape.Read(directory); // verifies the nested seal
        ArtifactSeal.Verify(directory, new[] { "trajectory.meta", "trajectory.tsv" }.Concat(StageSignalFiles.Files).ToArray());
        if (ArtifactSeal.Hash(Path.Combine(directory, "complete.tsv")) != stage.SealSha256 || tape.Frames.Length != stage.PoseCount ||
            tape.Metadata["gameSha256"] != gameHash || tape.Metadata["game"] != Game)
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

    // Game thread: reserves the stage's journal row and observes its finish at the true stage end, then detaches the
    // writers. The drain, the tape seal and its hash run on a worker; a 20-minute stage takes most of a second there.
    // A journal or timeline refusal happens while the writers are still attached, so Fail disposes them.
    private void SealCore(string reason, bool finished, double s)
    {
        var tape = _tape; var signals = _signals;
        if (tape == null) return;
        int rows = _signalRows, poses = _stagePoses;
        bool finish = finished && !_paused;
        var row = Journal("stage-sealed", s, new JsonObject { ["reason"] = reason, ["poses"] = poses, ["finished"] = finish });
        if (finish)
            _capture.Observe(new FullSessionState { Phase = FullSessionPhase.Finish, Marker = "stage.finish", Selection = _selection!.Copy() }, _poses, s);
        _tape = null; _signals = null; _analysis = null;
        string directory = Path.Combine(_root, _names.Stage);
        var running = StageSealRunning;
        _seal = new StageSeal { Row = row, Reason = reason, Poses = poses, Task = Task.Run<(string? Hash, string? Error)>(() =>
        {
            try
            {
                running?.Invoke();
                StageSignalFiles.Finish(signals!, rows, poses);
                tape.Complete(StageSignalFiles.Files);
                return (ArtifactSeal.Hash(Path.Combine(directory, "complete.tsv")), null);
            }
            catch (Exception ex)
            {
                try { tape.Dispose(); } catch { /* evidence is retained as written */ }
                try { signals?.Dispose(); } catch { /* reported below */ }
                TryWrite(Path.Combine(directory, "incomplete.txt"), "Stage not sealed (" + reason + "): " + ex.Message);
                return (null, ex.Message);
            }
        }) };
    }

    // Once, by the worker or game close: the timeline needs the resolved stage seal for its artifact.
    private (FullSessionTimeline Timeline, byte[] Bytes) FinishTimeline()
    {
        WaitStageSeal(Timeout.InfiniteTimeSpan);
        lock (_sync)
        {
            if (_timelineBytes == null && _timelineError == null)
            {
                try
                {
                    _timeline = _capture.Stop(_stopPoses, _stopSeconds, _artifact);
                    _timelineBytes = JsonSerializer.SerializeToUtf8Bytes(_timeline, Indented);
                }
                catch (Exception ex) { _timelineError = ex.Message; }
            }
            if (_timelineError != null) throw new InvalidDataException("the timeline could not be completed: " + _timelineError);
            return (_timeline!, _timelineBytes!);
        }
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
    private JsonObject Journal(string kind, double seconds, JsonObject detail)
    {
        lock (_sync)
        {
            // The end row is reserved (Prepare adds it); a longer observation is incomplete rather than silently truncated.
            if (_journal.Count >= FullSessionTimeline.MaxSteps - 1) throw new InvalidDataException("Observation row limit reached");
            var row = new JsonObject { ["seconds"] = seconds, ["kind"] = kind, ["detail"] = detail };
            _journal.Add(row);
            return row;
        }
    }
    // The observation rows so far as immutable lines, exactly as they are written.
    private string[] SnapshotJournal()
    {
        lock (_sync) return _journal.Select((row, i) => Line(row, i)).ToArray();
    }
    private static string Line(JsonObject row, int seq)
    {
        var line = new JsonObject { ["seq"] = seq };
        foreach (var pair in row) line[pair.Key] = Clone(pair.Value);
        return line.ToJsonString();
    }
    // Terminal only once the marker really exists. If it cannot be written, nothing claims an outcome: the status hold
    // stays bounded (60/300 s, or plugin close) and readers see neither a valid package nor a marker.
    private void Incomplete(string reason)
    {
        if (WriteIncomplete(_root, reason)) _outcome = "incomplete";
    }
    private static bool WriteIncomplete(string root, string reason)
    {
        string marker = Path.Combine(root, IncompleteFile);
        return TryWrite(marker, reason.Replace('\r', ' ').Replace('\n', ' ') + "\n") || File.Exists(marker);
    }
    private static string ReadMarker(string root)
    {
        try { string text = File.ReadAllText(Path.Combine(root, IncompleteFile)).Trim(); return text.Length > 512 ? text[..512] : text; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return ""; }
    }
    private static string Sha256(byte[] bytes) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
    private static void WriteNew(string path, byte[] bytes)
    {
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(bytes); output.Flush(true);
    }
    private static bool TryWriteNew(string path, byte[] bytes)
    {
        try { WriteNew(path, bytes); return true; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
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
