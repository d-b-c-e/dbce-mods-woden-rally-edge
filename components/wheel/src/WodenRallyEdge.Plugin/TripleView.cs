using BepInEx.Configuration;
using Dbce.TripleScreen;
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
    /// <summary>The saved views setting (F6 Cameras selector, STD-022).</summary>
    internal static TripleMode Mode { get => _mode?.Value ?? TripleMode.Auto; set { if (_mode != null && _mode.Value != value) _mode.Value = value; } }
    internal static bool MatchGameFov
    {
        get => _matchGameFov?.Value ?? false;
        set
        {
            if (_matchGameFov == null || _matchGameFov.Value == value) return;
            if (value) StartAdjustableAtShownAngle(); // before the switch, while the center angle is still the rig's
            SessionGameFov = null; // an explicit saved choice ends the session A/B
            _matchGameFov.Value = value;
        }
    }

    /// <summary>
    /// Both F6 routes to Adjustable (the Triple FOV choice and the locked row's button) start the on-screen mounted lens
    /// at the angle shown, within the 30–110° lens bounds (Core FovModeSwitch). Only that view's saved lens changes: the
    /// other view, Fit to car and the height, forward and tilt stay. A chase view, a fitted bonnet or no measured angle
    /// leaves every value alone (the saved lens then applies). The Advanced editor caches its pose before the button
    /// and saves only an edited pose, so this value is not overwritten in the same draw.
    /// </summary>
    private static void StartAdjustableAtShownAngle()
    {
        try
        {
            var view = MountedCamera.Cycle.View;
            bool mounted = Active && view != WodenRallyEdge.Core.MountedView.Stock, bumper = view == WodenRallyEdge.Core.MountedView.Bumper;
            var cfg = Runtime.Settings;
            double? shown = CenterVerticalFov;
            if (WodenRallyEdge.Core.FovModeSwitch.AdjustableStart(shown, mounted, bumper || !cfg.CameraAutoFit) is not { } fov) return;
            var pose = cfg.GetCameraPose(bumper);
            if (Math.Abs(pose.Fov - fov) < .05f) return;
            cfg.SetCameraPose(bumper, pose with { Fov = fov });
            Panel.SettingsChanged();
            Runtime.Log.LogInfo(FormattableString.Invariant($"Adjustable FOV: {(bumper ? "bumper" : "bonnet")} lens starts at {fov:0.0}° (shown {shown:0.0}°, saved lens was {pose.Fov:0.0}°)"));
        }
        catch (Exception ex) { Runtime.Log.LogWarning("Adjustable FOV start: " + ex.Message); }
    }
    internal static bool MountedFovLocked => Active && !MatchGameFov;
    /// <summary>The [Triple] geometry Physical mode uses, and whether it is still the shipped defaults.</summary>
    internal static string RigSettingsText => _panelWidth == null ? "" : WodenRallyEdge.Core.FovLockText.RigSettings(_panelWidth.Value, _panelHeight.Value,
        _eye.Value, _sideAngle.Value, _bezel.Value,
        new ConfigEntryBase[] { _panelWidth, _panelHeight, _eye, _sideAngle, _bezel }.All(e => Equals(e.BoxedValue, e.DefaultValue)));
    // Lock texts name the actual center angle (Core FovLockText); nothing here changes MatchGameFov.
    internal static string FovLockMessage => WodenRallyEdge.Core.FovLockText.KeyMessage(CenterVerticalFov);
    internal static string FovLockHint(bool bumper) => WodenRallyEdge.Core.FovLockText.ViewHint(bumper, CenterVerticalFov);
    /// <summary>The center view's vertical angle in degrees, or null before three views are active. Reads the views on
    /// screen (flat frusta while a curve cannot run), not the computed curved-source ones.</summary>
    internal static double? CenterVerticalFov =>
        Active && _shownViews is { Length: 3 } ? (Math.Atan(_shownViews[1].Top) - Math.Atan(_shownViews[1].Bottom)) * 180 / Math.PI : null;
    internal static string CenterFovStatus
    {
        get
        {
            if (!Active || _shownViews is not { Length: 3 }) return "Center projection angles appear when three views are active.";
            var center = _shownViews[1];
            double vertical = (Math.Atan(center.Top) - Math.Atan(center.Bottom)) * 180 / Math.PI;
            double horizontal = (Math.Atan(center.Right) - Math.Atan(center.Left)) * 180 / Math.PI;
            return FormattableString.Invariant($"Current center view: {vertical:0.0}° vertical, {horizontal:0.0}° horizontal.") +
                   (TripleCurve.Presented ? " Curved panels corrected." : TripleCurve.Active ? " Curved panels: waiting for the first presented frame."
                    : TripleCurve.Failure != null ? " Curve correction off: " + TripleCurve.Failure : "");
        }
    }
    private static ConfigEntry<string> _toggleKey = null!, _fovKey = null!;
    private static ConfigEntry<bool> _centerHud = null!, _matchGameFov = null!, _chaseGameFov = null!;
    private static ConfigEntry<float> _panelWidth = null!, _panelHeight = null!, _eye = null!, _sideAngle = null!, _bezel = null!, _curveRadius = null!;
    /// <summary>FovKey A/B, this session only, never saved: null follows the saved choices; false shows the measured
    /// perspective for every view (chase included); true shows the lens for every view (TripleGeometry.ShowsGameFov).</summary>
    internal static bool? SessionGameFov { get; private set; }
    internal static string FovKeyName => _fovKey?.Value ?? "F7";
    internal static string SessionFovText => SessionGameFov is { } session
        ? FovKeyName + " is showing the " + (session ? "game field of view" : "measured perspective") + " until the game restarts or you press it again; nothing is saved."
        : "";
    // Curved panels: the warps for the current views (null when flat), and the flat frusta kept for a curve that cannot run.
    private static CurvedPanelWarp[]? _warps;
    private static PanelFrustum[]? _flatViews;
    private static string _flatKey = "";
    // The views actually applied this frame (flat ones while a curve cannot run), for CenterVerticalFov and the status.
    private static PanelFrustum[]? _shownViews;
    private static bool _curvedApplied;

    private static bool _toggledOff, _failed, _loggedScene;
    private static Camera? _source, _left, _right, _hud;
    private static GameObject? _leftGo, _rightGo, _hudGo;
    private static Rect _sourceRect;
    private static bool _sourcePhysical;
    private static PanelFrustum[]? _views;
    private static string _viewsKey = "";
    private static double _nextLog, _nextCanvasScan;
#if WODEN_HUD_STATE_TRACE
    private static double _nextHudStateLog;
#endif
    private static readonly List<(Canvas Canvas, RenderMode Mode, Camera? World, float Plane)> MovedCanvases = new();
    private static readonly TripleHudDepth HudDepth = TripleHudDepth.Production;
    private const int UiLayer = 5;

    internal static void Bind(ConfigFile cfg)
    {
        _mode = cfg.Bind("Triple", "Mode", TripleMode.Auto, "Auto turns the three views on when the window is three screens wide (Surround or a borderless span); On forces them; Off keeps the stock single view.");
        _toggleKey = cfg.Bind("Triple", "ToggleKey", "F11", "Keyboard key that switches the three views on and off in game (Input System key name; None to disable).");
        _centerHud = cfg.Bind("Triple", "CenterHud", true, "Draw the game's HUD and menus on the center screen.");
        _matchGameFov = cfg.Bind("Triple", "MatchGameFov", false, "Adjustable FOV: derive all three projections from the active bonnet/bumper lens or stock game lens, using a virtual eye distance. False keeps the [Triple] rig geometry for mounted views.");
        _chaseGameFov = cfg.Bind("Triple", "ChaseUsesGameFov", true, "Use the game's field of view in stock chase cameras even in Physical rig mode; mounted views follow MatchGameFov.");
        _panelWidth = cfg.Bind("Triple", "PanelWidthMm", 708.4f, "Visible width of one screen, millimetres (32\" 16:9 is about 708).");
        _panelHeight = cfg.Bind("Triple", "PanelHeightMm", 398.5f, "Visible height of one screen, millimetres.");
        _eye = cfg.Bind("Triple", "EyeDistanceMm", 660f, "Distance from your eyes to the center screen, millimetres.");
        _sideAngle = cfg.Bind("Triple", "SideAngle", 70f, "How far each side screen is turned toward you, degrees (0 = flat row).");
        _bezel = cfg.Bind("Triple", "BezelMm", 8f, "Gap between the visible areas at each seam, millimetres.");
        _curveRadius = cfg.Bind("Triple", "CurveRadiusMm", 0f, "Monitor curve radius in millimetres (1500 for 1500R); 0 is flat. Eye distance is measured to the middle of the center screen glass; curved views are redrawn along your eye rays.");
        _fovKey = cfg.Bind("Triple", "FovKey", "F7", "Keyboard key that switches, for this session only, between the measured perspective and the game field of view in all three views (Input System key name; None to disable). Nothing is saved.");
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
            if (kb != null && Active && !Panel.Open && Enum.TryParse<Key>(_fovKey.Value, true, out var fovKey) && fovKey != Key.None && kb[fovKey].wasPressedThisFrame)
            {
                SessionGameFov = TripleGeometry.NextSessionGameFov(SessionGameFov, MatchGameFov, _chaseGameFov.Value, MountedCamera.Cycle.View);
                Runtime.Log.LogInfo("Triple FOV (" + _fovKey.Value + ", this session): " + (SessionGameFov.Value ? "game field of view" : "measured perspective"));
            }
        }
        catch { }
        string? why = WhyNot();
        if (why != null) { Stop(why); Status = "Off (" + why + ")"; return; }
        if (_source != null && !Alive(_source)) Stop("camera destroyed");
        Status = Active ? "On" + TripleCurve.Note : "Waiting for the driving camera";
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
            if (cam == null || !cam.isActiveAndEnabled || (cam.targetTexture != null && !TripleCurve.Owns(cam.targetTexture)) || cam.orthographic)
            {
                if (_source != null && (cam == null || cam.Pointer == _source.Pointer)) Stop(cam != null && cam.orthographic ? "orthographic camera" : "camera inactive");
                return;
            }
            if (_source != null && _source.Pointer != cam.Pointer) Stop("player camera changed");
            float? mountedFov = car.IsPlayer && MountedCamera.PlayerOwned && MountedCamera.Cycle.View != WodenRallyEdge.Core.MountedView.Stock
                ? MountedCamera.Pose(MountedCamera.Cycle.View == WodenRallyEdge.Core.MountedView.Bumper).Fov : null;
            Apply(cam, mountedFov);
        }
        catch (Exception ex)
        {
            _failed = true;
            Runtime.Log.LogError("Triple screens failed and are off until restart: " + ex);
            Stop("exception");
        }
    }

    private static void Apply(Camera source, float? ownedMountedFov)
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

        // PostProcessLayer.OnPreCull resets a non-physical camera's projection; a custom
        // matrix stays authoritative when physical properties are on. Switching them on re-derives fieldOfView from a
        // focal length nothing synced while the camera was non-physical (UnityPlayer set_usePhysicalProperties; owner
        // log 2026-10-15: 37 before, 45 after), so write the game's angle back: the physical-mode setter syncs the focal
        // length, and views whose camera mode does not rewrite the FOV every frame (Photo) keep it.
        float gameFov = source.fieldOfView;
        if (!source.usePhysicalProperties) { source.usePhysicalProperties = true; source.fieldOfView = gameFov; }
        var views = Views(gameFov, ownedMountedFov);
        // Curved panels: the views render full-rect into textures that TripleCurve redraws over the window. A curve that
        // cannot run keeps the original flat frusta, never the widened curved-source ones.
        bool curved = _warps != null && TripleCurve.Configure(_warps, _viewsKey, source, Screen.width / 3, Screen.height);
        if (_warps != null && !curved) views = FlatViews();
        _shownViews = views;
        if (!curved && _curvedApplied) { TripleNative.SetTarget(source, null); _curvedApplied = false; }
        float near = source.nearClipPlane, far = source.farClipPlane;
        var t = source.transform;
        var center = new Rect(1f / 3f, 0f, 1f / 3f, 1f);
        source.rect = curved ? new Rect(0f, 0f, 1f, 1f) : center;
        source.projectionMatrix = Frustum(views[1], near, far);
        Side(_left!, views[0], 0, source, t, near, far);
        Side(_right!, views[2], 2, source, t, near, far);
        if (curved)
        {
            // CopyFrom gave the sides the source target; each view gets its own, full rect.
            TripleNative.SetTarget(source, TripleCurve.Target(1));
            _left!.rect = new Rect(0f, 0f, 1f, 1f); TripleNative.SetTarget(_left, TripleCurve.Target(0));
            _right!.rect = new Rect(0f, 0f, 1f, 1f); TripleNative.SetTarget(_right, TripleCurve.Target(2));
            _curvedApplied = true;
        }
        if (_hud != null) { _hud.rect = center; TripleNative.SetDepth(_hud, source.depth + 10); }

        double now = Runtime.Clock.Elapsed.TotalSeconds;
#if WODEN_HUD_STATE_TRACE
        if (Runtime.DiagnosticNoForce && StagePlayback.OutputMuted && _hud != null && now >= _nextHudStateLog)
        {
            _nextHudStateLog = now + 1;
            LogHudState(source, _hud, now);
        }
#endif
        if (now >= _nextLog)
        {
            _nextLog = now + 30;
            Runtime.Log.LogInfo($"Triple: {Screen.width}x{Screen.height}, '{source.name}', near {near:0.###} far {far:0}, side yaw {views[2].YawDegrees:0.0}, " +
                                $"game vfov {gameFov:0.0}, center vfov {2 * Math.Atan(views[1].Top) * 180 / Math.PI:0.0}, hud canvases {MovedCanvases.Count}");
        }
    }

    private static double _viewEye;
    private static PanelFrustum[] Views(float gameFov, float? ownedMountedFov)
    {
        double eye = TripleGeometry.EyeDistanceForView(_panelHeight.Value, _eye.Value, MatchGameFov,
            _chaseGameFov.Value, MountedCamera.Cycle.View, gameFov, ownedMountedFov, SessionGameFov);
        float radius = _curveRadius.Value > 0f && float.IsFinite(_curveRadius.Value) ? _curveRadius.Value : 0f;
        string key = $"{_panelWidth.Value}|{_panelHeight.Value}|{eye:0.###}|{_sideAngle.Value}|{_bezel.Value}|{radius}";
        if (_views == null || key != _viewsKey)
        {
            _viewEye = eye;
            _warps = null;
            if (radius > 0f)
            {
                try
                {
                    var rig = CurvedRig.Compute(_panelWidth.Value, _panelHeight.Value, eye, _sideAngle.Value, _bezel.Value, radius);
                    _views = rig.Views; _warps = rig.Warps;
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    TripleCurve.Fail($"CurveRadiusMm {radius}: {ex.Message}");
                    _views = TripleGeometry.Compute(_panelWidth.Value, _panelHeight.Value, eye, _sideAngle.Value, _bezel.Value);
                }
            }
            else
            {
                if (TripleCurve.Failure != null || TripleCurve.Active) TripleCurve.Reset();
                _views = TripleGeometry.Compute(_panelWidth.Value, _panelHeight.Value, eye, _sideAngle.Value, _bezel.Value);
            }
            _viewsKey = key;
        }
        return _views;
    }

    /// <summary>The flat frusta for the current eye, for a curve that cannot run (built once per key).</summary>
    private static PanelFrustum[] FlatViews()
    {
        if (_flatViews == null || _flatKey != _viewsKey)
        {
            _flatViews = TripleGeometry.Compute(_panelWidth.Value, _panelHeight.Value, _viewEye, _sideAngle.Value, _bezel.Value);
            _flatKey = _viewsKey;
        }
        return _flatViews;
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
        dst.volumeTrigger = src.volumeTrigger ?? source.transform;
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
        if (!TripleNative.CanvasAvailable) throw new InvalidOperationException("HUD canvas read/write bindings unavailable: " + TripleNative.Report);
        var go = NewCamera("Woden.Triple.Hud", out camera);
        TripleNative.SetClearFlags(camera, CameraClearFlags.Depth);
        TripleNative.SetCullingMask(camera, 1 << UiLayer);
        TripleNative.SetDepth(camera, source.depth + 10);
        camera.fieldOfView = TripleHudDepth.FieldOfView;
        TripleNative.SetNear(camera, TripleHudDepth.Near);
        TripleNative.SetFar(camera, HudDepth.Far);
        go.transform.position = new Vector3(0f, TripleHudDepth.CameraY, 0f); // isolated UI volume; never follows the car
        Runtime.Log.LogInfo($"Triple HUD geometry: plane={HudDepth.Plane}, far={HudDepth.Far}; center HUD depth, physical scanout unqualified");
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
                float originalPlane = TripleNative.GetPlaneDistance(canvas);
                if (!float.IsFinite(originalPlane) || originalPlane < 0)
                    throw new InvalidOperationException("HUD original plane distance unavailable: " + canvas.name);
                MovedCanvases.Add((canvas, canvas.renderMode, canvas.worldCamera, originalPlane));
                TripleNative.SetRenderMode(canvas, RenderMode.ScreenSpaceCamera);
                TripleNative.SetWorldCamera(canvas, _hud);
                TripleNative.SetPlaneDistance(canvas, HudDepth.Plane);
                if (TripleNative.GetPlaneDistance(canvas) != HudDepth.Plane)
                    throw new InvalidOperationException("HUD plane distance readback differs: " + canvas.name);
                Runtime.Log.LogInfo($"Triple HUD: '{canvas.name}' moved to the center screen");
            }
        }
        catch (Exception ex)
        {
            RestoreCanvases();
            _failed = true; // do not repeatedly attempt a partial canvas migration
            Runtime.Log.LogWarning("Triple HUD refused; original canvas rollback attempted: " + ex.Message);
        }
    }

    private static void RestoreCanvases()
    {
        foreach (var (canvas, mode, world, plane) in MovedCanvases)
        {
            if (!Alive(canvas)) continue;
            // Each original property gets its own restore attempt. A failed optional depth
            // restore must not leave a canvas attached to the HUD camera we are destroying.
            try { TripleNative.SetPlaneDistance(canvas, plane); }
            catch (Exception ex) { Runtime.Log.LogWarning("Triple HUD plane restore failed: " + ex.Message); }
            try { TripleNative.SetRenderMode(canvas, mode); }
            catch (Exception ex) { Runtime.Log.LogWarning("Triple HUD mode restore failed: " + ex.Message); }
            try { TripleNative.SetWorldCamera(canvas, world); }
            catch (Exception ex) { Runtime.Log.LogWarning("Triple HUD camera restore failed: " + ex.Message); }
            try
            {
                if (TripleNative.GetPlaneDistance(canvas) != plane)
                    Runtime.Log.LogWarning("Triple HUD original plane readback differs: " + canvas.name);
            }
            catch (Exception ex) { Runtime.Log.LogWarning("Triple HUD plane readback failed: " + ex.Message); }
        }
        MovedCanvases.Clear();
    }

    internal static void Stop(string why)
    {
        bool had = _source != null || _leftGo != null || _rightGo != null || _hudGo != null;
        if (!had) return;
        RestoreCanvases();
        TripleCurve.Reset();
        try
        {
            if (_source != null && Alive(_source))
            {
                if (_curvedApplied) TripleNative.SetTarget(_source, null);
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
        _curvedApplied = false;
        _shownViews = null;
#if WODEN_HUD_STATE_TRACE
        _nextHudStateLog = 0;
#endif
        Runtime.Log.LogInfo("Triple screens off (" + why + "); game camera restored");
    }

#if WODEN_HUD_STATE_TRACE
    private static void LogHudState(Camera source, Camera hud, double elapsed)
    {
        try
        {
            string utc = DateTime.UtcNow.ToString("O");
            Runtime.Log.LogInfo($"HUD-STATE utc={utc} elapsed={elapsed:0.000} source='{source.name}' ptr={source.Pointer} rect={source.rect} depth={source.depth} " +
                                $"hudPtr={hud.Pointer} pos={hud.transform.position} rot={hud.transform.rotation.eulerAngles} rect={hud.rect} " +
                                $"depth={hud.depth} near={hud.nearClipPlane} far={hud.farClipPlane} canvases={MovedCanvases.Count}");
            foreach (var (canvas, _, _, _) in MovedCanvases)
            {
                if (!Alive(canvas))
                {
                    Runtime.Log.LogInfo($"HUD-STATE utc={utc} canvas=destroyed");
                    continue;
                }
                var world = canvas.worldCamera;
                Runtime.Log.LogInfo($"HUD-STATE utc={utc} canvas='{canvas.name}' ptr={canvas.Pointer} mode={canvas.renderMode} " +
                                    $"worldPtr={(world == null ? "null" : world.Pointer.ToString())} plane={TripleNative.GetPlaneDistance(canvas)}");
            }
        }
        catch (Exception ex) { Runtime.Log.LogWarning("HUD-STATE readback failed: " + ex.Message); }
    }
#endif

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
