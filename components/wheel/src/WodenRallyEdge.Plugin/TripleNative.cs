using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WodenRallyEdge;

/// <summary>
/// Engine bindings the triple views need whose managed setters Woden's build stripped.
/// </summary>
/// <remarks>
/// Same approach as <see cref="CameraNative"/>: UnityPlayer still registers the named
/// *_Injected bindings (native self pointer, then value; Unity objects as native pointers).
/// Nothing is guessed: if a required binding is missing, triple views stay off and the log
/// names it.
/// </remarks>
internal static class TripleNative
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetFloat(IntPtr self, float value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetInt(IntPtr self, int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetPtr(IntPtr self, IntPtr value);

    private static readonly List<string> Missing = new();
    private static readonly SetFloat? Depth = Resolve<SetFloat>("UnityEngine.Camera::set_depth_Injected");
    private static readonly SetInt? ClearFlags = Resolve<SetInt>("UnityEngine.Camera::set_clearFlags_Injected");
    private static readonly SetInt? CullingMask = Resolve<SetInt>("UnityEngine.Camera::set_cullingMask_Injected");
    private static readonly SetFloat? Near = Resolve<SetFloat>("UnityEngine.Camera::set_nearClipPlane_Injected");
    private static readonly SetFloat? Far = Resolve<SetFloat>("UnityEngine.Camera::set_farClipPlane_Injected");
    private static readonly SetPtr? CopyFromCamera = Resolve<SetPtr>("UnityEngine.Camera::CopyFrom_Injected");
    private static readonly SetInt? CanvasMode = Resolve<SetInt>("UnityEngine.Canvas::set_renderMode_Injected");
    private static readonly SetPtr? CanvasCamera = Resolve<SetPtr>("UnityEngine.Canvas::set_worldCamera_Injected");
    private static readonly SetFloat? CanvasPlane = Resolve<SetFloat>("UnityEngine.Canvas::set_planeDistance_Injected");

    private static T? Resolve<T>(string name) where T : Delegate
    {
        var address = IL2CPP.il2cpp_resolve_icall(name);
        if (address != IntPtr.Zero) return Marshal.GetDelegateForFunctionPointer<T>(address);
        Missing.Add(name);
        return null;
    }

    /// <summary>Null when every binding the side cameras need is present.</summary>
    internal static string? CameraUnavailable =>
        Depth == null || ClearFlags == null || CullingMask == null || Near == null || Far == null || CopyFromCamera == null
            ? "missing engine bindings: " + string.Join(", ", Missing) : null;
    internal static bool CanvasAvailable => CanvasMode != null && CanvasCamera != null;
    internal static string Report => Missing.Count == 0 ? "all triple engine bindings present" : "missing: " + string.Join(", ", Missing);

    private static IntPtr Ptr(UnityEngine.Object o) => o.m_CachedPtr;

    internal static void SetDepth(Camera c, float v) => Depth!(Ptr(c), v);
    internal static void SetClearFlags(Camera c, CameraClearFlags v) => ClearFlags!(Ptr(c), (int)v);
    internal static void SetCullingMask(Camera c, int v) => CullingMask!(Ptr(c), v);
    internal static void SetNear(Camera c, float v) => Near!(Ptr(c), v);
    internal static void SetFar(Camera c, float v) => Far!(Ptr(c), v);
    internal static void CopyFrom(Camera c, Camera source) => CopyFromCamera!(Ptr(c), Ptr(source));
    internal static void SetRenderMode(Canvas c, RenderMode v) => CanvasMode!(Ptr(c), (int)v);
    internal static void SetPlaneDistance(Canvas c, float v) => CanvasPlane?.Invoke(Ptr(c), v);
    internal static void SetWorldCamera(Canvas c, Camera? camera) => CanvasCamera!(Ptr(c), camera == null ? IntPtr.Zero : Ptr(camera));
}
