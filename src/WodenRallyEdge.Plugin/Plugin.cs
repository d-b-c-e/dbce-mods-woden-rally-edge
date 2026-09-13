using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using WodenRallyEdge.Core;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

namespace WodenRallyEdge;

[BepInPlugin(Id, "Woden Rally Edge Wheel", Version)]
public sealed class Plugin : BasePlugin
{
    public const string Id = "dbce.wodenrallyedgewheel";
    public const string Version = "0.1.0";
    public const string SupportedGameHash = "f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c";
    private Harmony? _harmony;
    public override void Load()
    {
        Runtime.Log = Log;
        Runtime.Settings = new(Config);
        string path = Path.Combine(Paths.GameRootPath, "GameAssembly.dll");
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        string hash = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        Log.LogInfo($"Woden Wheel {Version}; GameAssembly SHA256={hash}");
        if (hash != SupportedGameHash) { Log.LogError("Unsupported game build. Hooks remain disabled; regenerate interop and review changes first."); return; }
        try
        {
            Runtime.Start(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!);
            _harmony = new Harmony(Id);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            AddComponent<Lifecycle>();
            Log.LogInfo("Hooks installed; waiting for local MainCar.FixedUpdate. FFB output is not implemented in this milestone.");
        }
        catch { Runtime.Stop(); _harmony?.UnpatchSelf(); throw; }
    }
    public override bool Unload() { Runtime.Stop(); _harmony?.UnpatchSelf(); return true; }
}

public sealed class Settings
{
    public readonly int Player, ForzaPort, DetailPort, DetailHz;
    public readonly bool Record, WheelEnabled, Bonnet;
    public readonly float CameraHeight, CameraForward, CameraPitch;
    public Settings(ConfigFile c)
    {
        T Get<T>(string group, string key, T value, string description) => c.Bind(group, key, value, description).Value;
        Player = Get("General", "PlayerIndex", 0, "Exact MainCar.PlayerIndex to select. See discovered-player log lines; no automatic fallback.");
        ForzaPort = Get("Telemetry", "ForzaPort", 8000, "Loopback Forza Horizon 5 UDP. 0 disables this output. Restart after config edits.");
        DetailPort = Get("Telemetry", "DetailPort", 8001, "Loopback detailed JSON UDP. 0 disables this output.");
        DetailHz = Get("Telemetry", "DetailHz", 20, "Detailed UDP cap, 1..60 Hz. Forza and recording follow sampled physics ticks.");
        Record = Get("Diagnostics", "RecordSession", false, "Opt-in numeric JSONL recording, bounded to 20 minutes / 64 MiB. Never records video.");
        WheelEnabled = Get("Wheel", "Enabled", false, "Experimental physical axis route. Requires wheel-bindings.json with exact GUID and calibrated axes. No force output.");
        Bonnet = Get("Camera", "BonnetEnabled", false, "Experimental car-mounted view; needs visual validation. Off restores stock updates.");
        CameraHeight = Get("Camera", "Height", .85f, "Car-local vertical offset in Unity units.");
        CameraForward = Get("Camera", "Forward", .75f, "Car-local forward offset in Unity units.");
        CameraPitch = Get("Camera", "PitchDegrees", 3f, "Downward view pitch.");
    }
}

// Only Unity messages go on injected components. Managed logic stays outside IL2CPP registration.
public sealed class Lifecycle : MonoBehaviour
{
    public Lifecycle(IntPtr pointer) : base(pointer) { }
    private void Awake() => DontDestroyOnLoad(gameObject);
    private void Update() => Runtime.Update();
    private void OnApplicationQuit() => Runtime.Stop();
    private void OnDestroy() => Runtime.Stop();
}

internal static class Runtime
{
    internal static ManualLogSource Log = null!;
    internal static Settings Settings = null!;
    internal static readonly Stopwatch Clock = Stopwatch.StartNew();
    internal static readonly string SessionId = Guid.NewGuid().ToString("N");
    internal static readonly MotionProcessor Motion = new();
    internal static WheelInput? Wheel;
    internal static TelemetryOutput? Output;
    internal static long Sequence, HookCalls, LocalTicks;
    internal static double LastLocal = -10;
    private static double _nextReport, _lastIdle;
    private static bool _stopped = true;
    private static readonly HashSet<int> SeenPlayers = new();
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    internal static bool Focused { get { GetWindowThreadProcessId(GetForegroundWindow(), out uint pid); return pid == Environment.ProcessId; } }

    internal static void Start(string directory)
    {
        _stopped = false;
        string provenance = File.ReadAllText(Path.Combine(directory, "recording-provenance.json"));
        string? recording = Settings.Record ? Path.Combine(Paths.BepInExRootPath, "WodenRecordings", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + SessionId + ".jsonl") : null;
        Output = new(new(Settings.ForzaPort, Settings.DetailPort, Settings.DetailHz, recording), SessionId, provenance);
        if (Settings.WheelEnabled)
        {
            try { Wheel = new(directory, Path.Combine(Paths.ConfigPath, "wheel-bindings.json")); }
            catch (Exception ex) { Log.LogError("Wheel route unavailable; telemetry remains enabled: " + ex.Message); }
        }
    }
    internal static bool Select(MainCar car)
    {
        if (_stopped || car == null || !car.IsPlayer) return false;
        if (SeenPlayers.Add(car.PlayerIndex)) Log.LogInfo($"Discovered player index={car.PlayerIndex}, instance={car.GetInstanceID()}, configured={Settings.Player}");
        return car.PlayerIndex == Settings.Player;
    }
    internal static bool Driving(MainCar car) => Select(car) && Focused && !Pause.Paused && car.Status == MainCar.CarStatus.RACE && !car.Replay && !car.Respawning && !car.locked &&
        (car.MyControls == null || car.MyControls.PauseScript == null || !car.MyControls.PauseScript.PhotomodeActive);
    internal static void Update()
    {
        if (_stopped) return;
        double now = Clock.Elapsed.TotalSeconds;
        if ((!Focused || Pause.Paused || now - LastLocal > .5) && now - _lastIdle > .1)
        {
            MountedCamera.Restore();
            var sample = new TelemetrySample { SessionId = SessionId, Sequence = Sequence++, ElapsedSeconds = now,
                State = Pause.Paused ? "paused" : !Focused ? "unfocused" : "no-local-car" };
            Motion.Process(sample, System.Numerics.Quaternion.Identity);
            Output?.Publish(sample);
            _lastIdle = now;
        }
        if (now > _nextReport)
        {
            Log.LogInfo($"hooks={HookCalls}, localTicks={LocalTicks}, age={now-LastLocal:F2}s, Forza={Output?.ForzaPackets}, overwritten={Output?.OverwrittenTicks}, errors={Output?.SendErrors}, recording={Output?.RecordingStatus}, recordDrops={Output?.RecordingDrops}; {Output?.LastError}");
            _nextReport = now + 10;
        }
    }
    internal static void Stop()
    {
        if (_stopped) return;
        _stopped = true;
        Wheel?.Dispose();
        MountedCamera.Restore();
        Output?.Dispose();
        Log.LogInfo($"Stopped; outputWorkerStopped={Output?.Stopped}, recording={Output?.RecordingStatus}, drops={Output?.RecordingDrops}");
    }
}

[HarmonyPatch(typeof(MainCar), nameof(MainCar.FixedUpdate))]
internal static class CarHook
{
    private static void Prefix(MainCar __instance, out InputLease? __state)
    {
        __state = null;
        Runtime.HookCalls++;
        try { if (Runtime.Driving(__instance)) __state = Runtime.Wheel?.Apply(__instance); }
        catch (Exception ex) { Runtime.Log.LogError("Wheel input disabled for tick: " + ex.Message); }
    }
    private static void Postfix(MainCar __instance, InputLease? __state)
    {
        try
        {
            if (!Runtime.Select(__instance)) return;
            Runtime.LocalTicks++;
            Runtime.LastLocal = Runtime.Clock.Elapsed.TotalSeconds;
            Runtime.Output?.Publish(GameSampler.Read(__instance, __state));
        }
        catch (Exception ex) { if (Runtime.LocalTicks % 100 == 1) Runtime.Log.LogError("Sample failed: " + ex); }
        finally { __state?.Restore(); }
    }
    private static Exception? Finalizer(Exception? __exception, InputLease? __state) { __state?.Restore(); return __exception; }
}
