using System.Text.Json;
using Dbce.Wheel.Ffb;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

internal sealed record AxisBinding(Guid DeviceGuid, int Axis, AxisCalibration Calibration);
internal sealed record WheelBindings(AxisBinding Steer, AxisBinding Throttle, AxisBinding Brake);

internal sealed class WheelInput : IDisposable
{
    private readonly WheelBindings _bindings;
    private readonly Dictionary<Guid, int> _slots = new();
    private readonly Dictionary<Guid, int[]> _axes = new();
    private readonly byte[] _buttons = new byte[128];
    private bool _failed;
    internal WheelInput(string directory, string path)
    {
        _bindings = JsonSerializer.Deserialize<WheelBindings>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new IOException("Empty bindings");
        if (_bindings.Steer == null || _bindings.Throttle == null || _bindings.Brake == null) throw new IOException("All three wheel axes must be bound");
        foreach (var b in new[] { _bindings.Steer, _bindings.Throttle, _bindings.Brake })
            if (b.DeviceGuid == Guid.Empty || b.Axis is < 0 or > 7 || b.Calibration?.Valid != true) throw new IOException("Invalid GUID/axis/calibration");
        if (_bindings.Steer.Calibration.Centre == null || _bindings.Throttle.Calibration.Centre != null || _bindings.Brake.Calibration.Centre != null)
            throw new IOException("Steer needs a centre; pedals use rest/end only");
        if (!WheelFfbNative.Load(directory)) throw new IOException(WheelFfbNative.LastError);
        // Read slots are nonexclusive. Never Initialise(), create effects or send force.
        try
        {
            var devices = WheelFfbNative.ListAllDevices();
            foreach (var guid in new[] { _bindings.Steer.DeviceGuid, _bindings.Throttle.DeviceGuid, _bindings.Brake.DeviceGuid }.Distinct())
            {
                var matching = devices.Where(x => x.InstanceGuid == guid).ToArray();
                if (matching.Length != 1) throw new IOException("Exact bound device missing or ambiguous: " + guid);
                var d = matching[0];
                if (new[] { "vjoy", "vigem", "xoutput", "vxbox" }.Any(x => d.Name.Contains(x, StringComparison.OrdinalIgnoreCase))) throw new IOException("Virtual device rejected");
                int slot = WheelFfbNative.OpenRead(d.Index);
                if (slot < 0) throw new IOException("Cannot open " + d.Name);
                _slots.Add(guid, slot); _axes.Add(guid, new int[8]);
                Runtime.Log.LogInfo($"Wheel read slot: {d.Name}, GUID={guid}. No FFB acquisition.");
            }
        }
        catch { Dispose(); throw; }
    }
    internal InputLease? Apply(MainCar car)
    {
        if (_failed || car.MyControls == null) return null;
        foreach (var slot in _slots)
            if (!WheelFfbNative.Read(slot.Value, _axes[slot.Key], _buttons))
            {
                _failed = true;
                Runtime.Log.LogWarning("Wheel disconnected/read failed; stock controls retained. Restart to reopen calibrated devices.");
                return null;
            }
        float Read(AxisBinding b) => (float)b.Calibration.Normalize(_axes[b.DeviceGuid][b.Axis]);
        return new(car.MyControls, Read(_bindings.Steer), Read(_bindings.Throttle), Read(_bindings.Brake));
    }
    public void Dispose() { _failed = true; WheelFfbNative.CloseRead(); }
}

internal sealed class InputLease
{
    private Controls? _controls;
    private readonly float _stockSteer, _stockThrottle, _stockBrake;
    internal float Steer { get; }
    internal float Throttle { get; }
    internal float Brake { get; }
    internal InputLease(Controls controls, float steer, float throttle, float brake)
    {
        _controls = controls;
        _stockSteer = controls.Steering_float; _stockThrottle = controls.Pedal_Acc; _stockBrake = controls.Pedal_Bra;
        Steer = steer; Throttle = throttle; Brake = brake;
        try { controls.Steering_float = steer; controls.Pedal_Acc = throttle; controls.Pedal_Bra = brake; }
        catch { Restore(); throw; }
    }
    internal void Restore()
    {
        var c = _controls; _controls = null;
        if (c == null) return;
        try { c.Steering_float = _stockSteer; c.Pedal_Acc = _stockThrottle; c.Pedal_Bra = _stockBrake; }
        catch (Exception ex) { Runtime.Log.LogWarning("Input restore target gone: " + ex.Message); }
    }
}
