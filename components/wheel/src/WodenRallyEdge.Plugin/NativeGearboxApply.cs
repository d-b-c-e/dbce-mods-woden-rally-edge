using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine.SceneManagement;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// [Input] NativeGearbox at the game's own transmission choice, once per launch (NativeGearboxPreference). It looks only
// during the first main-menu visit (MapScreen, where StageStartup's own writes reach the car), with the value the
// launch started with: an F6 change applies at the next start. Leaving that menu before the record loads ends the
// attempt for this launch rather than changing the choice later in the session. Never throws into the frame loop.
internal static class NativeGearboxApply
{
    private const string MenuScene = "MapScreen";
    private static string? _setting;
    private static bool _done, _onMenu;
    internal static string Status { get; private set; } = "Keep: the game's own transmission choice";

    internal static void FrameTick()
    {
        if (_done) return;
        // Completed writes and the one in progress, so a failure reports what the record now holds (sana 96081).
        List<NativeGearboxPreference.Write>? completed = null;
        NativeGearboxPreference.Write? attempted = null;
        try
        {
            _setting ??= Runtime.Settings.NativeGearbox;
            if (NativeGearboxPreference.Parse(_setting) == NativeGearboxPreference.Choice.Keep) { Finish(NativeGearboxPreference.Decide(_setting, 0, false, null, null).Status, false); return; }
            if (SceneManager.GetActiveScene().name != MenuScene)
            {
                if (_onMenu) Finish("not applied: the first main menu closed before the game's saved choice loaded", true);
                return;
            }
            _onMenu = true;
            bool tooling = StagePlayback.OutputMuted || StagePlayback.Playing || StageRunLifecycle.ReplayPath != null || StageStartup.Validated;
            var stats = Progress.StatsData_;
            var plan = NativeGearboxPreference.Decide(_setting, Runtime.Settings.Player, tooling,
                stats?.Auto_Transmission?.ToArray(), stats?.Arcade_Auto_Transmission?.ToArray());
            if (plan.Outcome == NativeGearboxPreference.Outcome.Wait) { Status = plan.Status; return; }
            completed = new List<NativeGearboxPreference.Write>(plan.Writes.Count);
            {
                attempted = write;
                Il2CppStructArray<bool> target = write.Field == NativeGearboxPreference.AutoTransmission ? stats!.Auto_Transmission! : stats!.Arcade_Auto_Transmission!;
                target[write.Index] = write.After;
                bool readBack = target[write.Index];
                if (readBack != write.After)
                {
                    Finish(NativeGearboxPreference.FailedStatus(completed, write, "read back " + NativeGearboxPreference.Word(readBack)), true);
                    return;
                }
                completed.Add(write); attempted = null;
            }
            Finish(plan.Status + (plan.Writes.Count == 0 ? "" : " (" + string.Join(", ", plan.Writes.Select(NativeGearboxPreference.Describe)) + ")"),
                plan.Outcome == NativeGearboxPreference.Outcome.Refused);
        }
        catch (Exception ex)
        {
            // A setter or readback that threw leaves its element unknown; elements before it stay set.
            Finish(NativeGearboxPreference.FailedStatus(completed ?? new List<NativeGearboxPreference.Write>(), attempted, (attempted == null ? "" : "unknown after ") + ex.Message), true);
        }
    }
    private static void Finish(string status, bool warning)
    {
        _done = true; Status = status;
        if (warning) Runtime.Log.LogWarning("Native gearbox " + status);
        else if (NativeGearboxPreference.Parse(_setting) != NativeGearboxPreference.Choice.Keep) Runtime.Log.LogInfo("Native gearbox " + status);
    }
}
