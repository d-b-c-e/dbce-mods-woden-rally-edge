using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Consumer lifecycle policy. The adapter uses only the pinned toolkit's device API.
internal interface IForceDevice
{
    string? Error { get; }
    bool CanOpen { get; }
    bool Open(Guid guid);
    bool Write(float force);
    void ZeroAndStop();
    void Panic();
    void Close();
}

internal sealed class ForceController
{
    private readonly ForceSignal _signal = new();
    private readonly IForceDevice _device;
    private readonly IForceCadence? _cadence;
    private bool _native, _suspended = true, _faulted;
    private Guid _openedGuid;
    internal bool Armed => Runtime.Settings.FfbEnabled && !_faulted && !Runtime.DiagnosticNoForce;
    internal bool Connected => _native;
    internal string Status { get; private set; } = "Ready — feedback starts while driving";
    internal ForceResult Last { get; private set; } = new(false, "No car sample", 0, 0, 0, 0);
    internal float Sent { get; private set; }
    internal long Attempts { get; private set; }
    internal long Failures { get; private set; }
    internal long Opens { get; private set; }
    internal double OpenMilliseconds { get; private set; }
    internal bool? LastAccepted { get; private set; }
    internal ForceController(IForceDevice device, IForceCadence? cadence = null) { _device = device; _cadence = cadence; }

    internal void SetEnabled(bool enabled)
    {
        Runtime.Settings.FfbEnabled = enabled;
        _faulted = false;
        if (!enabled) Release("FFB off");
        else { _signal.Reset(); _cadence?.Reset(); Status = "Ready - feedback starts while driving"; }
        Runtime.Settings.Save();
    }
    internal void Disarm(string reason = "FFB unavailable")
    {
        Release(reason); _faulted = true;
    }
    internal void Reconnect(string reason)
    {
        Release(reason); _faulted = false;
    }
    internal void Panic()
    {
        // F8 is the same persistent Off preference; only an explicit On resumes.
        if (_native) _device.Panic();
        SetEnabled(false);
    }
    private void Release(string reason)
    {
        Suspend(reason);
        if (_native) { _device.Close(); _native = false; Runtime.Devices?.Refresh(); }
        Status = reason;
    }
    internal void Suspend(string reason)
    {
        if (!Runtime.Settings.FfbEnabled) Status = "FFB off";
        else if (!_faulted) Status = reason;
        Sent = 0; LastAccepted = null;
        _signal.Reset();
        _cadence?.Reset();
        if (_native && !_suspended) _device.ZeroAndStop();
        _suspended = true;
        // A pause, camera transition or bad contact is NOT a device disconnect.
        // Re-enumerating here stalled the main thread and invalidated the next
        // sample before the first real force write could be sent.
    }
    internal void Prepare()
    {
        if (Runtime.DiagnosticNoForce) { if (_native) Release("Diagnostic launch: force disabled"); Status = "Diagnostic launch: force disabled"; return; }
        if (!Runtime.Settings.FfbEnabled) { if (_native) Release("FFB off"); Status = "FFB off"; return; }
        if (_faulted || !Runtime.Focused || !StockWheelOwner.Ready || Runtime.Devices == null) return;
        var selection = Runtime.Devices.ResolveForceTarget(Runtime.Settings.FfbFollowSteering, Runtime.Settings.FfbGuid, Runtime.Wheel?.Bindings.Steer?.DeviceGuid);
        if (!selection.Ready) { if (_native) Release(selection.Reason); Status = selection.Reason; return; }
        var guid = selection.Guid!.Value;
        if (_native && guid != _openedGuid) Release("Output device changed");
        if (_native)
        {
            // A shared wheel reader cannot acquire the FFB handle itself. After
            // focus returns, use a zero write to let the toolkit recover access;
            // otherwise missing input would block every force write forever.
            if (!Runtime.Devices.IsReading(guid))
            {
                _signal.Reset();
                if (!_device.Write(0)) { Failures++; Disarm(_device.Error ?? "Wheel disconnected; Refresh to retry"); }
            }
            return;
        }
        // Window readiness is checked before closing readers or touching the
        // device. Waiting for Unity's window must not latch a failure/reconnect.
        if (!_device.CanOpen) { Status = "Waiting for the focused game window"; return; }
        // Open at zero from Update (including menus), never within car sampling.
        // The toolkit enforces exact GUID selection and virtual-device rejection.
        double start = Runtime.Clock.Elapsed.TotalMilliseconds;
        Runtime.Devices.CloseReaders();
        bool ready = false;
        try { Opens++; ready = _device.Open(guid); _native = ready; }
        catch { _device.ZeroAndStop(); _device.Close(); _native = false; _faulted = true; throw; }
        finally { Runtime.Devices.Refresh(); OpenMilliseconds = Runtime.Clock.Elapsed.TotalMilliseconds - start; }
        _native = ready; _openedGuid = guid; _suspended = true; _signal.Reset();
        if (!ready) { Failures++; _faulted = true; Status = _device.Error ?? "FFB open failed; choose On or Refresh to retry"; }
        else Status = "Ready — feedback starts while driving";
    }
    internal void Tick(TelemetrySample sample)
    {
        long resetBefore = _signal.ResetCount;
        Last = _signal.Evaluate(sample, Runtime.Settings.ForceOptions);
        sample.Add("ffb.frontLoad", Last.FrontLoad); sample.Add("ffb.alignmentEstimate", Last.Alignment); sample.Add("ffb.dampingEstimate", Last.Damping);
        sample.Add("ffb.preview", Last.Preview);
        string gate = Runtime.DiagnosticNoForce ? "diagnostic-force-disabled" : !Runtime.Settings.FfbEnabled ? "saved-off" : _faulted ? "faulted" : Panel.Open ? "settings-open" :
            !Runtime.Focused ? "unfocused" : !StockWheelOwner.Ready ? "stock-owner-unready" :
            !MountedCamera.PlayerOwned ? "camera-not-owned" :
            Runtime.Settings.WheelEnabled && !sample.Channels.ContainsKey("wheelInput.steer") ? "wheel-input-unavailable" :
            !Last.Valid ? "model-invalid" : !_native ? "wheel-not-connected" : "active";
        string? blocked = Runtime.DiagnosticNoForce ? "Diagnostic launch: force disabled" : !Runtime.Settings.FfbEnabled ? "FFB off" : _faulted ? Status : Panel.Open ? "FFB inactive — settings open" :
            !Runtime.Focused ? "Unfocused" : !StockWheelOwner.Ready ? StockWheelOwner.Status :
            !MountedCamera.PlayerOwned ? MountedCamera.Status :
            Runtime.Settings.WheelEnabled && !sample.Channels.ContainsKey("wheelInput.steer") ? "Wheel input unavailable" :
            !Last.Valid ? Last.Reason : !_native ? "Waiting for wheel connection" : null;
        if (blocked != null) { Suspend(blocked); Record(sample, gate, resetBefore); return; }
        _suspended = false;
        float command = Last.Preview;
        if (_cadence != null && !_cadence.TryProcess(command, Runtime.Settings.ForceOptions.PeakPercent, out command))
        {
            // A model reset alone cannot cancel an already accepted native command.
            Suspend("Cadence input invalid or stale"); Record(sample, "conditioning-invalid", resetBefore); return;
        }
        Attempts++;
        bool accepted = _device.Write(command);
        LastAccepted = accepted; Sent = accepted ? command : 0;
        if (!accepted)
        {
            string error = _device.Error ?? "Force update failed; choose On or Refresh to retry";
            Failures++; Disarm(error); LastAccepted = false;
            gate = "write-failed";
        }
        else Status = "FFB active";
        Record(sample, gate, resetBefore);
    }
    private void Record(TelemetrySample sample, string gate, long resetBefore)
    {
        sample.ForceStatus = Status;
        sample.ForceGate = gate;
        sample.ForceModelReason = Last.Reason;
        var tuning = Runtime.Settings.ForceOptions;
        sample.Add("ffb.modelValid", Last.Valid ? 1 : 0);
        sample.Add("ffb.modelReason", sample.Discontinuity != null ? 11 : ForceObservationSemantics.ModelReason(Last.Reason));
        sample.Add("ffb.modelResetBefore", resetBefore); sample.Add("ffb.modelResetAfter", _signal.ResetCount);
        sample.Add("ffb.gate", ForceObservationSemantics.Gate(gate));
        sample.Add("ffb.tuning.strengthPercent", tuning.Strength); sample.Add("ffb.tuning.peakPercent", tuning.PeakPercent);
        sample.Add("ffb.tuning.loadReference", tuning.LoadReference); sample.Add("ffb.tuning.slipScale", tuning.SlipScale);
        sample.Add("ffb.tuning.smoothingMs", tuning.SmoothingMs); sample.Add("ffb.tuning.damping", tuning.Damping);
        sample.Add("ffb.tuning.invert", tuning.Invert ? 1 : 0); sample.Add("ffb.tuning.modelVersion", 3);
        sample.Add("ffb.armed", Armed ? 1 : 0); sample.Add("ffb.sent", Sent); sample.Add("ffb.deliveryAttempts", Attempts); sample.Add("ffb.deliveryFailures", Failures);
        sample.Add("ffb.connected", Connected ? 1 : 0); sample.Add("ffb.connectionAttempts", Opens); sample.Add("ffb.lastConnectionMs", OpenMilliseconds);
        if (LastAccepted.HasValue) sample.Add("ffb.accepted", LastAccepted.Value ? 1 : 0);
    }
    internal void Shutdown()
    {
        Sent = 0;
        if (_native) { _device.ZeroAndStop(); _device.Close(); _native = false; }
        _signal.Reset(); _suspended = true; Status = "Stopped";
        _cadence?.Reset();
    }
}
