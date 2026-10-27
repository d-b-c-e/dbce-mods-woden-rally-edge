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
    internal float nearClipPlane = .1f, farClipPlane = 1000, depth = 0;
    internal readonly object transform = new();
    internal Rect rect = new(0, 0, 1, 1);
    internal object projectionMatrix = new();
    internal void ResetProjectionMatrix() { ProjectionResets++; }
}
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
internal static class TripleNative { internal static void SetDepth(Camera c, float value) { c.depth = value; } }

internal static partial class TripleView
{
    private static Camera? _source, _left, _right, _hud;
    private static GameObject? _leftGo, _rightGo, _hudGo;
    private static Rect _sourceRect;
    private static bool _sourcePhysical = false, _loggedScene = false, _failed = false;
    private static double _nextLog = 0;
    private static readonly Entry<bool> _centerHud = new(false);
    private static readonly List<object> MovedCanvases = new();
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
        Check(_observedGameFov == 37, "Views receives the captured game FOV");
        Check(photo.PhysicalWrites == 1 && photo.FovWrites == 1, "transition enables once then synchronizes focal lens once");
        AfterCarCamera(new(photo));
        Check(photo.fieldOfView == 37 && photo.PhysicalWrites == 1 && photo.FovWrites == 1, "photo without native per-frame FOV writer stays unchanged");
        photo.fieldOfView = 54; // A later native game/lens writer must win.
        AfterCarCamera(new(photo));
        Check(photo.fieldOfView == 54 && _observedGameFov == 54 && photo.FovWrites == 2, "later native FOV remains authoritative");

        var replacement = new Camera(2, 63);
        AfterCarCamera(new(replacement));
        Check(!photo.usePhysicalProperties && photo.fieldOfView == 54 && photo.rect == rect && photo.ProjectionResets == 1,
              "changed-camera path restores prior camera without stale lens overwrite");
        Check(replacement.usePhysicalProperties && replacement.fieldOfView == 63 && _observedGameFov == 63,
              "replacement captures its own lens before physical transition");
        Stop("fixture off");
        Check(!replacement.usePhysicalProperties && replacement.fieldOfView == 63 && replacement.ProjectionResets == 1 && _source == null,
              "Stop restores native mode and retains replacement lens");

        var alreadyPhysical = new Camera(3, 72, true);
        AfterCarCamera(new(alreadyPhysical));
        Check(alreadyPhysical.fieldOfView == 72 && alreadyPhysical.PhysicalWrites == 0 && alreadyPhysical.FovWrites == 0,
              "already-physical camera gets no unnecessary physical/lens write");
        Stop("fixture physical off");
        Check(alreadyPhysical.usePhysicalProperties && alreadyPhysical.fieldOfView == 72, "Stop preserves original physical camera mode/lens");
        Check(!_failed, "actual handler path completes without exception");
        Console.WriteLine($"{(expectRegression ? "BASELINE REGRESSION" : "SUCCESSOR")}: {_checks} checks, {_failures} failures; fake cameras only");
        return expectRegression ? (_failures >= 3 ? 0 : 2) : (_failures == 0 ? 0 : 1);
    }
}
internal static class Program { private static int Main(string[] args) => TripleView.Run(args.Contains("--expect-regression")); }
internal static class Screen { internal const int width = 7680, height = 1440; }
