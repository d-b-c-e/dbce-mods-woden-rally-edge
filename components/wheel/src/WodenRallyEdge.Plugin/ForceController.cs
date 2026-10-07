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
    // Force model 4 (Grip): evaluated every tick so its front-load reference stays current; drives the wheel when selected.
    private readonly GripSignal _grip = new();
    private int _carId, _carEpoch;
    internal string GripStatus { get; private set; } = "waiting for driving";
    private long ResetCount(ForceOptions o) => o.Grip ? _grip.ResetCount : _signal.ResetCount;
    private readonly IForceDevice _device;
    private readonly CommandProvenanceCapture? _capture;
    private readonly object _lifecycleLock = new();
    private int _stopRequested;
    internal bool StopRequested => System.Threading.Volatile.Read(ref _stopRequested) != 0;
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
    internal ForceController(IForceDevice device, CommandProvenanceCapture? capture = null)
    {
        _capture = capture;
        _device = capture == null ? device : new ProvenanceForceDevice(device, capture);
        _capture?.BindController();
    }
    private bool ObserveGuard(string code, bool value) { _capture?.Emit("guard", code, result: value); return value; }
    private void RefreshReaders(bool required = false)
    {
        if (_capture == null) { if (required) Runtime.Devices!.Refresh(); else Runtime.Devices?.Refresh(); }
        else if (required || Runtime.Devices != null) _capture.Call("refresh", _capture.SelectedToken, () => Runtime.Devices!.Refresh());
    }
    private void ResetSignal()
    {
        var selected = Runtime.Settings.ForceOptions;
        long before = ResetCount(selected);
        _signal.Reset(); _grip.Reset();
        _capture?.Emit("reset", "explicit", before: before, after: ResetCount(selected));
    }

    internal void SetEnabled(bool enabled)
    {
        lock (_lifecycleLock) { _capture?.Emit(StopRequested ? "terminal-reject" : "invocation", "setenabled", result: enabled); if (!StopRequested) SetEnabledCore(enabled); }
    }
    private void SetEnabledCore(bool enabled)
    {
        Runtime.Settings.FfbEnabled = enabled;
        _faulted = false;
        if (!enabled) Release("FFB off");
        else { ResetSignal(); Status = "Ready — feedback starts while driving"; }
        Runtime.Settings.Save();
    }
    internal void Disarm(string reason = "FFB unavailable")
    {
        lock (_lifecycleLock) { _capture?.Emit(StopRequested ? "terminal-reject" : "invocation", "disarm"); if (!StopRequested) DisarmCore(reason); }
    }
    private void DisarmCore(string reason)
    {
        Release(reason); _faulted = true;
    }
    internal void Reconnect(string reason)
    {
        lock (_lifecycleLock) { _capture?.Emit(StopRequested ? "terminal-reject" : "invocation", "reconnect"); if (!StopRequested) ReconnectCore(reason); }
    }
    private void ReconnectCore(string reason)
    {
        Release(reason); _faulted = false;
    }
    internal void Panic()
    {
        lock (_lifecycleLock) { _capture?.Emit(StopRequested ? "terminal-reject" : "invocation", "panic"); if (!StopRequested) PanicCore(); }
    }
    private void PanicCore()
    {
        // F8 is the same persistent Off preference; only an explicit On resumes.
        if (_native) _device.Panic();
        SetEnabled(false);
    }
    private void Release(string reason)
    {
        Suspend(reason);
        if (_native) { _device.Close(); _native = false; RefreshReaders(); }
        Status = reason;
    }
    internal void Suspend(string reason)
    {
        lock (_lifecycleLock) { _capture?.Emit(StopRequested ? "terminal-reject" : "invocation", "suspend"); if (!StopRequested) SuspendCore(reason); }
    }
    private void SuspendCore(string reason)
    {
        if (!Runtime.Settings.FfbEnabled) Status = "FFB off";
        else if (!_faulted) Status = reason;
        Sent = 0; LastAccepted = null;
        ResetSignal();
        if (_native && !_suspended) _device.ZeroAndStop();
        _suspended = true;
        // A pause, camera transition or bad contact is NOT a device disconnect.
        // Re-enumerating here stalled the main thread and invalidated the next
        // sample before the first real force write could be sent.
    }
    internal void Prepare()
    {
        lock (_lifecycleLock) {
            _capture?.Emit(StopRequested ? "terminal-reject" : "invocation", "prepare");
            if (!StopRequested) {
                try { PrepareCore(); }
                finally { _capture?.Emit("state", "prepare-ended", result: _native); }
            }
        }
    }
    private void PrepareCore()
    {
        if (ObserveGuard("diagnostic", Runtime.DiagnosticNoForce)) { if (_native) Release("Diagnostic launch: force disabled"); Status = "Diagnostic launch: force disabled"; return; }
        if (!ObserveGuard("enabled", Runtime.Settings.FfbEnabled)) { if (_native) Release("FFB off"); Status = "FFB off"; return; }
        if (ObserveGuard("faulted", _faulted) || !ObserveGuard("focused", Runtime.Focused) || !ObserveGuard("stock-ready", StockWheelOwner.Ready) || !ObserveGuard("hub-present", Runtime.Devices != null)) return;
        var selection = Runtime.Devices!.ResolveForceTarget(Runtime.Settings.FfbFollowSteering, Runtime.Settings.FfbGuid, Runtime.Wheel?.Bindings.Steer?.DeviceGuid);
        _capture?.Selection(selection.Guid, _openedGuid, CommandProvenanceCapture.TargetCode(selection), selection.Ready);
        if (!selection.Ready) { if (_native) Release(selection.Reason); Status = selection.Reason; return; }
        var guid = selection.Guid!.Value;
        if (_native && guid != _openedGuid) { _capture?.Emit("target-change", "changed", _capture.SelectedToken, _capture.Token(_openedGuid)); Release("Output device changed"); }
        if (_native)
        {
            // A shared wheel reader cannot acquire the FFB handle itself. After
            // focus returns, use a zero write to let the toolkit recover access;
            // otherwise missing input would block every force write forever.
            bool reading = _capture == null ? Runtime.Devices.IsReading(guid) :
                _capture.Call("is-reading", _capture.SelectedToken, () => Runtime.Devices.IsReading(guid));
            _capture?.Emit("reader", "is-reading", _capture.SelectedToken, result: reading);
            if (!reading)
            {
                ResetSignal();
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
        if (_capture == null) Runtime.Devices.CloseReaders();
        else _capture.Call("reader-close", _capture.SelectedToken, Runtime.Devices.CloseReaders);
        bool ready = false;
        try { Opens++; ready = _device.Open(guid); _native = ready; }
        catch { _device.ZeroAndStop(); _device.Close(); _native = false; _faulted = true; throw; }
        finally { RefreshReaders(required: true); OpenMilliseconds = Runtime.Clock.Elapsed.TotalMilliseconds - start; }
        _native = ready; _openedGuid = guid; _suspended = true; ResetSignal();
        if (!ready) { Failures++; _faulted = true; Status = _device.Error ?? "FFB open failed; choose On or Refresh to retry"; }
        else Status = "Ready — feedback starts while driving";
    }
    private readonly CrashStage _crash = new();
    internal string CrashStatus { get; private set; } = "waiting for driving";
    internal int CrashCount => _crash.Played;
    /// <summary>A body contact of the local car (collision hook), at Unity physics time.</summary>
    internal void CrashContact(double time, float normalSpeed, float verticalShare, bool road, bool classified)
    {
        lock (_lifecycleLock) _crash.Observe(time, normalSpeed, verticalShare, road, classified, Runtime.Settings.CrashStrength, out _);
    }
    // art of rally's crash cue through the shared-shape CrashStage (crash-constant-fallback@2): calculated on every
    // live tick, delivered only on the active path with the crash kick on, recorded with every contact since the last
    // tick in order (the iRacing force contract 2 review: a strongest-contact summary cannot be replayed).
    private float CrashTick(TelemetrySample sample, bool live, bool deliver, float steering)
    {
        double time = sample.SimulationSeconds;
        bool motion = sample.TryVector("motion.position.world", out var p) & sample.TryVector("motion.velocity.world", out var v);
        var m = new Dbce.Wheel.Ffb.MotionSample { Time = time, X = p.X, Y = p.Y, Z = p.Z, Vx = v.X, Vy = v.Y, Vz = v.Z };
        float output = _crash.Mix(live && motion, m, Runtime.Settings.CrashEnabled, deliver, Runtime.Settings.FfbInvert, steering);
        CrashStatus = !_crash.Live ? (Runtime.Settings.CrashEnabled ? "waiting for driving" : "off") : !Runtime.Settings.CrashEnabled ? "off"
            : _crash.Cue != 0 ? "playing" : CrashCount == 0 ? "ready" : $"ready ({CrashCount} played)";
        sample.Add("crash.modelVersion", CrashStage.ModelVersion); sample.Add("crash.enabled", Runtime.Settings.CrashEnabled ? 1 : 0); sample.Add("crash.strength", Runtime.Settings.CrashStrength);
        sample.Add("crash.cue", _crash.Cue); sample.Add("crash.delivered", _crash.Delivered ? 1 : 0); sample.Add("crash.count", CrashCount);
        sample.Add("crash.epoch", _crash.Epoch); sample.Add("crash.discontinuous", _crash.Discontinuous ? 1 : 0);
        sample.Add("crash.contacts", _crash.TickContacts); sample.Add("crash.contactsDropped", _crash.TickContactsDropped);
        for (int i = 0; i < _crash.TickContacts; i++)
        {
            var c = _crash.TickContact(i); string k = "crash.contact" + i + ".";
            sample.Add(k + "time", c.Time); sample.Add(k + "speed", c.Speed); sample.Add(k + "share", c.Share);
            sample.Add(k + "road", c.Road ? 1 : 0); sample.Add(k + "classified", c.Classified ? 1 : 0); sample.Add(k + "intensity", c.Intensity);
        }
        return output;
    }    internal void Tick(TelemetrySample sample)
    {
        lock (_lifecycleLock) { _capture?.Emit(StopRequested ? "terminal-reject" : "invocation", "tick"); if (!StopRequested) TickCore(sample); }
    }
    private void TickCore(TelemetrySample sample)
    {
        var options = Runtime.Settings.ForceOptions;
        if (sample.CarInstanceId != _carId) { _carId = sample.CarInstanceId; _carEpoch++; _grip.NewCar(); }
        long resetBefore = ResetCount(options);
        _capture?.Options(options);
        var grip = _grip.Evaluate(sample, options);
        Last = options.Grip ? grip : _signal.Evaluate(sample, options);
        var reference = _grip.Reference;
        string measured = reference.Kind == FrontLoadReferenceKind.Resting ? $"front load {reference.Load:F0} measured at rest"
            : reference.Kind == FrontLoadReferenceKind.Provisional ? $"front load {reference.Load:F0} provisional (stop briefly to measure it at rest)" : "measuring the front load (stop briefly, or drive for 2 s)";
        GripStatus = !options.Grip ? "not selected" : grip.Valid ? "grip model, " + measured : grip.Reason;
        sample.Add("ffb.grip.previousOutput", _grip.PreviousOutput); sample.Add("ffb.grip.reference", _grip.ReferenceUsed);
        sample.Add("ffb.grip.referenceKind", (int)reference.Kind); sample.Add("ffb.grip.referenceChanges", reference.Changes); sample.Add("ffb.grip.carEpoch", _carEpoch);
        sample.Add("ffb.grip.preview", grip.Preview); sample.Add("ffb.grip.valid", grip.Valid ? 1 : 0);
        _capture?.Emit("model", "evaluated", command: BitConverter.SingleToInt32Bits(Last.Preview), result: Last.Valid, before: resetBefore, after: ResetCount(options),
            simulation: sample.SimulationSeconds, elapsed: sample.ElapsedSeconds);
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
        _capture?.Emit("gate", gate);
        if (blocked != null) { CrashTick(sample, Last.Valid, false, 0f); sample.Add("ffb.deliveredOutput", 0); Suspend(blocked); Record(sample, gate, resetBefore); return; }
        _suspended = false;
        Attempts++;
        float output = CrashTick(sample, Last.Valid, true, Last.Preview);
        sample.Add("ffb.deliveredOutput", output);
        bool accepted = _device.Write(output);
        LastAccepted = accepted; Sent = accepted ? output : 0;
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
        _capture?.Emit("gate", gate);
        sample.ForceStatus = Status;
        sample.ForceGate = gate;
        sample.ForceModelReason = Last.Reason;
        var tuning = Runtime.Settings.ForceOptions;
        sample.Add("ffb.modelValid", Last.Valid ? 1 : 0);
        sample.Add("ffb.modelReason", sample.Discontinuity != null ? 11 : ForceObservationSemantics.ModelReason(Last.Reason));
        sample.Add("ffb.modelResetBefore", resetBefore); sample.Add("ffb.modelResetAfter", ResetCount(tuning));
        sample.Add("ffb.gate", ForceObservationSemantics.Gate(gate));
        sample.Add("ffb.tuning.strengthPercent", tuning.Strength); sample.Add("ffb.tuning.peakPercent", tuning.PeakPercent);
        sample.Add("ffb.tuning.loadReference", tuning.LoadReference); sample.Add("ffb.tuning.slipScale", tuning.SlipScale);
        sample.Add("ffb.tuning.smoothingMs", tuning.SmoothingMs); sample.Add("ffb.tuning.damping", tuning.Damping);
        sample.Add("ffb.tuning.invert", tuning.Invert ? 1 : 0); sample.Add("ffb.tuning.modelVersion", tuning.Model);
        sample.Add("ffb.tuning.loadRatio", tuning.LoadRatio); sample.Add("ffb.tuning.gripSmoothing", tuning.GripSmoothing);
        sample.Add("ffb.armed", Armed ? 1 : 0); sample.Add("ffb.sent", Sent); sample.Add("ffb.deliveryAttempts", Attempts); sample.Add("ffb.deliveryFailures", Failures);
        sample.Add("ffb.connected", Connected ? 1 : 0); sample.Add("ffb.connectionAttempts", Opens); sample.Add("ffb.lastConnectionMs", OpenMilliseconds);
        if (LastAccepted.HasValue) sample.Add("ffb.accepted", LastAccepted.Value ? 1 : 0);
    }
    internal void BeginRuntime()
    {
        lock (_lifecycleLock)
        {
            _capture?.Restart();
            if (_native) throw new InvalidOperationException("Close the prior runtime before restarting");
            System.Threading.Volatile.Write(ref _stopRequested, 0);
            _faulted = false; _suspended = true; ResetSignal(); Sent = 0;
        }
    }
    internal void Shutdown()
    {
        // Publish stop before waiting for the producer lock, then drain entered calls.
        System.Threading.Interlocked.Exchange(ref _stopRequested, 1);
        _capture?.Emit("state", "terminal-stop-published");
        lock (_lifecycleLock) { _capture?.Emit("invocation", "shutdown"); ShutdownCore(); _capture?.Emit("state", "shutdown-ended", result: !_native); }
    }
    private void ShutdownCore()
    {
        Sent = 0;
        string? error = null;
        if (_native)
        {
            try { _device.ZeroAndStop(); } catch (Exception ex) { error = ex.Message; }
            try { _device.Close(); _native = false; } catch (Exception ex) { error = ex.Message; }
        }
        ResetSignal(); _suspended = true;
        if (error != null) { _faulted = true; Status = "Shutdown failed: " + error; }
        else Status = "Stopped";
    }
}
