using System.Globalization;
using BepInEx.Configuration;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

/// <summary>Camera-set choices and offsets. Uses the existing atomic owned-key writer; never ConfigFile.Save.</summary>
internal sealed class CameraFramingStore
{
    private const string Section = "Triple";
    private readonly ConfigFile _config;
    private readonly Dictionary<string, object> _entries = new(StringComparer.Ordinal);
    private readonly ConfigEntry<bool> _bonnet, _bumper, _chase;
    internal CameraFramingStore(ConfigFile config)
    {
        _config = config;
        if (config.SaveOnConfigSet) throw new InvalidOperationException("Camera settings require explicit owned-key saves.");
        CheckLinks();
        // Preserve the framing that an existing global configuration actually showed.
        // A fresh configuration follows STD-046: bonnet Game, bumper Realistic, chase Game.
        bool? legacy = SavedBool("MatchGameFov"), chase = SavedBool("ChaseUsesGameFov");
        _bonnet = Entry("BonnetRealistic", legacy.HasValue ? !legacy.Value : false);
        _bumper = Entry("BumperRealistic", legacy.HasValue ? !legacy.Value : true);
        _chase = Entry("ChaseRealistic", !(legacy.GetValueOrDefault() || (chase ?? true)));
    }
    internal bool Realistic(CameraSlot camera) => Pick(camera).Value;
    private ConfigEntry<bool> Pick(CameraSlot camera) => camera.Mount switch
    {
        MountedView.Bonnet => _bonnet, MountedView.Bumper => _bumper,
        _ when camera.StockPreset < 0 && !camera.Isometric => _chase,
        _ => Entry(camera.Key + "Realistic", _chase.Value)
    };
    internal void SetRealistic(CameraSlot camera, bool value)
    {
        var entry = Pick(camera);
        Save((entry.Definition.Key, value ? "true" : "false"));
        entry.Value = value;
    }
    // The engine's F7 and F6 routes call this same handler. The developer route
    // returns only transient state and never enters a writer.
    internal int? Press(CameraSlot camera, bool developer, int? session, bool curveAvailable)
    {
        if (developer) return TripleGeometry.NextStep(session, curveAvailable);
        SetRealistic(camera, !Realistic(camera));
        return null;
    }
    private static string FovKey(CameraSlot camera, bool realistic) => camera.Key + (realistic ? "Realistic" : "Game") + "OffsetDegrees";
    private static string PositionKey(CameraSlot camera, int car, bool realistic, string component) =>
        "Car" + car.ToString(CultureInfo.InvariantCulture) + "." + camera.Key + (realistic ? ".Realistic." : ".Game.") + component + "Offset";
    internal float FovOffset(CameraSlot camera, bool realistic, float legacyGameOffset = 0) =>
        Entry(FovKey(camera, realistic), realistic ? 0 : legacyGameOffset).Value;
    internal void SetFov(CameraSlot camera, bool realistic, double start, double value)
    {
        float offset = (float)(value - start);
        // Validate finite storage, without clipping/rejecting a player's finite
        // offset according to this car's current lens. The final lens is bounded.
        _ = CameraLens.RequestedFov(start, offset);
        var entry = Entry(FovKey(camera, realistic), 0f);
        Save((entry.Definition.Key, Number(offset)));
        entry.Value = offset;
    }
    internal CameraOffset Position(CameraSlot camera, int car, bool realistic, CameraOffset fallback = default) => new(
        Entry(PositionKey(camera, car, realistic, "Side"), fallback.Side).Value,
        Entry(PositionKey(camera, car, realistic, "Height"), fallback.Height).Value,
        Entry(PositionKey(camera, car, realistic, "Forward"), fallback.Forward).Value,
        Entry(PositionKey(camera, car, realistic, "Pitch"), fallback.Pitch).Value);
    internal void SetPosition(CameraSlot camera, int car, bool realistic, CameraOffset value, CameraOffset fallback = default)
    {
        if (!value.Finite) throw new ArgumentOutOfRangeException(nameof(value), "Camera offsets must be finite.");
        var changes = new List<(string Key, string Value)>();
        var updates = new List<(ConfigEntry<float> Entry, float Value)>();
        Add("Side", value.Side, fallback.Side); Add("Height", value.Height, fallback.Height);
        Add("Forward", value.Forward, fallback.Forward); Add("Pitch", value.Pitch, fallback.Pitch);
        if (changes.Count == 0) return;
        Save(changes.ToArray());
        foreach (var update in updates) update.Entry.Value = update.Value;
        void Add(string component, float next, float initial)
        {
            var entry = Entry(PositionKey(camera, car, realistic, component), initial);
            if (entry.Value == next) return;
            changes.Add((entry.Definition.Key, Number(next))); updates.Add((entry, next));
        }
    }
    internal void Reset(CameraSlot camera, int car, bool realistic)
    {
        var fov = Entry(FovKey(camera, realistic), 0f);
        var positions = new[] { "Side", "Height", "Forward", "Pitch" }.Select(name => Entry(PositionKey(camera, car, realistic, name), 0f)).ToArray();
        Save(new[] { (fov.Definition.Key, "0") }.Concat(positions.Select(entry => (entry.Definition.Key, "0"))).ToArray());
        fov.Value = 0;
        foreach (var entry in positions) entry.Value = 0;
    }
    internal string LastCamera => Entry("LastCamera", "").Value;
    internal void Remember(CameraSlot camera)
    {
        var entry = Entry("LastCamera", "");
        if (entry.Value == camera.Key) return;
        Save((entry.Definition.Key, camera.Key)); entry.Value = camera.Key;
    }
    private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    private bool? SavedBool(string key)
    {
        string? value = ConfigLine.ReadValue(_config.ConfigFilePath, Section, key);
        if (value == null) return null;
        return bool.TryParse(value, out bool parsed) ? parsed : throw new InvalidDataException("Invalid saved camera choice: " + key);
    }
    private ConfigEntry<T> Entry<T>(string key, T fallback)
    {
        if (_entries.TryGetValue(key, out object? existing)) return (ConfigEntry<T>)existing;
        CheckLinks();
        string? value = ConfigLine.ReadValue(_config.ConfigFilePath, Section, key);
        if (value != null) Validate<T>(key, value);
        var entry = _config.Bind(Section, key, fallback, "Saved camera starting choice or offset. FOV offsets are per camera/set; position offsets are per car/camera/set.");
        _entries.Add(key, entry);
        return entry;
    }
    private static void Validate<T>(string key, string value)
    {
        if (typeof(T) == typeof(bool) && !bool.TryParse(value, out _)) throw new InvalidDataException("Invalid saved camera choice: " + key);
        if (typeof(T) == typeof(float) && (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || !float.IsFinite(number)))
            throw new InvalidDataException("Invalid saved camera offset: " + key);
    }
    private void Save(params (string Key, string Value)[] changes)
    {
        CheckLinks();
        // Refuse newly malformed owned values instead of silently replacing them.
        foreach (var change in changes)
        {
            string? value = ConfigLine.ReadValue(_config.ConfigFilePath, Section, change.Key);
            if (value == null || !_entries.TryGetValue(change.Key, out object? entry)) continue;
            if (entry is ConfigEntry<bool>) Validate<bool>(change.Key, value);
            if (entry is ConfigEntry<float>) Validate<float>(change.Key, value);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_config.ConfigFilePath))!);
        ConfigLine.WriteBatch(_config.ConfigFilePath, Section, changes);
    }
    private void CheckLinks()
    {
        for (string? path = Path.GetFullPath(_config.ConfigFilePath); path != null; path = Path.GetDirectoryName(path))
        {
            try { if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked camera settings path refused."); }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
}
