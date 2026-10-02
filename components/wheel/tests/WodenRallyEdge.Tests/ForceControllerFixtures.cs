using System.Diagnostics;
using WodenRallyEdge.Core;

// Compile the actual consumer controller against a fake game context and fake
// device. No Unity process, DirectInput DLL or physical output is loaded here.
namespace WodenRallyEdge;

internal static class Runtime
{
    internal static TestSettings Settings = new();
    internal static WheelInput? Wheel = new();
    internal static TestDevices? Devices = new();
    internal static bool Focused = true, DiagnosticNoForce;
    internal static readonly Stopwatch Clock = Stopwatch.StartNew();
    internal static readonly TestLog Log = new();
    internal static MainCar? Local;
    internal static bool Select(MainCar car) => car.Selected;
}
internal sealed class TestLog { internal void LogWarning(string message) { } internal void LogInfo(string message) { } }
internal sealed class TestSettings
{
    internal bool FfbEnabled = true, WheelEnabled = true, FfbFollowSteering = false;
    internal bool CountdownAssistEnabled;
    internal float CountdownSpeed = 75;
    internal string FfbGuid = Guid.NewGuid().ToString();
    internal ForceOptions Options = new();
    internal ForceOptions ForceOptions => Options;
    internal int Saves;
    internal void Save() => Saves++;
}
internal sealed class TestDevices
{
    internal int Refreshes, Closes;
    internal bool Readable = true;
    internal ForceCandidate[]? Candidates;
    internal ForceTarget ResolveForceTarget(bool follow, string id, Guid? steer) => ForceSelection.Resolve(follow, id, steer,
        Candidates ?? (Guid.TryParse(id, out var guid) ? new[] { new ForceCandidate(guid, "Fixture wheel", true) } : Array.Empty<ForceCandidate>()));
    internal bool IsReading(Guid guid) => Readable;
    internal bool TryAxis(AxisBinding? binding, out float value) { value = 0; return false; }
    internal void Refresh() => Refreshes++;
    internal void CloseReaders() => Closes++;
}
internal static class Panel { internal static bool Open; }
internal static class StockWheelOwner { internal static bool Ready = true; internal static string Status => "Stock ownership unresolved"; }
internal static class MountedCamera { internal static bool PlayerOwned = true; internal static string Status => "Camera takeover"; }

internal sealed class FakeForceDevice : IForceDevice
{
    internal int Opens, Closes, Zeros, Panics;
    internal readonly List<float> Writes = new();
    internal bool AcceptOpen = true, AcceptWrite = true;
    public bool CanOpen { get; set; } = true;
    public string? Error => "Fake driver failure";
    public bool Open(Guid guid) { Opens++; return AcceptOpen; }
    public bool Write(float value) { Writes.Add(value); return AcceptWrite; }
    public void ZeroAndStop() => Zeros++;
    public void Panic() => Panics++;
    public void Close() => Closes++;
}
internal static class ForceControllerChecks
{
    internal static (ForceController controller, FakeForceDevice device) Create()
    {
        Runtime.Settings = new(); Runtime.Devices = new(); Runtime.Focused = true; Runtime.DiagnosticNoForce = false;
        Panel.Open = false; StockWheelOwner.Ready = true; MountedCamera.PlayerOwned = true;
        var device = new FakeForceDevice(); return (new(device), device);
    }
    internal static void Recovery(Action<bool, string> check, Func<double, TelemetrySample> contact)
    {
        var (controller, device) = Create();
        controller.Prepare();
        check(device.Opens == 1 && device.Writes.Count == 0, "saved On prepares at zero without a session start");
        double time = 1;
        for (int i = 0; i < 80; i++) { controller.Prepare(); controller.Tick(contact(time += .02)); }
        check(device.Writes.Any(x => Math.Abs(x) > .01), "first drive reaches nonzero fake output");
        var observed = contact(time += .02); controller.Tick(observed);
        check(observed.Get("ffb.modelValid") == 1 && observed.Get("ffb.gate") == 0 &&
            observed.Get("ffb.modelResetBefore") == observed.Get("ffb.modelResetAfter"), "active sample records valid model/gate/reset semantics");
        int refreshes = Runtime.Devices!.Refreshes, writes = device.Writes.Count;
        var gap = contact(time += .02); gap.Discontinuity = "wall-time-gap"; controller.Tick(gap);
        check(gap.Get("ffb.modelValid") == 0 && gap.Get("ffb.modelReason") == 11 && gap.Get("ffb.gate") == 9 &&
            gap.Get("ffb.modelResetAfter") - gap.Get("ffb.modelResetBefore") == 2, "discontinuity records model reset plus gate reset in stream order");
        for (int i = 0; i < 20; i++) { controller.Suspend("camera transition"); controller.Prepare(); }
        check(device.Opens == 1 && device.Closes == 0 && Runtime.Devices.Refreshes == refreshes, "transient gates never reconnect or re-enumerate");
        check(device.Writes.Count == writes && controller.Sent == 0 && device.Zeros == 1, "suspension zeroes once and never sends stale nonzero force");
        controller.Tick(contact(time += .02));
        check(Math.Abs(device.Writes.Last()) < .01, "recovery restarts ramp near zero");
        for (int i = 0; i < 80; i++) controller.Tick(contact(time += .02));
        check(Math.Abs(device.Writes.Last()) > .01 && device.Opens == 1, "recovery reaches force with the same connection");
        Runtime.Focused = false; controller.Suspend("Unfocused"); controller.Prepare();
        Runtime.Devices.Readable = false; Runtime.Focused = true; controller.Prepare();
        check(device.Writes.Last() == 0 && device.Opens == 1, "focus recovery reacquires shared reader using zero, without reconnecting");
        Runtime.Devices.Readable = true;
        controller.Panic();
        check(!Runtime.Settings.FfbEnabled && Runtime.Settings.Saves == 1 && device.Panics == 1, "panic saves Off");
        for (int i = 0; i < 10; i++) { controller.Prepare(); controller.Tick(contact(time += .02)); }
        check(device.Opens == 1 && controller.Status == "FFB off", "normal gates cannot clear panic Off");
        controller.SetEnabled(true); controller.Prepare(); controller.Tick(contact(time += .02));
        check(device.Opens == 2 && Runtime.Settings.FfbEnabled, "the On control deliberately resumes");
        controller.Shutdown(); check(device.Closes == 2, "shutdown closes active output");
    }
    internal static void Failures(Action<bool, string> check, Func<double, TelemetrySample> contact)
    {
        var (controller, device) = Create(); device.AcceptOpen = false;
        for (int i = 0; i < 100; i++) controller.Prepare();
        check(device.Opens == 1 && controller.Failures == 1, "failed init does not retry and stall every two seconds");
        device.AcceptOpen = true; controller.SetEnabled(true); controller.Prepare();
        device.AcceptWrite = false; controller.Tick(contact(1));
        for (int i = 0; i < 50; i++) { controller.Prepare(); controller.Tick(contact(1.02 + i * .02)); }
        check(device.Opens == 2 && controller.Failures == 2 && controller.Sent == 0, "driver failure latches without a reconnect loop");
        check(controller.Status.Contains("Fake driver failure"), "gating preserves actionable error");
        controller.Shutdown();
        (controller, device) = Create(); Runtime.DiagnosticNoForce = true;
        controller.Prepare(); controller.Tick(contact(1));
        check(device.Opens == 0 && device.Writes.Count == 0 && Runtime.Settings.FfbEnabled, "agent diagnostic suppression preserves saved On");
        Runtime.DiagnosticNoForce = false; Runtime.Settings.FfbGuid = ""; controller.Prepare();
        check(device.Opens == 0, "missing exact selection never falls back");
    }
}
