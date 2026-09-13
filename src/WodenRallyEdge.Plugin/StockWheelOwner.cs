using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using UnityEngine;

namespace WodenRallyEdge;

// The game ships the Logitech SDK. Its live readers and previously downloaded
// effects must relinquish ownership before our DirectInput force route starts.
internal static class StockWheelOwner
{
    internal static bool Held { get; private set; }
    internal static bool Ready { get; private set; }
    internal static string Status { get; private set; } = "Stock wheel support";
    private static readonly Dictionary<int, (MonoBehaviour component, bool enabled)> Saved = new();
    [DllImport("kernel32", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string name);
    [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true)] private static extern IntPtr GetProcAddress(IntPtr module, string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ShutdownSdk();
    internal static void Update()
    {
        bool needed = Runtime.Settings.WheelEnabled || Runtime.Settings.FfbEnabled && Runtime.Force?.Armed == true;
        if (needed && !Held) Acquire();
        else if (!needed && Held) Release(true);
    }
    private static void Acquire()
    {
        Held = true; Ready = false;
        try
        {
            foreach (var component in UnityEngine.Object.FindObjectsOfType<Logitech.SteeringWheelReader>()) Track(component);
            foreach (var component in UnityEngine.Object.FindObjectsOfType<Logitech.LogitechSteeringWheel>()) Track(component);
            // Logitech's official SDK header declares void LogiSteeringShutdown(void).
            // Resolve ONLY an already-loaded game module; never install/load another SDK.
            IntPtr sdk = GetModuleHandleW("LogitechSteeringWheelEnginesWrapper.dll");
            if (sdk == IntPtr.Zero) sdk = GetModuleHandleW("LogitechSteeringWheel.dll");
            if (sdk != IntPtr.Zero)
            {
                IntPtr address = GetProcAddress(sdk, "LogiSteeringShutdown");
                if (address == IntPtr.Zero) { Status = "Cannot stop stock Logitech SDK; mod FFB blocked"; return; }
                Marshal.GetDelegateForFunctionPointer<ShutdownSdk>(address)();
            }
            Ready = true; Status = $"Mod owns wheel route; {Saved.Count} stock reader(s) suspended";
            Runtime.Log.LogInfo(Status);
        }
        catch (Exception ex) { Status = "Stock ownership failed; FFB blocked: " + ex.Message; Runtime.Log.LogError(Status); }
    }
    internal static void Track(MonoBehaviour component)
    {
        if (component == null) return;
        int id = component.GetInstanceID();
        if (!Saved.ContainsKey(id)) Saved.Add(id, (component, component.enabled));
        component.StopAllCoroutines(); component.enabled = false;
    }
    internal static void Release(bool restore)
    {
        if (!Held) return;
        Held = false; Ready = false;
        if (restore)
            foreach (var saved in Saved.Values)
                try { if (saved.component != null) { saved.component.enabled = saved.enabled; if (saved.enabled && saved.component is Logitech.LogitechSteeringWheel demo) demo.Start(); } }
                catch (Exception ex) { Runtime.Log.LogWarning("Stock wheel restore: " + ex.Message); }
        Saved.Clear(); Status = "Stock wheel support";
    }
}

[HarmonyPatch]
internal static class StockWheelHooks
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var type in new[] { typeof(Logitech.SteeringWheelReader), typeof(Logitech.LogitechSteeringWheel) })
            foreach (var name in new[] { "OnEnable", "Start", "FixedUpdate", "OnGUI" })
            { var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (method != null) yield return method; }
    }
    private static bool Prefix(MonoBehaviour __instance)
    {
        if (!StockWheelOwner.Held) return true;
        StockWheelOwner.Track(__instance); return false;
    }
}
