using HarmonyLib;
using UnityEngine;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

[HarmonyPatch(typeof(CountDown), nameof(CountDown.Update))]
internal static class CountdownTimerAssist
{
    internal static string Status { get; private set; } = "Waiting for a race with a time limit";
    private static long _adjustedUpdates;
    private static double _lastObserved = -10, _nextError;
    private static float _timeLeft;
    private static void Prefix(CountDown __instance, out CountdownAdjustment? __state)
    {
        __state = null;
        try
        {
            var cfg = Runtime.Settings;
            if (!cfg.CountdownAssistEnabled) { Status = "Off — normal countdown speed"; return; }
            if (cfg.CountdownSpeed >= 100) { Status = "100% — normal countdown speed"; return; }
            if (!OwnedSinglePlayerTimer(__instance)) { Status = "Waiting for the single-player race countdown"; return; }
            _lastObserved = Runtime.Clock.Elapsed.TotalSeconds; _timeLeft = __instance.TimeLeft;
            if (!__instance.Active || CountDown.InfiniteTime || Pause.Paused) { Status = "Countdown paused or inactive"; return; }
            var adjustment = CountdownRate.Prepare(__instance.TimerControl, Time.time, __instance.TimeLeft, cfg.CountdownSpeed);
            if (!adjustment.HasValue) return;
            __state = adjustment;
            __instance.TimerControl = adjustment.Value.AdjustedAnchor;
            Status = $"Countdown running at {cfg.CountdownSpeed:F0}%";
            if (++_adjustedUpdates == 1) Runtime.Log.LogInfo($"Countdown assist active at {cfg.CountdownSpeed:F0}%; native expiry uses scaled countdown, lap/stage clocks unchanged");
        }
        catch (Exception ex) { Report(ex); }
    }
    private static bool OwnedSinglePlayerTimer(CountDown timer)
    {
        var car = Runtime.Local;
        if (car == null || !Runtime.Select(car) || car.Replay || car.Status != MainCar.CarStatus.RACE) return false;
        var race = car.field_Private_RaceConditions_0;
        return race != null && race.PlayerCarList?.Count == 1 && timer.GM != null && race.GM == timer.GM;
    }
    private static void Postfix(CountDown __instance, CountdownAdjustment? __state) => Complete(__instance, __state);
    private static Exception? Finalizer(CountDown __instance, Exception? __exception, CountdownAdjustment? __state)
    { Complete(__instance, __state); return __exception; }
    private static void Complete(CountDown timer, CountdownAdjustment? adjustment)
    {
        if (!adjustment.HasValue) return;
        try
        {
            // Native Update normally replaces this with Time.time. Restore only
            // an unconsumed anchor (early return/exception), never its new value.
            float anchor = timer.TimerControl;
            float restored = adjustment.Value.RestoreIfUnused(anchor);
            if (restored != anchor) timer.TimerControl = restored;
            _lastObserved = Runtime.Clock.Elapsed.TotalSeconds; _timeLeft = timer.TimeLeft;
        }
        catch (Exception ex) { Report(ex); }
    }
    private static void Report(Exception ex)
    {
        Status = "Timer assist unavailable: " + ex.Message;
        if (Runtime.Clock.Elapsed.TotalSeconds < _nextError) return;
        _nextError = Runtime.Clock.Elapsed.TotalSeconds + 5; Runtime.Log.LogWarning(Status);
    }
    internal static void Record(TelemetrySample sample)
    {
        sample.Add("assist.countdown.speedPercent", Runtime.Settings.CountdownAssistEnabled ? Runtime.Settings.CountdownSpeed : 100);
        sample.Add("assist.countdown.adjustedUpdates", _adjustedUpdates);
        if (Runtime.Clock.Elapsed.TotalSeconds - _lastObserved < .2) sample.Add("assist.countdown.timeLeft", _timeLeft);
    }
}
