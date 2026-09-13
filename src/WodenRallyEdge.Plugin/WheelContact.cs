using Il2CppInterop.Runtime;
using UnityEngine;

namespace WodenRallyEdge;

internal static class WheelContact
{
    // BE #788 generates WheelHit as a boxed non-blittable value type (it contains
    // a Collider reference). Its generated out wrapper incorrectly offers only
    // an IntPtr-sized stack slot to native code, which overwrites adjacent memory.
    // Allocate the real IL2CPP value and pass its unboxed storage directly by ref.
    // The game-build guard pins this Unity VehiclesModule method token.
    private static readonly WheelHit Buffer = new();
    private static readonly IntPtr Method = IL2CPP.GetIl2CppMethodByToken(Il2CppClassPointerStore<WheelCollider>.NativeClassPtr, 100663315);
    private static long _reads;
    internal static unsafe bool Read(WheelCollider wheel, out WheelHit hit)
    {
        if (Method == IntPtr.Zero) throw new InvalidOperationException("GetGroundHit method unavailable");
        IntPtr storage = IL2CPP.il2cpp_object_unbox(Buffer.Pointer);
        if (storage == IntPtr.Zero) throw new InvalidOperationException("WheelHit storage unavailable");
        void** args = stackalloc void*[1]; args[0] = (void*)storage;
        IntPtr exception = IntPtr.Zero;
        IntPtr result = IL2CPP.il2cpp_runtime_invoke(Method, wheel.Pointer, args, ref exception);
        Il2CppException.RaiseExceptionIfNecessary(exception);
        if (result == IntPtr.Zero) throw new InvalidOperationException("GetGroundHit returned no boxed boolean");
        bool grounded = *(byte*)IL2CPP.il2cpp_object_unbox(result) != 0;
        hit = Buffer; GC.KeepAlive(Buffer);
        if (++_reads == 1)
        {
            uint alignment = 0; int size = IL2CPP.il2cpp_class_value_size(Il2CppClassPointerStore<WheelHit>.NativeClassPtr, ref alignment);
            Runtime.Log.LogInfo($"WheelHit contact read succeeded using {size}-byte IL2CPP value storage (alignment {alignment})");
        }
        return grounded;
    }
}
