using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    private static Camera? _lens;
    private static Renderer? _hiddenBody;
    private static bool _bodyWasForcedOff, _bodyHideWarned;
    private static readonly CameraLens LensMemory = new();
    private static string _restoreStage = "";
    private static int _restoreOwner;
    private static float _fov, _nearClip, _appliedFov, _appliedNearClip;
    private static int _parent;
    private static bool _foreignWriter, _playerOwned;
    private static double _observedAt = -10;
    internal static bool PlayerOwned => _playerOwned && !_foreignWriter && Runtime.Clock.Elapsed.TotalSeconds - _observedAt < .2;
    internal static CameraSlot CurrentSlot => new(Cycle.View, _cameraOwner?.PresetIndex ?? -1, _cameraOwner?.Mode == Car_Cam.Cam_Mode.IsoMetric);
    internal static float NativeFov(Camera camera) => _lens != null && _lens.Pointer == camera.Pointer ? LensMemory.Peek(camera.fieldOfView) : camera.fieldOfView;
    internal static float? BaseFov(CameraSlot camera) => camera.Mount == MountedView.Stock ? null :
        camera.Mount == MountedView.Bumper ? CameraPose.Bumper.Fov : CameraPose.Bonnet.Fov;
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
        bool shown = Runtime.CameraShown(camera.Maincar_);
        if (!shown) _restoreOwner = 0;
        _playerOwned = shown && !camera.Changing && !_foreignWriter;
        if (!_playerOwned) { Restore(); Runtime.Force.Suspend("Camera handoff / " + camera.Mode); }
        if (_playerOwned && TripleView.FramingEnabled) RestoreLastCamera(camera);
        Status = _foreignWriter ? "Game controls camera; press Camera to return to the normal cycle" : !shown ?
            "Game controls camera outside countdown / driving" : camera.Changing ? "Game camera transition" : Cycle.View.ToString();
        return _playerOwned;
    }
    private static void Prefix(Car_Cam __instance)
    {
        if (!Selected(__instance)) return;
        // Withdraw the previous output before native camera smoothing reads it.
        // This also covers stock chase when only its FOV, not its transform, changed.
        if (_target == null) RestoreLens();
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
        StagePlayback.Camera(__instance);
    }
    internal static void RecordedView(Car_Cam camera, int mode, int preset, MountedView view)
    {
        if (camera.Chase_Presets == null || preset < 0 || preset >= camera.Chase_Presets.Length || mode is < 0 or > 1)
            throw new InvalidOperationException("Recorded camera preset is unavailable");
        if (view == MountedView.Bonnet && !Runtime.Settings.Bonnet || view == MountedView.Bumper && !Runtime.Settings.Bumper)
            throw new InvalidOperationException("Recorded mounted camera is disabled");
        if ((int)camera.Mode == mode && camera.PresetIndex == preset && Cycle.View == view) return;
        Restore(); _foreignWriter = false;
        camera.Mode = (Car_Cam.Cam_Mode)mode; camera.PresetIndex = preset;
        camera.InstantCameraChange(); // native 0x855740 reads this preset; it does not increment it
        Cycle.Handoff();
        if (view != MountedView.Stock)
        {
            Cycle.StockChanged(preset, preset, true, true);
            if (view == MountedView.Bumper) Cycle.Advance(true, true);
        }
        Runtime.Log.LogInfo($"Recorded camera: {view}, stock preset {preset}");
    }
    private static void Postfix(Car_Cam __instance)
    {
        try
        {
            if (!Observe(__instance)) return;
            bool rear = !Panel.Open && Runtime.Wheel?.Button("Rear view", false) == true;
            var camera = __instance.field_Private_Camera_0;
            var car = __instance.Maincar_; var cfg = Runtime.Settings;
            bool bumper = Cycle.View == MountedView.Bumper;
            if (bumper) HideBody(car);
            var pose = Pose(bumper);
            var slot = CurrentSlot;
            bool framing = TripleView.FramingEnabled;
            var offset = framing ? TripleView.PositionOffset(slot, LegacyOffset(slot)) : default;
            if (framing || Cycle.View != MountedView.Stock)
            {
                _lens = camera; _fov = camera.fieldOfView; _nearClip = camera.nearClipPlane;
                _appliedFov = framing ? TripleView.FramedFov(slot, _fov, BaseFov(slot)) : pose.Fov;
                _appliedNearClip = Cycle.View == MountedView.Stock ? _nearClip : .03f;
                LensMemory.Applied(_fov, _appliedFov);
                camera.fieldOfView = _appliedFov;
                if (Cycle.View != MountedView.Stock) CameraNative.NearClip(camera, _appliedNearClip);
            }
            if (Cycle.View == MountedView.Stock && !rear && offset == default) return;
            _target = camera.transform; _position = _target.position; _rotation = _target.rotation;
            if (Cycle.View != MountedView.Stock)
            {
                _target.position = car.transform.TransformPoint(new Vector3(pose.Side, pose.Height, pose.Forward));
                _target.rotation = car.transform.rotation * Quaternion.Euler(pose.Pitch, 0, 0);
            }
            else if (framing)
            {
                _target.position += car.transform.TransformPoint(new Vector3(offset.Side, offset.Height, offset.Forward)) - car.transform.position;
                _target.rotation = _target.rotation * Quaternion.Euler(offset.Pitch, 0, 0);
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
        if (foreign) { _target = null; RestoreLens(); RestoreBody(); } else Restore();
        Cycle.Handoff(); _foreignWriter |= foreign; _playerOwned = false; Status = reason;
        Runtime.Force.Suspend(reason);
    }
    internal static bool BeforeChange(Car_Cam camera, out int previous)
    {
        previous = -1;
        if (!Selected(camera) || !NormalMode(camera) || !Runtime.CameraAvailable(camera.Maincar_)) return true;
        if (camera.CamChangeButtonIspressed) return true;
        _foreignWriter = false;
        Restore();
        if (Cycle.Advance(Runtime.Settings.Bonnet, Runtime.Settings.Bumper))
        { camera.CamChangeButtonIspressed = true; Status = Cycle.View.ToString(); TripleView.RememberCamera(); Runtime.Log.LogInfo("Camera cycle: " + Status); return false; }
        if (camera.Mode == Car_Cam.Cam_Mode.IsoMetric)
        {
            Cycle.StockChanged(camera.PresetIndex, camera.PresetIndex, Runtime.Settings.Bonnet, Runtime.Settings.Bumper);
            camera.CamChangeButtonIspressed = true; Status = Cycle.View.ToString(); TripleView.RememberCamera(); return false;
        }
        previous = camera.PresetIndex; return true;
    }
    internal static void AfterChange(Car_Cam camera, int previous)
    {
        if (previous < 0 || !Selected(camera) || !camera.CamChangeButtonIspressed) return;
        Cycle.StockChanged(previous, camera.PresetIndex, Runtime.Settings.Bonnet, Runtime.Settings.Bumper);
        TripleView.RememberCamera();
        Status = Cycle.View.ToString(); Runtime.Log.LogInfo($"Camera cycle: {Status}, stock preset {camera.PresetIndex}");
    }
    internal static void Restore()
    {
        var target = _target; _target = null;
        RestoreLens(); RestoreBody();
        if (target == null) return;
        try { target.position = _position; target.rotation = _rotation; }
        catch (Exception ex) { Runtime.Log.LogWarning("Camera restore target gone: " + ex.Message); }
    }
    private static void RestoreLens()
    {
        var camera = _lens; _lens = null;
        if (camera == null) return;
        try
        {
            // Preserve a lens value changed by another game camera writer.
            camera.fieldOfView = LensMemory.Remove(camera.fieldOfView);
            if (Math.Abs(camera.nearClipPlane - _appliedNearClip) < .0001f) CameraNative.NearClip(camera, _nearClip);
        }
        catch (Exception ex) { Runtime.Log.LogWarning("Camera lens restore target gone: " + ex.Message); }
    }
    private static void HideBody(MainCar car)
    {
        try
        {
            var renderer = car.CarMesh?.GetComponent<Renderer>();
            if (renderer == null) return;
            _hiddenBody = renderer; _bodyWasForcedOff = renderer.forceRenderingOff;
            renderer.forceRenderingOff = true;
        }
        catch (Exception ex) { if (!_bodyHideWarned) { _bodyHideWarned = true; Runtime.Log.LogWarning("Bumper body visibility unavailable: " + ex.Message); } }
    }
    private static void RestoreBody()
    {
        var body = _hiddenBody; _hiddenBody = null;
        if (body == null) return;
        // Keep the native enabled state, layers, colliders, lights and effects
        // untouched. This lease owns only the body's rendering override.
        try { if (body.forceRenderingOff) body.forceRenderingOff = _bodyWasForcedOff; }
        catch (Exception ex) { Runtime.Log.LogWarning("Bumper body restore target gone: " + ex.Message); }
    }
    internal static CameraPose Pose(bool bumper)
    {
        if (!TripleView.FramingEnabled) return LegacyPose(bumper);
        var slot = new CameraSlot(bumper ? MountedView.Bumper : MountedView.Bonnet);
        var calculated = CalculatedPose(bumper);
        return TripleView.PositionOffset(slot, LegacyOffset(slot)).On(calculated) with
        { Fov = TripleView.FramedFov(slot, calculated.Fov, calculated.Fov) };
    }
    private static CameraPose CalculatedPose(bool bumper) => Bounds() is { } body
        ? bumper ? CameraPose.FitBumper(body.Lo, body.Hi, CameraPose.Bumper, CameraPose.BumperAheadDefault) : CameraPose.FitBonnet(body.Lo, body.Hi)
        : bumper ? CameraPose.Bumper : CameraPose.Bonnet;
    internal static CameraOffset LegacyOffset(CameraSlot camera) => camera.Mount == MountedView.Stock ? default :
        CameraOffset.Between(LegacyPose(camera.Mount == MountedView.Bumper), CalculatedPose(camera.Mount == MountedView.Bumper));
    private static CameraPose LegacyPose(bool bumper)
    {
        var cfg = Runtime.Settings;
        if (bumper)
            return Bounds() is { } b ? CameraPose.FitBumper(b.Lo, b.Hi, cfg.GetCameraPose(true), cfg.BumperAhead) : cfg.GetCameraPose(true);
        if (!cfg.CameraAutoFit) return cfg.GetCameraPose(false);
        return CalculatedPose(false);
    }

    /// <summary>Saves an adjusted pose; the bumper keeps its distance from the body front so it carries to other cars.</summary>
    internal static void SavePose(bool bumper, CameraPose pose)
    {
        var cfg = Runtime.Settings;
        cfg.SetCameraPose(bumper, pose);
        if (bumper && Bounds() is { } b) cfg.BumperAhead = CameraPose.BumperAhead(b.Hi, pose);
        if (!bumper) cfg.CameraAutoFit = false;
    }

    private static void RestoreLastCamera(Car_Cam camera)
    {
        string stage = SceneManager.GetActiveScene().name;
        int owner = camera.GetInstanceID();
        if (_restoreOwner == owner && _restoreStage == stage) return;
        string saved = TripleView.LastCamera;
        if (saved.Length == 0) { _restoreOwner = owner; _restoreStage = stage; return; }
        try
        {
            if (!CameraSlot.TryParse(saved, out var slot)) { _restoreOwner = owner; _restoreStage = stage; return; }
            MountedView view = slot.Mount;
            if (view == MountedView.Bonnet && !Runtime.Settings.Bonnet || view == MountedView.Bumper && !Runtime.Settings.Bumper) return;
            var mode = camera.Mode; int preset = camera.PresetIndex;
            if (view == MountedView.Stock)
            {
                if (slot.StockPreset >= 0) preset = slot.StockPreset;
                mode = slot.Isometric ? Car_Cam.Cam_Mode.IsoMetric : Car_Cam.Cam_Mode.Chase;
            }
            int available = camera.Chase_Presets?.Length ?? 0;
            if (slot.RestorePreset(preset, available) is not { } readyPreset) return;
            preset = readyPreset;
            _restoreOwner = owner; _restoreStage = stage;
            RecordedView(camera, (int)mode, preset, view);
            Runtime.Log.LogInfo("Restored saved camera at stage start: " + saved);
        }
        catch (Exception ex) { Runtime.Log.LogWarning("Saved camera unavailable: " + ex.Message); }
    }

    private static int _boundsCar;
    private static (System.Numerics.Vector3 Lo, System.Numerics.Vector3 Hi)? _bounds;

    /// <summary>The local car's body-mesh bounds in car space, measured once per car.</summary>
    private static (System.Numerics.Vector3 Lo, System.Numerics.Vector3 Hi)? Bounds()
    {
        var car = Runtime.Local;
        if (car == null) return null;
        if (_boundsCar == car.GetInstanceID() && _bounds.HasValue) return _bounds;
        bool first = _boundsCar != car.GetInstanceID();
        _boundsCar = car.GetInstanceID(); _bounds = null;
        try
        {
            var renderer = car.CarMesh;
            var mesh = renderer?.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) { if (first) Runtime.Log.LogWarning("Camera fit: waiting for the body mesh; temporary fallback offsets"); return null; }
            var bounds = mesh.bounds; var min = bounds.min; var max = bounds.max;
            var lo = new System.Numerics.Vector3(float.PositiveInfinity);
            var hi = new System.Numerics.Vector3(float.NegativeInfinity);
            for (int i = 0; i < 8; i++)
            {
                var local = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                var p = car.transform.InverseTransformPoint(renderer!.transform.TransformPoint(local));
                var v = new System.Numerics.Vector3(p.x, p.y, p.z);
                lo = System.Numerics.Vector3.Min(lo, v); hi = System.Numerics.Vector3.Max(hi, v);
            }
            _bounds = (lo, hi);
            Runtime.Log.LogInfo($"Camera fit: body front {hi.Z:0.###} m; bumper view {Runtime.Settings.BumperAhead:0.###} m ahead of it");
        }
        catch (Exception ex) { if (first) Runtime.Log.LogWarning("Camera fit unavailable; using fallback offsets: " + ex.Message); }
        return _bounds;
    }
}

[HarmonyPatch(typeof(Car_Cam), nameof(Car_Cam.ChangeCamera))]
internal static class CameraCycleHook
{
    private static bool Prefix(Car_Cam __instance, out int __state) => MountedCamera.BeforeChange(__instance, out __state);
    private static void Postfix(Car_Cam __instance, int __state) => MountedCamera.AfterChange(__instance, __state);
}
