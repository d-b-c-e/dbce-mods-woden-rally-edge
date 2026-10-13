using System.Numerics;
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
    const string Hash = "f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c";
    const double Dt = .02;
    static readonly ForceOptions Options = new(Model: 6);

    sealed class Fixture : IDisposable
    {
        public readonly string Temp, Root;
        public readonly TelemetryOutput Ordinary;
        public readonly OrdinaryFullSession Session;
        public double Now = 1, Physics = 250;
        int _sequence;
        public Fixture()
        {
            Temp = Path.Combine(Path.GetTempPath(), "woden-full-session-" + Guid.NewGuid().ToString("N"));
            Root = Path.Combine(Temp, "request-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Ordinary = new TelemetryOutput(new OutputOptions(ForzaPort: 0, DetailPort: 0, RecordingPath: Path.Combine(Root, "source.jsonl"), Enabled: false),
                "fixture", "{\"fixture\":true}", "0.2.14", new Dictionary<string, string> { ["gameAssemblySha256"] = Hash });
            Session = new OrdinaryFullSession(Root, "source.jsonl", Identity(), "diagnostic", 0);
        }
        public static StageSignalFiles.Identity Identity() => new(Hash, typeof(FullSessionChecks).Assembly.Location, "{\"fixture\":true}", "0.2.14", "fixture");
        public TelemetrySample Sample(string state = "driving")
        {
            var s = new TelemetrySample { SessionId = "fixture", Sequence = _sequence++, ElapsedSeconds = Now, SimulationSeconds = Physics, CarInstanceId = 1, State = state };
            s.Vector("motion.position.world", new Vector3(0, 0, (float)(Physics * 20))); s.Vector("motion.velocity.world", new Vector3(0, 0, 20));
            s.Add("motion.speed", 20); s.Add("motion.velocity.local.z", 20); s.Add("game.trueSpeed", 59 + (_sequence % 3));
            s.Add("camera.mode", 0); s.Add("camera.stockPreset", 2); s.Add("camera.mountedView", 0);
            s.Add("ffb.grip.reference", 10000); s.Add("ffb.grip.referenceKind", 2); s.Add("ffb.grip.carEpoch", 1);
            return s;
        }
        public static TrajectoryFrame Frame(double time) => new() { Time = time,
            Values = new float[] { 0, 0, (float)time, 0, 0, 0, 1, 0, 0, 20, 0, 0, 0, .1f, .5f, 0, 4000, 3 } };
        /// <summary>One physics tick as CarHook runs it. A paused tick has no pose; its postfix sample has no stage row.</summary>
        public void Tick(bool paused = false, bool pose = true, bool signal = true)
        {
            Physics += paused ? 0 : Dt; Now += Dt;
            if (pose && !paused) Session.Pose(Frame(Physics), "Kenya SS1|car=8", Dt, Now);
            else if (paused) Session.Pause(Now);
            var sample = Sample();
            if (signal) Session.Signal(sample, Options);
            Ordinary.Publish(sample);
        }
        public void Open(bool physicalOutput = false) => Session.OpenStage(Selection(), "Kenya SS1|car=8", Dt, physicalOutput,
            dir => File.WriteAllText(Path.Combine(dir, "stage-context.json"), "{\"schema\":\"woden.stage-context@1\",\"fixture\":true}\n"), Options, Now);
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
    static FullSessionSelection Selection() => new() { Scene = "Kenya SS1", Biome = "Arcade Route 1", Track = "Kenya SS1",
        Car = "car=8", Mode = "Arcade/Arcade", Start = "route=0;round=0", FixedDeltaTime = Dt };

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
        using (var f = new Fixture())
        {
            File.WriteAllText(f.Path_("session.json"), "{}");
            check(Throws(() => new OrdinaryFullSession(f.Root, "source.jsonl", Fixture.Identity(), "diagnostic", 0)), "existing package output refuses arming");
            check(Throws(() => new OrdinaryFullSession(f.Root, "source.jsonl", Fixture.Identity(), "player", 0)), "unknown origin refused");
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
        check(Status() == "status=Stopping", "past the bound, a committing package still holds while the game runs");
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
        var ordinary = new StageLaunchOwner();
        check(ordinary.TryClaimOrdinary(supervisedPresent: false) && ordinary.OrdinaryOwns, "a clean ordinary launch is claimed");
        check(!ordinary.MuteFor(true) && !ordinary.PollSupervised,
            "a later or stale stage request neither mutes the ordinary launch nor reaches the commands that reset force state");
        check(!ordinary.MuteFor(false), "no request, no mute");
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
