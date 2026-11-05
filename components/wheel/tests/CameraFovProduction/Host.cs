using BepInEx.Configuration;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Substitute only native camera availability/measurements and the visible notice.
// The store, exact editing method bodies and lens/frustum policy are production.
internal sealed class FakeCamera { internal float NativeFov; }
internal static class MountedCamera
{
    internal static float? Start;
    internal static float? BaseFov(CameraSlot _) => Start;
    internal static float NativeFov(FakeCamera camera) => camera.NativeFov;
}
internal static class Runtime
{
    internal static readonly FakeSettings Settings = new();
    internal sealed class FakeSettings { internal float CameraFov = 70, BumperFov = 70; }
}
internal static partial class TripleView
{
    private static CameraFramingStore? _framing;
    private static FakeCamera? _source;
    private static object? _views;
    private static ConfigEntry<bool>? _matchGameFov, _chaseGameFov;
    internal static bool CameraEditAllowed = true;
    internal static CameraSlot CurrentCamera;
    internal static double MeasuredFov = 34;
    internal static string Notice = "";
    internal static bool ViewsInvalidated => _views == null;
    private static void Announce(string? suffix = null) => Notice = suffix ?? "changed";
    internal static CameraFramingStore Store => _framing!;
    internal static void Open(string path, CameraSlot camera, float native = 70)
    {
        var cfg = new ConfigFile(path, false); cfg.SaveOnConfigSet = false;
        _framing = new CameraFramingStore(cfg); CurrentCamera = camera;
        _source = new FakeCamera { NativeFov = native }; CameraEditAllowed = true;
        _views = new object();
        MountedCamera.Start = null; MeasuredFov = 34; Notice = "";
    }
    internal static void Native(float value) { _source!.NativeFov = value; }
    internal static void SourceAvailable(bool value) => _source = value ? new FakeCamera { NativeFov = 70 } : null;
}
