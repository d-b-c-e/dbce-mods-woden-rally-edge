using WodenRallyEdge;
using WodenRallyEdge.Core;

internal static class StageReplayChecks
{
    internal static void Run(Action<bool, string> check)
    {
        for (int bits = 0; bits < 32; bits++)
            check(StageReplayPolicy.BackgroundAllowed((bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0,
                (bits & 8) != 0, (bits & 16) != 0) == (bits == 31), "Background replay requires every authorization predicate: " + bits);
        var driving = new PlayerControlState(PlayerPhase.Racing, true, false, false, false, false, false, false, false);
        check(!StageReplayPolicy.Eligibility(driving, false).Driving, "Ordinary/recording unfocused driving stays blocked");
        check(StageReplayPolicy.Eligibility(driving, true).Driving, "Accepted background replay can drive");
        check(!driving.Focused, "Replay projection does not mutate the ordinary input/force state");
        foreach (var blocked in new[] { driving with { Selected = false }, driving with { PanelOpen = true },
            driving with { Paused = true }, driving with { Replay = true }, driving with { Respawning = true },
            driving with { PhotoMode = true }, driving with { Locked = true }, driving with { Phase = PlayerPhase.Countdown },
            driving with { Phase = PlayerPhase.Finished }, driving with { Phase = PlayerPhase.Destroyed }, driving with { Phase = PlayerPhase.Unavailable } })
            check(!StageReplayPolicy.Eligibility(blocked, true).Driving, "Background replay retains non-focus driving exclusion: " + blocked);
        check(StageReplayPolicy.Eligibility(driving, true).CameraAvailable, "Background replay retains camera eligibility");
        check(!StageReplayPolicy.Eligibility(driving with { PhotoMode = true }, true).CameraAvailable, "Photo mode blocks replay camera");
        check(!StageReplayPolicy.Eligibility(driving with { Paused = true }, true).CameraAvailable, "Pause blocks replay camera");
        check(StageReplayPolicy.Eligibility(driving with { Focused = true }, false).Driving, "Ordinary foreground driving unchanged");
    }
}
