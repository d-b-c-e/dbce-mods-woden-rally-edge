using System.Text.Json;

namespace WodenRallyEdge.Core;

public sealed record AxisBinding(Guid DeviceGuid, int Axis, AxisCalibration Calibration)
{
    public bool Inverted { get; init; }
    public double Normalize(int raw) => (Inverted ? Calibration with { Rest = Calibration.End, End = Calibration.Rest } : Calibration).Normalize(raw);
    public bool Valid => DeviceGuid != Guid.Empty && Axis is >= 0 and < 8 && Calibration?.Valid == true;
}
public sealed record ButtonBinding(Guid DeviceGuid, int Button)
{
    // Explicit profile semantics. Existing F6 eight-way bindings remain exact.
    public bool HatNeighbours { get; init; }
    public bool Valid => DeviceGuid != Guid.Empty && Dbce.Wheel.Ffb.DigitalInput.Valid(Button) && (!HatNeighbours || Button >= 128);
}
public sealed class Bindings
{
    public int Version { get; set; } = 1;
    public AxisBinding? Steer { get; set; }
    public AxisBinding? Throttle { get; set; }
    public AxisBinding? Brake { get; set; }
    public AxisBinding? Handbrake { get; set; }
    public bool HandbrakeUsesAxis { get; set; }
    public Dictionary<string, ButtonBinding> Buttons { get; set; } = new();
    public Dictionary<string, string> CameraKeys { get; set; } = CameraTuning.DefaultKeys();
    /// <summary>Load moved untouched old camera defaults to the current layout (not saved).</summary>
    [System.Text.Json.Serialization.JsonIgnore] public bool CameraKeysMigrated { get; set; }
    public string? Conflict(string action, ButtonBinding? button = null, string? key = null)
    {
        // One physical control may intentionally serve several actions. Assignment
        // never removes another action; the consumer owns context dispatch.
        if (button != null) return null;
        return key == null || key == "None" ? null : CameraKeys.FirstOrDefault(x => x.Key != action && x.Value.Equals(key, StringComparison.OrdinalIgnoreCase)).Key;
    }
    private static bool SharedContext(string a, string b)
    {
        bool Global(string s) => s is "Settings panel" or "Panic stop" or "Pause";
        bool Menu(string s) => s is "Confirm" or "Back" || s.StartsWith("Menu ", StringComparison.Ordinal);
        return Global(a) || Global(b) || Menu(a) == Menu(b);
    }
    public Bindings Copy() => new() { Version = Version, Steer = Steer, Throttle = Throttle, Brake = Brake, Handbrake = Handbrake,
        HandbrakeUsesAxis = HandbrakeUsesAxis, Buttons = new(Buttons), CameraKeys = new(CameraKeys) };
    public bool DrivingAxesReady => Steer?.Valid == true && Steer.Calibration.Centre.HasValue &&
        Throttle?.Valid == true && !Throttle.Calibration.Centre.HasValue && Brake?.Valid == true && !Brake.Calibration.Centre.HasValue;
    public AxisBinding? Axis(string name) => name switch { "Steer" => Steer, "Throttle" => Throttle, "Brake" => Brake, "Handbrake" => Handbrake, _ => null };
    public void SetAxis(string name, AxisBinding? binding)
    {
        if (binding != null && !binding.Valid) throw new ArgumentException("Invalid axis binding");
        switch (name) { case "Steer": Steer = binding; break; case "Throttle": Throttle = binding; break; case "Brake": Brake = binding; break; case "Handbrake": Handbrake = binding; break; default: throw new ArgumentException(name); }
    }
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    public static Bindings Load(string path)
    {
        if (!File.Exists(path)) return new();
        string json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json);
        bool hadKeys = document.RootElement.EnumerateObject().Any(p => p.Name.Equals("CameraKeys", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind != JsonValueKind.Null);
        var bindings = JsonSerializer.Deserialize<Bindings>(json, Json) ?? throw new IOException("Empty bindings");
        if (bindings.Version != 1) throw new IOException("Unsupported binding version");
        foreach (var name in new[] { "Steer", "Throttle", "Brake", "Handbrake" })
            if (bindings.Axis(name) is { } b && !b.Valid) throw new IOException("Invalid saved " + name + " calibration");
        if (bindings.Buttons == null || bindings.Buttons.Any(x => x.Value?.Valid != true)) throw new IOException("Invalid saved button binding");
        // No saved keys, or an untouched earlier default set: the current family layout.
        if (!hadKeys) { bindings.CameraKeys = CameraTuning.DefaultKeys(); bindings.CameraKeysMigrated = true; }
        else bindings.CameraKeysMigrated = CameraTuning.MigratePreviousDefaults(bindings.CameraKeys);
        return bindings;
    }
    public void Save(string path)
    {
        string full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        string temp = full + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(this, Json));
            if (File.Exists(full)) File.Replace(temp, full, full + ".bak", true); else File.Move(temp, full);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

// Captures rest/centre first. Only one moving axis is accepted. Steering then
// requires BOTH endpoints, so binding with half a turn cannot silently calibrate it.
public sealed class AxisCapture
{
    private readonly Dictionary<(Guid guid, int axis), int> _baseline;
    private (Guid guid, int axis)? _selected;
    private readonly bool _steering;
    private int _min, _max, _rest;
    public bool Ambiguous { get; private set; }
    public bool Detected => _selected.HasValue;
    public int Minimum => _min;
    public int Maximum => _max;
    public string Status => Ambiguous ? "Several axes moved. Cancel, release controls, and try again." : !Detected ?
        (_steering ? "Turn the wheel right first, then sweep fully left/right." : "Move the control fully, then release it.") :
        (_steering ? $"Sweep both ends. Raw {_min} .. {_max}; centre {_rest}." : $"Raw rest {_rest}; observed {_min} .. {_max}.");
    private int _direction;
    public AxisCapture(Dictionary<(Guid, int), int> baseline, bool steering) { _baseline = new(baseline); _steering = steering; }
    public void Observe(Dictionary<(Guid guid, int axis), int> values)
    {
        if (Ambiguous) return;
        if (!_selected.HasValue)
        {
            var moved = values.Where(x => _baseline.TryGetValue(x.Key, out int rest) && Math.Abs(x.Value - rest) >= 8000).ToArray();
            if (moved.Length > 1) { Ambiguous = true; return; }
            if (moved.Length == 0) return;
            _selected = moved[0].Key; _rest = _baseline[_selected.Value]; _min = Math.Min(_rest, moved[0].Value); _max = Math.Max(_rest, moved[0].Value);
            _direction = Math.Sign(moved[0].Value - _rest);
        }
        if (values.TryGetValue(_selected.Value, out int value)) { _min = Math.Min(_min, value); _max = Math.Max(_max, value); }
    }
    public AxisBinding? Finish()
    {
        if (!_selected.HasValue || Ambiguous) return null;
        AxisCalibration calibration;
        if (_steering)
        {
            if (_rest - _min < 8000 || _max - _rest < 8000) return null;
            calibration = new(_direction > 0 ? _min : _max, _direction > 0 ? _max : _min, _rest);
        }
        else
        {
            int far = _direction > 0 ? _max : _min;
            if (Math.Abs(far - _rest) < 8000) return null;
            calibration = new(_rest, far);
        }
        return new(_selected.Value.guid, _selected.Value.axis, calibration);
    }
}
