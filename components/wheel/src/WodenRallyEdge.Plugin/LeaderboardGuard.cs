using HarmonyLib;

namespace WodenRallyEdge;

/// <summary>
/// STD-018: an assist the game does not offer never reaches online scores. Once the countdown
/// assist has slowed the single-player time limit in a race, that race's Steam leaderboard upload
/// (the game's <c>SteamLeaderBoard.UpdateScore</c>, its only Steam leaderboard upload) is refused. Lap/stage clocks are unchanged by the
/// assist, but it can let a run finish that the stock time limit would have ended.
/// </summary>
/// <remarks>
/// A new local car (next race or restart) clears the mark; a missing car (finish screen) keeps it,
/// so the upload at the end of an assisted race is still refused. Default-off assist: stock play
/// uploads as normal.
/// </remarks>
[HarmonyPatch(typeof(SteamLeaderBoard), nameof(SteamLeaderBoard.UpdateScore))]
internal static class LeaderboardGuard
{
    private static IntPtr _car;
    private static bool _assisted;
    internal static long Skipped { get; private set; }

    internal static string Status => _assisted
        ? "Online times: not uploaded (countdown assist used this race)"
        : "Online times: uploaded as normal";

    internal static void MarkAssisted() { Observe(); _assisted = true; }

    private static void Observe()
    {
        try
        {
            var car = Runtime.Local;
            var pointer = car == null ? IntPtr.Zero : car.Pointer;
            if (pointer != IntPtr.Zero && pointer != _car) { _car = pointer; _assisted = false; }
        }
        catch { } // keep the current mark if the car cannot be read
    }

    private static bool Prefix()
    {
        Observe();
        if (!_assisted) return true;
        Skipped++;
        Runtime.Log.LogInfo("Steam leaderboard upload skipped: the countdown assist was used in this race (STD-018)");
        return false;
    }
}
