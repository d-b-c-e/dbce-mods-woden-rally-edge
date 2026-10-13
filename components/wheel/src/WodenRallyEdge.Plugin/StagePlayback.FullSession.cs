using System.Globalization;
using System.Text.Json;
using Dbce.Wheel.Sessions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WodenRallyEdge;

internal static partial class StagePlayback
{
    // The ordinary launch's first driven stage, recorded with the stage tool's own pose and signal writers (DRIVE's
    // owner-capture pattern). Unlike StageSession.Record it never mutes outputs or resets force state, and a launch
    // that arms it never polls the supervised stage commands: the two cannot share the writers.
    private static OrdinaryFullSession? _full;
    private static readonly StageLaunchOwner Owner = new();
    private static double _fullTick = -1, _fullLastPose, _fullTerminalAt = double.NaN;
    private static Task? _fullPackage;
    private static string? _fullReported;
    internal static bool FullObserving => _full?.Observing == true;
    internal static StageSignalFiles.Identity SignalIdentity => new(Runtime.GameAssemblyHash, typeof(Plugin).Assembly.Location,
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!, "recording-provenance.json")), Plugin.Version, Runtime.SessionId);

    private static volatile bool _fullClosing;
    /// <summary>The ordinary status holds at Stopping until session.json or its incomplete marker exists (bounded;
    /// released at plugin close).</summary>
    internal static bool FullPackagePending => _full?.HoldStatus(_fullClosing, _fullTerminalAt, Runtime.Clock.Elapsed.TotalSeconds) == true;

    internal static void ArmFullSession(string captureDirectory, StageSignalFiles.Identity identity, string inputOrigin)
    {
        try
        {
            if (Owner.OrdinaryOwns) throw new InvalidOperationException("already armed in this launch");
            // The request's own statement (human, automated, or unknown when omitted); owner only for human input.
            var full = new OrdinaryFullSession(captureDirectory, "source.jsonl", identity, inputOrigin, Runtime.Clock.Elapsed.TotalSeconds);
            if (!Owner.TryClaimOrdinary(Session.Active || StageRunLifecycle.ReplayPath != null ||
                File.Exists(Path.Combine(ControlRoot, "request.txt"))))
                throw new InvalidOperationException("supervised stage control is present in this launch");
            _full = full;
            Runtime.Log.LogInfo("Ordinary full-session capture armed: the first driven stage, configured outputs retained; supervised stage commands ignored in this launch.");
        }
        catch (Exception ex) { _full = null; Runtime.Log.LogWarning("Ordinary full-session capture not armed: " + ex.Message); }
    }

    internal static void FullScene(string scene) => _full?.Scene(scene, scene is "LoadingScene" or "Loading Screen", Runtime.Clock.Elapsed.TotalSeconds);
    internal static void FullLoad(string from, string to, string method, Dictionary<string, object?> selection) =>
        _full?.Load(from, to, method, JsonSerializer.SerializeToNode(selection), Runtime.Clock.Elapsed.TotalSeconds);

    private static void FullBeforeCar(MainCar car)
    {
        var full = _full;
        if (full == null || !full.Observing || !Runtime.Select(car)) return;
        try
        {
            double time = Time.timeAsDouble;
            if (time == _fullTick) return;
            _fullTick = time;
            _car = car;
            double now = Runtime.Clock.Elapsed.TotalSeconds;
            if (full.StageOpen && Pause.Paused) { full.Pause(now); _fullLastPose = now; return; }
            if (!full.StageOpen)
            {
                if (!full.CanOpenStage || !Target.Ready) return;
                full.OpenStage(FullSelection(car), Target.Scenario, Time.fixedDeltaTime,
                    StageLaunchOwner.PhysicalOutput(OutputMuted, Runtime.DiagnosticNoForce, Runtime.Settings.FfbEnabled),
                    directory => StageCaptureContext.Write(directory, car), Runtime.Settings.ForceOptions, now);
                if (!full.StageOpen) return;
            }
            else if (!Target.Ready)
            {
                bool finished = car.Status == MainCar.CarStatus.END;
                full.Seal(finished ? "finished" : "driving eligibility ended", finished, now);
                return;
            }
            full.Pose(Target.Read(), Target.Scenario, Time.fixedDeltaTime, now);
            _fullLastPose = now;
        }
        catch (Exception ex) { full.Fail("prefix: " + ex.Message); }
    }

    private static FullSessionSelection FullSelection(MainCar car)
    {
        var stats = Progress.StatsData_ ?? throw new InvalidOperationException("Cannot read the stage's game mode");
        var race = car.field_Private_RaceConditions_0 ?? throw new InvalidOperationException("Cannot read the stage's race context");
        var round = race.MyGameround;
        string Named(string? value) => string.IsNullOrWhiteSpace(value) ? "unavailable" : value!;
        return new FullSessionSelection
        {
            Scene = SceneManager.GetActiveScene().name, Biome = Named(race.MyCompetition?.name), Track = Named(round?.Track?.name),
            Car = "car=" + car.CarId.ToString(CultureInfo.InvariantCulture), Mode = stats.Gamemode + "/" + stats.SubGamemode,
            Start = "route=" + stats.ArcadeRoute.ToString(CultureInfo.InvariantCulture) + ";round=" + stats.ArcadeRound.ToString(CultureInfo.InvariantCulture),
            FixedDeltaTime = Time.fixedDeltaTime,
        };
    }

    private static void FullUpdate(double now)
    {
        var full = _full;
        if (full == null) return;
        try
        {
            if (full.Observing)
            {
                if (Owner.OrdinaryConflict(Session.Active)) full.Fail("a supervised stage session became active during the ordinary capture");
                else if (full.StageOpen)
                {
                    // A timeScale-0 pause never reaches FixedUpdate.
                    if (Pause.Paused) { full.Pause(now); _fullLastPose = now; }
                    else if (now - _fullLastPose > 30) full.Seal("no physics callback for 30 seconds", false, now);
                }
                if (full.Observing && Runtime.Output?.RecordingActive != true) full.Stop(now);
            }
            if (full.Stopped && !full.Terminal && _fullPackage == null && TerminalSource(Runtime.Output?.RecordingStatus))
            {
                _fullTerminalAt = now;
                _fullPackage = Task.Run(full.Package);
            }
            Report(full);
        }
        catch (Exception ex) { full.Fail("update: " + ex.Message); }
    }

    /// <summary>Plugin stop, after the ordinary recorder closed. Never parses or hashes a source on this thread: within
    /// 10 s it waits only for the detached stage seal or a commit already begun, otherwise it hands the package to the
    /// companion's finalizer (full-session-pending.json).</summary>
    internal static void FinishFullOnExit()
    {
        var full = _full;
        if (full == null) return;
        try
        {
            full.Stop(Runtime.Clock.Elapsed.TotalSeconds);
            if (!full.Terminal && full.Failure == null && TerminalSource(Runtime.Output?.RecordingStatus))
                full.CloseForExit(TimeSpan.FromSeconds(10), _fullPackage);
            else if (!full.Terminal) full.Abandon("Game closed before the ordinary signal file was closed; the original capture files are retained.");
            Report(full);
        }
        catch (Exception ex) { Runtime.Log.LogWarning("Ordinary full-session close: " + ex.Message); }
        finally { _fullClosing = true; } // the final status Flush reports the recorder's real state
    }

    private static bool TerminalSource(string? status) => status is "Completed" or "LimitReached" or "Faulted";
    private static void Report(OrdinaryFullSession full)
    {
        string? state = full.Outcome ?? (full.Failure != null ? "failed" : null);
        if (state == null || state == _fullReported) return;
        _fullReported = state;
        if (state == "package") Runtime.Log.LogInfo($"Ordinary full-session package sealed: {full.Poses} poses; playback not admitted yet | {full.Root}");
        else if (state == "pending") Runtime.Log.LogInfo($"Ordinary full-session package handed to the companion's finalizer: {full.Poses} poses | {full.Root}");
        else Runtime.Log.LogWarning("Ordinary full-session capture incomplete: " + (full.Failure ?? "see " + OrdinaryFullSession.IncompleteFile) + " | " + full.Root);
    }
}
