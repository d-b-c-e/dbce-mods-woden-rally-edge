using WodenRallyEdge.Core;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace WodenRallyEdge;

internal sealed class InputLease
{
    private Il2CppReferenceArray<GamePadSystem.Actions>? _actions;
    private readonly Dictionary<int, GamePadSystem.Actions> _original = new();
    private readonly WheelInput _input;
    private readonly double _started = Runtime.Clock.Elapsed.TotalMilliseconds;
    internal float Handbrake { get; }
    private static readonly (string, int)[] Buttons = { ("Gear down", 1), ("Respawn", 2), ("Gear up", 3), ("Lights", 4), ("Camera", 5), ("Next song", 8), ("Horn", 11), ("Records", 12) };
    internal InputLease(Il2CppReferenceArray<GamePadSystem.Actions> actions, float steer, float throttle, float brake, WheelInput input, MainCar car)
    {
        _actions = actions; _input = input;
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
            bool available = Runtime.Devices!.TryAxis(input.Bindings.Handbrake, out float axis);
            var stock = actions[0];
            Handbrake = HandbrakeInput.Amount(input.Bindings.HandbrakeUsesAxis, available, axis, input.Button("Handbrake", false), stock.Pressed || stock.value > .5f);
            Set(0, Handbrake > 0 ? 1 : 0, true);
            if (input.Bindings.Handbrake?.Valid == true) { input.HandbrakeCar = car; input.HandbrakeAmount = Handbrake; }
            foreach (var (name, index) in Buttons)
                if (input.Bindings.Buttons.ContainsKey(name) || input.Bindings.CameraKeys.ContainsKey(name)) Set(index, input.Button(name, false) ? 1 : 0, true);
        }
        catch { Restore(); throw; }
    }
    internal void Restore()
    {
        var actions = _actions; _actions = null; if (actions == null) return;
        _input.HandbrakeCar = null; _input.HandbrakeAmount = 0;
        TimingDiagnostics.ControlsMs = Runtime.Clock.Elapsed.TotalMilliseconds - _started;
        try { foreach (var entry in _original) actions[entry.Key] = entry.Value; }
        catch (Exception ex) { Runtime.Log.LogWarning("Input restore target gone: " + ex.Message); }
    }
}
