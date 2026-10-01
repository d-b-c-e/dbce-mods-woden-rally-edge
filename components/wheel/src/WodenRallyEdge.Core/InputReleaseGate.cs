namespace WodenRallyEdge.Core;

// Shared by panel handoff and binding capture. Unknown/stale input is not neutral.
public sealed class InputReleaseGate
{
    private double? _neutralSince;
    public bool Ready { get; private set; }
    public void Reset() { _neutralSince = null; Ready = false; }
    public bool Observe(bool known, bool held, double now)
    {
        if (!known || held || !double.IsFinite(now)) { Reset(); return false; }
        if (_neutralSince == null || now < _neutralSince) _neutralSince = now;
        return Ready = now - _neutralSince >= .1;
    }
}
