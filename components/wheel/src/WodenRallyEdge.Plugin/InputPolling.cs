using UnityEngine;

namespace WodenRallyEdge;

// Startup consumers may run before Lifecycle.Update. Share that frame's normal
// reader poll so bound Settings is fresh without adding a second device read.
internal static class InputPolling
{
    private static int _frame = -1;
    private static Exception? _failure;
    internal static void OncePerFrame()
    {
        if (_frame == Time.frameCount)
        {
            if (_failure != null) throw new InvalidOperationException("Input poll failed this frame", _failure);
            return;
        }
        _frame = Time.frameCount;
        _failure = null;
        double started = Runtime.Clock.Elapsed.TotalMilliseconds;
        try { Runtime.Devices?.Poll(); }
        catch (Exception ex) { _failure = ex; throw; }
        TimingDiagnostics.PollMs = Runtime.Clock.Elapsed.TotalMilliseconds - started;
    }
}
