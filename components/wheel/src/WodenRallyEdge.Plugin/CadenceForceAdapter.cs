namespace WodenRallyEdge;

// Source candidate seam. Normal construction has no cadence adapter and retains legacy output.
internal interface IForceCadence
{
    bool TryProcess(float structural, float peakPercent, out float command);
    void Reset();
}

#if WODEN_CADENCE_CANDIDATE
// The only algorithm is the shared sidecar. No collision producer exists in Woden.
internal sealed class CadenceForceAdapter : IForceCadence
{
    private readonly Func<double> _outputClock;
    private Dbce.Wheel.Ffb.CadenceImpactMixer? _mixer;
    private double? _end;
    internal CadenceForceAdapter(Func<double> outputClock) => _outputClock = outputClock;
    public void Reset() { _mixer?.Reset(); _end = null; }
    public bool TryProcess(float structural, float peakPercent, out float command)
    {
        command = 0;
        double now = _outputClock();
        if (!double.IsFinite(now) || now < 0 || !float.IsFinite(structural) ||
            !float.IsFinite(peakPercent) || peakPercent < 0 || peakPercent > 50)
        { Reset(); return false; }
        float cap = peakPercent / 100;
        if (_mixer == null || _mixer.PeakLimit != cap)
        {
            _mixer = new(cap, 1.5f, 0); // No event reserve or activation.
            _end = null;
        }
        // Prime with a real timestamp and zero. Never invent an initial interval.
        if (!_end.HasValue) { _end = now; return true; }
        command = _mixer.Mix(structural, _end.Value, now, true, impactInputsVerified: false);
        _end = now;
        if (_mixer.LastInputRejected) { Reset(); command = 0; return false; }
        return true;
    }
}
#endif
