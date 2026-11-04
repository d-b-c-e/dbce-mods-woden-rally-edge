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
    /// <summary>Load supplied defaults because no camera keys were saved (not saved).</summary>
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
    /// <summary>A driving axis as F6 captures it: valid, steering about a centre, pedals from rest. The guided setup
    /// asks for the same thing, so it never reports Ready for bindings the wheel route refuses.</summary>
    public bool DrivingAxisReady(string name) => Axis(name) is { Valid: true } b && b.Calibration.Centre.HasValue == (name == "Steer");
    public bool DrivingAxesReady => DrivingAxisReady("Steer") && DrivingAxisReady("Throttle") && DrivingAxisReady("Brake");
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
        // Persisted keys are user choices, including layouts equal to earlier defaults.
        if (!hadKeys) { bindings.CameraKeys = CameraTuning.DefaultKeys(); bindings.CameraKeysMigrated = true; }
        return bindings;
    }
    public void Save(string path) => SaveJson(path, JsonSerializer.Serialize(this, Json));
    public void SaveDeadzone(string path, string axis, AxisBinding previous)
    {
        var updated = Axis(axis) ?? throw new IOException("Missing edited axis");
        if (!updated.Valid || (updated with { Calibration = updated.Calibration with { Deadzone = previous.Calibration.Deadzone } }) != previous)
            throw new IOException("Deadzone save cannot change the axis binding");
        if (!File.Exists(path)) { Save(path); return; }
        string full = Path.GetFullPath(path);
        if ((File.GetAttributes(full) & (FileAttributes.ReparsePoint | FileAttributes.ReadOnly)) != 0)
            throw new IOException("Linked or read-only bindings refused");
        // Deny content writers while retaining the snapshot through replacement.
        // Independently renaming editors must still be excluded by the caller.
        using var input = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (input.Length > 1024 * 1024) throw new IOException("Oversized bindings");
        byte[] before;
        using (var copy = new MemoryStream()) { input.CopyTo(copy); before = copy.ToArray(); }
        int bom = before.Length >= 3 && before[0] == 0xef && before[1] == 0xbb && before[2] == 0xbf ? 3 : 0;
        using var document = JsonDocument.Parse(before.AsMemory(bom)); // Validate every byte, not just the owned object.
        var axisSpan = Member(before.AsSpan(bom), axis);
        if (axisSpan.Start < 0) throw new IOException("Missing saved " + axis);
        var axisBytes = before.AsSpan(bom + axisSpan.Start, axisSpan.End - axisSpan.Start);
        var calibrationSpan = Member(axisBytes, "Calibration");
        if (calibrationSpan.Start < 0) throw new IOException("Missing saved Calibration");
        if (JsonSerializer.Deserialize<AxisBinding>(axisBytes, Json) != previous)
            throw new IOException("Saved axis changed; reopen the deadzone editor");
        int calibrationStart = bom + axisSpan.Start + calibrationSpan.Start;
        var deadzoneSpan = Member(before.AsSpan(calibrationStart, calibrationSpan.End - calibrationSpan.Start), "Deadzone");
        if (updated.Calibration.Deadzone == previous.Calibration.Deadzone) return;
        byte[] value = JsonSerializer.SerializeToUtf8Bytes(updated.Calibration.Deadzone);
        int start, end;
        if (deadzoneSpan.Start >= 0) {
            start = calibrationStart + deadzoneSpan.Start; end = calibrationStart + deadzoneSpan.End;
        } else {
            start = end = calibrationStart + deadzoneSpan.Close;
            value = System.Text.Encoding.UTF8.GetBytes((deadzoneSpan.HasMembers ? "," : "") + "\"Deadzone\":" + System.Text.Encoding.UTF8.GetString(value));
        }
        byte[] after = new byte[start + value.Length + before.Length - end];
        before.AsSpan(0, start).CopyTo(after);
        value.CopyTo(after, start);
        before.AsSpan(end).CopyTo(after.AsSpan(start + value.Length));
        string temp = full + ".tmp-" + Guid.NewGuid().ToString("N");
        bool owned = false;
        try {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                owned = true; stream.Write(after, 0, after.Length); stream.Flush(true);
            }
            if (!File.ReadAllBytes(full).AsSpan().SequenceEqual(before)) throw new IOException("Saved bindings changed; reopen the editor");
            string backup = full + ".bak";
            if (File.Exists(backup) || Directory.Exists(backup)) backup += "." + Guid.NewGuid().ToString("N");
            File.Replace(temp, full, backup, true);
            owned = false;
        } finally {
            if (owned) try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    // Locate one immediate member using JSON tokens; escaped property names and
    // nested lookalikes cannot make a string search edit the wrong value.
    private static (int Start, int End, int Close, bool HasMembers) Member(ReadOnlySpan<byte> json, string name)
    {
        var reader = new Utf8JsonReader(json);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) throw new IOException("Expected saved object " + name);
        int start = -1, end = -1; bool members = false;
        while (reader.Read()) {
            if (reader.TokenType == JsonTokenType.EndObject) return (start, end, checked((int)reader.TokenStartIndex), members);
            if (reader.TokenType != JsonTokenType.PropertyName) throw new IOException("Invalid saved object");
            members = true;
            bool match = string.Equals(reader.GetString(), name, StringComparison.OrdinalIgnoreCase);
            if (!reader.Read()) throw new IOException("Missing saved value");
            int a = checked((int)reader.TokenStartIndex); reader.Skip();
            if (match) {
                if (start >= 0) throw new IOException("Ambiguous saved " + name);
                start = a; end = checked((int)reader.BytesConsumed);
            }
        }
        throw new IOException("Unterminated saved object");
    }
    private static void SaveJson(string path, string json)
    {
        string full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        string temp = full + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temp, json);
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
        (_steering ? $"Sweep both ends. Raw {_min} .. {_max}; center {_rest}." : $"Raw rest {_rest}; observed {_min} .. {_max}.");
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
