using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WodenTripleScreenProbe;

/// <summary>
/// Named Unity bindings whose managed wrappers were stripped from this IL2CPP player.
/// Resolve all bindings before creating private cameras; never guess an address or call a
/// generated out/ref non-blittable wrapper.
/// </summary>
internal sealed class NativeCamera
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetObject(IntPtr self, IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetInt(IntPtr self, int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetFloat(IntPtr self, float value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RenderCall(IntPtr self);

    private readonly SetObject _target;
    private readonly SetInt _mask, _clear;
    private readonly SetFloat _near;
    private readonly RenderCall _render;

    internal NativeCamera()
    {
        _target = Resolve<SetObject>("UnityEngine.Camera::set_targetTexture_Injected");
        _mask = Resolve<SetInt>("UnityEngine.Camera::set_cullingMask_Injected");
        _clear = Resolve<SetInt>("UnityEngine.Camera::set_clearFlags_Injected");
        _near = Resolve<SetFloat>("UnityEngine.Camera::set_nearClipPlane_Injected");
        _render = Resolve<RenderCall>("UnityEngine.Camera::Render");
    }

    private static T Resolve<T>(string name) where T : Delegate
    {
        var pointer = IL2CPP.il2cpp_resolve_icall(name);
        if (pointer == IntPtr.Zero) throw new MissingMethodException("Required Unity binding unavailable: " + name);
        return Marshal.GetDelegateForFunctionPointer<T>(pointer);
    }

    internal void SetTarget(Camera camera, RenderTexture? target) =>
        _target(camera.m_CachedPtr, target == null ? IntPtr.Zero : target.m_CachedPtr);
    internal void SetMask(Camera camera, int mask) => _mask(camera.m_CachedPtr, mask);
    internal void SetClear(Camera camera, CameraClearFlags flags) => _clear(camera.m_CachedPtr, (int)flags);
    internal void SetNear(Camera camera, float near) => _near(camera.m_CachedPtr, near);
    internal void Render(Camera camera) => _render(camera.m_CachedPtr);
}
