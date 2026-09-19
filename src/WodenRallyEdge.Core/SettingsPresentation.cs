namespace WodenRallyEdge.Core;

// Presentation policy has no access to devices, forces, streams or bindings.
public static class SettingsPresentation
{
    public static readonly string[] CorePages = { "Setup", "Controls", "FFB", "Cameras", "Telemetry", "Help" };
    public static string View(string? view) => view == "Advanced" ? "Advanced" : "Simple";
    public static string Page(string? page, string view) => (page != null && CorePages.Contains(page)) || page == "Driving" && View(view) == "Advanced" ? page! : "Setup";
    public static string[] Pages(string view) => View(view) == "Advanced" ? new[] { "Setup", "Controls", "FFB", "Cameras", "Driving", "Telemetry", "Help" } : CorePages;
    public const string EditLock = "Finish or cancel the current edit to change view.";
}

public sealed record ForceTarget(Guid? Guid, string Reason)
{
    public bool Ready => Guid.HasValue;
}
public sealed record ForceCandidate(Guid Guid, string Name, bool ForceFeedback, bool Virtual = false);
public static class ForceSelection
{
    public static ForceTarget Resolve(bool followSteering, string explicitGuid, Guid? steering, IEnumerable<ForceCandidate> candidates)
    {
        Guid guid;
        if (followSteering)
        {
            if (!steering.HasValue || steering == Guid.Empty) return new(null, "Bind Steering in Controls to select its wheel.");
            guid = steering.Value;
        }
        else if (!Guid.TryParse(explicitGuid, out guid) || guid == Guid.Empty) return new(null, "Select an FFB device.");
        var matches = candidates.Where(c => c.Guid == guid).ToArray();
        if (matches.Length == 0) return new(null, "Saved device disconnected. Reconnect it and Refresh.");
        if (matches.Length != 1) return new(null, "Device identity is ambiguous. Refresh or select another wheel.");
        if (matches[0].Virtual) return new(null, "Virtual devices cannot receive FFB. Select a physical wheel.");
        if (!matches[0].ForceFeedback) return new(null, "Selected device has no FFB. Select a force-feedback wheel.");
        return new(guid, matches[0].Name);
    }
}

// A blocked context consumes a hold. Resume only after release; never catch up
// missed repeats after a frame stall. Reset uses the same gate without repeat.
public sealed class ShortcutRepeat
{
    private bool _held, _blocked;
    private double _next;
    public bool Tick(bool held, bool allowed, double now, bool repeat = true)
    {
        if (!allowed) { _blocked = held; _held = held; return false; }
        if (!held) { _held = false; _blocked = false; return false; }
        if (_blocked) return false;
        if (!_held) { _held = true; _next = now + .35; return true; }
        if (!repeat || now < _next) return false;
        _next = now + .1; return true;
    }
}
