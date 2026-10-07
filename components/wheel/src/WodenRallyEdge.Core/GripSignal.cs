using Dbce.Wheel.Ffb;

namespace WodenRallyEdge.Core;

/// <summary>Where the grip model's full-scale load comes from.</summary>
public enum FrontLoadReferenceKind { None = 0, Provisional = 1, Driving = 2 }

/// <summary>
/// The front axle's reference load for Woden's grip model: the duration-weighted mean front load while driving above
/// <see cref="MinKmh"/> with both fronts grounded, provisional until <see cref="QualifiedSeconds"/> of such driving, then
/// "driving". Kept for the car; a new car measures again.
/// </summary>
/// <remarks>
/// Not iRacing's resting rule, on evidence from the owner's Kenya drive: Woden's arcade physics load the fronts
/// differently by state. On the grid (countdown) the fronts carry about 13,560; for the 0.08 s still moment of the
/// launch about 5,850; while driving about 8,400 at every speed above 15 km/h (not speed-squared downforce). The rebuilt
/// lateral force uses the actual driving load, so the driving load is the matching full-scale reference.
/// </remarks>
public sealed class FrontLoadReference
{
    public const double MinKmh = 15, QualifiedSeconds = 2, MaxGapSeconds = .1;
    private double _last = -1, _seconds, _sum;

    public double Load { get; private set; }
    public FrontLoadReferenceKind Kind { get; private set; }
    public int Changes { get; private set; }

    public void Observe(double time, double frontLoad, int frontGrounded, double speedMs, bool driving)
    {
        if (!double.IsFinite(time) || !double.IsFinite(frontLoad) || !double.IsFinite(speedMs)) { _last = -1; return; }
        double dt = _last < 0 ? 0 : time - _last;
        if (dt <= 0 || dt > MaxGapSeconds) dt = 0;
        _last = time;
        if (!driving || frontGrounded < 2 || frontLoad <= 0 || Math.Abs(speedMs) * 3.6 < MinKmh || dt == 0) return;
        _seconds += dt; _sum += frontLoad * dt;
        var kind = _seconds >= QualifiedSeconds ? FrontLoadReferenceKind.Driving : FrontLoadReferenceKind.Provisional;
        if (kind != Kind) Changes++;
        Kind = kind; Load = _sum / _seconds;
    }

    public void Clear() { _last = -1; _seconds = _sum = 0; Load = 0; if (Kind != FrontLoadReferenceKind.None) Changes++; Kind = FrontLoadReferenceKind.None; }

    /// <summary>Offline replay: the reference as recorded on a row (the production one can predate the capture).</summary>
    public void Restore(double load, FrontLoadReferenceKind kind)
    {
        if (!double.IsFinite(load) || load < 0 || kind is < FrontLoadReferenceKind.None or > FrontLoadReferenceKind.Driving || (kind == FrontLoadReferenceKind.None) != (load == 0))
            throw new IOException("Invalid recorded grip reference");
        Load = load; Kind = kind;
    }
}
/// <summary>
/// Force model version 4, "Grip": art of rally's model from the shared toolkit (<see cref="AxleForceCurve"/>, STD-025) on
/// Woden's front tyres. The front lateral force is rebuilt per grounded front wheel from what the game's own
/// WheelCollider uses: contact load x the sideways friction curve at |sidewaysSlip| x its stiffness (the curve is read
/// live, because the game varies it). The trail input is the slip relative to that curve's peak, so the wheel lightens
/// past peak grip as in art of rally. Full scale is LoadRatio x the mean driving front load; gain is Strength / 50
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
    /// <summary>Force ramps in over this long after a reset (unpause, stage start), as the classic shaper does.</summary>
    public const double RampSeconds = .5;
    private double? _time, _steer, _rampStart;
    private float _output;
    public long ResetCount { get; private set; }
    /// <summary>Output before the last evaluation, and the reference load it used, for recordings.</summary>
    public float PreviousOutput { get; private set; }
    public double ReferenceUsed { get; private set; }

    // Woden's Stop() resets the model on every non-driving tick; the reference is kept for the car.
    public void Reset() { _time = null; _steer = null; _rampStart = null; _output = 0; ResetCount++; }
    public void NewCar() { Reset(); Reference.Clear(); }

    /// <summary>Curve evaluator identity recorded with the model (the shape is an estimate, see <see cref="Curve"/>).</summary>
    public const string CurveEstimate = "unity-friction-two-piece-flat-tangent-estimate@1";

    /// <summary>
    /// Estimated Unity sideways friction curve value at |<paramref name="slip"/>|. Unity documents a two-piece spline,
    /// (0,0) to the extremum and then to the asymptote, with zero tangent at the extremum and at the asymptote; it does
    /// not document the start tangent or the exact basis. This estimate uses a quadratic ease-out for the first piece
    /// (zero slope at the extremum) and a smoothstep for the second (flat at both knots). The result is an
    /// <b>estimated tyre force</b>, not the solver's exact lateral newtons (Codex design review 2026-10-07).
    /// </summary>
    public static double Curve(double slip, double extremumSlip, double extremumValue, double asymptoteSlip, double asymptoteValue)
    {
        slip = Math.Abs(slip);
        if (slip <= extremumSlip) { double u = 1 - slip / extremumSlip; return extremumValue * (1 - u * u); }
        if (slip <= asymptoteSlip) { double u = (slip - extremumSlip) / (asymptoteSlip - extremumSlip); return extremumValue + (asymptoteValue - extremumValue) * u * u * (3 - 2 * u); }
        return asymptoteValue;
    }

    /// <param name="shared">Muted stage analysis passes the production reference (already observed this tick) instead of measuring its own.</param>
    public ForceResult Evaluate(TelemetrySample s, ForceOptions options, FrontLoadReference? shared = null)
    {
        var reference = shared ?? Reference;
        PreviousOutput = _output; ReferenceUsed = reference.Load;
        ForceResult Stop(string why) { Reset(); return new(false, why, 0, 0, 0, 0); }
        double time = s.SimulationSeconds;
        bool motion = s.Channels.TryGetValue("motion.speed", out double speed) && double.IsFinite(speed) && speed >= 0 &&
            s.Channels.TryGetValue("motion.velocity.local.z", out double forward) && double.IsFinite(forward);
        // The reference averages the driving front load (see FrontLoadReference for why not the grid or a stop).
        double observedLoad = 0; int grounded = 0;
        foreach (string corner in new[] { "fl", "fr" })
            if (s.Channels.TryGetValue("wheel." + corner + ".grounded", out double g) && g == 1 && s.Channels.TryGetValue("wheel." + corner + ".contactForce", out double f) && double.IsFinite(f))
            { observedLoad += f; grounded++; }
        if (shared == null && motion && double.IsFinite(time)) Reference.Observe(time, observedLoad, grounded, speed, s.Driving && s.Discontinuity == null);
        ReferenceUsed = reference.Load;

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
        _time = time; _rampStart ??= time;
        float gain = Math.Clamp(options.Strength, 0, 100) / 50f;
        float ramp = (float)Math.Clamp((time - _rampStart.Value) / RampSeconds, 0, 1);
        if (reference.Kind == FrontLoadReferenceKind.None) { _output = 0; return new(true, "measuring front load", (float)load, 0, damping, 0); }
        // The friction curve's peak stands in for art's ideal slip angle: the trail is 0.8 there and 0.6 at twice it.
        float target = AxleForceCurve.Normalised((float)fy, (float)(relative / n * PeakSlipDeg), PeakSlipDeg, (float)(speed * 3.6),
            (float)(options.LoadRatio * reference.Load), gain, options.Invert);
        _output = AxleForceCurve.Smooth(_output, target, Math.Clamp(options.GripSmoothing, 0, .95f));
        float d = options.Invert ? -damping : damping;
        float preview = Math.Clamp((_output + d * gain) * ramp, -1f, 1f);
        return new(true, speed * 3.6 < 3 ? "low-speed fade" : "grip model", (float)load, target, damping, preview);
    }
}
