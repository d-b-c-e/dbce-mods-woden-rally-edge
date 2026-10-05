using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WodenRallyEdge;

// Unity 6 strips these unused managed getters from this game's interop. The
// registered injected bindings take the native Unity object, as CameraNative does.
// Blittable quaternion storage avoids boxed IL2CPP out/ref value wrappers.
internal static class PlaybackBodyNative
{
    [StructLayout(LayoutKind.Sequential)] private struct RawQuaternion { public float X, Y, Z, W; }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] private delegate bool GetBool(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetBool(IntPtr self, [MarshalAs(UnmanagedType.I1)] bool value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetInt(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetInt(IntPtr self, int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void GetQuaternion(IntPtr self, out RawQuaternion value);
    private static T Resolve<T>(string name) where T : Delegate
    {
        var address = IL2CPP.il2cpp_resolve_icall("UnityEngine.Rigidbody::" + name + "_Injected");
        if (address == IntPtr.Zero) throw new MissingMethodException("Required playback Rigidbody binding: " + name);
        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }
    private static readonly GetBool GetKinematic = Resolve<GetBool>("get_isKinematic");
    private static readonly GetBool GetCollisions = Resolve<GetBool>("get_detectCollisions");
    private static readonly SetBool SetCollisions = Resolve<SetBool>("set_detectCollisions");
    private static readonly GetInt GetInterpolation = Resolve<GetInt>("get_interpolation");
    private static readonly SetInt SetInterpolation = Resolve<SetInt>("set_interpolation");
    private static readonly GetQuaternion GetRotation = Resolve<GetQuaternion>("get_rotation");
    private static IntPtr Pointer(Rigidbody body) => body != null && body.m_CachedPtr != IntPtr.Zero ? body.m_CachedPtr : throw new InvalidOperationException("Playback body destroyed");
    internal static bool Kinematic(Rigidbody body) => GetKinematic(Pointer(body));
    internal static bool Collisions(Rigidbody body) => GetCollisions(Pointer(body));
    internal static void Collisions(Rigidbody body, bool value) => SetCollisions(Pointer(body), value);
    internal static int Interpolation(Rigidbody body) => GetInterpolation(Pointer(body));
    internal static void Interpolation(Rigidbody body, int value) => SetInterpolation(Pointer(body), value);
    internal static Quaternion Rotation(Rigidbody body) { GetRotation(Pointer(body), out var q); return new Quaternion(q.X, q.Y, q.Z, q.W); }
}
