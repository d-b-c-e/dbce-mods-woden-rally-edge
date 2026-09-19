using System.Numerics;
using System.Text.Json.Serialization;

namespace WodenRallyEdge.Core;

public sealed class TelemetrySample
{
    public string Schema => TelemetrySchema.Name;
    public int Version => TelemetrySchema.Version;
    public string SessionId { get; init; } = "";
    public long Sequence { get; init; }
    public double ElapsedSeconds { get; init; }
    public double SimulationSeconds { get; init; }
    public string Phase => "MainCar.FixedUpdate.postfix.prePhysicsSolve";
    public string State { get; set; } = "idle";
    public int CarInstanceId { get; init; }
    public Dictionary<string, double> Channels { get; } = new(StringComparer.Ordinal);
    public List<string> Unavailable { get; } = new();
    public string? Discontinuity { get; set; }
    public string? ForceStatus { get; set; }
    [JsonIgnore] public bool Driving => State == "driving";

    public void Add(string key, double value)
    {
        if (!TelemetrySchema.Channels.ContainsKey(key)) throw new ArgumentException("Undeclared telemetry channel: " + key);
        if (double.IsFinite(value)) Channels[key] = value;
        else { Channels.Remove(key); Unavailable.Add(key + ":nonfinite"); }
    }
    public void Vector(string key, Vector3 value)
    { Add(key + ".x", value.X); Add(key + ".y", value.Y); Add(key + ".z", value.Z); }
    public double Get(string key) => Channels.TryGetValue(key, out var value) ? value : 0;
    public bool TryVector(string key, out Vector3 value)
    {
        value = default;
        if (!Channels.TryGetValue(key + ".x", out var x) || !Channels.TryGetValue(key + ".y", out var y) || !Channels.TryGetValue(key + ".z", out var z)) return false;
        value = new((float)x, (float)y, (float)z);
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }
}

public sealed class MotionProcessor
{
    private TelemetrySample? _previous;
    private double _distance;

    public void Process(TelemetrySample sample, Quaternion orientation)
    {
        var previous = _previous;
        _previous = null;
        if (!sample.Driving || !sample.TryVector("motion.velocity.world", out var velocity) ||
            !sample.TryVector("motion.position.world", out var position) || !float.IsFinite(orientation.LengthSquared()) || Math.Abs(orientation.LengthSquared() - 1) > .01f ||
            !double.IsFinite(sample.ElapsedSeconds) || !double.IsFinite(sample.SimulationSeconds) || sample.ElapsedSeconds < 0 || sample.SimulationSeconds < 0 || !float.IsFinite(velocity.Length()))
        { _distance = 0; sample.Discontinuity = "inactive-or-invalid-motion"; if (sample.Driving) sample.State = "invalid-motion"; return; }
        var inverse = Quaternion.Inverse(orientation);
        sample.Vector("motion.velocity.local", Vector3.Transform(velocity, inverse));
        sample.Add("motion.speed", velocity.Length());
        double dt = sample.SimulationSeconds - (previous?.SimulationSeconds ?? sample.SimulationSeconds);
        string? reset = previous == null ? "first-sample" : previous.CarInstanceId != sample.CarInstanceId ? "car-changed" :
            previous.SessionId != sample.SessionId ? "session-changed" : dt <= 0 || dt > .1 ? "simulation-time-gap" :
            sample.ElapsedSeconds - previous.ElapsedSeconds <= 0 || sample.ElapsedSeconds - previous.ElapsedSeconds > .5 ? "wall-time-gap" : null;
        if (reset == null && previous != null && previous.TryVector("motion.position.world", out var priorPosition) && previous.TryVector("motion.velocity.world", out var priorVelocity))
        {
            // A teleport/reset must not turn into a dashboard or shaker acceleration spike.
            var expected = (priorVelocity + velocity) * (float)(dt * .5);
            if ((position - priorPosition - expected).Length() > Math.Max(2, velocity.Length() * dt * 2)) reset = "position-discontinuity";
            else
            {
                var acceleration = (velocity - priorVelocity) / (float)dt;
                sample.Vector("motion.acceleration.world", acceleration);
                var local = Vector3.Transform(acceleration, inverse);
                sample.Vector("motion.acceleration.local", local);
                sample.Vector("motion.accelerationG.local", local / 9.80665f);
                _distance += (priorVelocity.Length() + velocity.Length()) * dt * .5;
                foreach (var corner in TelemetrySchema.Corners)
                {
                    string key = "wheel." + corner + ".hubOffset.local.y";
                    if (sample.Channels.TryGetValue(key, out var hub) && previous.Channels.TryGetValue(key, out var priorHub))
                        sample.Add("wheel." + corner + ".hubVelocity", (hub - priorHub) / dt);
                }
            }
        }
        if (reset != null) _distance = 0;
        sample.Discontinuity = reset;
        sample.Add("motion.distance", _distance);
        _previous = sample;
    }
}
