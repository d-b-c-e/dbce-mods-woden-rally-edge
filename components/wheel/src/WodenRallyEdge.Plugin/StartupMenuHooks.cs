using HarmonyLib;
using UnityEngine;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// These two verified consumers read legacy Input directly, bypassing both
// GamePadSystem and EventSystem. Keep the guard scoped to the startup screens.
[HarmonyPatch(typeof(DailyMessage), nameof(DailyMessage.Update))]
internal static class DailyMessageInputHook
{
    internal static bool Prefix(DailyMessage __instance, out StartupButtonLease? __state)
    {
        __state = null;
        if (!StartupMenuGuard.Before(false)) { StartupWheelControls.Reset(); return false; }
        __state = StartupWheelControls.Daily(__instance); return true;
    }
    internal static void Postfix(StartupButtonLease? __state) => __state?.Restore();
    internal static void Finalizer(StartupButtonLease? __state) => __state?.Restore();
}
[HarmonyPatch(typeof(TitleScreenScript), nameof(TitleScreenScript.FixedUpdate))]
internal static class TitleScreenInputHook
{
    internal static bool Prefix(TitleScreenScript __instance, out StartupButtonLease? __state)
    {
        __state = null;
        if (!StartupMenuGuard.Before(true)) { StartupWheelControls.Reset(); return false; }
        __state = StartupWheelControls.Title(__instance); return true;
    }
    internal static void Postfix(StartupButtonLease? __state) => __state?.Restore();
    internal static void Finalizer(StartupButtonLease? __state) => __state?.Restore();
}

// One original native callback owns the temporary Start value. Its animation,
// sound, scene policy and other side effects remain the game's own. Restore even
// when the native callback throws; do not carry a synthetic held flag to MapScreen.
internal sealed class StartupButtonLease
{
    private MenuControls? _controls;
    private readonly bool _previous;
    internal StartupButtonLease(MenuControls controls)
    {
        _previous = controls.ButtonStart; _controls = controls;
        try { controls.ButtonStart = true; }
        catch { Restore(); throw; }
    }
    internal void Restore()
    {
        if (_controls is not { } controls) return;
        try {
            controls.ButtonStart = _previous;
            bool readback = controls.ButtonStart;
            if (readback != _previous) throw new InvalidOperationException("Native Start readback differs");
            _controls = null;
            Runtime.Log.LogInfo("Bound wheel startup input restored: Start=" + readback);
        }
        catch (Exception ex) { Runtime.Log.LogWarning("Startup button restore failed: " + ex.Message); }
    }
}

// The startup notice/title bypass EventSystem, so the ordinary menu dispatcher
// has nowhere to send bound Confirm/Start. Use those same saved wheel bindings
// in a scoped native menu field. A fresh neutral interval is required
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
        bool Down(string action) => wheel.Bindings.Buttons.TryGetValue(action,out var b) && hub.Button(b,false,action);
        if (!bound || Down("Back") || Input.GetKey(KeyCode.Escape)) { Release.Reset(); return false; }
        bool held = Down("Confirm") || Down("Pause");
        if (!held) { Release.Observe(true, false, Runtime.Clock.Elapsed.TotalSeconds); return false; }
        bool ready = Release.Ready; Release.Reset(); return ready;
    }
    private static StartupButtonLease Begin(MenuControls controls, string screen)
    {
        var lease = new StartupButtonLease(controls);
        try {
            MenuNavigation.SuppressStartupConfirm();
            Runtime.Log.LogInfo("Bound wheel Confirm/Start: native " + screen + " input");
            return lease;
        } catch { lease.Restore(); throw; }
    }
    internal static StartupButtonLease? Daily(DailyMessage message)
    {
        try
        {
            var controls=message.MyControls;
            if (!Press(message.GetInstanceID(), controls != null && message.field_Private_Boolean_0 && !MenuCameraScript.Exiting)) return null;
            if (controls == null) return null;
            if (UnityEngine.Object.FindObjectOfType<MenuCameraScript>() == null) return null;
            return Begin(controls, "daily-message");
        }
        catch (Exception ex) { Reset(); Runtime.Log.LogWarning("Startup wheel input unavailable: " + ex.Message); return null; }
    }
    internal static StartupButtonLease? Title(TitleScreenScript title)
    {
        try
        {
            var controls=title.MyControls;
            bool eligible = !title.Starting && !title.field_Private_Boolean_0 && title.FrameCount < title.FramesToDemo && controls != null && !controls.ButtonB && title.SceneToLoad == "MapScreen" && !MenuCameraScript.Exiting;
            if (!Press(title.GetInstanceID(), eligible)) return null;
            if (controls == null) return null;
            if (UnityEngine.Object.FindObjectOfType<MenuCameraScript>() == null) return null;
            return Begin(controls, "title");
        }
        catch (Exception ex) { Reset(); Runtime.Log.LogWarning("Startup wheel input unavailable: " + ex.Message); return null; }
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
