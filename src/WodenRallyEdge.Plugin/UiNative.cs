using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WodenRallyEdge;

// Unity 6000.3 uses native GUIStyle pointers for *_Injected calls. These few
// engine bindings survived in UnityPlayer even where their managed API is stripped.
// Never guess offsets or invoke a missing pointer; keep default styling as fallback.
internal static class UiNative
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetInt(IntPtr self, int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetBool(IntPtr self, [MarshalAs(UnmanagedType.I1)] bool value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetLock(CursorLockMode value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] private delegate bool GetVisible();
    private static readonly SetInt? FontSize = Resolve<SetInt>("UnityEngine.GUIStyle::set_fontSize_Injected");
    private static readonly SetBool? Wrap = Resolve<SetBool>("UnityEngine.GUIStyle::set_wordWrap_Injected");
    private static readonly SetLock? Lock = Resolve<SetLock>("UnityEngine.Cursor::set_lockState");
    private static readonly GetVisible? Visible = Resolve<GetVisible>("UnityEngine.Cursor::get_visible");
    private static T? Resolve<T>(string name) where T : Delegate
    {
        var address = IL2CPP.il2cpp_resolve_icall(name);
        if (address != IntPtr.Zero) return Marshal.GetDelegateForFunctionPointer<T>(address);
        Runtime.Log.LogWarning("Optional UI engine binding unavailable: " + name); return null;
    }
    internal static bool CursorVisible => Visible?.Invoke() ?? true;
    internal static void CursorLock(CursorLockMode value) => Lock?.Invoke(value);
    internal static void Style(GUIStyle style, int size, bool wrap) { FontSize?.Invoke(style.m_Ptr, size); Wrap?.Invoke(style.m_Ptr, wrap); }
}
