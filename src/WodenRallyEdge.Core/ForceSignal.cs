using Dbce.Wheel.Ffb;

namespace WodenRallyEdge.Core;

public sealed record ForceOptions(float Strength = 10, float PeakPercent = 25, float LoadReference = 6000, float SlipScale = .35f,
    float SmoothingMs = 35, float Damping = .05f, bool Invert = false);
public sealed record ForceResult(bool Valid, string Reason, float FrontLoad, float Alignment, float Damping, float Preview);

// A provisional contact-weighted slip estimate, NOT measured steering rack torque.
// Device-output conditioning is delegated to the shared toolkit.
public sealed class ForceSignal
{
    private readonly ForceShaper _shaper = new();
    private double? _time;
    private double? _steer;
    public void Reset() { _time = null; _steer = null; _shaper.Reset(); }
    public ForceResult Evaluate(TelemetrySample s, ForceOptions options)
    {
        ForceResult Stop(string why) { Reset(); return new(false, why, 0, 0, 0, 0); }
        if (!s.Driving) return Stop(s.State);
        if (s.Discontinuity != null) return Stop(s.Discontinuity);
        if (!double.IsFinite(s.SimulationSeconds) || !s.Channels.TryGetValue("motion.speed", out double speed) || !double.IsFinite(speed) || speed < 0 ||
            !s.Channels.TryGetValue("motion.velocity.local.z", out double forward) || !double.IsFinite(forward)) return Stop("motion unavailable");
        if (forward < -.5) return Stop("reverse: signal not validated");
        float[] settings = { options.Strength, options.PeakPercent, options.LoadReference, options.SlipScale, options.SmoothingMs, options.Damping };
        if (settings.Any(x => !float.IsFinite(x)) || options.LoadReference <= 0 || options.SlipScale <= 0) return Stop("invalid tuning");
        double sum = 0, load = 0;
        foreach (string corner in new[] { "fl", "fr" })
        {
            string p = "wheel." + corner + ".";
            if (!s.Channels.TryGetValue(p + "grounded", out double grounded) || grounded is not (0 or 1)) return Stop("front contact unavailable");
            if (grounded == 0) continue;
            if (!s.Channels.TryGetValue(p + "contactForce", out double force) || !s.Channels.TryGetValue(p + "sidewaysSlip", out double slip) ||
                !double.IsFinite(force) || force < 0 || !double.IsFinite(slip)) return Stop("invalid front contact");
            load += force;
            sum += force * Math.Tanh(slip / options.SlipScale);
        }
        if (load <= 0) return Stop("front wheels airborne");
        double dt = _time.HasValue ? s.SimulationSeconds - _time.Value : .02;
        if (dt <= 0 || dt > .1) return Stop("force sample gap");
        float alignment = (float)Math.Clamp(-sum / options.LoadReference, -1, 1);
        float damping = 0;
        if (s.Channels.TryGetValue("wheelInput.steer", out double steering) && double.IsFinite(steering))
        {
            if (_steer.HasValue) damping = (float)Math.Clamp(-(steering - _steer.Value) / dt * Math.Clamp(options.Damping, 0, .5f), -.5, .5);
            _steer = steering;
        }
        else _steer = null;
        _time = s.SimulationSeconds;
        _shaper.Strength = 50; // Our user-facing strength is a literal percent applied below.
        _shaper.Invert = options.Invert; _shaper.SmoothingMs = Math.Clamp(options.SmoothingMs, 0, 200);
        _shaper.SoftSaturation = .5f; _shaper.SlewPerSecond = 1.5f;
        _shaper.FadeStartKmh = 3; _shaper.FadeFullKmh = 12; _shaper.RampSeconds = .5f;
        _shaper.PeakLimit = Math.Clamp(options.PeakPercent / 100, 0, .5f);
        float result = _shaper.Shape((alignment + damping) * Math.Clamp(options.Strength / 100, 0, 1), (float)(speed * 3.6), (float)dt);
        return new(true, speed * 3.6 < 3 ? "low-speed fade" : "estimated tyre signal", (float)load, alignment, damping, result);
    }
}
