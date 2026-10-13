using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

namespace WodenRallyEdge;

// Capture the focused Unity player window before device enumeration can let
// focus move elsewhere. Never pass zero to native 0.5's foreground fallback.
internal static class GameWindow
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(IntPtr hwnd, StringBuilder name, int count);
    internal static bool Eligible(long handle, uint owner, string windowClass, bool visible) =>
        handle != 0 && handle >= int.MinValue && handle <= uint.MaxValue && owner == Environment.ProcessId && visible && windowClass == "UnityWndClass";
    private static bool IsOwned(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return false;
        GetWindowThreadProcessId(hwnd, out uint pid);
        var name = new StringBuilder(128);
        GetClassNameW(hwnd, name, name.Capacity);
        return Eligible(hwnd.ToInt64(), pid, name.ToString(), IsWindowVisible(hwnd));
    }
    internal static int Capture()
    {
        var hwnd = GetForegroundWindow();
        return IsOwned(hwnd) ? unchecked((int)hwnd.ToInt64()) : 0;
    }
    internal static bool IsOwned(int hwnd) => IsOwned(new IntPtr(hwnd));
    internal static bool IsFocused(int hwnd) => hwnd != 0 && Capture() == hwnd;

    // Ordinary input may open while BepInEx's console owns foreground. This
    // fallback is shim-only: physical FFB continues to use Capture() above.
    internal static int CaptureForShim() => SelectShimWindow(GetForegroundWindow(), () =>
    {
        using var process = Process.GetCurrentProcess();
        return process.MainWindowHandle;
    }, IsOwned);

    internal static int SelectShimWindow(IntPtr foreground, Func<IntPtr> mainWindow, Func<IntPtr, bool> owned)
    {
        if (owned(foreground)) return unchecked((int)foreground.ToInt64());
        var main = mainWindow();
        return owned(main) ? unchecked((int)main.ToInt64()) : 0;
    }
}
