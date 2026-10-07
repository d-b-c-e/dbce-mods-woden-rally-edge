using Dbce.Wheel.Ffb;

namespace WodenRallyEdge.Core;

/// <summary>One body contact as the crash stage saw it, in arrival order.</summary>
/// <param name="Strength">Crash strength (percent) in force when the contact arrived; it scales any cue this contact starts.</param>
public readonly record struct CrashContactRecord(double Time, float Speed, float Share, bool Road, bool Classified, float Intensity, float Strength);

/// <summary>
/// art of rally's crash cue (toolkit <see cref="CrashDetector"/> and <see cref="CrashCue"/>, through the constant-force
/// fallback <c>crash-constant-fallback@2</c>) as one deterministic stage, the same shape as iRacing Arcade's
/// <c>CrashStage</c> (force contract 2): contacts arrive between ticks and are kept in order; <see cref="Mix"/> runs once
/// per force tick, calculates the cue whenever the car is live and adds it only when delivery is allowed.
/// Pure, so an offline replay can run the same code from the recorded contacts and motion.
/// </summary>
public sealed class CrashStage
{
    public const int ModelVersion = 2;
    public const int MaxContactsPerTick = 8;

    private readonly CrashDetector _detector = new();
    private readonly CrashCue _cue = new();
    private readonly CrashContactRecord[] _pending = new CrashContactRecord[MaxContactsPerTick];
    private readonly CrashContactRecord[] _tick = new CrashContactRecord[MaxContactsPerTick];
    private int _pendingCount, _pendingDropped;

    public bool Live { get; private set; }
    public int Epoch => _detector.Epoch;
    public bool Discontinuous => _detector.Discontinuous;
    public int Played => _cue.Played;
    /// <summary>Cue calculated on the last tick, -1..1 (already inverted with the force), delivered or not.</summary>
    public float Cue { get; private set; }
    public bool Delivered { get; private set; }
    /// <summary>Contacts observed between the previous tick and the last one (an immutable snapshot until the next tick).</summary>
    public int TickContacts { get; private set; }
    public int TickContactsDropped { get; private set; }
    public CrashContactRecord TickContact(int index) => _tick[index];

    /// <summary>A body contact of the local car. Returns the cue magnitude it asked for (0 = none).</summary>
    public float Observe(double time, float normalSpeed, float verticalShare, bool road, bool classified, float strengthPercent, out bool played)
    {
        played = false;
        float intensity = Live ? _detector.Observe(time, normalSpeed, verticalShare, road) : 0f;
        if (_pendingCount < MaxContactsPerTick) _pending[_pendingCount++] = new(time, normalSpeed, verticalShare, road, classified, intensity, strengthPercent);
        else _pendingDropped++;
        if (intensity <= 0f) return 0f;
        float magnitude = intensity * Math.Clamp(float.IsFinite(strengthPercent) ? strengthPercent : 0f, 0f, 100f) / 100f;
        played = _cue.Trigger(magnitude, time);
        return magnitude;
    }

    /// <summary>Once per force tick: track the car, render the cue and add it to the steering force (-1..1) when allowed.</summary>
    public float Mix(bool live, in MotionSample motion, bool crashEnabled, bool deliver, bool invert, float steering)
    {
        Array.Copy(_pending, _tick, _pendingCount);
        TickContacts = _pendingCount; TickContactsDropped = _pendingDropped; _pendingCount = _pendingDropped = 0;
        Cue = 0f; Delivered = false;
        live = live && double.IsFinite(motion.Time) && motion.Time > 0;
        if (!live) { if (Live) { _detector.Reset(); _cue.Reset(); } Live = false; return steering; }
        Live = true;
        _detector.Track(motion);
        if (_detector.Discontinuous) _cue.Reset();
        float cue = _cue.Sample(motion.Time);
        Cue = invert ? -cue : cue;
        if (!crashEnabled || !deliver || cue == 0f) return steering;
        Delivered = true;
        return Math.Clamp(steering + Cue, -1f, 1f);
    }

    public void Reset() { _detector.Reset(); _cue.Reset(); Cue = 0f; Delivered = false; }
}