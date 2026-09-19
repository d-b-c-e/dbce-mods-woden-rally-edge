namespace WodenRallyEdge;

internal sealed class FakeToolkitApi : IToolkitForceApi
{
    internal readonly List<string> Calls = new();
    internal int Window;
    internal Guid Guid;
    internal bool AcceptGuards = true;
    public string? Error { get; private set; }
    public bool Initialise(int window, Guid guid) { Window = window; Guid = guid; Calls.Add("init"); return true; }
    public void HoldTimeout(int milliseconds) => Calls.Add("hold:" + milliseconds);
    public bool ExitGuards() { Calls.Add("guards"); if (!AcceptGuards) Error = "fixture: guard denied"; return AcceptGuards; }
    public bool SetForce(int force) { Calls.Add("force:" + force); return true; }
    public void Zero() { Calls.Add("zero"); Error = null; }
    public void Stop() => Calls.Add("stop");
    public void Panic() => Calls.Add("panic");
    public void Shutdown() => Calls.Add("shutdown");
}

internal static class ToolkitForceChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var api = new FakeToolkitApi(); int focusedWindow = 0x1234; Guid guid = Guid.NewGuid();
        var adapter = new ToolkitForceDevice(api, () => focusedWindow, window => window == 0x1234);
        check(adapter.CanOpen, "captures verified game window before closing readers");
        focusedWindow = 0x5678; // Another process takes focus during device work.
        check(adapter.Open(guid), "open succeeds with the retained owned handle");
        check(api.Window == 0x1234 && api.Guid == guid, "exact captured HWND and wheel GUID reach toolkit; foreign foreground is never used");
        check(api.Calls.SequenceEqual(new[] { "init", "hold:150", "guards", "force:0", "zero", "stop" }), "watchdog/exit guard precede the initial zero, with no nonzero test output");
        adapter.Close(); check(api.Calls.Last() == "shutdown", "close goes through toolkit");
        api = new(); adapter = new(api, () => 0, _ => true);
        check(!adapter.CanOpen && !adapter.Open(guid) && api.Calls.Count == 0, "zero/missing window can never enter toolkit fallback");
        api = new(); bool owned = true; adapter = new(api, () => 0x1234, _ => owned);
        check(adapter.CanOpen, "initial window exists"); owned = false;
        check(!adapter.Open(guid) && api.Calls.Count == 0, "destroyed or reassigned handle rejected before native init");
        api = new() { AcceptGuards = false }; adapter = new(api, () => 0x1234, _ => true);
        check(adapter.CanOpen && !adapter.Open(guid), "guard failure still refuses arming");
        check(adapter.Error == "fixture: guard denied", "cleanup cannot erase the original failure");
        check(api.Calls.SequenceEqual(new[] { "init", "hold:150", "guards", "zero", "stop", "shutdown" }), "failed guard zeroes/stops/closes without delivering force");
    }
}
