using HarmonyLib;

namespace WodenRallyEdge;

// These two verified consumers read legacy Input directly, bypassing both
// GamePadSystem and EventSystem. Keep the guard scoped to the startup screens.
[HarmonyPatch(typeof(DailyMessage), nameof(DailyMessage.Update))]
internal static class DailyMessageInputHook
{
    internal static bool Prefix() => StartupMenuGuard.Before(false);
}
[HarmonyPatch(typeof(TitleScreenScript), nameof(TitleScreenScript.FixedUpdate))]
internal static class TitleScreenInputHook
{
    internal static bool Prefix() => StartupMenuGuard.Before(true);
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
