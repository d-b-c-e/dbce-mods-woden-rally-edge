using Dbce.Wheel.Ffb;

namespace WodenRallyEdge.Core;

/// <summary>Where the grip model's full-scale load comes from.</summary>
public enum FrontLoadReferenceKind { None = 0, Provisional = 1, Resting = 2 }

/// <summary>
/// The front axle's reference load for the grip model's full scale, observed on every valid tick apart from driving
/// and output. Same rules as iRacing Arcade's GripReference (Codex review 2026-10-06): <b>resting</b> = both fronts
/// grounded, under 0.5 m/s, for 0.5 s of continuous ticks within 5% of their mean, kept for the car; until then a
/// <b>provisional</b> duration-weighted mean over at least 2 s of grounded ticks; before that, none.
/// </summary>
public sealed class FrontLoadReference
{
    public const double StillMs = .5, RestingSeconds = .5, RestingSpread = .05, ProvisionalSeconds = 2, MaxGapSeconds = .1;
    private double _last = -1, _stillStart = -1, _stillSum, _movingSeconds, _movingSum, _stillMin, _stillMax;
    private int _stillCount;

    public double Load { get; private set; }
    public FrontLoadReferenceKind Kind { get; private set; }
    public int Changes { get; private set; }

    public void Observe(double time, double frontLoad, int frontGrounded, double speedMs)
    {
        if (!double.IsFinite(time) || !double.IsFinite(frontLoad) || !double.IsFinite(speedMs)) { _last = -1; _stillStart = -1; return; }
        double dt = _last < 0 ? 0 : time - _last;
        if (_last >= 0 && (dt <= 0 || dt > MaxGapSeconds)) { _stillStart = -1; dt = 0; }
        _last = time;
        bool grounded = frontGrounded >= 2 && frontLoad > 0;
        if (grounded && Math.Abs(speedMs) < StillMs)
        {
            if (_stillStart < 0) { _stillStart = time; _stillSum = 0; _stillCount = 0; _stillMin = double.MaxValue; _stillMax = 0; }
            _stillSum += frontLoad; _stillCount++; _stillMin = Math.Min(_stillMin, frontLoad); _stillMax = Math.Max(_stillMax, frontLoad);
            if (time - _stillStart >= RestingSeconds)
            {
                double mean = _stillSum / _stillCount;
                if (_stillMax - _stillMin <= RestingSpread * mean) Set(mean, FrontLoadReferenceKind.Resting);
                _stillStart = -1;
            }
        }
        else _stillStart = -1;
        if (Kind != FrontLoadReferenceKind.Resting && grounded && dt > 0)
        {
            _movingSeconds += dt; _movingSum += frontLoad * dt;
            if (_movingSeconds >= ProvisionalSeconds) Set(_movingSum / _movingSeconds, FrontLoadReferenceKind.Provisional);
        }
    }

    private void Set(double load, FrontLoadReferenceKind kind)
    {
        if (kind != Kind || kind == FrontLoadReferenceKind.Resting && load != Load) Changes++;
        Load = load; Kind = kind;
    }

    public void Interrupt() { _last = -1; _stillStart = -1; }
    public void Clear() { Interrupt(); _movingSeconds = _movingSum = 0; Load = 0; if (Kind != FrontLoadReferenceKind.None) Changes++; Kind = FrontLoadReferenceKind.None; }
}

/// <summary>
/// Force model version 4, "Grip": art of rally's model from the shared toolkit (<see cref="AxleForceCurve"/>, STD-025) on
/// Woden's front tyres. The front lateral force is rebuilt per grounded front wheel from what the game's own
/// WheelCollider uses: contact load x the sideways friction curve at |sidewaysSlip| x its stiffness (the curve is read
/// live, because the game varies it). The trail input is the slip relative to that curve's peak, so the wheel lightens
/// past peak grip as in art of rally. Full scale is LoadRatio x the front load measured at rest; gain is Strength / 50
/// as in every mod (STD-003), so displayed 50 means the same as art of rally's 50 (STD-021). No 25% cap: that belongs to
/// the classic estimate (version 3), which stays selectable and unchanged.
/// </summary>
/// <remarks>
/// The friction curve is approximated by straight segments through (0,0), the extremum and the asymptote; Unity's
/// internal spline between them is not exposed. The sign follows the classic model's verified direction (-sign(slip)).
/// </remarks>
public sealed class GripSignal
{
    public const int ModelVersion = 4;
    private const float PeakSlipDeg = 8f;
    public readonly FrontLoadReference Reference = new();
    private double? _time, _steer;
    private float _output;
    public long ResetCount { get; private set; }
    /// <summary>Output before the last evaluation, and the reference load it used, for recordings.</summary>
    public float PreviousOutput { get; private set; }
    public double ReferenceUsed { get; private set; }

    // Woden's Stop() resets the model on every non-driving tick; the reference keeps observing (it handles gaps itself),
    // so the grid countdown can measure the resting load.
    public void Reset() { _time = null; _steer = null; _output = 0; ResetCount++; }
    public void NewCar() { Reset(); Reference.Clear(); }

    /// <summary>Straight-segment Unity friction curve value at <paramref name="slip"/> (absolute).</summary>
    public static double Curve(double slip, double extremumSlip, double extremumValue, double asymptoteSlip, double asymptoteValue)
    {
        slip = Math.Abs(slip);
        if (slip <= extremumSlip) return extremumValue * slip / extremumSlip;
        if (slip <= asymptoteSlip) return extremumValue + (asymptoteValue - extremumValue) * (slip - extremumSlip) / (asymptoteSlip - extremumSlip);
        return asymptoteValue;
    }

    public ForceResult Evaluate(TelemetrySample s, ForceOptions options)
    {
        PreviousOutput = _output; ReferenceUsed = Reference.Load;
        ForceResult Stop(string why) { Reset(); return new(false, why, 0, 0, 0, 0); }
        double time = s.SimulationSeconds;
        bool motion = s.Channels.TryGetValue("motion.speed", out double speed) && double.IsFinite(speed) && speed >= 0 &&
            s.Channels.TryGetValue("motion.velocity.local.z", out double forward) && double.IsFinite(forward);
        // The reference watches every valid tick, driving or not (the grid countdown measures the resting load).
        double observedLoad = 0; int grounded = 0;
        foreach (string corner in new[] { "fl", "fr" })
            if (s.Channels.TryGetValue("wheel." + corner + ".grounded", out double g) && g == 1 && s.Channels.TryGetValue("wheel." + corner + ".contactForce", out double f) && double.IsFinite(f))
            { observedLoad += f; grounded++; }
        if (motion && double.IsFinite(time)) Reference.Observe(time, observedLoad, grounded, speed);
        ReferenceUsed = Reference.Load;

        if (!s.Driving) return Stop(s.State);
        if (s.Discontinuity != null) return Stop(s.Discontinuity);
        if (!double.IsFinite(time) || !motion) return Stop("motion unavailable");
        if (s.Channels["motion.velocity.local.z"] < -.5) return Stop("reverse: signal not validated");
        float[] settings = { options.Strength, options.LoadRatio, options.GripSmoothing, options.Damping };
        if (settings.Any(x => !float.IsFinite(x)) || options.LoadRatio <= 0) return Stop("invalid tuning");
        double fy = 0, load = 0, relative = 0; int n = 0;
        foreach (string corner in new[] { "fl", "fr" })
        {
            string p = "wheel." + corner + ".";
            if (!s.Channels.TryGetValue(p + "grounded", out double g) || g is not (0 or 1)) return Stop("front contact unavailable");
            if (g == 0) continue;
            if (!s.Channels.TryGetValue(p + "contactForce", out double force) || !s.Channels.TryGetValue(p + "sidewaysSlip", out double slip) ||
                !double.IsFinite(force) || force < 0 || !double.IsFinite(slip)) return Stop("invalid front contact");
            if (!s.Channels.TryGetValue(p + "sideFriction.extremumSlip", out double es) || !s.Channels.TryGetValue(p + "sideFriction.extremumValue", out double ev) ||
                !s.Channels.TryGetValue(p + "sideFriction.asymptoteSlip", out double asl) || !s.Channels.TryGetValue(p + "sideFriction.asymptoteValue", out double av) ||
                !s.Channels.TryGetValue(p + "sideFriction.stiffness", out double st) || !(es > 0) || !(asl > es) || !double.IsFinite(ev) || !double.IsFinite(av) || !double.IsFinite(st) || st < 0)
                return Stop("friction curve unavailable");
            load += force;
            fy -= Math.Sign(slip) * force * Curve(slip, es, ev, asl, av) * st;
            relative += Math.Abs(slip) / es; n++;
        }
        if (load <= 0) return Stop("front wheels airborne");
        double dt = _time.HasValue ? time - _time.Value : .02;
        if (dt <= 0 || dt > .1) return Stop("force sample gap");
        float damping = 0;
        if (s.Channels.TryGetValue("wheelInput.steer", out double steering) && double.IsFinite(steering))
        {
            if (_steer.HasValue) damping = (float)Math.Clamp(-(steering - _steer.Value) / dt * Math.Clamp(options.Damping, 0, .5f), -.5, .5);
            _steer = steering;
        }
        else _steer = null;
        _time = time;
        float gain = Math.Clamp(options.Strength, 0, 100) / 50f;
        if (Reference.Kind == FrontLoadReferenceKind.None) { _output = 0; return new(true, "measuring front load", (float)load, 0, damping, 0); }
        // The friction curve's peak stands in for art's ideal slip angle: the trail is 0.8 there and 0.6 at twice it.
        float target = AxleForceCurve.Normalised((float)fy, (float)(relative / n * PeakSlipDeg), PeakSlipDeg, (float)(speed * 3.6),
            (float)(options.LoadRatio * Reference.Load), gain, options.Invert);
        _output = AxleForceCurve.Smooth(_output, target, Math.Clamp(options.GripSmoothing, 0, .95f));
        float d = options.Invert ? -damping : damping;
        float preview = Math.Clamp(_output + d * gain, -1f, 1f);
        return new(true, speed * 3.6 < 3 ? "low-speed fade" : "grip model", (float)load, target, damping, preview);
    }
}
