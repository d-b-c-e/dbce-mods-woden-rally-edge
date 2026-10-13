using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dbce.Wheel.Playback;
using Dbce.Wheel.Recording;
using Dbce.Wheel.Sessions;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// The ordinary full-session capture (OrdinaryFullSession, StageSignalFiles, DiagnosticRecordingControl's hold) compiled
// from production source, driven like CarHook does: prefix pose, postfix sample to the stage copy, then the ordinary
// recorder. Real TelemetryOutput/SessionRecorder, TrajectoryRecorder, ArtifactSeal and SessionReader. Synthetic rows;
// no game, device, network or owner file.
internal static class FullSessionChecks
{
    internal const string Hash = "f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c";
    const double Dt = .02;
    static readonly ForceOptions Options = new(Model: 6);

    internal sealed class Fixture : IDisposable
    {
        public readonly string Temp, Root;
        public readonly TelemetryOutput Ordinary;
        public readonly OrdinaryFullSession Session;
        public double Now = 1, Physics = 250, Step = Dt;
        /// <summary>Runs on each tick's sample before the stage copy and the ordinary recorder see it.</summary>
        public Action<TelemetrySample>? Extend;
        /// <summary>Adds the production channels the stage review needs (tune, friction curves, delivery counters).</summary>
        public bool Reviewable;
        int _sequence, _delivery;
        /// <param name="properties">Ordinary recording properties for the capture root (default: the game build only).</param>
        public Fixture(RecordingOptions? recording = null, Func<string, Dictionary<string, string>>? properties = null, string inputOrigin = "unknown")
        {
            Temp = Path.Combine(Path.GetTempPath(), "woden-full-session-" + Guid.NewGuid().ToString("N"));
            Root = Path.Combine(Temp, "request-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Ordinary = new TelemetryOutput(new OutputOptions(ForzaPort: 0, DetailPort: 0, RecordingPath: Path.Combine(Root, "source.jsonl"), Enabled: false),
                "fixture", "{\"fixture\":true}", "0.2.14", properties?.Invoke(Root) ?? new Dictionary<string, string> { ["gameAssemblySha256"] = Hash }, recording);
            Session = new OrdinaryFullSession(Root, "source.jsonl", Identity(), inputOrigin, 0);
        }
        public static StageSignalFiles.Identity Identity() => new(Hash, typeof(FullSessionChecks).Assembly.Location, "{\"fixture\":true}", "0.2.14", "fixture");
        public TelemetrySample Sample(string state = "driving")
        {
            var s = new TelemetrySample { SessionId = "fixture", Sequence = _sequence++, ElapsedSeconds = Now, SimulationSeconds = Physics, CarInstanceId = 1, State = state };
            s.Vector("motion.position.world", new Vector3(0, 0, (float)(Physics * 20))); s.Vector("motion.velocity.world", new Vector3(0, 0, 20));
            s.Add("motion.speed", 20); s.Add("motion.velocity.local.z", 20); s.Add("game.trueSpeed", 59 + (_sequence % 3));
            s.Add("camera.mode", 0); s.Add("camera.stockPreset", 2); s.Add("camera.mountedView", 0);
            s.Add("ffb.grip.reference", 10000); s.Add("ffb.grip.referenceKind", 2); s.Add("ffb.grip.carEpoch", 1);
            if (Reviewable)
            {
                var o = Options;
                s.Add("ffb.tuning.strengthPercent", o.Strength); s.Add("ffb.tuning.peakPercent", o.PeakPercent); s.Add("ffb.tuning.loadReference", o.LoadReference);
                s.Add("ffb.tuning.slipScale", o.SlipScale); s.Add("ffb.tuning.smoothingMs", o.SmoothingMs); s.Add("ffb.tuning.damping", o.Damping);
                s.Add("ffb.tuning.invert", o.Invert ? 1 : 0); s.Add("ffb.tuning.modelVersion", o.Model);
                s.Add("ffb.tuning.loadRatio", o.LoadRatio); s.Add("ffb.tuning.gripSmoothing", o.GripSmoothing); s.Add("ffb.grip.curveEstimateVersion", 1);
                s.Add("wheelInput.steer", Math.Sin(Physics) * .2);
                foreach (string corner in new[] { "fl", "fr" })
                {
                    string p = "wheel." + corner + ".";
                    s.Add(p + "grounded", 1); s.Add(p + "contactForce", 5000); s.Add(p + "sidewaysSlip", .3);
                    s.Add(p + "sideFriction.extremumSlip", 2); s.Add(p + "sideFriction.asymptoteSlip", 2);
                    s.Add(p + "sideFriction.extremumValue", 1); s.Add(p + "sideFriction.asymptoteValue", 1); s.Add(p + "sideFriction.stiffness", 1);
                }
                // Forces were on: the production cumulative counters advance once per sample.
                s.Add("ffb.sent", .1); s.Add("ffb.connectionAttempts", 1); s.Add("ffb.deliveryAttempts", _delivery++);
            }
            return s;
        }
        public static TrajectoryFrame Frame(double time) => new() { Time = time,
            Values = new float[] { 0, 0, (float)time, 0, 0, 0, 1, 0, 0, 20, 0, 0, 0, .1f, .5f, 0, 4000, 3 } };
        /// <summary>One physics tick as CarHook runs it. A paused tick has no pose; its postfix sample has no stage row.</summary>
        public void Tick(bool paused = false, bool pose = true, bool signal = true)
        {
            Physics += paused ? 0 : Step; Now += Step;
            if (pose && !paused) Session.Pose(Frame(Physics), "Kenya SS1|car=8", Step, Now);
            else if (paused) Session.Pause(Now);
            var sample = Sample();
            Extend?.Invoke(sample);
            if (signal) Session.Signal(sample, Options);
            Ordinary.Publish(sample);
        }
        public void Open(bool physicalOutput = false, Action<Dictionary<string, object?>>? context = null) => Session.OpenStage(Selection(Step), "Kenya SS1|car=8", Step, physicalOutput,
            dir => File.WriteAllText(Path.Combine(dir, "stage-context.json"), Context(context)), Options, Now);
        // StageCaptureContext.Write's field set with synthetic values, so consumers can test the context checks.
        internal static string Context(Action<Dictionary<string, object?>>? edit = null)
        {
            var context = new Dictionary<string, object?> {
            ["schema"] = "woden.stage-context@1", ["role"] = "observed launch context; native startup restoration is not yet qualified",
            ["scene"] = "Kenya SS1", ["gameAssemblySha256"] = Hash, ["carId"] = 8, ["playerIndex"] = 0, ["playerCount"] = 1,
            ["gameMode"] = "Arcade", ["subMode"] = "Arcade", ["arcadeRoute"] = 0, ["arcadeRound"] = 0, ["competitionIndex"] = 0,
            ["competitionRound"] = 0, ["competitionAsset"] = "Arcade Route 1", ["trackAsset"] = "Kenya SS1", ["trackName"] = "Kenya SS1",
            ["roundHour"] = 12.0, ["roundTimeSpeed"] = 0, ["roundLaps"] = 1, ["roundWeather"] = "Clear", ["raceLaps"] = 1, ["raceRivals"] = 0,
            ["reverseTrack"] = false, ["damage"] = true, ["rollingStart"] = false, ["ghostMode"] = false, ["fuelConsumptionScale"] = 1.0,
            ["selection"] = new Dictionary<string, object?> { ["gameMode"] = "Arcade", ["subMode"] = "Arcade", ["playerCount"] = 1,
                ["arcadeRoute"] = 0, ["arcadeRound"] = 0, ["arcadeCars"] = new[] { 8, 0, 0, 0 }, ["arcadeSkins"] = new[] { 0, 0, 0, 0 },
                ["arcadeAutoTransmission"] = new[] { true, true, true, true }, ["playerCamera"] = new[] { 1, 1, 1, 1 }, ["demo"] = false },
            ["observedScenes"] = new[] { "SplashScreen", "Title Screen", "MapScreen", "Loading Screen", "StagePresentation", "Kenya SS1" },
            ["nativeSceneRequests"] = Array.Empty<object>(), ["observedScenesTruncated"] = false, ["nativeSceneRequestsTruncated"] = false,
            ["fixture"] = true };
            edit?.Invoke(context);
            return JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = true });
        }
        public void Menus()
        {
            foreach (var (scene, loading) in new[] { ("SplashScreen", false), ("Title Screen", false), ("MapScreen", false) })
                Session.Scene(scene, loading, Now += .5);
            Session.Load("MapScreen", "StagePresentation", "MenuCameraScript.LoadScene", new JsonObject { ["arcadeRoute"] = 0, ["arcadeCars"] = new JsonArray(8, 0, 0, 0) }, Now += .5);
            Session.Scene("Loading Screen", true, Now += .5);
            Session.Scene("StagePresentation", false, Now += .5);
            Session.Scene("Kenya SS1", false, Now += .5);
        }
        public void CloseOrdinary()
        {
            Ordinary.StopRecording();
            for (int i = 0; i < 100 && Ordinary.RecordingStatus is "Recording" or "Stopping"; i++) Thread.Sleep(20);
        }
        public string Path_(string name) => Path.Combine(Root, name);
        public void Dispose()
        {
            try { Ordinary.Dispose(); } catch { }
            try { Directory.Delete(Temp, true); } catch { }
        }
    }
    static FullSessionSelection Selection(double step = Dt) => new() { Scene = "Kenya SS1", Biome = "Arcade Route 1", Track = "Kenya SS1",
        Car = "car=8", Mode = "Arcade/Arcade", Start = "route=0;round=0", FixedDeltaTime = step };

    /// <summary>Boot, menus, a stage with a pause and a finish: one sealed package with phase-aligned stage signals.</summary>
    public static void Package(Action<bool, string> check)
    {
        using var f = new Fixture();
        f.Menus();
        f.Open();
        check(f.Session.StageOpen, "the first eligible tick opens the stage");
        for (int i = 0; i < 40; i++) f.Tick();
        for (int i = 0; i < 5; i++) f.Tick(paused: true); // no pose, no stage row
        for (int i = 0; i < 35; i++) f.Tick();
        f.Session.Seal("finished", true, f.Now);
        check(!f.Session.CanOpenStage, "one stage per launch; later stages are analysis-only");
        f.Session.Scene("StagePresentation", false, f.Now += 1);
        for (int i = 0; i < 3; i++) { f.Now += .1; f.Ordinary.Publish(f.Sample("no-local-car")); } // the recorder refuses repeated times
        f.CloseOrdinary();
        f.Session.Stop(f.Now += .1);
        f.Session.Package();
        check(f.Session.Outcome == "package" && f.Session.Failure == null, "package sealed: " + f.Session.Failure + " " + Read(f.Path_(OrdinaryFullSession.IncompleteFile)));
        check(!File.Exists(f.Path_(OrdinaryFullSession.IncompleteFile)), "no incomplete marker beside a sealed package");

        var names = FullSessionPackageNames.FromSignal("source.jsonl");
        ArtifactSeal.Verify(f.Root, names.OuterSealFiles());
        var descriptor = JsonNode.Parse(File.ReadAllText(f.Path_("session.json")))!.AsObject();
        check((string?)descriptor["schema"] == OrdinaryFullSession.PackageSchema && (string?)descriptor["game"] == OrdinaryFullSession.Game &&
            (string?)descriptor["gameBuildSha256"] == Hash && (string?)descriptor["origin"] == "diagnostic" && (string?)descriptor["outcome"] == "stopped",
            "descriptor identity, diagnostic origin and stopped outcome");
        check((string?)descriptor["car"] == "car=8" && (string?)descriptor["biome"] == "Arcade Route 1" && (string?)descriptor["stage"] == "source.stage" &&
            (string?)descriptor["observation"] == "source.session.jsonl" && (string?)descriptor["timeline"] == "source.stage-session.json", "descriptor payloads");
        check(descriptor["playbackAdapter"] == null && ((string?)descriptor["playbackUnavailableReason"])?.Contains("not admitted") == true,
            "no playback adapter before S2; the reason says why");

        var tape = TrajectoryTape.Read(f.Path_("source.stage"));
        check(tape.Frames.Length == 75 && tape.Metadata["physicalOutput"] == "false" && tape.Metadata["scenario"] == "Kenya SS1|car=8" &&
            tape.Metadata["scope"].Contains("configured outputs retained", StringComparison.Ordinal), "75 contiguous poses across the pause, truthful scope");
        ArtifactSeal.Verify(f.Path_("source.stage"), "trajectory.meta", "trajectory.tsv", "source.jsonl", "force-config.json", "channels.json", "stage-context.json");
        // StageStartup's own readers: every stage row aligned to its pose, with the recorded HUD speed.
        var hud = new RecordedHudSpeed(); int rows = 0;
        foreach (var row in SessionReader.Read(Path.Combine(f.Root, "source.stage", "source.jsonl")))
        {
            if (row.Kind != SessionRecordKind.Sample) continue;
            hud.Add(row.Sample.Channels);
            check(row.Sample.Channels["capture.trajectoryIndex"] == rows++, "stage row index");
            check(row.Sample.Channels.ContainsKey("analysis.force.contractVersion"), "stage-local force analysis rides on each row");
        }
        var speeds = hud.Finish(tape.Frames.Length);
        check(rows == 75 && speeds.Recorded, "one recorded HUD-speed row per pose");

        var timeline = JsonSerializer.Deserialize<FullSessionTimeline>(File.ReadAllBytes(f.Path_("source.stage-session.json")))!;
        timeline.Validate(OrdinaryFullSession.TimelineSchema, forPlayback: true);
        var poses = timeline.Steps.Where(s => s.Kind == FullSessionStepKind.Poses).ToArray();
        check(poses.Length == 2 && poses[0].PoseCount == 40 && poses[1].FirstPose == 40 && poses[1].PoseCount == 35, "the pause splits the pose intervals");
        check(timeline.Steps.Any(s => s.State?.Phase == FullSessionPhase.Paused) && timeline.Steps.Any(s => s.Action?.Kind == FullSessionActionKind.FinishStage) &&
            timeline.Steps.Any(s => s.State?.Phase == FullSessionPhase.Finish), "pause and observed finish recorded");
        check(timeline.Steps.Count(s => s.State?.Marker.StartsWith("scene:", StringComparison.Ordinal) == true) == 7 &&
            timeline.Steps.Any(s => s.State?.Phase == FullSessionPhase.Loading && s.State.Marker == "load:StagePresentation"), "menus and native loads from boot");
        check(timeline.Stage!.PoseCount == 75 && timeline.Stage.SealSha256 == ArtifactSeal.Hash(Path.Combine(f.Root, "source.stage", "complete.tsv")), "stage reference");

        var journal = File.ReadAllLines(f.Path_("source.session.jsonl")).Select(l => JsonNode.Parse(l)!.AsObject()).ToArray();
        check(journal.Select((r, i) => (int?)r["seq"] == i).All(x => x), "contiguous observation sequence");
        check((string?)journal[0]["kind"] == "header" && (string?)journal[0]["detail"]!["schema"] == OrdinaryFullSession.ObservationSchema &&
            (bool?)journal[0]["detail"]!["diagnostic"] == true && (string?)journal[^1]["kind"] == "end", "header and end rows");
        var kinds = journal.Select(r => (string?)r["kind"]).ToArray();
        check(new[] { "scene", "load", "stage-open", "pause", "resume", "stage-sealed", "stop" }.All(k => kinds.Contains(k)), "observed events: " + string.Join(",", kinds));
        var load = journal.First(r => (string?)r["kind"] == "load")["detail"]!;
        check((int?)load["selection"]!["arcadeCars"]![0] == 8, "native load keeps the selection snapshot");
        var counters = journal[^1]["detail"]!["sourceCounters"]!;
        SessionFooter? footer = null;
        foreach (var row in SessionReader.Read(f.Path_("source.jsonl"))) if (row.Footer != null) footer = row.Footer;
        check(footer != null && (long?)counters["WrittenSamples"] == footer.Counts.WrittenSamples && (bool?)journal[^1]["detail"]!["complete"] == footer.Completed,
            "end row matches the immutable ordinary footer");

        // A consumer fixture (Wheelkit) can take these exact writer bytes instead of a hand-made package.
        if (Environment.GetEnvironmentVariable("WODEN_FULL_SESSION_FIXTURE_OUT") is { Length: > 0 } golden)
            CopyTree(f.Root, Path.Combine(golden, Path.GetFileName(f.Root)));

        // The outer seal binds the payloads: a changed stage seal is refused.
        File.AppendAllText(Path.Combine(f.Root, "source.stage", "complete.tsv"), " ");
        check(Throws(() => ArtifactSeal.Verify(f.Root, names.OuterSealFiles())), "outer seal refuses a changed nested seal");
    }

    /// <summary>Truthful incomplete outcomes: never a session.json without a sealed, aligned stage and a clean source.</summary>
    public static void Incomplete(Action<bool, string> check)
    {
        using (var f = new Fixture())
        {
            f.Menus();
            f.CloseOrdinary(); f.Session.Stop(f.Now); f.Session.Package();
            check(f.Session.Outcome == "incomplete" && !File.Exists(f.Path_("session.json")) &&
                Read(f.Path_(OrdinaryFullSession.IncompleteFile)).Contains("No sealed driven stage", StringComparison.Ordinal), "menu-only session is evidence only");
            check(File.Exists(f.Path_("source.stage-session.json")) && File.Exists(f.Path_("source.session.jsonl")), "menu-only timeline and observation kept");
        }
        using (var f = new Fixture())
        {
            f.Menus(); f.Open();
            f.Tick(); f.Tick(signal: false); f.Tick(); // the second pose never got its postfix row
            check(f.Session.Failure != null && f.Session.Outcome == "incomplete" && !f.Session.Observing, "a pose without its signal row fails the capture");
            check(File.Exists(Path.Combine(f.Root, "source.stage", "incomplete.txt")) && !File.Exists(f.Path_("session.json")), "stage and package marked incomplete");
            f.Tick(); f.Session.Stop(f.Now); f.Session.Package();
            check(!File.Exists(f.Path_("session.json")) && !File.Exists(f.Path_("source.stage-session.json")), "a failed capture writes nothing further");
        }
        using (var f = new Fixture())
        {
            f.Menus(); f.Open();
            for (int i = 0; i < 20; i++) f.Tick();
            f.Physics += Dt; // a skipped physics step
            for (int i = 0; i < 5; i++) f.Tick();
            check(!f.Session.StageOpen && f.Session.Observing, "a physics step gap seals the stage, keeping its contiguous poses");
            f.CloseOrdinary(); f.Session.Stop(f.Now); f.Session.Package();
            check(f.Session.Outcome == "package" && TrajectoryTape.Read(Path.Combine(f.Root, "source.stage")).Frames.Length == 20, "sealed at the gap: 20 poses");
        }
        using (var f = new Fixture())
        {
            f.Menus(); f.Open();
            for (int i = 0; i < 10; i++) f.Tick();
            f.Ordinary.FailRecording(f.Now, "force analysis: fixture"); // the ordinary source itself records a failure
            f.CloseOrdinary(); f.Session.Stop(f.Now); f.Session.Package();
            check(f.Session.Outcome == "incomplete" && !File.Exists(f.Path_("session.json")) &&
                Read(f.Path_(OrdinaryFullSession.IncompleteFile)).Contains("capture failure", StringComparison.Ordinal), "a failed ordinary source is never packaged");
        }
        using (var f = new Fixture())
        {
            f.Menus(); f.Open();
            f.Tick();
            f.Session.Seal("driving eligibility ended", false, f.Now);
            f.CloseOrdinary(); f.Session.Stop(f.Now); f.Session.Package();
            check(File.Exists(Path.Combine(f.Root, "source.stage", "incomplete.txt")) && f.Session.Outcome == "incomplete", "a one-pose stage is not sealed or packaged");
        }
        using (var f = new Fixture())
        {
            f.Menus(); f.Open();
            for (int i = 0; i < 10; i++) f.Tick();
            check(f.Session.Abandon("Game closed before package verification finished"), "abandon before commit");
            f.CloseOrdinary(); f.Session.Stop(f.Now); f.Session.Package();
            check(!File.Exists(f.Path_("session.json")) && f.Session.Outcome == "incomplete", "an abandoned capture never commits a descriptor");
        }
        using (var f = new Fixture(new RecordingOptions { QueueCapacity = 512, MaxDurationSeconds = 1, MaxFileBytes = 64L * 1024 * 1024, MaxChannelsPerSample = 512 }))
        {
            f.Menus(); f.Open();
            for (int i = 0; i < 80; i++) f.Tick(); // 1.6 s of recorded time against a 1 s limit
            f.CloseOrdinary();
            check(f.Ordinary.RecordingStatus == "LimitReached", "the ordinary recorder stopped at its limit: " + f.Ordinary.RecordingStatus);
            f.Session.Stop(f.Now); f.Session.Package();
            check(f.Session.Outcome == "incomplete" && !File.Exists(f.Path_("session.json")) && !File.Exists(f.Path_("complete.tsv")) &&
                Read(f.Path_(OrdinaryFullSession.IncompleteFile)).Contains("did not complete", StringComparison.Ordinal),
                "a clean limit-reached footer is never packaged, as the case finalizer refuses it");
        }
        using (var f = new Fixture())
        {
            f.Menus();
            Directory.CreateDirectory(f.Path_(OrdinaryFullSession.IncompleteFile)); // the marker path cannot become a file
            f.CloseOrdinary(); f.Session.Stop(f.Now); f.Session.Package();
            check(f.Session.Outcome == null && !f.Session.Terminal && !File.Exists(f.Path_("session.json")),
                "a refused marker write claims no outcome and no package");
            check(f.Session.HoldStatus(false, 10, 10 + OrdinaryFullSession.MaxHoldSeconds - 1), "the status hold continues within its bound");
            check(!f.Session.HoldStatus(false, 10, 10 + OrdinaryFullSession.MaxHoldSeconds + 1) && !f.Session.Terminal,
                "the bound releases it, still without claiming an outcome");
            check(!f.Session.HoldStatus(true, double.NaN, 0), "plugin close releases it");
        }
        using (var f = new Fixture())
        {
            File.WriteAllText(f.Path_("session.json"), "{}");
            check(Throws(() => new OrdinaryFullSession(f.Root, "source.jsonl", Fixture.Identity(), "unknown", 0)), "existing package output refuses arming");
            foreach (string bad in new[] { "player", "owner", "diagnostic", "Human", "" })
                check(Throws(() => new OrdinaryFullSession(Path.Combine(f.Temp, "other"), "source.jsonl", Fixture.Identity(), bad, 0)), "input origin " + bad + " refused");
        }
    }

    /// <summary>The ordinary status shows Completed only after the package or its marker exists.</summary>
    public static void StatusHold(Action<bool, string> check)
    {
        using var f = new Fixture();
        var id = Guid.Parse(Path.GetFileName(f.Root)["request-".Length..]);
        var control = new DiagnosticRecordingControl(f.Root, id, Environment.ProcessId, DateTime.UtcNow.Ticks, f.Ordinary);
        bool hold = true;
        control.HoldTerminal = () => hold;
        string Status() => File.ReadAllLines(Path.Combine(f.Root, "control", "status.txt")).First(l => l.StartsWith("status=", StringComparison.Ordinal));
        string Active() => File.ReadAllLines(Path.Combine(f.Root, "control", "status.txt")).First(l => l.StartsWith("active=", StringComparison.Ordinal));
        control.Flush();
        check(Status() == "status=Recording", "recording publishes unchanged");
        f.CloseOrdinary();
        control.Flush();
        check(Status() == "status=Stopping" && Active() == "active=False", "a closed recorder is held at Stopping while the package is pending");
        hold = false; control.Flush();
        check(Status() == "status=Completed", "Completed after the hold releases");
        control.HoldTerminal = () => throw new InvalidOperationException("fixture");
        control.Flush();
        check(Status() == "status=Completed", "a failing hold never hides the recorder state");
    }

    /// <summary>The production hold policy (OrdinaryFullSession.HoldStatus) at plugin close with a commit already begun.</summary>
    public static void ClosingHold(Action<bool, string> check)
    {
        using var f = new Fixture();
        f.Menus(); f.Open();
        for (int i = 0; i < 10; i++) f.Tick();
        f.CloseOrdinary(); f.Session.Stop(f.Now);
        var id = Guid.Parse(Path.GetFileName(f.Root)["request-".Length..]);
        var control = new DiagnosticRecordingControl(f.Root, id, Environment.ProcessId, DateTime.UtcNow.Ticks, f.Ordinary);
        bool closing = false; double startedAt = double.NaN, now = 100;
        control.HoldTerminal = () => f.Session.HoldStatus(closing, startedAt, now);
        string Status() => File.ReadAllLines(Path.Combine(f.Root, "control", "status.txt")).First(l => l.StartsWith("status=", StringComparison.Ordinal));
        control.Flush();
        check(Status() == "status=Stopping", "held while packaging has not started");
        using var started = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        f.Session.CommitStarted = () => { started.Set(); release.Wait(TimeSpan.FromSeconds(30)); };
        startedAt = now;
        var worker = Task.Run(f.Session.Package);
        check(started.Wait(TimeSpan.FromSeconds(30)), "the package commit began");
        check(!f.Session.Abandon("fixture close") && !f.Session.Terminal, "a commit in progress cannot be abandoned or claimed terminal");
        now = startedAt + OrdinaryFullSession.MaxHoldSeconds + 1;
        control.Flush();
        check(Status() == "status=Stopping", "past the 60 s bound, a committing package still holds while the game runs");
        check(!f.Session.HoldStatus(false, startedAt, startedAt + OrdinaryFullSession.MaxCommitHoldSeconds + 1) && !f.Session.Terminal,
            "a hung commit is released at 300 s without claiming a package");
        closing = true;
        control.Flush();
        check(Status() == "status=Completed" && !f.Session.Terminal, "plugin close releases the hold: the last status is the recorder's real state");
        release.Set();
        check(worker.Wait(TimeSpan.FromSeconds(30)) && f.Session.Outcome == "package", "the commit itself still completes and verifies");
        ArtifactSeal.Verify(f.Root, FullSessionPackageNames.FromSignal("source.jsonl").OuterSealFiles());
        check(!f.Session.HoldStatus(false, startedAt, now), "a terminal package never holds");
    }

    /// <summary>StagePlayback's launch owner: supervised stage control and the ordinary capture never share a launch.</summary>
    public static void Ownership(Action<bool, string> check)
    {
        var supervised = new StageLaunchOwner();
        check(supervised.MuteFor(true) && supervised.PollSupervised, "an unclaimed launch keeps the supervised request mute and commands");
        check(!supervised.TryClaimOrdinary(supervisedPresent: true) && !supervised.OrdinaryOwns && supervised.MuteFor(true),
            "a present stage request refuses the ordinary claim");
        check(!supervised.OrdinaryConflict(true), "an unclaimed launch has no ordinary writer to conflict with");
        var ordinary = new StageLaunchOwner();
        check(ordinary.TryClaimOrdinary(supervisedPresent: false) && ordinary.OrdinaryOwns, "a clean ordinary launch is claimed");
        check(!ordinary.OrdinaryConflict(false) && !StageLaunchOwner.PhysicalOutput(true, false, true),
            "an independent no-force mute does not compete for the ordinary stage writers");
        check(ordinary.OrdinaryConflict(true), "an active supervised stage session conflicts with the ordinary writer");
        check(!ordinary.MuteFor(true) && !ordinary.PollSupervised,
            "a later or stale stage request neither mutes the ordinary launch nor reaches the commands that reset force state");
        check(!ordinary.MuteFor(false), "no request, no mute");
        check(!StageLaunchOwner.PhysicalOutput(false, true, true) &&
              !StageLaunchOwner.PhysicalOutput(false, false, false) &&
              StageLaunchOwner.PhysicalOutput(false, false, true),
            "the stage-open physical-output flag reflects every current output gate");
    }

    /// <summary>The stage copy records for as long as the ordinary capture, so it takes the ordinary profile, as the
    /// ordinary source does by default. Read from the live recorders' own option snapshots.</summary>
    public static void StageProfile(Action<bool, string> check)
    {
        using var f = new Fixture();
        f.Menus(); f.Open(); f.Tick();
        var expected = RecordingProfiles.Ordinary();
        foreach (var (name, output) in new[] { ("stage copy", Nested(f.Session)), ("ordinary source", f.Ordinary) })
        {
            var actual = RecorderOptions(output ?? throw new InvalidOperationException(name + " is not recording"));
            check(actual.MaxFileBytes == expected.MaxFileBytes && actual.MaxDurationSeconds == expected.MaxDurationSeconds &&
                actual.QueueCapacity == expected.QueueCapacity && actual.MaxChannelsPerSample == expected.MaxChannelsPerSample,
                $"{name} uses the ordinary profile: {actual.MaxFileBytes} B, {actual.MaxDurationSeconds} s");
        }
        check(RecordingProfiles.Stage().MaxFileBytes != expected.MaxFileBytes && RecordingProfiles.Stage().MaxDurationSeconds != expected.MaxDurationSeconds,
            "the supervised stage profile differs, so the check tells them apart");
    }

    /// <summary>Measurement only (WODEN_LONG_SESSION_SECONDS; WODEN_LONG_SESSION_TEXT=longest for longest-text filler).
    /// One ordinary capture whose single stage lasts the whole duration at the measured 1/60 s step, through the actual
    /// force controller, with rows widened by channels no force, crash or replay path reads. Prints the stage seal (game
    /// thread), the package (worker) and the case finalizer's reprocess, with memory. Asserts only that each completes;
    /// the existing bounds are compared in the report, not here.</summary>
    public static void LongSession(Action<bool, string> check, double seconds, bool longestText)
    {
        var (controller, _) = ForceControllerChecks.Create(); controller.Prepare();
        var force = new ForceOptions();
        using var f = new Fixture(properties: root =>
        {
            string config = RecordingArtifacts.WriteForceConfig(Path.Combine(root, "force-config.json"), force);
            string profile = RecordingArtifacts.WriteCaptureProfile(Path.Combine(root, "capture-profile.json"), Hash, new string('2', 64), new string('3', 64), new string('4', 64), true, true, true, "fixture-guid");
            return new() { ["recordingContract"] = "dbce.wheel.replay-case@1", ["capability"] = "signal-reprocess", ["requestId"] = Guid.NewGuid().ToString(),
                ["caseId"] = "long-session", ["disableForces"] = "false", ["gameAssemblySha256"] = Hash, ["pluginSha256"] = new string('2', 64),
                ["pluginProductVersion"] = "0.2.14+fixture-source", ["runtimeSource"] = "fixture-source", ["forceConfigSha256"] = config, ["captureProfileSha256"] = profile };
        });
        f.Step = 1.0 / RecordingProfiles.PhysicsHz;
        string[] filler = TelemetrySchema.Channels.Keys.Where(Filler).ToArray();
        int tick = 0;
        f.Extend = s =>
        {
            foreach (string c in new[] { "fl", "fr" })
            { s.Add("wheel." + c + ".grounded", 1); s.Add("wheel." + c + ".contactForce", 3000); s.Add("wheel." + c + ".sidewaysSlip", .3 * Math.Sin(tick * .005)); }
            s.Add("wheelInput.steer", 0);
            for (int i = 0; i < filler.Length; i++) s.Add(filler[i], longestText ? -1.2345678901234567E-300 : Math.Round(Math.Sin(tick * .01 + i) * 100, 7));
            controller.Tick(s);
            tick++;
        };
        f.Menus(); f.Open();
        var nested = Nested(f.Session) ?? throw new InvalidOperationException("the stage did not open");
        int ticks = (int)(seconds * RecordingProfiles.PhysicsHz);
        var clock = Stopwatch.StartNew();
        for (int i = 0; i < ticks; i++) { f.Tick(); Pace(f.Ordinary); Pace(nested); }
        double produced = clock.Elapsed.TotalSeconds;

        double seal = Time(() => f.Session.Seal("finished", true, f.Now));
        check(!f.Session.StageOpen && f.Session.Failure == null && File.Exists(Path.Combine(f.Root, "source.stage", "complete.tsv")), "stage sealed: " + f.Session.Failure);
        f.Session.Scene("StagePresentation", false, f.Now += 1);
        f.CloseOrdinary();
        check(f.Ordinary.RecordingStatus == "Completed" && f.Ordinary.RecordingDrops == 0, "ordinary source completed without drops: " + f.Ordinary.RecordingStatus);
        f.Session.Stop(f.Now += .1);
        double package = Time(f.Session.Package);
        check(f.Session.Outcome == "package", "package sealed: " + f.Session.Failure + " " + Read(f.Path_(OrdinaryFullSession.IncompleteFile)));

        // The package's reads and hashes again, now from the OS cache, to attribute its time.
        string outer = f.Path_("source.jsonl"), stage = Path.Combine(f.Root, "source.stage", "source.jsonl");
        long outerRows = 0, stageRows = 0;
        double readOuter = Time(() => { foreach (var r in SessionReader.Read(outer)) if (r.Sample != null) outerRows++; });
        double readStage = Time(() => { foreach (var r in SessionReader.Read(stage)) if (r.Sample != null) stageRows++; });
        double hashOuter = Time(() => ArtifactSeal.Hash(outer)), hashStage = Time(() => ArtifactSeal.Hash(stage));

        // WodenCaseFinalizer reprocess on the same capture after the package, as the companion runs it after exit.
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long baseManaged = GC.GetTotalMemory(true), baseWorkingSet = Process.GetCurrentProcess().WorkingSet64;
        RecordedForceReplayResult? result = null;
        var peak = new PeakSampler();
        double reprocess = Time(() => result = RecordedForceReplay.Reprocess(f.Root));
        peak.Stop();
        check(result != null && result.SourceSamples == outerRows, "reprocess completed over every source row");
        Console.WriteLine($"  reprocess memory: managed peak {Mb(peak.Managed)} MB over {Mb(baseManaged)} MB before; working set peak {Mb(peak.WorkingSet)} MB over {Mb(baseWorkingSet)} MB before");

        long outerBytes = new FileInfo(outer).Length, stageBytes = new FileInfo(stage).Length;
        Console.WriteLine($"  long session: {seconds:0.#} s at {RecordingProfiles.PhysicsHz} Hz, {(longestText ? "longest-text" : "measured-like")} filler; produced in {produced:0.0} s");
        Console.WriteLine($"  ordinary source {Mb(outerBytes)} MB, {outerRows} rows, {outerBytes / Math.Max(1, outerRows)} B/row; stage copy {Mb(stageBytes)} MB, {stageRows} rows, {stageBytes / Math.Max(1, stageRows)} B/row");
        Console.WriteLine($"  stage seal (game thread): {seal:0.00} s");
        Console.WriteLine($"  package (worker): {package:0.00} s [bounds: 60 s to reach the commit, 300 s for a running commit, 10 s at game close]");
        Console.WriteLine($"    cached re-reads: ordinary source stream {readOuter:0.00} s, stage copy stream {readStage:0.00} s; SHA-256 {hashOuter:0.00} s and {hashStage:0.00} s");
        Console.WriteLine($"  case reprocess (after exit): {reprocess:0.00} s; {result!.SourceSamples} source, {result.ModelSamples} model and {result.DrivingSamples} driving samples over {result.DrivingSeconds:0.###} s");
        long Size(params string[] parts) => new FileInfo(Path.Combine(new[] { f.Root }.Concat(parts).ToArray())).Length;
        Console.WriteLine($"  other files: trajectory.tsv {Size("source.stage", "trajectory.tsv")} B, force-observation.jsonl {Size("force-observation.jsonl")} B, " +
            $"case.json {Size("case.json")} B, source.session.jsonl {Size("source.session.jsonl")} B, source.stage-session.json {Size("source.stage-session.json")} B");
    }

    // Channels no force, crash or replay path reads: the rear wheels, the front-wheel fields beside the contact the
    // models read, game state, timing, controls, assists and raw wheel input.
    static bool Filler(string name)
    {
        if (name.StartsWith("wheel.rl.", StringComparison.Ordinal) || name.StartsWith("wheel.rr.", StringComparison.Ordinal)) return true;
        if (name.StartsWith("wheel.fl.", StringComparison.Ordinal) || name.StartsWith("wheel.fr.", StringComparison.Ordinal))
        {
            string field = name["wheel.fl.".Length..];
            return field is not ("grounded" or "contactForce" or "sidewaysSlip") && !field.StartsWith("sideFriction.", StringComparison.Ordinal);
        }
        return name.StartsWith("game.", StringComparison.Ordinal) && name != "game.trueSpeed" || name.StartsWith("timing.", StringComparison.Ordinal) ||
            name.StartsWith("controls.", StringComparison.Ordinal) || name.StartsWith("assist.", StringComparison.Ordinal) || name.StartsWith("wheelRaw.", StringComparison.Ordinal);
    }

    // A game tick is about 16.7 ms; this loop is faster than the writers, so it waits for them rather than overflow their queues.
    static void Pace(TelemetryOutput output)
    {
        static long Backlog(RecordingCounters c) => c.AcceptedSamples + c.AcceptedMarkers - c.WrittenSamples - c.WrittenMarkers;
        if (output.RecordingCounters is not { } counters || Backlog(counters) < 384) return;
        while (output.RecordingStatus == "Recording" && output.RecordingCounters is { } now && Backlog(now) > 64) Thread.Sleep(1);
    }

    static TelemetryOutput? Nested(OrdinaryFullSession session) =>
        (TelemetryOutput?)typeof(OrdinaryFullSession).GetField("_signals", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session);

    // The recorder's own snapshot of its options (Dbce.Wheel.Recording SessionRecorder.options): what the writer enforces.
    static RecordingOptions RecorderOptions(TelemetryOutput output)
    {
        object recorder = typeof(TelemetryOutput).GetField("_recorder", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(output)
            ?? throw new InvalidOperationException("no recorder");
        return (RecordingOptions)(recorder.GetType().GetField("options", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(recorder)
            ?? throw new InvalidOperationException("SessionRecorder.options was not found"));
    }

    static double Time(Action action) { var clock = Stopwatch.StartNew(); action(); return clock.Elapsed.TotalSeconds; }
    static long Mb(long bytes) => bytes / 1_000_000;

    // Peak managed heap and working set, sampled every 50 ms.
    sealed class PeakSampler
    {
        readonly Thread _thread;
        volatile bool _stop;
        public long Managed, WorkingSet;
        public PeakSampler()
        {
            _thread = new Thread(() =>
            {
                using var process = Process.GetCurrentProcess();
                while (!_stop) { Managed = Math.Max(Managed, GC.GetTotalMemory(false)); process.Refresh(); WorkingSet = Math.Max(WorkingSet, process.WorkingSet64); Thread.Sleep(50); }
            }) { IsBackground = true };
            _thread.Start();
        }
        public void Stop() { _stop = true; _thread.Join(); }
    }

    static void CopyTree(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        foreach (string directory in Directory.GetDirectories(source)) CopyTree(directory, Path.Combine(target, Path.GetFileName(directory)));
    }
    static string Read(string path) => File.Exists(path) ? File.ReadAllText(path) : "";
    static bool Throws(Action action) { try { action(); return false; } catch (Exception) { return true; } }
}
