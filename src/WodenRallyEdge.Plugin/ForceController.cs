using Dbce.Wheel.Ffb;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

internal sealed class ForceController
{
    private readonly ForceSignal _signal = new();
    private bool _native, _stoppedByPlayer;
    private double _retryAfter;
    internal bool Armed { get; private set; }
    internal string Status { get; private set; } = "FFB not started for this session";
    internal ForceResult Last { get; private set; } = new(false, "No car sample", 0, 0, 0, 0);
    internal float Sent { get; private set; }
    internal long Attempts { get; private set; }
    internal long Failures { get; private set; }
    internal bool? LastAccepted { get; private set; }
    internal void Arm()
    {
        if (!Runtime.Settings.FfbEnabled) { Status = "Enable force feedback first"; return; }
        if (!Guid.TryParse(Runtime.Settings.FfbGuid, out var guid) || guid == Guid.Empty) { Status = "Choose an FFB wheel first"; return; }
        var result = new DevicePreference { InstanceGuid = guid }.Resolve(WheelFfbNative.ListFfbDevices());
        if (!result.Success) { Status = result.Message; return; }
        Armed = true; _stoppedByPlayer = false; _retryAfter = 0; _signal.Reset(); Status = "Ready — close settings and drive to start feedback";
    }
    internal void Disarm(string reason = "Disarmed") { Armed = false; Suspend(reason); }
    internal void Panic() { Armed = false; _stoppedByPlayer = true; if (_native) WheelFfbNative.Panic(); Suspend("FFB stopped"); }
    internal void Suspend(string reason)
    {
        Status = _stoppedByPlayer ? "FFB stopped — start FFB for this session to resume" : reason; Sent = 0; LastAccepted = null;
        if (!_native) return;
        _signal.Reset();
        // FreeDirectInput releases only the FFB device; reopen readers that shared it.
        WheelFfbNative.Zero(); WheelFfbNative.Stop(); WheelFfbNative.Shutdown(); _native = false;
        Runtime.Devices?.Refresh();
    }
    internal void Tick(TelemetrySample sample)
    {
        Last = _signal.Evaluate(sample, Runtime.Settings.ForceOptions);
        sample.Add("ffb.frontLoad", Last.FrontLoad); sample.Add("ffb.alignmentEstimate", Last.Alignment); sample.Add("ffb.dampingEstimate", Last.Damping);
        sample.Add("ffb.preview", Last.Preview);
        string? blocked = !Runtime.Settings.FfbEnabled ? "FFB off" : !Armed ? "FFB not started for this session" : Panel.Open ? "FFB inactive — settings open" :
            !Runtime.Focused ? "Unfocused" : !StockWheelOwner.Ready ? StockWheelOwner.Status :
            !MountedCamera.PlayerOwned ? MountedCamera.Status :
            Runtime.Settings.WheelEnabled && !sample.Channels.ContainsKey("wheelInput.steer") ? "Wheel input unavailable" : !Last.Valid ? Last.Reason : null;
        if (blocked != null) { Suspend(blocked); Record(sample); return; }
        if (!_native)
        {
            if (Runtime.Clock.Elapsed.TotalSeconds < _retryAfter) { Record(sample); return; }
            _retryAfter = Runtime.Clock.Elapsed.TotalSeconds + 2;
            Runtime.Devices?.CloseReaders();
            bool ready = Guid.TryParse(Runtime.Settings.FfbGuid, out var guid) && WheelFfbNative.Initialise("", -1, 0, guid);
            if (ready)
            {
                WheelFfbNative.HoldTimeout(150);
                ready = WheelFfbNative.ExitGuards();
                if (!ready) WheelFfbNative.Shutdown();
            }
            _native = ready; Runtime.Devices?.Refresh(); _signal.Reset();
            if (!ready) { Status = WheelFfbNative.LastError ?? "FFB open failed"; Failures++; Record(sample); return; }
            Status = "Connected; ramping from zero"; WheelFfbNative.SetForce(0); Record(sample); return;
        }
        Attempts++;
        bool accepted = WheelFfbNative.SetForce((int)Math.Round(Last.Preview * 10000));
        LastAccepted = accepted; Sent = accepted ? Last.Preview : 0;
        if (!accepted) { Failures++; Suspend(WheelFfbNative.LastError ?? "Force update failed"); LastAccepted = false; _retryAfter = Runtime.Clock.Elapsed.TotalSeconds + 2; }
        else Status = "FFB active";
        Record(sample);
    }
    private void Record(TelemetrySample sample)
    {
        var tuning = Runtime.Settings.ForceOptions;
        sample.Add("ffb.tuning.strengthPercent", tuning.Strength); sample.Add("ffb.tuning.peakPercent", tuning.PeakPercent);
        sample.Add("ffb.tuning.loadReference", tuning.LoadReference); sample.Add("ffb.tuning.slipScale", tuning.SlipScale);
        sample.Add("ffb.tuning.smoothingMs", tuning.SmoothingMs); sample.Add("ffb.tuning.damping", tuning.Damping);
        sample.Add("ffb.tuning.invert", tuning.Invert ? 1 : 0); sample.Add("ffb.tuning.modelVersion", 1);
        sample.Add("ffb.armed", Armed ? 1 : 0); sample.Add("ffb.sent", Sent); sample.Add("ffb.deliveryAttempts", Attempts); sample.Add("ffb.deliveryFailures", Failures);
        if (LastAccepted.HasValue) sample.Add("ffb.accepted", LastAccepted.Value ? 1 : 0);
    }
    internal void Shutdown()
    {
        Armed = false; Sent = 0;
        if (_native) { WheelFfbNative.Zero(); WheelFfbNative.Stop(); WheelFfbNative.Shutdown(); _native = false; }
        _signal.Reset(); Status = "Stopped";
    }
}
