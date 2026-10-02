namespace WodenRallyEdge;

// Consumer callback ownership: stop is published before waiting for entered callbacks.
// Native cleanup runs only after StopAndDrain returns, outside this monitor.
internal sealed class RuntimeActivityGate
{
    private readonly object _sync = new();
    private int _stopped = 1;
    private Action? _afterDrain;
    private bool _cleanupSet;
    private bool _cleanupClaimed, _cleanupDone;
    internal bool Stopped => System.Threading.Volatile.Read(ref _stopped) != 0;
    internal void BeginRuntime()
    {
        lock (_sync)
        {
            if (!Stopped || (_cleanupSet && !_cleanupDone)) throw new InvalidOperationException("Drain and finish the previous runtime before starting");
            _cleanupSet = _cleanupClaimed = _cleanupDone = false; _afterDrain = null;
            System.Threading.Volatile.Write(ref _stopped, 0);
        }
    }
    internal IDisposable? TryEnter()
    {
        System.Threading.Monitor.Enter(_sync);
        if (Stopped) { System.Threading.Monitor.Exit(_sync); return null; }
        return new Lease(this);
    }
    internal void StopAndDrain(Action? cleanup = null)
    {
        System.Threading.Interlocked.Exchange(ref _stopped, 1);
        lock (_sync) { if (!_cleanupSet) { _cleanupSet = true; _afterDrain = cleanup; } }
        RunCleanupOutsideCallback();
    }
    private void RunCleanupOutsideCallback()
    {
        // A same-thread nested quit cannot drain its own callback. Its outermost
        // lease runs cleanup after leaving the monitor; external callers drain by acquiring it.
        if (System.Threading.Monitor.IsEntered(_sync)) return;
        Action? cleanup;
        lock (_sync)
        {
            if (!_cleanupSet || _cleanupClaimed) return;
            _cleanupClaimed = true; cleanup = _afterDrain; _afterDrain = null;
        }
        try { cleanup?.Invoke(); } finally { lock (_sync) { _cleanupDone = true; } }
    }
    private sealed class Lease : IDisposable
    {
        private RuntimeActivityGate? _owner;
        internal Lease(RuntimeActivityGate owner) => _owner = owner;
        public void Dispose()
        {
            var owner = _owner; _owner = null;
            if (owner != null) { System.Threading.Monitor.Exit(owner._sync); owner.RunCleanupOutsideCallback(); }
        }
    }
}
