using BepInEx.Configuration;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.PostProcessing;
using Object = UnityEngine.Object;

namespace WodenRallyEdge;

public enum TripleMode { Auto, On, Off }

/// <summary>
/// Triple-screen views across one wide window (Surround or a borderless span).
/// </summary>
/// <remarks>
/// After <c>Car_Cam.LateUpdate</c> (and the mounted-camera postfix) has placed the player
/// camera, it becomes the center view (middle third, off-axis projection). Two plain cameras
/// copy its settings and post-processing every frame, turned by the side-panel angle, into
/// the outer thirds. Screen-space canvases move onto a HUD camera drawn in the center third.
/// Built-in render pipeline with Post Processing v2, like art of rally: the PostProcessLayer
/// resets non-physical projections in OnPreCull, so views use physical properties. Render
/// only: no input, physics, timing or scoring. Everything is put back when it turns off.
/// </remarks>
internal static class TripleView
{
    internal static string Status { get; private set; } = "Off";
    internal static bool Active => _source != null;

    private static ConfigEntry<TripleMode> _mode = null!;
    /// <summary>Triples are not turned off (for SpanWindow).</summary>
    internal static bool Allowed => _mode != null && _mode.Value != TripleMode.Off;
    private static ConfigEntry<string> _toggleKey = null!;
    private static ConfigEntry<bool> _centerHud = null!, _matchGameFov = null!, _chaseGameFov = null!;
    private static ConfigEntry<float> _panelWidth = null!, _panelHeight = null!, _eye = null!, _sideAngle = null!, _bezel = null!;

    private static bool _toggledOff, _failed, _loggedScene;
    private static Camera? _source, _left, _right, _hud;
    private static GameObject? _leftGo, _rightGo, _hudGo;
    private static Rect _sourceRect;
    private static bool _sourcePhysical;
    private static PanelFrustum[]? _views;
    private static string _viewsKey = "";
    private static double _nextLog, _nextCanvasScan;
    private static readonly List<(Canvas Canvas, RenderMode Mode, Camera? World)> MovedCanvases = new();
    private const int UiLayer = 5;

    internal static void Bind(ConfigFile cfg)
    {
        _mode = cfg.Bind("Triple", "Mode", TripleMode.Auto, "Auto turns the three views on when the window is three screens wide (Surround or a borderless span); On forces them; Off keeps the stock single view.");
        _toggleKey = cfg.Bind("Triple", "ToggleKey", "F11", "Keyboard key that switches the three views on and off in game (Input System key name; None to disable).");
        _centerHud = cfg.Bind("Triple", "CenterHud", true, "Draw the game's HUD and menus on the center screen.");
        _matchGameFov = cfg.Bind("Triple", "MatchGameFov", false, "Use the game's field of view on the center screen in every camera, instead of the physical eye distance.");
        _chaseGameFov = cfg.Bind("Triple", "ChaseUsesGameFov", true, "Use the game's field of view in the stock chase cameras; bonnet and bumper views stay physical.");
        _panelWidth = cfg.Bind("Triple", "PanelWidthMm", 708.4f, "Visible width of one screen, millimetres (32\" 16:9 is about 708).");
        _panelHeight = cfg.Bind("Triple", "PanelHeightMm", 398.5f, "Visible height of one screen, millimetres.");
        _eye = cfg.Bind("Triple", "EyeDistanceMm", 660f, "Distance from your eyes to the center screen, millimetres.");
        _sideAngle = cfg.Bind("Triple", "SideAngle", 70f, "How far each side screen is turned toward you, degrees (0 = flat row).");
        _bezel = cfg.Bind("Triple", "BezelMm", 8f, "Gap between the visible areas at each seam, millimetres.");
    }

    private static string? WhyNot()
    {
        if (_failed) return "failed, restart the game";
        if (_mode.Value == TripleMode.Off) return "turned off";
        if (_mode.Value == TripleMode.Auto && Screen.width < Screen.height * 2.9f) return "window is not three screens wide";
        if (TripleNative.CameraUnavailable is { } missing) return missing;
        if (_toggledOff) return "toggle key";
        return null;
    }

    /// <summary>Once per frame from Runtime.Update.</summary>
    internal static void FrameTick(double now)
    {
        try
        {
            var kb = Keyboard.current;
            if (kb != null && Enum.TryParse<Key>(_toggleKey.Value, true, out var key) && key != Key.None && kb[key].wasPressedThisFrame)
            {
                _toggledOff = !_toggledOff;
                Runtime.Log.LogInfo("Triple screens " + (_toggledOff ? "toggled off" : "toggled on"));
            }
        }
        catch { }
        string? why = WhyNot();
        if (why != null) { Stop(why); Status = "Off (" + why + ")"; return; }
        if (_source != null && !Alive(_source)) Stop("camera destroyed");
        Status = Active ? "On" : "Waiting for the driving camera";
        if (Active && _centerHud.Value && now >= _nextCanvasScan) { _nextCanvasScan = now + 2; MoveCanvases(); }
    }

    internal static void AfterCarCamera(Car_Cam carCam)
    {
        if (WhyNot() != null) return;
        try
        {
            // Rendering only: follow the selected player's camera, or a non-player camera
            // (attract demo, spectating) when that's what is on screen.
            var car = carCam.Maincar_;
            if (car == null || (car.IsPlayer && !Runtime.Select(car))) return;
            var cam = carCam.field_Private_Camera_0;
            if (cam == null || !cam.isActiveAndEnabled || cam.targetTexture != null || cam.orthographic)
            {
                if (_source != null && (cam == null || cam.Pointer == _source.Pointer)) Stop(cam != null && cam.orthographic ? "orthographic camera" : "camera inactive");
                return;
            }
            if (_source != null && _source.Pointer != cam.Pointer) Stop("player camera changed");
            Apply(cam);
        }
        catch (Exception ex)
        {
            _failed = true;
            Runtime.Log.LogError("Triple screens failed and are off until restart: " + ex);
            Stop("exception");
        }
    }

    private static void Apply(Camera source)
    {
        if (_source == null)
        {
            _source = source;
            _sourceRect = source.rect;
            _sourcePhysical = source.usePhysicalProperties;
            _leftGo = NewCamera("Woden.Triple.Left", out _left);
            _rightGo = NewCamera("Woden.Triple.Right", out _right);
            if (_centerHud.Value) { _hudGo = NewHudCamera(source, out _hud); MoveCanvases(); }
            Runtime.Log.LogInfo($"Triple screens on: '{source.name}' at {Screen.width}x{Screen.height}");
            if (!_loggedScene) { _loggedScene = true; LogCameras(source); }
        }

        var views = Views(source.fieldOfView);
        float near = source.nearClipPlane, far = source.farClipPlane;
        var t = source.transform;

        // PostProcessLayer.OnPreCull resets a non-physical camera's projection; a custom
        // matrix stays authoritative when physical properties are on.
        source.usePhysicalProperties = true;
        source.rect = new Rect(1f / 3f, 0f, 1f / 3f, 1f);
        source.projectionMatrix = Frustum(views[1], near, far);
        Side(_left!, views[0], 0, source, t, near, far);
        Side(_right!, views[2], 2, source, t, near, far);
        if (_hud != null) { _hud.rect = source.rect; TripleNative.SetDepth(_hud, source.depth + 10); }

        double now = Runtime.Clock.Elapsed.TotalSeconds;
        if (now >= _nextLog)
        {
            _nextLog = now + 30;
            Runtime.Log.LogInfo($"Triple: {Screen.width}x{Screen.height}, '{source.name}', near {near:0.###} far {far:0}, side yaw {views[2].YawDegrees:0.0}, " +
                                $"game vfov {source.fieldOfView:0.0}, center vfov {2 * Math.Atan(views[1].Top) * 180 / Math.PI:0.0}, hud canvases {MovedCanvases.Count}");
        }
    }

    private static PanelFrustum[] Views(float gameFov)
    {
        bool useGameFov = _matchGameFov.Value || _chaseGameFov.Value && MountedCamera.Cycle.View == WodenRallyEdge.Core.MountedView.Stock;
        double eye = useGameFov ? TripleGeometry.EyeDistanceForVerticalFov(_panelHeight.Value, gameFov) : _eye.Value;
        string key = $"{_panelWidth.Value}|{_panelHeight.Value}|{eye:0.###}|{_sideAngle.Value}|{_bezel.Value}";
        if (_views == null || key != _viewsKey)
        {
            _views = TripleGeometry.Compute(_panelWidth.Value, _panelHeight.Value, eye, _sideAngle.Value, _bezel.Value);
            _viewsKey = key;
        }
        return _views;
    }

    private static Matrix4x4 Frustum(PanelFrustum v, float near, float far) =>
        Matrix4x4.Frustum((float)(v.Left * near), (float)(v.Right * near), (float)(v.Bottom * near), (float)(v.Top * near), near, far);

    private static void Side(Camera side, PanelFrustum view, int index, Camera source, Transform t, float near, float far)
    {
        TripleNative.CopyFrom(side, source);
        side.usePhysicalProperties = true;
        TripleNative.SetDepth(side, source.depth + 1 + index);
        side.transform.SetPositionAndRotation(t.position, t.rotation * Quaternion.Euler(0f, (float)view.YawDegrees, 0f));
        TripleNative.SetNear(side, near);
        TripleNative.SetFar(side, far);
        side.rect = new Rect(index / 3f, 0f, 1f / 3f, 1f);
        side.projectionMatrix = Frustum(view, near, far);
        SyncPostProcessing(side, source);
        if (!side.enabled) side.enabled = true;
    }

    private static void SyncPostProcessing(Camera side, Camera source)
    {
        var src = source.GetComponent<PostProcessLayer>();
        var dst = side.GetComponent<PostProcessLayer>();
        if (src == null || !src.enabled) { if (dst != null) dst.enabled = false; return; }
        if (dst == null)
        {
            var resources = src.m_Resources;
            if (resources == null) return; // side views render without post-processing
            dst = side.gameObject.AddComponent(Il2CppType.Of<PostProcessLayer>()).Cast<PostProcessLayer>();
            dst.Init(resources);
            Runtime.Log.LogInfo($"{side.name}: post-processing layer added (AA {src.antialiasingMode}, volume mask 0x{src.volumeLayer.value:X})");
        }
        dst.volumeLayer = src.volumeLayer;
        dst.volumeTrigger = side.transform;
        dst.stopNaNPropagation = src.stopNaNPropagation;
        dst.finalBlitToCameraTarget = src.finalBlitToCameraTarget;
        dst.antialiasingMode = src.antialiasingMode == PostProcessLayer.Antialiasing.TemporalAntialiasing
            ? PostProcessLayer.Antialiasing.FastApproximateAntialiasing // TAA jitters custom projections
            : src.antialiasingMode;
        if (!dst.enabled) dst.enabled = true;
    }

    private static GameObject NewCamera(string name, out Camera camera)
    {
        var go = new GameObject(name);
        Object.DontDestroyOnLoad(go);
        camera = go.AddComponent(Il2CppType.Of<Camera>()).Cast<Camera>();
        camera.enabled = false; // untagged, so it never becomes Camera.main
        return go;
    }

    private static GameObject NewHudCamera(Camera source, out Camera camera)
    {
        var go = NewCamera("Woden.Triple.Hud", out camera);
        TripleNative.SetClearFlags(camera, CameraClearFlags.Depth);
        TripleNative.SetCullingMask(camera, 1 << UiLayer);
        TripleNative.SetDepth(camera, source.depth + 10);
        camera.fieldOfView = 60f;
        TripleNative.SetNear(camera, 0.1f);
        TripleNative.SetFar(camera, 10f);
        go.transform.position = new Vector3(0f, -10000f, 0f); // away from the scene; it only sees canvases
        camera.enabled = true;
        return go;
    }

    private static void MoveCanvases()
    {
        if (_hud == null || !Alive(_hud) || !TripleNative.CanvasAvailable) return;
        try
        {
            foreach (var canvas in Object.FindObjectsOfType<Canvas>(false))
            {
                if (canvas == null || !canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                MovedCanvases.Add((canvas, canvas.renderMode, canvas.worldCamera));
                TripleNative.SetRenderMode(canvas, RenderMode.ScreenSpaceCamera);
                TripleNative.SetWorldCamera(canvas, _hud);
                TripleNative.SetPlaneDistance(canvas, 1f);
                Runtime.Log.LogInfo($"Triple HUD: '{canvas.name}' moved to the center screen");
            }
        }
        catch (Exception ex) { Runtime.Log.LogWarning("Triple HUD: " + ex.Message); }
    }

    private static void RestoreCanvases()
    {
        foreach (var (canvas, mode, world) in MovedCanvases)
            try
            {
                if (canvas == null || !Alive(canvas)) continue;
                TripleNative.SetRenderMode(canvas, mode);
                TripleNative.SetWorldCamera(canvas, world);
            }
            catch { }
        MovedCanvases.Clear();
    }

    internal static void Stop(string why)
    {
        bool had = _source != null || _leftGo != null || _rightGo != null || _hudGo != null;
        if (!had) return;
        RestoreCanvases();
        try
        {
            if (_source != null && Alive(_source))
            {
                _source.ResetProjectionMatrix();
                _source.rect = _sourceRect;
                _source.usePhysicalProperties = _sourcePhysical;
            }
        }
        catch (Exception ex) { Runtime.Log.LogWarning("Triple: restoring the game camera: " + ex.Message); }
        foreach (var go in new[] { _leftGo, _rightGo, _hudGo })
            try { if (go != null && Alive(go)) Object.Destroy(go); } catch { }
        _source = _left = _right = _hud = null;
        _leftGo = _rightGo = _hudGo = null;
        Runtime.Log.LogInfo("Triple screens off (" + why + "); game camera restored");
    }

    private static void LogCameras(Camera source)
    {
        try
        {
            Runtime.Log.LogInfo("  " + TripleNative.Report);
            foreach (var c in Object.FindObjectsOfType<Camera>(false))
                Runtime.Log.LogInfo($"  camera '{c.name}' depth {c.depth} rect {c.rect} mask 0x{c.cullingMask:X} target {(c.targetTexture == null ? "screen" : c.targetTexture.name)} " +
                                    $"ortho {c.orthographic} hdr {c.allowHDR}");
            var components = source.gameObject.GetComponents<Component>();
            var names = new List<string>();
            foreach (var component in components) if (component != null) names.Add(component.GetIl2CppType().Name);
            Runtime.Log.LogInfo($"  player camera components: {string.Join(", ", names)}");
            var layer = source.GetComponent<PostProcessLayer>();
            if (layer != null) Runtime.Log.LogInfo($"  post-processing: AA {layer.antialiasingMode}, volume mask 0x{layer.volumeLayer.value:X}, trigger {(layer.volumeTrigger == null ? "-" : layer.volumeTrigger.name)}");
        }
        catch (Exception ex) { Runtime.Log.LogWarning("Triple camera inventory: " + ex.Message); }
    }

    private static bool Alive(Object o) { try { return o != null && !o.WasCollected; } catch { return false; } }
}

[HarmonyPatch(typeof(Car_Cam), nameof(Car_Cam.LateUpdate))]
internal static class TripleCarCameraLateUpdate
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Car_Cam __instance) => TripleView.AfterCarCamera(__instance);
}
