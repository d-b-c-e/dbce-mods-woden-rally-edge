using HarmonyLib;
using UnityEngine;

namespace WodenRallyEdge;

/// <summary>
/// Body contacts of the local car for the crash cue (toolkit CrashDetector / CrashCue, art of rally's shape,
/// owner 2026-10-06). Observes before the game's handler; never changes the collision, physics or damage.
/// </summary>
[HarmonyPatch(typeof(MainImpacts), nameof(MainImpacts.OnCollisionEnter))]
internal static class CrashHook
{
    private static bool _faulted;

    private static void Prefix(MainImpacts __instance, Collision __0)
    {
        if (_faulted || __instance == null || __0 == null) return;
        try
        {
            var car = Runtime.Local;
            var mine = car == null ? null : car.field_Private_MainImpacts_0;
            if (mine == null || mine.Pointer != __instance.Pointer) return;
            var relative = __0.relativeVelocity;
            float strongest = -1f, vertical = 1f;
            var contacts = __0.contacts;              // this build strips contactCount/GetContact; called once per contact start
            int n = contacts == null ? 0 : Math.Min(contacts.Length, 8);
            for (int i = 0; i < n; i++)
            {
                var normal = contacts![i].m_Normal;      // the normal property is stripped; the struct field remains
                float length = normal.magnitude;
                if (!(length > .5f)) continue;
                float share = Math.Abs(normal.y) / length;
                float speed = Math.Abs(Vector3.Dot(relative, normal)) / length;
                if (share <= Dbce.Wheel.Ffb.CrashDetector.MaxVerticalShare && speed > strongest) { strongest = speed; vertical = share; }
            }
            if (strongest >= 0) Runtime.Force.CrashContact(Time.timeAsDouble, strongest, vertical);
        }
        catch (Exception ex) { _faulted = true; Runtime.Log.LogWarning("Crash observation disabled for this session: " + ex.Message); }
    }
}
