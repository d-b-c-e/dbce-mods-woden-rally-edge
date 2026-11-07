using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using BepInEx;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

/// <summary>
/// The only way product code reaches the developer tooling (recording, stage playback, native observation, course
/// dumps). Each entry point checks <see cref="InternalTools.Enabled"/> and then calls an out-of-line body, so with the
/// tools off no method that mentions a <c>Dbce.Wheel.Recording</c> or <c>Dbce.Wheel.Playback</c> type is ever compiled
/// and neither DLL is loaded. This class must not hold fields of those types or of the classes behind it.
/// </summary>
internal static class DevSession
{
    private static bool On => InternalTools.Enabled;

    // ---- launch, outputs and stop ------------------------------------------------------------------------------

    /// <summary>Before outputs: consume a requested diagnostic launch and initialize the stage and course tools.</summary>
    internal static void Start(string directory) { if (On) Dev.Start(directory); }
    /// <summary>The recording path for the first output, or null.</summary>
    internal static string? RecordingPath() => On ? Dev.RecordingPath() : null;
    internal static TelemetryOutput CreateOutput(OutputOptions options) =>
        On ? Dev.CreateOutput(options) : new TelemetryOutput(options, Runtime.SessionId, "", Plugin.Version);
    /// <summary>After outputs: arm a requested recording's control and full session.</summary>
    internal static void AfterOutputs() { if (On) Dev.AfterOutputs(); }
    internal static void BeginNativeObservation() { if (On) Dev.BeginNativeObservation(); }
    internal static void ConfirmPatches(string harmonyId) { if (On) Dev.ConfirmPatches(harmonyId); }
    internal static void Update(double now) { if (On) Dev.Update(now); }
    internal static void StageGui() { if (On) Dev.StageGui(); }
    internal static void RecordingGui() { if (On) Dev.RecordingGui(); }
    internal static void Stop() { if (On) Dev.Stop(); }
    internal static void BeforeShutdown() { if (On) Dev.BeforeShutdown(); }
    internal static void AfterOutputDisposed() { if (On) Dev.AfterOutputDisposed(); }

    // ---- per car ---------------------------------------------------------------------------------------------

    /// <summary>The MainCar.FixedUpdate prefix result: false while a stage replay drives the car.</summary>
    internal static bool BeforeCar(MainCar car) => !On || Dev.BeforeCar(car);
    internal static void Observe(TelemetrySample sample) { if (On) Dev.Observe(sample); }
    internal static void ObserveCar(MainCar car, TelemetrySample sample) { if (On) Dev.ObserveCar(car, sample); }
    internal static void Camera(Car_Cam camera) { if (On) Dev.Camera(camera); }

    // ---- state product code consults -------------------------------------------------------------------------

    internal static bool OutputMuted => On && Dev.OutputMuted();
    internal static bool Playing => On && Dev.Playing();
    internal static bool BackgroundReplay => On && Dev.BackgroundReplay();
    internal static bool Owns(MainCar car) => On && Dev.Owns(car);
    /// <summary>A stage tool or replay owns this launch (the native gearbox leaves it alone).</summary>
    internal static bool Tooling => On && Dev.Tooling();
    /// <summary>A muted stage or a confirmed test-save request owns this launch.</summary>
    internal static bool MutedOrTestLaunch => On && Dev.MutedOrTestLaunch();

    /// <summary>Development recordings stamp the toolkit Recording provenance when a dev install provides it.</summary>
    internal static string RecordingProvenance(string directory)
    {
        string path = Path.Combine(directory, "recording-provenance.json");
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    /// <summary>Harmony Prepare for the developer patches: they are not installed unless the tools are on.</summary>
    internal static bool PatchesEnabled() => On;

    /// <summary>The bodies. Only reached with the tools on.</summary>
    private static class Dev
    {
        private static DiagnosticLaunch? _recordLaunch;
        private static DiagnosticRecordingControl? _recordingControl;
        private static string? _recordingPath;
        private static Dictionary<string, string>? _recordingProperties;
        private static ForceAnalysisHistory? _recordingAnalysis;
        private static string _provenance = "";

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Start(string directory)
        {
            var log = Runtime.Log;
            _provenance = RecordingProvenance(directory);
            string requestPath = Path.Combine(Paths.ConfigPath, "woden-record-next-launch.json");
            Runtime.DiagnosticNoForce = File.Exists(requestPath);
            try { _recordLaunch = DiagnosticLaunch.Consume(requestPath, DateTimeOffset.UtcNow); Runtime.DiagnosticNoForce = _recordLaunch?.DisableForces == true; }
            catch (Exception ex) { log.LogError("Diagnostic launch rejected; force suppressed for this run: " + ex.Message); }
            if (_recordLaunch?.NativeObservationSource != null && Runtime.Settings.TelemetryEnabled)
                throw new IOException("Native observation requires network delivery disabled before startup");
            if (_recordLaunch != null) log.LogInfo($"Diagnostic launch {_recordLaunch.Id}; physical FFB suppressed={Runtime.DiagnosticNoForce}");
            StagePlayback.Initialize();
            CourseRouteDump.Initialize(log);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static string? RecordingPath()
        {
            if (Runtime.Output == null)
            {
                if (_recordLaunch != null) PrepareRequestedRecording();
                else if (Runtime.Settings.Record) _recordingPath = Path.Combine(Paths.BepInExRootPath, "WodenRecordings", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N") + ".jsonl");
            }
            return _recordingPath;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static TelemetryOutput CreateOutput(OutputOptions options) =>
            new(options, Runtime.SessionId, _provenance, Plugin.Version, _recordingProperties);

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void AfterOutputs()
        {
            var output = Runtime.Output;
            if (_recordLaunch == null || output == null || _recordingPath == null) return;
            try
            {
                using var process = Process.GetCurrentProcess();
                _recordingControl = new DiagnosticRecordingControl(Path.GetDirectoryName(_recordingPath)!,
                    _recordLaunch.Id, process.Id, process.StartTime.ToUniversalTime().Ticks, output);
                StagePlayback.ArmFullSession(Path.GetDirectoryName(_recordingPath)!, StagePlayback.SignalIdentity, _recordLaunch.EffectiveInputOrigin);
                _recordingControl.HoldTerminal = () => StagePlayback.FullPackagePending;
            }
            catch (Exception ex) { Runtime.Log.LogWarning("Ordinary recording control unavailable; F6 Stop remains available: " + ex.Message); }
        }

        private static void PrepareRequestedRecording()
        {
            var request = _recordLaunch!;
            var settings = Runtime.Settings;
            string root = Path.Combine(Paths.BepInExRootPath, "WodenRecordings");
            string directory = Path.Combine(root, "request-" + request.Id.ToString("N"));
            if (Directory.Exists(directory)) throw new IOException("Recording request directory already exists: " + directory);
            Directory.CreateDirectory(directory);
            _recordingPath = Path.Combine(directory, "source.jsonl");
            string configPath = Path.Combine(Paths.ConfigPath, "dbce.wodenrallyedgewheel.cfg");
            string bindingsPath = Path.Combine(Paths.ConfigPath, "wheel-bindings.json");
            string pluginPath = typeof(Plugin).Assembly.Location;
            string pluginHash = RecordingArtifacts.Sha256(pluginPath);
            string configHash = RecordingArtifacts.WriteForceConfig(Path.Combine(directory, "force-config.json"), settings.ForceOptions);
            string profileHash = RecordingArtifacts.WriteCaptureProfile(Path.Combine(directory, "capture-profile.json"), Runtime.GameAssemblyHash, pluginHash,
                Identity(configPath), Identity(bindingsPath), settings.WheelEnabled, settings.FfbEnabled, settings.FfbFollowSteering, settings.FfbGuid);
            string productVersion = FileVersionInfo.GetVersionInfo(pluginPath).ProductVersion ?? Plugin.Version;
            string sourceRevision = productVersion.Contains('+') ? productVersion[(productVersion.IndexOf('+') + 1)..] : "unknown";
            _recordingProperties = new()
            {
                ["recordingContract"] = "dbce.wheel.replay-case@1", ["capability"] = "signal-reprocess",
                ["requestId"] = request.Id.ToString(), ["caseId"] = request.EffectiveCaseId, ["inputOrigin"] = request.EffectiveInputOrigin,
                ["disableForces"] = request.DisableForces.ToString().ToLowerInvariant(), ["gameAssemblySha256"] = Runtime.GameAssemblyHash,
                ["pluginSha256"] = pluginHash, ["pluginProductVersion"] = productVersion, ["runtimeSource"] = sourceRevision,
                ["forceConfigSha256"] = configHash, ["captureProfileSha256"] = profileHash,
                ["analysisForceContract"] = ForceAnalysisHistory.Contract, ["analysisForce"] = ForceAnalysisHistory.Scope
            };
            _recordingAnalysis = new ForceAnalysisHistory();
            Runtime.Log.LogInfo($"Recording request {request.Id} maps to {_recordingPath}; case={request.EffectiveCaseId}, capability=signal-reprocess");
        }

        private static string Identity(string path)
        {
            if (File.Exists(path)) return RecordingArtifacts.Sha256(path);
            return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("missing:" + Path.GetFileName(path)))).ToLowerInvariant();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void BeginNativeObservation() => NativeObservation.Begin(_recordLaunch);
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ConfirmPatches(string harmonyId) => TestSaveGuard.Confirm(harmonyId);

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Update(double now)
        {
            StagePlayback.Update(now);
            NativeObservation.Tick(now);
            _recordingControl?.Tick();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void StageGui() => StagePlayback.OnGui();
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void RecordingGui() => RecordingStatusOverlay.Draw(_recordingControl);
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Stop() => StagePlayback.Stop();
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void BeforeShutdown() => CourseRouteDump.Stop();
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void AfterOutputDisposed()
        {
            StagePlayback.FinishFullOnExit();
            _recordingControl?.Flush();
            _recordingAnalysis = null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool BeforeCar(MainCar car) => StagePlayback.BeforeCar(car);

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Observe(TelemetrySample sample)
        {
            ObserveForceAnalysis(sample);
            StagePlayback.Observe(sample);
        }

        private static void ObserveForceAnalysis(TelemetrySample sample)
        {
            var output = Runtime.Output;
            if (_recordingAnalysis == null || output?.RecordingActive != true) return;
            try { _recordingAnalysis.Record(sample, Runtime.Settings.ForceOptions); }
            catch (Exception ex)
            {
                // Analysis failure invalidates this recording, never the actual output lifecycle.
                _recordingAnalysis = null; output.FailRecording(sample.ElapsedSeconds, "force analysis: " + ex.Message);
                Runtime.Log.LogError("Independent force analysis stopped recording: " + ex.Message);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ObserveCar(MainCar car, TelemetrySample sample)
        {
            NativeObservation.Observe(car, sample);
            CourseRouteDump.Observe(car, sample);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Camera(Car_Cam camera) => StagePlayback.Camera(camera);
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool OutputMuted() => StagePlayback.OutputMuted;
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool Playing() => StagePlayback.Playing;
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool BackgroundReplay() => StagePlayback.BackgroundReplay;
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool Owns(MainCar car) => StagePlayback.Owns(car);
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool Tooling() => StagePlayback.OutputMuted || StagePlayback.Playing || StageRunLifecycle.ReplayPath != null || StageStartup.Validated;
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool MutedOrTestLaunch() => StagePlayback.OutputMuted || TestSaveGuard.Active;
    }
}
