using WodenRallyEdge.Core;
using UnityEngine.InputSystem;

namespace WodenRallyEdge;

internal sealed class WheelInput
{
    internal static readonly string[] ButtonActions = { "Gear up", "Gear down", "Handbrake", "Camera", "Rear view", "Lights", "Horn", "Respawn", "Pause", "Settings panel", "Panic stop", "Records", "Next song" };
    private readonly string _path;
    internal Bindings Bindings { get; private set; } = new();
    internal string Status { get; private set; } = "Ready to bind";
    internal AxisCapture? Capture { get; private set; }
    internal string? CaptureAxis { get; private set; }
    internal string? CaptureButton { get; private set; }
    private double _captureDeadline, _captureAfter;
    private AppliedInput? _last;
    internal MainCar? HandbrakeCar;
    internal float HandbrakeAmount;
    internal long AppliedTicks { get; private set; }
    internal long PreRaceTicks { get; private set; }
    internal AppliedInput? LastFor(MainCar car) => _last is { } last && last.Car == car.GetInstanceID() && Runtime.Clock.Elapsed.TotalSeconds - last.At < .1 ? last : null;
    internal WheelInput(string path)
    {
        _path = path;
        try { Bindings = Bindings.Load(path); } catch (Exception ex) { Status = "Bindings could not load: " + ex.Message; }
    }
    internal void Save() { try { Bindings.Save(_path); Status = "Bindings saved"; } catch (Exception ex) { Status = "Save failed: " + ex.Message; } }
    internal void BeginAxis(string name)
    {
        Cancel(); Runtime.Force?.Suspend("calibration"); Runtime.Devices?.Poll();
        CaptureAxis = name; Capture = new(Runtime.Devices!.AxesSnapshot(), name == "Steer");
        _captureDeadline = Runtime.Clock.Elapsed.TotalSeconds + 30;
    }
    internal void BeginButton(string name)
    {
        Cancel(); CaptureButton = name; _captureDeadline = Runtime.Clock.Elapsed.TotalSeconds + 20;
        _captureAfter = Runtime.Clock.Elapsed.TotalSeconds + .3; Runtime.Devices?.ClearPresses();
    }
    internal void UpdateCapture()
    {
        if (Capture == null && CaptureButton == null) return;
        if (Runtime.Clock.Elapsed.TotalSeconds > _captureDeadline) { Cancel(); Status = "Binding timed out; previous binding retained"; return; }
        Capture?.Observe(Runtime.Devices!.AxesSnapshot());
        if (CaptureButton != null && Runtime.Clock.Elapsed.TotalSeconds > _captureAfter)
        {
            var pressed = Runtime.Devices!.PressedButtons().ToArray();
            bool camera = CameraTuning.Actions.Contains(CaptureButton);
            var kb = Keyboard.current;
            var keys = camera && kb != null ? CameraShortcuts.BindableKeys.Where(k => kb[k].wasPressedThisFrame).ToArray() : Array.Empty<Key>();
            if (pressed.Length == 1 && keys.Length == 0)
            {
                Bindings.Buttons[CaptureButton] = pressed[0];
                if (camera) Bindings.CameraKeys[CaptureButton] = "None";
                if (CaptureButton == "Handbrake") Bindings.HandbrakeUsesAxis = false;
                Cancel(); Save();
            }
            else if (keys.Length == 1 && pressed.Length == 0)
            {
                string action = CaptureButton, key = keys[0].ToString();
                foreach (string other in Bindings.CameraKeys.Where(x => x.Value == key).Select(x => x.Key).ToArray()) Bindings.CameraKeys[other] = "None";
                Bindings.CameraKeys[action] = key; Bindings.Buttons.Remove(action); Cancel(); Save();
            }
            else if (pressed.Length > 1) Status = "Press one button at a time";
        }
    }
    internal void FinishAxis()
    {
        var b = Capture?.Finish();
        if (b == null || CaptureAxis == null) { Status = "Sweep the required range before saving"; return; }
        Bindings.SetAxis(CaptureAxis, b); if (CaptureAxis == "Handbrake") Bindings.HandbrakeUsesAxis = true; Cancel(); Save();
    }
    internal void Cancel() { Capture = null; CaptureAxis = null; CaptureButton = null; Runtime.Devices?.ClearPresses(); }
    internal bool Button(string action, bool edge = true) => (Bindings.Buttons.TryGetValue(action, out var b) && Runtime.Devices?.Button(b, edge) == true) || CameraShortcuts.KeyPressed(action, edge);
    internal InputLease? Apply(Controls controls)
    {
        var car = controls.field_Private_MainCar_0;
        if (car == null || !Runtime.ControlState(car).WheelAvailable) return null;
        if (!Runtime.Settings.WheelEnabled || car.MyControls == null || !Bindings.DrivingAxesReady || Runtime.Devices == null) return null;
        var hub = Runtime.Devices;
        if (!hub.TryAxis(Bindings.Steer, out float steer) || !hub.TryAxis(Bindings.Throttle, out float throttle) || !hub.TryAxis(Bindings.Brake, out float brake))
        { Status = "A bound device is not responding; stock controls retained. Use Refresh devices."; return null; }
        var actions = ControlActions.For(controls);
        if (actions == null || actions.Length < 18) { Status = "Waiting for Woden action table"; return null; }
        var lease = new InputLease(actions, steer, throttle, brake, this, car);
        _last = new(car.GetInstanceID(), Runtime.Clock.Elapsed.TotalSeconds, steer, throttle, brake, lease.Handbrake);
        if (++AppliedTicks == 1) Runtime.Log.LogInfo("Wheel action-table route active: " + string.Join(", ", new[] { 6, 7, 16, 17 }.Select(i => i + "=" + actions[i].name)));
        if (car.Status == MainCar.CarStatus.WARMING && ++PreRaceTicks == 1)
            Runtime.Log.LogInfo("Countdown wheel route active: throttle, steering and bound controls reach native input; game start-line lock retained, FFB remains gated");
        Status = "Wheel action table active; ticks " + AppliedTicks;
        return lease;
    }
}

internal sealed record AppliedInput(int Car, double At, float Steer, float Throttle, float Brake, float Handbrake);
