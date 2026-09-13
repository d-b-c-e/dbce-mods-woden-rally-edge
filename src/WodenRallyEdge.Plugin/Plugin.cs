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
    public const string Version = "0.2.2";
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
            Log.LogInfo("F6 opens settings and bindings; F8 stops FFB. Force output starts disarmed each launch. Waiting for local MainCar.FixedUpdate.");
        }
        catch { Runtime.Stop(); _harmony?.UnpatchSelf(); throw; }
    }
    public override bool Unload() { Runtime.Stop(); _harmony?.UnpatchSelf(); return true; }
}

// Only Unity messages go on injected components. Managed logic stays outside IL2CPP registration.
public sealed class Lifecycle : MonoBehaviour
{
    public Lifecycle(IntPtr pointer) : base(pointer) { }
    private void Awake() => DontDestroyOnLoad(gameObject);
    private void Update() => Runtime.Update();
    private void OnGUI() => Panel.Draw();
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
    internal static DeviceHub? Devices;
    internal static readonly ForceController Force = new();
    internal static MainCar? Local;
    internal static TelemetryOutput? Output;
    internal static long Sequence, HookCalls, LocalTicks;
    internal static double LastLocal = -10;
    private static double _nextReport, _lastIdle;
    private static bool _stopped = true;
    private static string _provenance = "";
    private static readonly HashSet<int> SeenPlayers = new();
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    internal static bool Focused { get { GetWindowThreadProcessId(GetForegroundWindow(), out uint pid); return pid == Environment.ProcessId; } }

    internal static void Start(string directory)
    {
        _stopped = false;
        _provenance = File.ReadAllText(Path.Combine(directory, "recording-provenance.json"));
        ApplyOutputs();
        Wheel = new(Path.Combine(Paths.ConfigPath, "wheel-bindings.json"));
        try { Devices = new(directory); }
        catch (Exception ex) { Log.LogError("Wheel route unavailable; telemetry remains enabled: " + ex.Message); }
    }
    internal static void ApplyOutputs()
    {
        Settings.Validate();
        if (Settings.ForzaPort != 0 && Settings.ForzaPort == Settings.DetailPort)
            throw new ArgumentException("Forza and detailed telemetry need different ports.");
        string? recording = Settings.Record ? Path.Combine(Paths.BepInExRootPath, "WodenRecordings", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N") + ".jsonl") : null;
        Output?.Dispose();
        if (Output?.Stopped == false) throw new InvalidOperationException("Previous output worker is still stopping; retry shortly.");
        Output = new(new(Settings.ForzaPort, Settings.DetailPort, Settings.DetailHz, recording), SessionId, _provenance);
        Settings.Save();
        Log.LogInfo("Telemetry outputs applied; capture=" + (recording ?? "disabled"));
    }
    internal static bool Select(MainCar car)
    {
        if (_stopped || car == null || !car.IsPlayer) return false;
        if (SeenPlayers.Add(car.PlayerIndex)) Log.LogInfo($"Discovered player index={car.PlayerIndex}, instance={car.GetInstanceID()}, configured={Settings.Player}");
        if (car.PlayerIndex != Settings.Player) return false;
        Local = car;
        return true;
    }
    internal static bool Driving(MainCar car) => Select(car) && Focused && !Panel.Open && !Pause.Paused && car.Status == MainCar.CarStatus.RACE && !car.Replay && !car.Respawning && !car.locked &&
        (car.MyControls == null || car.MyControls.PauseScript == null || !car.MyControls.PauseScript.PhotomodeActive);
    internal static void Update()
    {
        if (_stopped) return;
        double now = Clock.Elapsed.TotalSeconds;
        try
        {
            Devices?.Poll();
            Panel.Update();
            StockWheelOwner.Update();
            if (!Focused || Panel.Open || Pause.Paused || now - LastLocal > .15 || Local == null || !Driving(Local) || !MountedCamera.PlayerOwned)
                Force.Suspend(!Focused ? "Unfocused" : Panel.Open ? "Settings open" : Pause.Paused ? "Paused" : "Waiting for live driving samples");
            if (Panel.Open || Local == null || !Driving(Local)) Devices?.ClearPresses();
        }
        catch (Exception ex) { Force.Disarm("Runtime error"); if (now > _nextReport) Log.LogError("Input/UI update failed: " + ex); }
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
            Log.LogInfo($"hooks={HookCalls}, localTicks={LocalTicks}, age={now-LastLocal:F2}s, wheelTicks={Wheel?.AppliedTicks}, camera={MountedCamera.Status}, ffb={Force.Status}, writes={Force.Attempts}, failures={Force.Failures}, Forza={Output?.ForzaPackets}, overwritten={Output?.OverwrittenTicks}, errors={Output?.SendErrors}, recording={Output?.RecordingStatus}, recordDrops={Output?.RecordingDrops}; {Output?.LastError}");
            _nextReport = now + 10;
        }
    }
    internal static void Stop()
    {
        if (_stopped) return;
        _stopped = true;
        Force.Shutdown();
        Panel.Close(false);
        Wheel?.Cancel();
        Devices?.Dispose();
        StockWheelOwner.Release(false);
        MountedCamera.Restore();
        Output?.Dispose();
        Log.LogInfo($"Stopped; outputWorkerStopped={Output?.Stopped}, recording={Output?.RecordingStatus}, drops={Output?.RecordingDrops}");
    }
}

[HarmonyPatch(typeof(MainCar), nameof(MainCar.FixedUpdate))]
internal static class CarHook
{
    private static void Prefix() => Runtime.HookCalls++;
    private static void Postfix(MainCar __instance)
    {
        try
        {
            if (!Runtime.Select(__instance)) return;
            Runtime.LocalTicks++;
            Runtime.LastLocal = Runtime.Clock.Elapsed.TotalSeconds;
            var sample = GameSampler.Read(__instance, Runtime.Wheel?.LastFor(__instance));
            Runtime.Force.Tick(sample);
            Runtime.Output?.Publish(sample);
        }
        catch (Exception ex) { Runtime.Force.Suspend("Sample failed"); if (Runtime.LocalTicks % 100 == 1) Runtime.Log.LogError("Sample failed: " + ex); }
    }
}

[HarmonyPatch(typeof(Controls), nameof(Controls.FixedUpdate))]
internal static class ControlsHook
{
    private static void Prefix(Controls __instance, out InputLease? __state)
    {
        __state = null;
        try { __state = Runtime.Wheel?.Apply(__instance); }
        catch (Exception ex) { Runtime.Force.Suspend("Input failed"); Runtime.Log.LogError("Wheel action override: " + ex.Message); }
    }
    private static void Postfix(InputLease? __state) => __state?.Restore();
    private static Exception? Finalizer(Exception? __exception, InputLease? __state) { __state?.Restore(); return __exception; }
}
