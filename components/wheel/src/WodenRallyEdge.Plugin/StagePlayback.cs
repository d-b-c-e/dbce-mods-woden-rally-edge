using System.Globalization;
using System.Reflection;
using System.Text.Json;
using BepInEx;
using Dbce.Wheel.Playback;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Explicit developer commands only. This adapter shares the trajectory/lifecycle
// contract with other games; game-owned physics and channel semantics stay here.
internal static class StagePlayback
{
    internal static readonly string ControlRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dbce", "StagePlayback", "woden");
    private static readonly Adapter Target = new();
    private static readonly StageSession Session = new(Target);
    private static FileSessionControl? _control;
    private static MainCar? _car;
    private static TelemetryOutput? _signals;
    private static readonly ForceSignal AnalysisForce = new();
    private static readonly GripSignal AnalysisGrip = new();
    private static double _lastTick = -1;
    private static int _signalCount;
    private static int _unfocusedReplaySteps;
    internal static bool OutputMuted { get; private set; }
    internal static bool Playing => Session.Playing;
    internal static bool BackgroundReplay => StageReplayPolicy.BackgroundAllowed(Session.ReplayActive,
        StageRunLifecycle.ReplayPath != null, StageStartup.Validated, OutputMuted, StageStartup.BackgroundConfigured);
    internal static void Initialize()
    {
        try
        {
            if (File.Exists(Path.Combine(ControlRoot, "request.txt"))) Target.MuteOutputs();
            StageRunLifecycle.Initialize(ControlRoot);
            _control = new FileSessionControl(ControlRoot, Session);
            StageStartup.Initialize(StageRunLifecycle.ReplayPath);
        }
        catch (Exception ex) { Runtime.Log.LogWarning("Stage control unavailable: " + ex.Message); }
    }
    internal static void Update(double now)
    {
        try
        {
            if (_control == null) return;
            if (File.Exists(Path.Combine(ControlRoot, "request.txt"))) Target.MuteOutputs();
            _control.Poll(now);
            if (Session.Active && UnityEngine.Input.GetKeyDown(KeyCode.F12)) Session.Stop();
            StageRunLifecycle.Tick(Session.Active, Session.Status, _control.LastError, now);
            StageStartup.Tick(Session.Active, Session.ReplayActive, Session.Playing, now);
            StageVisualCapture.Tick(Session.Playing && OutputMuted, Session.Samples, now);
        }
        catch (Exception ex) { Session.Abort("stage control: " + ex.Message); }
    }
    internal static bool BeforeCar(MainCar car)
    {
        if (!Session.Active || !Runtime.Select(car)) return true;
        try
        {
            _car = car;
            double time = Time.timeAsDouble;
            if (time != _lastTick)
            {
                _lastTick = time;
                if (Session.Playing && !Runtime.Focused) _unfocusedReplaySteps++;
                Session.FixedStep(Runtime.Clock.Elapsed.TotalSeconds);
            }
            return !Session.Playing;
        }
        catch (Exception ex) { Session.Abort(ex.Message); return true; }
    }
    internal static void Observe(TelemetrySample sample)
    {
        if (!Session.Recording || _signals == null) return;
        // The real output gate resets its own filter while muted. Keep an explicit
        // independent model stream for normalization, never a fabricated native write.
        try
        {
            if (_signalCount != Session.Samples - 1) throw new IOException("Source sample does not match the next recorded pose");
            sample.Add("capture.trajectoryIndex", _signalCount);
            // The selected model (classic v3 or grip v4), independent of the muted output gate.
            var options = Runtime.Settings.ForceOptions;
            var analysis = options.Grip ? AnalysisGrip.Evaluate(sample, options, Runtime.Force?.GripReference) : AnalysisForce.Evaluate(sample, options);
            sample.Add("analysis.force.preview", analysis.Preview);
            sample.Add("analysis.force.valid", analysis.Valid ? 1 : 0);
            sample.Add("analysis.force.reset", options.Grip ? AnalysisGrip.ResetCount : AnalysisForce.ResetCount);
            sample.Add("analysis.force.modelVersion", options.Model);
            _signals.Publish(sample);
            _signalCount++;
        }
        catch (Exception ex) { Session.Abort("signal capture: " + ex.Message); }
    }
    internal static bool Owns(MainCar car) => Playing && _car != null && car.Pointer == _car.Pointer;
    internal static void Stop() => Session.Stop();
    internal static void Camera(Car_Cam camera)
    {
        if (!Playing || camera.Maincar_ == null || !Owns(camera.Maincar_)) return;
        try { StageStartup.ApplyCamera(camera, Session.Samples - 1); }
        catch (Exception ex) { Session.Abort("Recorded camera: " + ex.Message); }
    }
    internal static void OnGui()
    {
        if (!OutputMuted) return;
        string status = Session.Recording ? "RECORDING - drive normally" :
            Session.Playing ? "PLAYBACK - leave driving controls released" : StageRunLifecycle.Closing ? Session.Status : StageStartup.Status ?? Session.Status;
        if (Session.Recording || Session.Playing)
            status += " | " + (Session.Samples * Time.fixedDeltaTime).ToString("F1", CultureInfo.InvariantCulture) + " s";
        if (_control?.LastError is { } error) status = "Session command failed: " + error;
        if (StageRunLifecycle.Closing) status += " | Closing after save";
        StageStatusOverlay.Draw(status);
    }

    private sealed class Adapter : IStageAdapter
    {
        private Rigidbody? _body;
        private MainCar? _ownedCar;
        private bool _kinematic, _collisions, _acquired;
        private int _interpolation;
        private Vector3 _velocity, _angular;
        public string Game => "Super Woden Rally Edge";
        public string GameHash => Runtime.GameAssemblyHash;
        public string Scenario => _car == null ? "unavailable" : SceneManager.GetActiveScene().name + "|car=" + _car.CarId.ToString(CultureInfo.InvariantCulture);
        public double FixedDeltaTime => Time.fixedDeltaTime;
        public bool Ready => PlaybackAllowed && StageStartup.ValidateRace(_car!);
        // Reuse all normal driving exclusions, including photo mode, native
        // replay and the start/end lock. Only our own trajectory ownership is
        // ignored here; it remains a force/input exclusion everywhere else.
        public bool PlaybackAllowed => _car != null && StageReplayPolicy.Eligibility(
            Runtime.ControlState(_car, includeStageOwnership: false), BackgroundReplay).Driving &&
            (_ownedCar == null || _car.Pointer == _ownedCar.Pointer) &&
            _car.Rb != null && _car.field_Private_RaceConditions_0?.PlayerCarList?.Count == 1;
        public void MuteOutputs()
        {
            if (OutputMuted) return;
            OutputMuted = true;
            Runtime.DiagnosticNoForce = true;
            Runtime.Force.Suspend("Session playback: physical output suppressed");
            Runtime.Output?.ConfigureNetwork(Runtime.Output.ActiveOptions with { Enabled = false });
            Runtime.Log.LogInfo("Stage tooling latched physical force and network output off; original model and telemetry sampling remain active.");
        }
        public void StartSignals(string directory)
        {
            AnalysisForce.Reset(); AnalysisGrip.NewCar(); _signalCount = 0;
            StageCaptureContext.Write(directory, _car ?? throw new InvalidOperationException("Player unavailable"));
            RecordingArtifacts.WriteForceConfig(Path.Combine(directory, "force-config.json"), Runtime.Settings.ForceOptions);
            File.WriteAllText(Path.Combine(directory, "channels.json"), JsonSerializer.Serialize(TelemetrySchema.Channels));
            var properties = new Dictionary<string, string> { ["capability"] = "signal-reprocess", ["physicalOutput"] = "false", ["captureSource"] = "live-physics",
                ["gameAssemblySha256"] = GameHash, ["pluginSha256"] = RecordingArtifacts.Sha256(typeof(Plugin).Assembly.Location),
                ["forceConfigSha256"] = RecordingArtifacts.Sha256(Path.Combine(directory, "force-config.json")),
                ["analysisForce"] = "Independent ForceSignal@3 history; no delivery gates or device writes; actual ffb.* stream remains separate",
                ["posePhase"] = "MainCar.FixedUpdate.prefix; signals from postfix before Unity solve",
                ["alignment"] = "capture.trajectoryIndex is the zero-based trajectory row; sample.simulationSeconds is the absolute physics clock" };
            _signals = new TelemetryOutput(new OutputOptions(RecordingPath: Path.Combine(directory, "source.jsonl"), Enabled: false), Runtime.SessionId,
                File.ReadAllText(Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!, "recording-provenance.json")), Plugin.Version, properties,
                new Dbce.Wheel.Recording.RecordingOptions { QueueCapacity = 512, MaxDurationSeconds = 1801, MaxFileBytes = 128L * 1024 * 1024, MaxChannelsPerSample = 512 });
        }
        public string[] FinishSignals()
        {
            var signals = _signals; _signals = null;
            if (signals == null) return Array.Empty<string>();
            signals.Dispose();
            if (signals.RecordingDrops != 0 || signals.RecordingError != null || signals.RecordingStatus != "Completed" || _signalCount < 2 || _signalCount != Session.Samples)
                throw new IOException("Signal recording was not complete: " + signals.RecordingStatus + "; " + signals.RecordingError);
            return new[] { "source.jsonl", "force-config.json", "channels.json", "stage-context.json" };
        }
        public TrajectoryFrame Read()
        {
            var car = _car ?? throw new InvalidOperationException("Player car unavailable");
            var rb = car.Rb ?? throw new InvalidOperationException("Player body unavailable");
            var p = rb.position; var q = PlaybackBodyNative.Rotation(rb); var v = rb.linearVelocity; var w = rb.angularVelocity;
            return new TrajectoryFrame { Time = Time.timeAsDouble, Values = new[] { p.x, p.y, p.z, q.x, q.y, q.z, q.w, v.x, v.y, v.z,
                w.x, w.y, w.z, car.Steering_Wheel_Pos, car.Pedal_Acc_Value, car.Pedal_Brake_Value, car.Rpm, (float)car.MyGear } };
        }
        public void Acquire()
        {
            _unfocusedReplaySteps = 0;
            Runtime.Log.LogInfo("Stage replay acquired: focused=" + Runtime.Focused + "; backgroundAuthorized=" + BackgroundReplay);
            var car = _car ?? throw new InvalidOperationException("Player unavailable");
            _body = car.Rb;
            _ownedCar = car;
            _kinematic = PlaybackBodyNative.Kinematic(_body); _collisions = PlaybackBodyNative.Collisions(_body); _interpolation = PlaybackBodyNative.Interpolation(_body);
            _velocity = _body.linearVelocity; _angular = _body.angularVelocity;
            _acquired = true;
            _body.isKinematic = true; PlaybackBodyNative.Collisions(_body, false); PlaybackBodyNative.Interpolation(_body, 1);
        }
        public void Apply(TrajectoryFrame frame)
        {
            if (_body == null || _car == null) throw new InvalidOperationException("Playback body destroyed");
            var a = frame.Values;
            _velocity = new Vector3(a[7], a[8], a[9]); _angular = new Vector3(a[10], a[11], a[12]);
            _body.MovePosition(new Vector3(a[0], a[1], a[2])); _body.MoveRotation(new Quaternion(a[3], a[4], a[5], a[6]));
            _car.Steering_Wheel_Pos = a[13]; _car.Pedal_Acc_Value = a[14]; _car.Pedal_Brake_Value = a[15]; _car.Rpm = a[16]; _car.MyGear = (int)a[17];
            _car.TrueSpeed = (int)(_velocity.magnitude * 3.6f);
        }
        public void Release()
        {
            if (!_acquired) { _body = null; _ownedCar = null; return; }
            _acquired = false;
            var body = _body; _body = null; _ownedCar = null;
            CleanupActions.Run(() => { if (body != null) body.isKinematic = _kinematic; },
                () => { if (body != null) PlaybackBodyNative.Collisions(body, _collisions); },
                () => { if (body != null) PlaybackBodyNative.Interpolation(body, _interpolation); },
                () => { if (body != null && !_kinematic) body.linearVelocity = _velocity; },
                () => { if (body != null && !_kinematic) body.angularVelocity = _angular; });
            Runtime.Log.LogInfo("Stage replay released: unfocusedReplaySteps=" + _unfocusedReplaySteps);
        }
    }
}

[HarmonyPatch]
internal static class StagePlaybackResetGuards
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in new[] { "ChangeCarPosition", "RespawnFunc", "SnapToGround", "GetDamage", "StopCarEnd", "AutopilotEnd" })
            yield return AccessTools.Method(typeof(MainCar), name) ?? throw new MissingMethodException(name);
    }
    private static bool Prefix(MainCar __instance) => !StagePlayback.Owns(__instance);
}

// Developer stage sessions never submit results or progression. These guards
// stay latched after Stop, through the game's subsequent finish/menu callbacks.
[HarmonyPatch(typeof(SteamLeaderBoard), nameof(SteamLeaderBoard.UpdateScore))]
internal static class StageLeaderboardGuard { private static bool Prefix() => !StagePlayback.OutputMuted; }
[HarmonyPatch(typeof(Achievements), nameof(Achievements.Unlock))]
internal static class StageAchievementGuard { private static bool Prefix() => !StagePlayback.OutputMuted; }
[HarmonyPatch(typeof(RaceConditions), nameof(RaceConditions.Finish))]
internal static class StageFinishGuard { private static bool Prefix() => !StagePlayback.OutputMuted; }
[HarmonyPatch(typeof(SaveOnEnable), nameof(SaveOnEnable.OnEnable))]
internal static class StageSaveGuard { private static bool Prefix() => !StagePlayback.OutputMuted; }
