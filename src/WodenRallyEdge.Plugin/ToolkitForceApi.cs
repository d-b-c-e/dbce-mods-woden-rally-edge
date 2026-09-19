using Dbce.Wheel.Ffb;

namespace WodenRallyEdge;

internal interface IToolkitForceApi
{
    string? Error { get; }
    bool Initialise(int window, Guid guid);
    void HoldTimeout(int milliseconds);
    bool ExitGuards();
    bool SetForce(int force);
    void Zero();
    void Stop();
    void Panic();
    void Shutdown();
}

// All device/output operations stay in the pinned toolkit. This thin boundary
// also lets the real window/guard adapter be exercised without a physical wheel.
internal sealed class ToolkitForceApi : IToolkitForceApi
{
    public string? Error => WheelFfbNative.LastError;
    public bool Initialise(int window, Guid guid) => WheelFfbNative.Initialise("", -1, window, guid);
    public void HoldTimeout(int milliseconds) => WheelFfbNative.HoldTimeout(milliseconds);
    public bool ExitGuards() => WheelFfbNative.ExitGuards();
    public bool SetForce(int force) => WheelFfbNative.SetForce(force);
    public void Zero() => WheelFfbNative.Zero();
    public void Stop() => WheelFfbNative.Stop();
    public void Panic() => WheelFfbNative.Panic();
    public void Shutdown() => WheelFfbNative.Shutdown();
}
