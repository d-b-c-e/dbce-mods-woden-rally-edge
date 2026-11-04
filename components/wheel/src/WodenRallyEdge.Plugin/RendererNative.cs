using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WodenRallyEdge;

// This game's generated Renderer omits forceRenderingOff. Both exact injected
// names remain registered in the supported UnityPlayer (SHA256 9FEEDB45...).
// Match the native-self / I1 bool convention already used by CameraNative and
// UiNative. Missing bindings disable this optional override; never change enabled.
internal static class RendererNative
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)] private delegate bool GetBool(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetBool(IntPtr self, [MarshalAs(UnmanagedType.I1)] bool value);
    private static readonly GetBool? Get = Resolve<GetBool>("UnityEngine.Renderer::get_forceRenderingOff_Injected");
    private static readonly SetBool? Set = Resolve<SetBool>("UnityEngine.Renderer::set_forceRenderingOff_Injected");
    private static T? Resolve<T>(string name) where T : Delegate
    {
        var address = IL2CPP.il2cpp_resolve_icall(name);
        if (address != IntPtr.Zero) return Marshal.GetDelegateForFunctionPointer<T>(address);
        Runtime.Log.LogWarning("Optional body visibility binding unavailable: " + name); return null;
    }
    internal static bool TryGet(Renderer body, out bool value)
    {
        value = false;
        if (Get == null || Set == null || body.m_CachedPtr == IntPtr.Zero) return false;
        value = Get(body.m_CachedPtr); return true;
    }
    internal static void ForceOff(Renderer body, bool value)
    {
        if (Get != null && Set != null && body.m_CachedPtr != IntPtr.Zero) Set(body.m_CachedPtr, value);
    }
}
