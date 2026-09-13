using WodenRallyEdge.Core;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

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
    internal long AppliedTicks { get; private set; }
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
            if (pressed.Length == 1) { Bindings.Buttons[CaptureButton] = pressed[0]; Cancel(); Save(); }
            else if (pressed.Length > 1) Status = "Press one button at a time";
        }
    }
    internal void FinishAxis()
    {
        var b = Capture?.Finish();
        if (b == null || CaptureAxis == null) { Status = "Sweep the required range before saving"; return; }
        Bindings.SetAxis(CaptureAxis, b); Cancel(); Save();
    }
    internal void Cancel() { Capture = null; CaptureAxis = null; CaptureButton = null; Runtime.Devices?.ClearPresses(); }
    internal bool Button(string action, bool edge = true) => Bindings.Buttons.TryGetValue(action, out var b) && Runtime.Devices?.Button(b, edge) == true;
    internal InputLease? Apply(Controls controls)
    {
        var car = controls.field_Private_MainCar_0;
        if (car == null || !Runtime.Driving(car)) return null;
        if (!Runtime.Settings.WheelEnabled || car.MyControls == null || !Bindings.DrivingAxesReady || Runtime.Devices == null) return null;
        var hub = Runtime.Devices;
        if (!hub.TryAxis(Bindings.Steer, out float steer) || !hub.TryAxis(Bindings.Throttle, out float throttle) || !hub.TryAxis(Bindings.Brake, out float brake))
        { Status = "A bound device is not responding; stock controls retained. Use Refresh devices."; return null; }
        var pads = controls.field_Private_GamePadSystem_0?.Game_Pads;
        var pad = pads == null || pads.Count == 0 ? controls.field_Private_Game_Pad_0 :
            controls.Controller_Int >= 0 && controls.Controller_Int < pads.Count ? pads[controls.Controller_Int] : null;
        if (pad?.PadActions == null || pad.PadActions.Length < 18) { Status = "Waiting for Woden action table"; return null; }
        var lease = new InputLease(pad.PadActions, steer, throttle, brake, this);
        _last = new(car.GetInstanceID(), Runtime.Clock.Elapsed.TotalSeconds, steer, throttle, brake);
        if (++AppliedTicks == 1) Runtime.Log.LogInfo("Wheel action-table route active: " + string.Join(", ", new[] { 6, 7, 16, 17 }.Select(i => i + "=" + pad.PadActions[i].name)));
        Status = "Wheel action table active; ticks " + AppliedTicks;
        return lease;
    }
}

internal sealed record AppliedInput(int Car, double At, float Steer, float Throttle, float Brake);

internal sealed class InputLease
{
    private Il2CppReferenceArray<GamePadSystem.Actions>? _actions;
    private readonly Dictionary<int, GamePadSystem.Actions> _original = new();
    internal InputLease(Il2CppReferenceArray<GamePadSystem.Actions> actions, float steer, float throttle, float brake, WheelInput input)
    {
        _actions = actions;
        void Set(int index, float value, bool merge = false)
        {
            // Actions is a non-blittable VALUE array. The indexer boxes a copy;
            // write the changed value back through the array indexer explicitly.
            _original[index] = actions[index];
            var action = actions[index];
            action.value = merge ? Math.Max(action.value, value) : value;
            action.Pressed = merge ? action.Pressed || value > .5f : value > .5f;
            actions[index] = action;
        }
        try
        {
            Set(7, throttle); Set(6, brake); Set(16, Math.Max(0, steer)); Set(17, Math.Max(0, -steer));
            foreach (var (name, index) in new (string, int)[] { ("Handbrake", 0), ("Gear down", 1), ("Respawn", 2), ("Gear up", 3),
                ("Lights", 4), ("Camera", 5), ("Next song", 8), ("Horn", 11), ("Records", 12) })
                if (input.Bindings.Buttons.ContainsKey(name)) Set(index, input.Button(name, false) ? 1 : 0, true);
        }
        catch { Restore(); throw; }
    }
    internal void Restore()
    {
        var actions = _actions; _actions = null; if (actions == null) return;
        try { foreach (var entry in _original) actions[entry.Key] = entry.Value; }
        catch (Exception ex) { Runtime.Log.LogWarning("Input restore target gone: " + ex.Message); }
    }
}
