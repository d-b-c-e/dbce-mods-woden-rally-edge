using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WodenRallyEdge;

internal static class CameraNative
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetFloat(IntPtr self, float value);
    private static readonly SetFloat? SetNear = Resolve();
    private static SetFloat? Resolve()
    {
        // Woden strips the managed setter. UnityPlayer still registers this
        // named binding, using the same native-self/float ABI as farClipPlane.
        var address = IL2CPP.il2cpp_resolve_icall("UnityEngine.Camera::set_nearClipPlane_Injected");
        if (address != IntPtr.Zero) return Marshal.GetDelegateForFunctionPointer<SetFloat>(address);
        Runtime.Log.LogWarning("Camera near-clip binding unavailable; retaining game clip distance"); return null;
    }
    internal static void NearClip(Camera camera, float value)
    {
        if (SetNear != null && camera.m_CachedPtr != IntPtr.Zero) SetNear(camera.m_CachedPtr, value);
    }
}
