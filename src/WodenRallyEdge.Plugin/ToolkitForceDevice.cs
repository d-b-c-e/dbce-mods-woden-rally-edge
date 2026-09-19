namespace WodenRallyEdge;

internal sealed class ToolkitForceDevice : IForceDevice
{
    private readonly IToolkitForceApi _api;
    private readonly Func<int> _captureWindow;
    private readonly Func<int, bool> _ownsWindow;
    private int _window;
    private string? _error;
    public string? Error => _error ?? _api.Error;
    public bool CanOpen { get { _window = _captureWindow(); return _window != 0; } }
    internal ToolkitForceDevice() : this(new ToolkitForceApi(), GameWindow.Capture, GameWindow.IsOwned) { }
    internal ToolkitForceDevice(IToolkitForceApi api, Func<int> captureWindow, Func<int, bool> ownsWindow)
    { _api = api; _captureWindow = captureWindow; _ownsWindow = ownsWindow; }
    public bool Open(Guid guid)
    {
        _error = null;
        if (_window == 0 || !_ownsWindow(_window)) { _error = "Game window changed; choose On or Refresh to retry"; return false; }
        Runtime.Log.LogInfo($"FFB opening on verified Unity game window 0x{_window:X8}, process {Environment.ProcessId}");
        if (!_api.Initialise(_window, guid)) { _error = _api.Error; return false; }
        _api.HoldTimeout(150);
        if (_api.ExitGuards() && _api.SetForce(0)) { ZeroAndStop(); return true; }
        _error = _api.Error ?? "FFB exit guard or initial zero failed";
        ZeroAndStop(); _api.Shutdown(); return false;
    }
    public bool Write(float force) => _api.SetForce((int)Math.Round(force * 10000));
    public void ZeroAndStop() { _api.Zero(); _api.Stop(); }
    public void Panic() => _api.Panic();
    public void Close() => _api.Shutdown();
}
