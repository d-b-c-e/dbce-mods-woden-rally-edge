// Only camera property side effects are modeled. Rendering/IL2CPP/Unity are not executed.
internal sealed class Camera
{
    private float _fov, _focalAngle = 45;
    private bool _physical;
    internal Camera(long pointer, float fov, bool physical = false) { Pointer = pointer; _fov = fov; _physical = physical; }
    internal float fieldOfView { get => _fov; set { _fov = _focalAngle = value; FovWrites++; } }
    internal bool usePhysicalProperties
    {
        get => _physical;
        set { PhysicalWrites++; if (value && !_physical) _fov = _focalAngle; _physical = value; }
    }
    internal int FovWrites, PhysicalWrites, ProjectionResets;
    internal long Pointer;
    internal string name = "fake game camera";
    internal bool isActiveAndEnabled = true, orthographic = false;
    internal object? targetTexture = null;
    internal PostProcessLayer? Post = new();
    internal T? GetComponent<T>() where T : class => Post as T;
    internal float nearClipPlane = .1f, farClipPlane = 1000, depth = 0;
    internal readonly object transform = new();
    internal Rect rect = new(0, 0, 1, 1);
    internal object projectionMatrix = new();
    internal void ResetProjectionMatrix() { ProjectionResets++; }
}
internal sealed class PostProcessLayer
{
    internal enum Antialiasing { None, FastApproximateAntialiasing, SubpixelMorphologicalAntialiasing, TemporalAntialiasing }
    internal bool enabled = true;
    internal Antialiasing antialiasingMode = Antialiasing.TemporalAntialiasing;
    internal int HistoryResets;
    internal void ResetHistory() => HistoryResets++;
}
internal static class TripleCurve
{
    internal static bool ForceCurved = false;
    private static readonly object[] Targets = [new(), new(), new()];
    internal static bool Active => ForceCurved;
    internal static bool Owns(object value) => Targets.Contains(value);
    internal static bool Configure(object[] _, string key, Camera source, int width, int height) => ForceCurved;
    internal static object? Target(int index) => Targets[index];
    internal static void Reset() { ForceCurved = false; }
}
internal static class TripleGeometry { internal const int AngleStep = 2; }
internal readonly record struct Rect(float X, float Y, float Width, float Height);
internal sealed class GameObject { }
internal static class Object { internal static void Destroy(GameObject _) { } }
internal sealed class Entry<T>(T value) { internal T Value = value; }
internal readonly record struct PanelFrustum(double Top, double YawDegrees);
internal sealed class Car { internal bool IsPlayer = true; }
internal sealed class Car_Cam(Camera camera) { internal Car Maincar_ = new(); internal Camera field_Private_Camera_0 = camera; }
internal sealed class Logger
{
    internal void LogInfo(string _) { }
    internal void LogWarning(string _) { }
    internal void LogError(string message) => throw new Exception(message);
}
internal static class Runtime
{
    internal static readonly Logger Log = new();
    internal static readonly FakeClock Clock = new();
    internal static bool Select(Car _) => true;
}
internal sealed class FakeClock { internal TimeSpan Elapsed => TimeSpan.Zero; }
namespace WodenRallyEdge.Core { internal enum MountedView { Stock, Bumper } }
internal static class MountedCamera
{
    internal static bool PlayerOwned = false;
    internal static readonly FakeCycle Cycle = new();
    internal static FakePose Pose(bool _) => new();
}
internal sealed class FakeCycle { internal WodenRallyEdge.Core.MountedView View = WodenRallyEdge.Core.MountedView.Stock; }
internal sealed class FakePose { internal float Fov = 37; }
internal static class TripleNative {
    internal static void SetDepth(Camera c, float value) { c.depth = value; }
    internal static void SetTarget(Camera c, object? target) { c.targetTexture = target; }
}

internal static partial class TripleView
{
    private static Camera? _source, _left, _right, _hud;
    private static GameObject? _leftGo, _rightGo, _hudGo;
    private static Rect _sourceRect;
    private static bool _sourcePhysical = false, _loggedScene = false, _failed = false;
    private static double _nextLog = 0;
    private static readonly Entry<bool> _centerHud = new(false);
    private static readonly List<object> MovedCanvases = new();
    internal static PostProcessLayer? _sourcePost = null;
    internal static PostProcessLayer.Antialiasing _sourceAa = PostProcessLayer.Antialiasing.None;
    internal static bool _sourceAaOverridden = false;
    internal static bool _curvedApplied = false;
    internal static object[]? _warps = null;
    internal static PanelFrustum[]? _shownViews = null;
    internal static string _viewsKey = "fixture";
    internal static int? SessionStep = null;
    private static PanelFrustum[] FlatViews() => Views(37, null);
    private static float _observedGameFov;
    private static string? WhyNot() => null;
    private static bool Alive(object _) => true;
    private static GameObject NewCamera(string _, out Camera camera) { camera = new(99, 45); return new(); }
    private static GameObject NewHudCamera(Camera _, out Camera camera) => NewCamera("hud", out camera);
    private static void MoveCanvases() { }
    private static void RestoreCanvases() { }
    private static void LogCameras(Camera _) { }
    private static PanelFrustum[] Views(float fov, float? _) { _observedGameFov = fov; return [new(1, -70), new(1, 0), new(1, 70)]; }
    private static object Frustum(PanelFrustum _, float near, float far) => new();
    private static void Side(Camera side, PanelFrustum view, int index, Camera source, object transform, float near, float far) { }
    private static int _checks, _failures;
    private static void Check(bool value, string name) { _checks++; if (!value) { _failures++; Console.WriteLine("FAIL " + name); } }

    internal static int Run(bool expectRegression)
    {
        var photo = new Camera(1, 37); var rect = photo.rect;
        AfterCarCamera(new(photo));
        Check(photo.usePhysicalProperties && photo.fieldOfView == 37, "first physical transition retains photo/game FOV");
        Check(photo.Post!.antialiasingMode == PostProcessLayer.Antialiasing.FastApproximateAntialiasing && photo.Post.HistoryResets == 1,
              "center rejects temporal projection/history while triples own it");
        Check(_observedGameFov == 37, "Views receives the captured game FOV");
        Check(photo.PhysicalWrites == 1 && photo.FovWrites == 1, "transition enables once then synchronizes focal lens once");
        AfterCarCamera(new(photo));
        Check(photo.fieldOfView == 37 && photo.PhysicalWrites == 1 && photo.FovWrites == 1, "photo without native per-frame FOV writer stays unchanged");
        Check(photo.Post!.HistoryResets == 1, "stable triple frame does not reset history again");
        photo.fieldOfView = 54; // A later native game/lens writer must win.
        AfterCarCamera(new(photo));
        Check(photo.fieldOfView == 54 && _observedGameFov == 54 && photo.FovWrites == 2, "later native FOV remains authoritative");

        var replacement = new Camera(2, 63);
        AfterCarCamera(new(replacement));
        Check(!photo.usePhysicalProperties && photo.fieldOfView == 54 && photo.rect == rect && photo.ProjectionResets == 1,
              "changed-camera path restores prior camera without stale lens overwrite");
        Check(photo.Post!.antialiasingMode == PostProcessLayer.Antialiasing.TemporalAntialiasing && photo.Post.HistoryResets == 2,
              "camera handoff restores previous center AA and invalidates old history");
        Check(replacement.usePhysicalProperties && replacement.fieldOfView == 63 && _observedGameFov == 63,
              "replacement captures its own lens before physical transition");
        Stop("fixture off");
        Check(!replacement.usePhysicalProperties && replacement.fieldOfView == 63 && replacement.ProjectionResets == 1 && _source == null,
              "Stop restores native mode and retains replacement lens");

        Check(replacement.Post!.antialiasingMode == PostProcessLayer.Antialiasing.TemporalAntialiasing && replacement.Post.HistoryResets == 2,
              "Original restores center AA without stale triple history");

        var alreadyPhysical = new Camera(3, 72, true);
        AfterCarCamera(new(alreadyPhysical));
        Check(alreadyPhysical.fieldOfView == 72 && alreadyPhysical.PhysicalWrites == 0 && alreadyPhysical.FovWrites == 0,
              "already-physical camera gets no unnecessary physical/lens write");
        Stop("fixture physical off");
        Check(alreadyPhysical.usePhysicalProperties && alreadyPhysical.fieldOfView == 72, "Stop preserves original physical camera mode/lens");
        var cycling = new Camera(4, 51);
        for (int cycle = 0; cycle < 4; cycle++) {
            SessionStep = TripleGeometry.AngleStep;
            AfterCarCamera(new(cycling)); AfterCarCamera(new(cycling));
            Check(cycling.Post!.antialiasingMode == PostProcessLayer.Antialiasing.FastApproximateAntialiasing && cycling.Post.HistoryResets == cycle * 2 + 1,
                  "F7 Angle enters with one history reset, never temporal projection");
            SessionStep = 1; Stop("F7 Original");
            Check(cycling.Post.antialiasingMode == PostProcessLayer.Antialiasing.TemporalAntialiasing && cycling.Post.HistoryResets == (cycle + 1) * 2,
                  "F7 Original returns native AA with fresh center history on every cycle");
            Check(cycling.rect == new Rect(0, 0, 1, 1) && !cycling.usePhysicalProperties && cycling.fieldOfView == 51,
                  "F7 repeated cycle preserves full-window native lens");
        }
        var curved = new Camera(9, 55);
        _warps = [new(), new(), new()];
        for (int cycle = 0; cycle < 2; cycle++) {
            TripleCurve.ForceCurved = true; SessionStep = 3;
            AfterCarCamera(new(curved));
            Check(curved.targetTexture != null && curved.Post!.antialiasingMode == PostProcessLayer.Antialiasing.FastApproximateAntialiasing,
                  "Angle+curve owns a target and protects center from TAA");
            SessionStep = 1; Stop("curve to Original");
            Check(curved.targetTexture == null && curved.Post!.antialiasingMode == PostProcessLayer.Antialiasing.TemporalAntialiasing && curved.Post.HistoryResets == (cycle + 1) * 2,
                  "curve to Original clears target and resets native center history every cycle");
        }
        _warps = null;
        var external = new Camera(5, 42);
        AfterCarCamera(new(external));
        external.Post!.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
        AfterCarCamera(new(external)); Stop("external AA");
        Check(external.Post.antialiasingMode == PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing,
              "detectable external AA choice is retained");
        var absent = new Camera(6, 42) { Post = null };
        AfterCarCamera(new(absent)); Stop("no PP layer");
        Check(absent.fieldOfView == 42 && _source == null, "missing post-process layer stays supported");
        var disabled = new Camera(7, 42); disabled.Post!.enabled = false;
        AfterCarCamera(new(disabled)); Stop("disabled PP layer");
        Check(disabled.Post.antialiasingMode == PostProcessLayer.Antialiasing.TemporalAntialiasing && disabled.Post.HistoryResets == 0,
              "disabled post-process layer is untouched");
        var nonTemporal = new Camera(8, 42); nonTemporal.Post!.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
        AfterCarCamera(new(nonTemporal)); Stop("SMAA layer");
        Check(nonTemporal.Post.antialiasingMode == PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing && nonTemporal.Post.HistoryResets == 2,
              "non-temporal AA retained and camera-cut history reset");
        Check(!_failed, "actual handler path completes without exception");
        Console.WriteLine($"{(expectRegression ? "BASELINE REGRESSION" : "SUCCESSOR")}: {_checks} checks, {_failures} failures; fake cameras only");
        return expectRegression ? (_failures >= 3 ? 0 : 2) : (_failures == 0 ? 0 : 1);
    }
}
internal static class Program { private static int Main(string[] args) => TripleView.Run(args.Contains("--expect-regression")); }
internal static class Screen { internal const int width = 7680, height = 1440; }
