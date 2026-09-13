using HarmonyLib;
using UnityEngine;

namespace WodenRallyEdge;

[HarmonyPatch(typeof(Car_Cam), nameof(Car_Cam.LateUpdate))]
internal static class MountedCamera
{
    private static Transform? _target;
    private static Vector3 _position;
    private static Quaternion _rotation;
    private static int _owner;
    private static void Prefix(Car_Cam __instance) { if (_owner == __instance.GetInstanceID()) Restore(); }
    private static void Postfix(Car_Cam __instance)
    {
        if (!Runtime.Settings.Bonnet) return;
        try
        {
            var car = __instance.Maincar_;
            if (car == null || !Runtime.Driving(car)) return;
            var camera = __instance.field_Private_Camera_0;
            if (camera == null || !camera.enabled || !camera.gameObject.activeInHierarchy) return;
            var settings = Runtime.Settings;
            if (!float.IsFinite(settings.CameraHeight) || !float.IsFinite(settings.CameraForward) || !float.IsFinite(settings.CameraPitch)) return;
            Restore();
            _owner = __instance.GetInstanceID();
            _target = camera.transform; _position = _target.position; _rotation = _target.rotation;
            _target.position = car.transform.TransformPoint(new Vector3(0, Math.Clamp(settings.CameraHeight, .1f, 3), Math.Clamp(settings.CameraForward, -2, 4)));
            _target.rotation = car.transform.rotation * Quaternion.Euler(Math.Clamp(settings.CameraPitch, -30, 30), 0, 0);
        }
        catch (Exception ex) { Restore(); Runtime.Log.LogWarning("Bonnet view unavailable: " + ex.Message); }
    }
    internal static void Restore()
    {
        var target = _target; _target = null; _owner = 0;
        if (target == null) return;
        try { target.position = _position; target.rotation = _rotation; }
        catch (Exception ex) { Runtime.Log.LogWarning("Camera restore target gone: " + ex.Message); }
    }
}
