using System.Security.Cryptography;
using System.Text.Json;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using WodenTripleScreenProbe.Core;

namespace WodenTripleScreenProbe;

/// <summary>Private-target experiment. It never changes the game's camera or display output.</summary>
[BepInPlugin(Id, "Woden Triple-Screen Probe", Version)]
public sealed class Plugin : BasePlugin
{
    internal const string Id = "dbce.woden-triplescreen-probe";
    internal const string Version = "0.0.1";
    private const string GameHash = "f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c";
    private Harmony? _patches;
    private ProbeRenderer? _renderer;

    public override void Load()
    {
        string settingsPath = Path.Combine(Paths.ConfigPath, "dbce.woden-triplescreen-probe.json");
        if (!File.Exists(settingsPath)) { Log.LogInfo("Triple-screen probe is off (no opt-in file)."); return; }
        try
        {
            using var settings = JsonDocument.Parse(File.ReadAllBytes(settingsPath));
            if (!settings.RootElement.TryGetProperty("enabled", out var enabled) ||
                enabled.ValueKind != JsonValueKind.True)
            { Log.LogInfo("Triple-screen probe is off."); return; }

            using var gameAssembly = File.OpenRead(Path.Combine(Paths.GameRootPath, "GameAssembly.dll"));
            using var sha = SHA256.Create();
            string hash = Convert.ToHexString(sha.ComputeHash(gameAssembly)).ToLowerInvariant();
            if (hash != GameHash) { Log.LogError("Triple-screen probe rejected unknown game build."); return; }

            string integration = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DBCE", "TripleScreen", "games", "super-woden-rally-edge");
            var layout = LayoutV1.Parse(File.ReadAllBytes(Path.Combine(integration, "desired-layout.json")));
            layout.RequireCurrentOwnerRig();
            _renderer = new ProbeRenderer(layout, integration, Log);
            ProbeHook.Owner = _renderer;
            _patches = new Harmony(Id);
            _patches.PatchAll(typeof(Plugin).Assembly);
            AddComponent<ProbeLifecycle>();
            Log.LogWarning("Triple-screen private-target probe enabled. No display or game camera is changed; do not submit ranked times.");
        }
        catch (Exception ex)
        {
            ProbeHook.Owner = null;
            _patches?.UnpatchSelf(); _patches = null;
            _renderer?.Stop(); _renderer = null;
            Log.LogError("Triple-screen probe stayed off: " + ex);
        }
    }

    public override bool Unload()
    {
        ProbeHook.Owner = null;
        _patches?.UnpatchSelf();
        _renderer?.Stop();
        return true;
    }
}

[HarmonyPatch(typeof(Car_Cam), nameof(Car_Cam.LateUpdate))]
[HarmonyAfter("dbce.wodenrallyedgewheel")]
internal static class ProbeHook
{
    internal static ProbeRenderer? Owner;
    private static void Postfix(Car_Cam __instance) => Owner?.Observe(__instance);
}

// Injected MonoBehaviour contains Unity messages only.
public sealed class ProbeLifecycle : MonoBehaviour
{
    public ProbeLifecycle(IntPtr pointer) : base(pointer) { }
    private void Update() => ProbeHook.Owner?.Tick();
    private void OnApplicationQuit() => ProbeHook.Owner?.Stop();
    private void OnDestroy() => ProbeHook.Owner?.Stop();
}
