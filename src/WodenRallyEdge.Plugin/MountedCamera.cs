using HarmonyLib;
using UnityEngine;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

[HarmonyPatch(typeof(Car_Cam), nameof(Car_Cam.LateUpdate))]
internal static class MountedCamera
{
    internal static readonly CameraCycle Cycle = new();
    internal static string Status { get; private set; } = "Waiting for player camera";
    private static Car_Cam? _cameraOwner;
    private static Transform? _target;
    private static Vector3 _position, _appliedLocalPosition;
    private static Quaternion _rotation, _appliedLocalRotation;
    private static int _parent;
    private static bool _foreignWriter, _playerOwned;
    private static double _observedAt = -10;
    internal static bool PlayerOwned => _playerOwned && !_foreignWriter && Runtime.Clock.Elapsed.TotalSeconds - _observedAt < .2;
    private static bool Selected(Car_Cam camera) => camera.Maincar_ != null && Runtime.Select(camera.Maincar_);
    private static bool NormalMode(Car_Cam camera) => camera.Mode is Car_Cam.Cam_Mode.Chase or Car_Cam.Cam_Mode.IsoMetric;
    private static bool Observe(Car_Cam camera)
    {
        if (!Selected(camera)) return false;
        _observedAt = Runtime.Clock.Elapsed.TotalSeconds;
        if (_cameraOwner == null || _cameraOwner.GetInstanceID() != camera.GetInstanceID())
        { Restore(); Cycle.Handoff(); _foreignWriter = false; _cameraOwner = camera; }
        var output = camera.field_Private_Camera_0;
        bool active = camera.enabled && camera.gameObject.activeInHierarchy && output != null && output.enabled && output.gameObject.activeInHierarchy;
        if (!NormalMode(camera) || !active || camera.Maincar_.Replay || camera.Maincar_.Status is MainCar.CarStatus.END or MainCar.CarStatus.DESTROYED)
        {
            Handoff("Game camera: " + camera.Mode, false); _playerOwned = false; return false;
        }
        bool presetChanged = Cycle.View != MountedView.Stock && camera.PresetIndex != Cycle.ExpectedStockPreset;
        if (Cycle.Reconcile(camera.PresetIndex, Runtime.Settings.Bonnet, Runtime.Settings.Bumper)) Handoff("Mounted camera released", presetChanged);
        _playerOwned = Runtime.Driving(camera.Maincar_) && !camera.Changing && !_foreignWriter;
        if (!_playerOwned) { Restore(); Runtime.Force.Suspend("Camera handoff / " + camera.Mode); }
        Status = _foreignWriter ? "Game controls camera; press Camera to return to the normal cycle" : !Runtime.Driving(camera.Maincar_) ?
            "Game controls camera outside driving" : camera.Changing ? "Game camera transition" : Cycle.View.ToString();
        return _playerOwned;
    }
    private static void Prefix(Car_Cam __instance)
    {
        if (!Selected(__instance)) return;
        if (_target != null)
        {
            int parent = _target.parent == null ? 0 : _target.parent.GetInstanceID();
            var q = _target.localRotation; var applied = _appliedLocalRotation;
            float dot = Math.Abs(q.x * applied.x + q.y * applied.y + q.z * applied.z + q.w * applied.w);
            bool moved = parent != _parent || (_target.localPosition - _appliedLocalPosition).sqrMagnitude > .0001f || !float.IsFinite(dot) || dot < .9999996f;
            if (moved) Handoff("Another game script took the camera", true);
            else Restore();
        }
        Observe(__instance);
    }
    private static void Postfix(Car_Cam __instance)
    {
        try
        {
            if (!Observe(__instance)) return;
            bool rear = Runtime.Wheel?.Button("Rear view", false) == true;
            if (Cycle.View == MountedView.Stock && !rear) return;
            var camera = __instance.field_Private_Camera_0;
            var car = __instance.Maincar_; var cfg = Runtime.Settings;
            bool bumper = Cycle.View == MountedView.Bumper;
            float height = bumper ? cfg.BumperHeight : cfg.CameraHeight, forward = bumper ? cfg.BumperForward : cfg.CameraForward, pitch = bumper ? cfg.BumperPitch : cfg.CameraPitch;
            _target = camera.transform; _position = _target.position; _rotation = _target.rotation;
            if (Cycle.View != MountedView.Stock)
            {
                _target.position = car.transform.TransformPoint(new Vector3(0, height, forward));
                _target.rotation = car.transform.rotation * Quaternion.Euler(pitch, 0, 0);
            }
            if (rear)
            {
                var local = car.transform.InverseTransformPoint(_target.position); var carRotation = car.transform.rotation;
                var inverseCar = new Quaternion(-carRotation.x, -carRotation.y, -carRotation.z, carRotation.w);
                _target.position = car.transform.TransformPoint(new Vector3(-local.x, local.y, -local.z));
                _target.rotation = carRotation * Quaternion.Euler(0, 180, 0) * inverseCar * _target.rotation;
            }
            _appliedLocalPosition = _target.localPosition; _appliedLocalRotation = _target.localRotation;
            _parent = _target.parent == null ? 0 : _target.parent.GetInstanceID();
        }
        catch (Exception ex) { Handoff("Camera unavailable: " + ex.Message, true); Runtime.Log.LogWarning(Status); }
    }
    internal static void Handoff(string reason, bool foreign)
    {
        // A foreign writer already installed its own pose; do not overwrite it
        // with our previous stock snapshot on the way out.
        if (foreign) _target = null; else Restore();
        Cycle.Handoff(); _foreignWriter |= foreign; _playerOwned = false; Status = reason;
        Runtime.Force.Suspend(reason);
    }
    internal static bool BeforeChange(Car_Cam camera, out int previous)
    {
        previous = -1;
        if (!Selected(camera) || !NormalMode(camera) || !Runtime.Driving(camera.Maincar_)) return true;
        if (camera.CamChangeButtonIspressed) return true;
        _foreignWriter = false;
        Restore();
        if (Cycle.Advance(Runtime.Settings.Bonnet, Runtime.Settings.Bumper))
        { camera.CamChangeButtonIspressed = true; Status = Cycle.View.ToString(); Runtime.Log.LogInfo("Camera cycle: " + Status); return false; }
        if (camera.Mode == Car_Cam.Cam_Mode.IsoMetric)
        {
            Cycle.StockChanged(camera.PresetIndex, camera.PresetIndex, Runtime.Settings.Bonnet, Runtime.Settings.Bumper);
            camera.CamChangeButtonIspressed = true; Status = Cycle.View.ToString(); return false;
        }
        previous = camera.PresetIndex; return true;
    }
    internal static void AfterChange(Car_Cam camera, int previous)
    {
        if (previous < 0 || !Selected(camera) || !camera.CamChangeButtonIspressed) return;
        Cycle.StockChanged(previous, camera.PresetIndex, Runtime.Settings.Bonnet, Runtime.Settings.Bumper);
        Status = Cycle.View.ToString(); Runtime.Log.LogInfo($"Camera cycle: {Status}, stock preset {camera.PresetIndex}");
    }
    internal static void Restore()
    {
        var target = _target; _target = null;
        if (target == null) return;
        try { target.position = _position; target.rotation = _rotation; }
        catch (Exception ex) { Runtime.Log.LogWarning("Camera restore target gone: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(Car_Cam), nameof(Car_Cam.ChangeCamera))]
internal static class CameraCycleHook
{
    private static bool Prefix(Car_Cam __instance, out int __state) => MountedCamera.BeforeChange(__instance, out __state);
    private static void Postfix(Car_Cam __instance, int __state) => MountedCamera.AfterChange(__instance, __state);
}
