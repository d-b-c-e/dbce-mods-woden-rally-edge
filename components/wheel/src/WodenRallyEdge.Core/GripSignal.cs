using Dbce.Wheel.Ffb;

namespace WodenRallyEdge.Core;

/// <summary>Where the grip model's full-scale load comes from.</summary>
public enum FrontLoadReferenceKind { None = 0, Provisional = 1, Driving = 2 }

/// <summary>
/// The front axle's reference load for Woden's grip model: the duration-weighted mean front load while forward-valid and above
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
/// The grip force model, versions 4 through 6: art of rally's model from the shared toolkit (<see cref="AxleForceCurve"/>, STD-025) on
/// Woden's front tyres. The front lateral force is rebuilt per grounded front wheel from what the game's own
/// WheelCollider uses: contact load x the sideways friction curve at |sidewaysSlip| x its stiffness (the curve is read
/// live, because the game varies it). The trail input is the slip relative to that curve's peak, so the wheel lightens
/// past peak grip as in art of rally. Full scale is LoadRatio x the mean driving front load; gain is Strength / 50
/// as in every mod (STD-003), the scale art of rally uses at 50 (STD-021; matching its level is still being calibrated). No 25% cap: that belongs to
/// the classic estimate (version 3), which stays behind the config key and unchanged.
/// Version 5 (STD-027, 2026-10-08) is version 4 with one difference: Strength scales only the tyre force, so the
/// steering damping keeps its own gain instead of also scaling with Strength. At Strength 50 the two are identical.
/// Version 6 also admits coincident positive slip knots with exactly equal nonnegative values, as observed in Woden's
/// native friction curve. The same estimate rises to that value and then stays flat. Versions 4 and 5 retain their
/// strictly ordered knot requirement so their recordings replay exactly.
/// Version 7 (2026-10-24) adds centering from the wheel position and turns the damping around. On the owner's drives,
/// Woden's arcade front slip near center was small and uncorrelated with the wheel: at 60+ km/h and |steer| under 0.1 the
/// tyre force had the restoring sign only about half the time. art of rally's force has it 95% of the time and builds
/// about 1.6 per unit steer. The centering term <see cref="CenteringPeak"/> x tanh(steer / <see cref="CenteringSteer"/>)
/// supplies that build-up; it scales with (speed / <see cref="CenteringFullKmh"/>)^2 (full from there), the low-speed fade,
/// the front load against the reference, and Strength / 50 like the tyre force. The damping term had the opposite sign to
/// the tyre force's restoring direction, so it pushed the wheel along its own motion; from version 7 it resists it.
/// Versions 4 to 6 keep their damping sign and have no centering, so their recordings replay exactly.
/// Version 8 (2026-10-25) is version 7 with <see cref="BaseGain"/> on the tyre force and the centering. The owner drove
/// version 7 at Strength 74.745544, GripLoadRatio 1.5029 and Damping 0.05 and asked for exactly that feel to be Strength
/// 50. Damping keeps its own gain, so version 8 at Strength 50 is version 7 at 74.745544, bit for bit.
/// </summary>
/// <remarks>
/// The friction curve is estimated through (0,0), the extremum and the asymptote (<see cref="Curve"/>); Unity's
/// internal spline between them is not exposed. The sign follows the classic model's verified direction (-sign(slip)).
/// </remarks>
public sealed class GripSignal
{
    private const double ReverseValidityLimit = -.5;
    /// <summary>The latest grip model: version 7 with the owner-accepted <see cref="BaseGain"/>.</summary>
    public const int ModelVersion = 8;
    /// <summary>Version 6 plus centering from the wheel position and damping that resists its motion. Kept for exact replay.</summary>
    public const int CenteringVersion = 7;
    /// <summary>Version 8: the gain on the tyre force and the centering at Strength 50, the owner's accepted v7 Strength 74.745544 / 50.</summary>
    public const float BaseGain = 74.745544f / 50f;
    /// <summary>Accepts equal positive slip knots with exactly equal nonnegative values. Kept for exact replay.</summary>
    public const int EqualKnotsVersion = 6;
    /// <summary>Version 7 centering at Strength 50: its plateau, the steering input where it reaches tanh(1) of it, and the speed
    /// (km/h) from which it is full. Fitted to the owner's art of rally recording (2026-10-13, 60+ km/h) by |steer| band.</summary>
    public const float CenteringPeak = .12f, CenteringSteer = .05f, CenteringFullKmh = 60;
    /// <summary>Strength scales only the tyre force (STD-027); strictly ordered knots. Kept for exact replay.</summary>
    public const int IndependentDampingVersion = 5;
    /// <summary>The first grip model: Strength also scaled the damping term. Kept for exact replay of its recordings.</summary>
    public const int CoupledDampingVersion = 4;
    public static bool IsGripModel(int model) => model is CoupledDampingVersion or IndependentDampingVersion or EqualKnotsVersion or CenteringVersion or ModelVersion;
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
    /// <summary>The two terms of the last preview before the final clamp, ramp included: the tyre (steering) force at the
    /// strength gain, and the damping term at its own gain. Recorded so normalization can compare tyre force alone.</summary>
    public float LastSteering { get; private set; }
    public float LastDamping { get; private set; }
    /// <summary>Version 7: the centering term of the last preview, ramp included (zero for older versions).</summary>
    public float LastCentering { get; private set; }

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
        PreviousOutput = _output; ReferenceUsed = reference.Load; LastSteering = LastDamping = LastCentering = 0;
        ForceResult Stop(string why) { Reset(); return new(false, why, 0, 0, 0, 0); }
        double time = s.SimulationSeconds;
        bool motion = s.Channels.TryGetValue("motion.speed", out double speed) && double.IsFinite(speed) && speed >= 0 &&
            s.Channels.TryGetValue("motion.velocity.local.z", out double forward) && double.IsFinite(forward);
        // The reference averages the driving front load (see FrontLoadReference for why not the grid or a stop).
        // Only a row whose grounded front loads are all usable enters the mean (Codex review of 82f2812: rejected
        // negative/nonfinite loads poisoned it).
        double observedLoad = 0; int grounded = 0; bool loadsUsable = true;
        foreach (string corner in new[] { "fl", "fr" })
        {
            if (!s.Channels.TryGetValue("wheel." + corner + ".grounded", out double g) || g is not (0 or 1)) { loadsUsable = false; continue; }
            if (g == 0) continue;
            if (s.Channels.TryGetValue("wheel." + corner + ".contactForce", out double f) && double.IsFinite(f) && f >= 0) { observedLoad += f; grounded++; }
            else loadsUsable = false;
        }
        // motion.speed is unsigned. Apply the same signed reverse boundary as the
        // force model before a live row can teach the front-load reference.
        if (shared == null && motion && loadsUsable && double.IsFinite(time))
            Reference.Observe(time, observedLoad, grounded, speed,
                s.Driving && s.Discontinuity == null && s.Channels["motion.velocity.local.z"] >= ReverseValidityLimit);
        ReferenceUsed = reference.Load;

        if (!s.Driving) return Stop(s.State);
        if (s.Discontinuity != null) return Stop(s.Discontinuity);
        if (!double.IsFinite(time) || !motion) return Stop("motion unavailable");
        if (s.Channels["motion.velocity.local.z"] < ReverseValidityLimit) return Stop("reverse: signal not validated");
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
                !s.Channels.TryGetValue(p + "sideFriction.stiffness", out double st) || !double.IsFinite(es) || !double.IsFinite(asl) || !(es > 0) ||
                !(asl > es || options.Model >= EqualKnotsVersion && asl == es && av == ev) ||
                !double.IsFinite(ev) || !double.IsFinite(av) || ev < 0 || av < 0 || !double.IsFinite(st) || st < 0)
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
            // Version 7: the tyre force's restoring sign is the steer's own sign, so resisting the motion has the rate's sign.
            if (options.Model >= CenteringVersion) damping = -damping;
            _steer = steering;
        }
        else _steer = null;
        _time = time; _rampStart ??= time;
        float gain = Math.Clamp(options.Strength, 0, 100) / 50f * (options.Model >= ModelVersion ? BaseGain : 1f);
        float ramp = (float)Math.Clamp((time - _rampStart.Value) / RampSeconds, 0, 1);
        if (reference.Kind == FrontLoadReferenceKind.None) { _output = 0; return new(true, "measuring front load", (float)load, 0, damping, 0); }
        // The friction curve's peak stands in for art's ideal slip angle: the trail is 0.8 there and 0.6 at twice it.
        float target = AxleForceCurve.Normalised((float)fy, (float)(relative / n * PeakSlipDeg), PeakSlipDeg, (float)(speed * 3.6),
            (float)(options.LoadRatio * reference.Load), gain, options.Invert);
        _output = AxleForceCurve.Smooth(_output, target, Math.Clamp(options.GripSmoothing, 0, .95f));
        // Damping fades out at parking speed with the tyre force (the classic shaper fades the whole request; Codex review
        // of 82f2812: a stationary steering motion wrote -0.5).
        float fade = AxleForceCurve.SmoothStep01((float)((speed * 3.6 - AxleForceCurve.FadeStartKmh) / (AxleForceCurve.FadeFullKmh - AxleForceCurve.FadeStartKmh)));
        float d = (options.Invert ? -damping : damping) * fade;
        float centre = 0;
        if (options.Model >= CenteringVersion && _steer.HasValue)
        {
            double scale = Math.Min(1, speed * 3.6 / CenteringFullKmh);
            centre = (float)(CenteringPeak * Math.Tanh(_steer.Value / CenteringSteer) * scale * scale * Math.Min(1.5, load / reference.Load)) * gain * fade;
            if (options.Invert) centre = -centre;
        }
        // Version 4 scaled the damping with Strength; from version 5 Strength is the steering (tyre) strength only, and a
        // Classic-selected shadow evaluation (model 3) keeps independent damping and strictly ordered knots.
        float dampingGain = options.Model == CoupledDampingVersion ? gain : 1f;
        LastSteering = _output * ramp; LastDamping = d * dampingGain * ramp; LastCentering = centre * ramp;
        float preview = Math.Clamp((_output + centre + d * dampingGain) * ramp, -1f, 1f);
        return new(true, speed * 3.6 < 3 ? "low-speed fade" : "grip model", (float)load, target, damping, preview);
    }
}
