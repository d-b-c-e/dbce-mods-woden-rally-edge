using System.Runtime.InteropServices;
using BepInEx.Configuration;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace WodenRallyEdge;

/// <summary>
/// Adds the desktop's own size to the game's resolution list when it is missing, so a
/// Surround desktop (7680x1440) can be chosen in Options like any other resolution.
/// </summary>
/// <remarks>
/// The game's list is hardcoded and stops at 5120x1440; its saved setting is an index
/// into it, applied once at startup. Forcing the size from outside instead meant extra
/// swapchain rebuilds at 7680, which reset the GPU driver on 2026-10-04. With the entry in
/// the list the game's own single apply does the work, and at the desktop size it is a
/// no-op.
/// </remarks>
// Start fills ResList, then FirstResolutionSet applies the saved index.
[HarmonyPatch(typeof(ResolutionManager), nameof(ResolutionManager.FirstResolutionSet))]
internal static class DesktopResolution
{
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);

    private static ConfigEntry<bool> _enabled = null!;

    internal static void Bind(ConfigFile cfg) =>
        _enabled = cfg.Bind("Triple", "OfferDesktopResolution", true, "Add the desktop's size (7680x1440 under Surround) to the game's resolution list when it is missing.");

    private static void Prefix(ResolutionManager __instance)
    {
        try
        {
            if (!_enabled.Value) return;
            // Screen.currentResolution reports the window in FullScreenWindow; ask Windows.
            int width = GetSystemMetrics(0), height = GetSystemMetrics(1);
            var list = __instance.ResList;
            if (list == null || list.Length == 0 || width < 1280 || height < 720) return;
            // A saved index past the stock list was this entry on a previous desktop (Surround);
            // keep it valid on any desktop so FirstResolutionSet never indexes out of range.
            int saved = -1;
            try { saved = PlayerPrefs.GetInt("Resolution", -1); } catch { }
            bool present = false;
            for (int i = 0; i < list.Length; i++)
                if ((int)list[i].x == width && (int)list[i].y == height) present = true;
            if (present && saved < list.Length) return;

            var desktop = new Vector2(width, height);
            __instance.ResList = Grow(list, desktop);
            // FirstResolutionSet also indexes Progress.Resolutions, a copy of the same list.
            var progress = UnityEngine.Object.FindObjectOfType<Progress>();
            if (progress != null && progress.Resolutions != null && progress.Resolutions.Length == list.Length)
                progress.Resolutions = Grow(progress.Resolutions, desktop);
            Runtime.Log.LogInfo($"Resolution list: added desktop {width}x{height} as entry {list.Length}; progress list {(progress == null ? "absent" : progress.Resolutions?.Length.ToString() ?? "null")}");
        }
        catch (Exception ex) { Runtime.Log.LogWarning("Resolution list: " + ex.Message); }
    }

    private static Il2CppStructArray<Vector2> Grow(Il2CppStructArray<Vector2> list, Vector2 extra)
    {
        var grown = new Il2CppStructArray<Vector2>(list.Length + 1);
        for (int i = 0; i < list.Length; i++) grown[i] = list[i];
        grown[list.Length] = extra;
        return grown;
    }
}
