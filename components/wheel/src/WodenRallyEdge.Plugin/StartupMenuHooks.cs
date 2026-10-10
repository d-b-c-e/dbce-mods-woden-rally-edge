using HarmonyLib;
using UnityEngine;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// These two verified consumers read legacy Input directly, bypassing both
// GamePadSystem and EventSystem. Keep the guard scoped to the startup screens.
[HarmonyPatch(typeof(DailyMessage), nameof(DailyMessage.Update))]
internal static class DailyMessageInputHook
{
    internal static bool Prefix(DailyMessage __instance)
    {
        if (!StartupMenuGuard.Before(false)) { StartupWheelControls.Reset(); return false; }
        return !StartupWheelControls.Daily(__instance);
    }
}
[HarmonyPatch(typeof(TitleScreenScript), nameof(TitleScreenScript.FixedUpdate))]
internal static class TitleScreenInputHook
{
    internal static bool Prefix(TitleScreenScript __instance)
    {
        if (!StartupMenuGuard.Before(true)) { StartupWheelControls.Reset(); return false; }
        return !StartupWheelControls.Title(__instance);
    }
}

// The startup notice/title bypass EventSystem, so the ordinary menu dispatcher
// has nowhere to send bound Confirm/Start. Use those same saved wheel bindings
// and the verified native menu transition. A fresh neutral interval is required
// per screen and after focus/capture/reader loss; a held press never skips both.
internal static class StartupWheelControls
{
    private static readonly InputReleaseGate Release = new();
    private static int _screen;
    internal static void Reset() { Release.Reset(); _screen = 0; }
    private static bool Press(int screen, bool eligible)
    {
        if (_screen != screen) { Release.Reset(); _screen = screen; }
        var wheel = Runtime.Wheel; var hub = Runtime.Devices;
        if (!eligible || !Runtime.Focused || Panel.Open || MenuOwnership.Closing || wheel == null || hub == null || wheel.Capturing)
        { Release.Reset(); return false; }
        bool bound = false;
        foreach (string action in new[] { "Confirm", "Pause", "Back" })
        {
            if (!wheel.Bindings.Buttons.TryGetValue(action, out var b)) continue;
            var matches = hub.Devices.Where(d => d.Info.InstanceGuid == b.DeviceGuid).ToArray();
            if (!b.Valid || matches.Length != 1 || !matches[0].Ok) { Release.Reset(); return false; }
            if (action != "Back") bound = true;
        }
        if (!bound || wheel.Button("Back", false) || Input.GetKey(KeyCode.Escape)) { Release.Reset(); return false; }
        bool held = wheel.Button("Confirm", false) || wheel.Button("Pause", false);
        if (!held) { Release.Observe(true, false, Runtime.Clock.Elapsed.TotalSeconds); return false; }
        bool ready = Release.Ready; Release.Reset(); return ready;
    }
    internal static bool Daily(DailyMessage message)
    {
        try
        {
            if (!Press(message.GetInstanceID(), message.field_Private_Boolean_0 && !MenuCameraScript.Exiting)) return false;
            if (UnityEngine.Object.FindObjectOfType<MenuCameraScript>() == null) return false;
            message.field_Private_Boolean_0 = false;
            try { MenuCameraScript.LoadScene("Title Screen", false, false); }
            catch { if (!MenuCameraScript.Exiting) message.field_Private_Boolean_0 = true; throw; }
            Runtime.Log.LogInfo("Bound wheel Confirm/Start: native daily-message transition");
            return true;
        }
        catch (Exception ex) { Reset(); Runtime.Log.LogWarning("Startup wheel input unavailable: " + ex.Message); return false; }
    }
    internal static bool Title(TitleScreenScript title)
    {
        try
        {
            bool eligible = !title.Starting && title.MyControls != null && !title.MyControls.ButtonB && title.SceneToLoad == "MapScreen" && !MenuCameraScript.Exiting;
            if (!Press(title.GetInstanceID(), eligible)) return false;
            if (UnityEngine.Object.FindObjectOfType<MenuCameraScript>() == null) return false;
            title.Starting = true;
            try { MenuCameraScript.LoadScene(title.SceneToLoad, true, false); }
            catch { if (!MenuCameraScript.Exiting) title.Starting = false; throw; }
            Runtime.Log.LogInfo("Bound wheel Confirm/Start: native title transition");
            return true;
        }
        catch (Exception ex) { Reset(); Runtime.Log.LogWarning("Startup wheel input unavailable: " + ex.Message); return false; }
    }
}

internal static class StartupMenuGuard
{
    private static long _dailyCalls, _dailyBlocked, _titleCalls, _titleBlocked;
    internal static string Status => $"startup daily={_dailyBlocked}/{_dailyCalls}, title={_titleBlocked}/{_titleCalls} blocked/calls";
    internal static bool Before(bool title)
    {
        if (title) _titleCalls++; else _dailyCalls++;
        bool known = true;
        try
        {
            if (!Panel.Open && Runtime.Focused)
            {
                InputPolling.OncePerFrame();
                known = Panel.ObserveSettingsOpening();
            }
        }
        catch (Exception ex) { known = false; MenuOwnership.Failed("Startup settings read", ex); }
        if (known && !MenuOwnership.Blocking) return true;
        long blocked = title ? ++_titleBlocked : ++_dailyBlocked;
        if (blocked == 1) Runtime.Log.LogInfo($"Startup input guard active: {(title ? "TitleScreenScript.FixedUpdate" : "DailyMessage.Update")}; settingsOpen={Panel.Open}, inputKnown={known}");
        return false;
    }
}
