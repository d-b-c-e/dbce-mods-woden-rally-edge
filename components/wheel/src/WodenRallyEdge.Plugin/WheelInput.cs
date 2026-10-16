using WodenRallyEdge.Core;
using UnityEngine.InputSystem;

namespace WodenRallyEdge;

internal sealed class WheelInput
{
    internal static readonly string[] ButtonActions = { "Gear up", "Gear down", "Handbrake", "Camera", "Rear view", "Lights", "Horn", "Respawn", "Pause", "Settings panel", "Panic stop", "Records", "Next song",
        "Gear 1", "Gear 2", "Gear 3", "Gear 4", "Gear 5", "Gear 6", "Gear R" };
    /// <summary>H-pattern gate actions, in gate order (R last).</summary>
    internal static readonly string[] GateActions = { "Gear 1", "Gear 2", "Gear 3", "Gear 4", "Gear 5", "Gear 6", "Gear R" };
    internal readonly HPatternShifter Shifter = new();
    /// <summary>Any gate is bound: the H-pattern drives the gearbox (sequential shift buttons still work out of gear).</summary>
    internal bool HPatternBound => ShimInput.Owns ? ShimInput.HPattern : GateActions.Any(Bindings.Buttons.ContainsKey);
    // The force wheel's follow-steering identity: the shared profile's steering once the shim owns input; none while
    // shared input is requested but not owning and not actually refused (a stale legacy wheel never opens); the native
    // Steer binding otherwise (Native backend, or the shim refused with Native retained).
    internal Guid? SteeringGuid => ShimInput.Owns ? ShimInput.SteeringGuid : ShimInput.PendingShared ? null : Bindings.Steer?.DeviceGuid;
    internal string? SteeringReason =>
        ShimInput.Owns ? (ShimInput.SteeringGuid.HasValue ? null : "The shared controls profile has no usable steering binding. Check it and restart.")
        : ShimInput.StoppedBeforeOpen ? "Shared input stopped before opening; the force wheel follows its steering after a restart."
        : ShimInput.PendingShared ? "Waiting for the shared controls profile to open; the force wheel follows its steering binding."
        : null;
    /// <summary>The held gate: 1..6, <see cref="HPatternShifter.Reverse"/>, or 0 out of gear.</summary>
    internal int HeldGate(MainCar? car = null)
    {
        if (ShimInput.Owns)
        {
            // Gear gates are physical levels. The native Manual selection uses
            // the current stick position, as the existing Native backend does.
            bool neutral = Button("Neutral", false, car);
            int selected = 0, count = 0;
            for (int i = 0; i < GateActions.Length; i++)
                if (Button(GateActions[i], false, car)) { selected = i == 6 ? HPatternShifter.Reverse : i + 1; count++; }
            return neutral || count != 1 ? 0 : selected;
        }
        if (Button("Gear R", false)) return HPatternShifter.Reverse;
        for (int i = 0; i < HPatternShifter.MaxGates; i++) if (Button(GateActions[i], false)) return i + 1;
        return 0;
    }
    private readonly string _path;
    internal Bindings Bindings { get; private set; } = new();
    internal string Status { get; private set; } = "Ready to bind";
    internal AxisCapture? Capture { get; private set; }
    internal string? CaptureAxis { get; private set; }
    internal string? CaptureButton { get; private set; }
    private double _captureDeadline, _captureAfter;
    internal float CaptureDeadzone;
    internal bool CaptureInvert;
    private AxisBinding? _deadzoneBinding;
    internal bool EditingDeadzone => _deadzoneBinding != null;
    private Action<Bindings>? _pendingEdit;
    internal bool SavePending => _pendingEdit != null;
    internal bool Capturing => Capture != null || CaptureButton != null || EditingDeadzone || SavePending;
    internal string? SaveError { get; private set; }
    internal AxisBinding? PreviewBinding
    {
        get
        {
            var b = _deadzoneBinding ?? Capture?.Finish(); if (b == null) return null;
            var c = b.Calibration with { Deadzone = CaptureDeadzone / 100 };
            return EditingDeadzone ? b with { Calibration = c } : b with { Calibration = c, Inverted = CaptureInvert };
        }
    }
    private AppliedInput? _last;
    internal MainCar? HandbrakeCar;
    internal float HandbrakeAmount;
    internal long AppliedTicks { get; private set; }
    internal long PreRaceTicks { get; private set; }
    internal AppliedInput? LastFor(MainCar car) => _last is { } last && last.Car == car.GetInstanceID() && Runtime.Clock.Elapsed.TotalSeconds - last.At < .1 ? last : null;
    internal WheelInput(string path)
    {
        _path = path;
        try
        {
            Bindings = Bindings.Load(path);
            if (Bindings.CameraKeysMigrated) Runtime.Log.LogInfo("Camera keys: default numpad layout is 8/2 forward/back, 9/3 up/down, 4/6 left/right, 7/1 tilt, +/- FOV, 5 reset (untouched old defaults moved)");
        }
        catch (Exception ex) { Status = "Bindings could not load: " + ex.Message; SaveError = Status; }
    }
    internal void Save() => TryCommit(_ => { });
    internal bool TryCommit(Action<Bindings> edit)
    {
        var proposed = Bindings.Copy();
        try
        {
            edit(proposed); proposed.Save(_path);
            Bindings = proposed; _pendingEdit = null; Status = "Bindings saved"; SaveError = null; return true;
        }
        catch (Exception ex) { _pendingEdit = edit; Status = "Save failed: " + ex.Message + ". Retry save or Cancel to retain the previous binding."; SaveError = Status; return false; }
    }
    internal void RetrySave()
    {
        if (_pendingEdit == null) return;
        if (CaptureAxis != null) { FinishAxis(); return; }
        if (TryCommit(_pendingEdit)) Cancel();
    }
    internal void BeginAxis(string name, bool existing = false)
    {
        Cancel(); Runtime.Force?.Suspend("calibration"); Runtime.Devices?.Poll();
        var snapshot = Runtime.Devices!.AxesSnapshot(); var old = Bindings.Axis(name);
        if (existing && old != null) snapshot = snapshot.Where(x => x.Key == (old.DeviceGuid, old.Axis)).ToDictionary(x => x.Key, x => x.Value);
        CaptureAxis = name; Capture = new(snapshot, name == "Steer");
        CaptureDeadzone = existing && old != null ? (float)old.Calibration.Deadzone * 100 : name == "Steer" ? 0 : 3; CaptureInvert = false;
        Status = "Calibration is provisional. Cancel keeps your previous assignment.";
        _captureDeadline = Runtime.Clock.Elapsed.TotalSeconds + 30;
    }
    internal void BeginDeadzone(string name)
    {
        Cancel();
        var binding = Bindings.Axis(name);
        if (binding?.Valid != true) { Status = "Bind the control before editing its deadzone."; return; }
        Runtime.Force?.Suspend("calibration");
        _deadzoneBinding = binding; CaptureAxis = name;
        CaptureDeadzone = (float)binding.Calibration.Deadzone * 100;
        Status = "Only the deadzone changes. Saved endpoints and inversion stay unchanged. Save deadzone applies this draft.";
        _captureDeadline = Runtime.Clock.Elapsed.TotalSeconds + 30;
    }
    internal void BeginButton(string name)
    {
        Cancel(); Status = "Press one button or camera key. Escape cancels."; CaptureButton = name; _captureDeadline = Runtime.Clock.Elapsed.TotalSeconds + 20;
        _captureAfter = Runtime.Clock.Elapsed.TotalSeconds + .3; Runtime.Devices?.ClearPresses(); MenuOwnership.BeginCapture();
    }
    internal void UpdateCapture()
    {
        if (SavePending) return; // Keep the exact failed proposal until explicit Retry/Cancel.
        if (Capture == null && CaptureButton == null && !EditingDeadzone) return;
        if (Runtime.Clock.Elapsed.TotalSeconds > _captureDeadline) { Cancel(); Status = "Binding timed out; previous binding retained"; return; }
        Capture?.Observe(Runtime.Devices!.AxesSnapshot());
        if (CaptureButton != null && !MenuOwnership.CaptureReady()) { Status = "Release keys and menu controls before binding. Escape cancels."; return; }
        if (CaptureButton != null && Runtime.Clock.Elapsed.TotalSeconds > _captureAfter)
        {
            var pressed = Runtime.Devices!.PressedButtons().ToArray();
            bool camera = CameraTuning.Actions.Contains(CaptureButton);
            var kb = Keyboard.current;
            if (camera && kb != null && (kb[Key.F6].wasPressedThisFrame || kb[Key.F8].wasPressedThisFrame))
                Status = "F6 and F8 are reserved for Settings and Stop FFB. Press another key.";
            var keys = camera && kb != null ? CameraShortcuts.BindableKeys.Where(k => kb[k].wasPressedThisFrame).ToArray() : Array.Empty<Key>();
            if (pressed.Length == 1 && keys.Length == 0)
            {
                string action = CaptureButton;
                if (TryCommit(proposed => { proposed.Buttons[action] = pressed[0]; if (camera) proposed.CameraKeys[action] = "None"; })) Cancel();
            }
            else if (keys.Length == 1 && pressed.Length == 0)
            {
                string action = CaptureButton, key = keys[0].ToString();
                if (CameraShortcuts.ModifierHeld) { Status = "Chords and modifier keys are unsupported. Release them and press one key."; return; }
                var conflict = Bindings.Conflict(action, key: key);
                if (conflict != null) { Status = "Already assigned to " + conflict + ". Clear that binding first."; return; }
                if (TryCommit(proposed => { proposed.CameraKeys[action] = key; proposed.Buttons.Remove(action); })) Cancel();
            }
            else if (pressed.Length + keys.Length > 1) Status = "Press one key or button at a time; chords are unsupported.";
        }
    }
    internal void FinishAxis()
    {
        var b = PreviewBinding;
        if (b == null || CaptureAxis == null) { Status = "Sweep the required range before saving"; return; }
        if (!EditingDeadzone) Runtime.Devices?.Poll();
        if (!EditingDeadzone && Runtime.Devices?.TryAxis(b, out _) != true)
        { Status = "Calibration device disconnected. Reconnect and retry, or Cancel to keep the saved binding."; return; }
        string action = CaptureAxis;
        if (TryCommit(proposed => proposed.SetAxis(action, b))) Cancel();
    }
    internal void Cancel() { if (SavePending) SaveError = null; _pendingEdit = null; _deadzoneBinding = null; Capture = null; CaptureAxis = null; CaptureButton = null; Runtime.Devices?.ClearPresses(); }
    internal bool Button(string action, bool edge = true, MainCar? car = null) => (ShimInput.Owns ? ShimInput.Button(action, edge, car) :
        Bindings.Buttons.TryGetValue(action, out var b) && Runtime.Devices?.Button(b, edge, action) == true) || CameraShortcuts.KeyPressed(action, edge);
    internal InputLease? Apply(Controls controls)
    {
        _last = null; HandbrakeCar = null; HandbrakeAmount = 0;
        var car = controls.field_Private_MainCar_0;
        if (car == null || !Runtime.ControlState(car).WheelAvailable) return null;
        if (!Runtime.Settings.WheelEnabled || car.MyControls == null) return null;
        if (ShimInput.Owns) return ApplyShim(controls, car);
        if (!Bindings.DrivingAxesReady || Runtime.Devices == null) return null;
        var hub = Runtime.Devices;
        if (!hub.TryAxis(Bindings.Steer, out float steer) || !hub.TryAxis(Bindings.Throttle, out float throttle) || !hub.TryAxis(Bindings.Brake, out float brake))
        { Status = "A bound device is not responding; stock controls retained. Use Refresh devices."; return null; }
        var actions = ControlActions.For(controls);
        if (actions == null || actions.Length < 18) { Status = "Waiting for Woden action table"; return null; }
        bool shiftUp = false, shiftDown = false;
        if (HPatternBound)
        {
            int gate = HeldGate();
            bool automatic = car.Aids?.Automatic == true;
            (throttle, brake) = HPatternShifter.Pedals(gate, throttle, brake, automatic);
            (shiftUp, shiftDown) = Shifter.Step(gate, car.MyGear, car.Gears, automatic, car.TransmissionSystem?.Changing == true, Runtime.Clock.Elapsed.TotalSeconds);
        }
        var lease = new InputLease(actions, steer, throttle, brake, this, car, shiftUp, shiftDown);
        _last = new(car.GetInstanceID(), Runtime.Clock.Elapsed.TotalSeconds, steer, throttle, brake, lease.Handbrake);
        if (++AppliedTicks == 1) Runtime.Log.LogInfo("Wheel action-table route active: " + string.Join(", ", new[] { 6, 7, 16, 17 }.Select(i => i + "=" + actions[i].name)));
        if (car.Status == MainCar.CarStatus.WARMING && ++PreRaceTicks == 1)
            Runtime.Log.LogInfo("Countdown wheel route active: throttle, steering and bound controls reach native input; game start-line lock retained, FFB remains gated");
        Status = "Wheel action table active; ticks " + AppliedTicks;
        return lease;
    }
    private InputLease? ApplyShim(Controls controls, MainCar car)
    {
        ShimInput.Poll();
        if (!ShimInput.Ready) { Status = ShimInput.Status; return null; }
        var actions = ControlActions.For(controls);
        if (actions == null || actions.Length < 18) return null;
        // The wheel-only admission means these are native keyboard actions, not
        // a duplicate reading of the wheel or an unrelated pad. Restore afterwards.
        float wheelThrottle = ShimInput.Axis("throttle"), wheelBrake = ShimInput.Axis("brake");
        bool shiftUp = false, shiftDown = false;
        int gate = HeldGate(car);
        if (ShimInput.HPatternReady(car) && ShimInput.AxesLive)
        {
            (wheelThrottle, wheelBrake) = HPatternShifter.Pedals(gate, wheelThrottle, wheelBrake);
            (shiftUp, shiftDown) = Shifter.Step(gate, car.MyGear, car.Gears, false, car.TransmissionSystem?.Changing == true,
                Runtime.Clock.Elapsed.TotalSeconds);
        }
        else Shifter.Stop();
        // The gear emulator applies only to wheel pedals. Native keyboard input
        // is composed afterward, retaining its own throttle, brake and shifting.
        var (steer, throttle, brake) = ShimActionMap.Compose(ShimInput.Axis("steer"), wheelThrottle, wheelBrake,
            actions[16].value - actions[17].value, actions[7].value, actions[6].value);
        var lease = new InputLease(actions, steer, throttle, brake, this, car, shiftUp, shiftDown, shim: true);
        if (ShimInput.AxesLive) _last = new(car.GetInstanceID(), Runtime.Clock.Elapsed.TotalSeconds, steer, throttle, brake, lease.Handbrake);
        AppliedTicks++; Status = ShimInput.Status;
        return lease;
    }
}

internal sealed record AppliedInput(int Car, double At, float Steer, float Throttle, float Brake, float Handbrake);
