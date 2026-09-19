using HarmonyLib;
using UnityEngine;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Build-guarded adaptation of MainCar.HandBrake(bool RELEASED). The native
// pressed branch applies rear brake torque only above 30 wheel rpm and writes
// its configured grip-loss amount. Preserve that branch and scale its result.
// Native acceleration cut remains binary; no engine/assist defaults are changed.
[HarmonyPatch(typeof(MainCar), nameof(MainCar.HandBrake))]
internal static class AnalogHandbrake
{
    private static double _nextError;
    private static void Prefix(MainCar __instance, bool __0, out HandbrakeLease? __state)
    {
        __state = null;
        var input = Runtime.Wheel;
        if (__0 || input?.HandbrakeCar == null || input.HandbrakeCar.Pointer != __instance.Pointer ||
            input.HandbrakeAmount <= 0 || input.HandbrakeAmount >= 1) return;
        try { __state = new HandbrakeLease(__instance, input.HandbrakeAmount); __state.Apply(); }
        catch (Exception ex)
        {
            __state?.Restore(false); __state = null;
            if (Runtime.Clock.Elapsed.TotalSeconds > _nextError) { Runtime.Log.LogWarning("Analog handbrake unavailable; stock button behavior retained: " + ex.Message); _nextError = Runtime.Clock.Elapsed.TotalSeconds + 5; }
        }
    }
    private static void Postfix(HandbrakeLease? __state) => __state?.Restore(true);
    private static Exception? Finalizer(Exception? __exception, HandbrakeLease? __state) { __state?.Restore(false); return __exception; }
}
