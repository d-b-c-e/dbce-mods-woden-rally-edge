namespace WodenRallyEdge.Core;

public sealed record ChannelDefinition(string Unit, string Origin, string Source);

public static class TelemetrySchema
{
    public const string Name = "dbce.woden.telemetry";
    public const int Version = 1;
    public static readonly string[] Corners = { "fl", "fr", "rl", "rr" };
    public static IReadOnlyDictionary<string, ChannelDefinition> Channels { get; } = Create();

    private static Dictionary<string, ChannelDefinition> Create()
    {
        var c = new Dictionary<string, ChannelDefinition>(StringComparer.Ordinal);
        void Add(string key, string unit, string origin, string source) => c.Add(key, new(unit, origin, source));
        void Vector(string key, string unit, string origin, string source)
        { foreach (var axis in new[] { "x", "y", "z" }) Add(key + "." + axis, unit, origin, source); }
        Add("sample.simulationSeconds", "s", "measured", "Time.timeAsDouble called inside FixedUpdate; idle heartbeat uses 0");
        Add("sample.sequence", "integer", "measured", "Sample attempt sequence; gaps possible in UDP");
        Add("sample.driving", "boolean 0/1", "derived", "Selected player, RACE state, focused, unpaused, not replay/photo/respawning/locked");
        Vector("motion.position.world", "m", "measured", "Rigidbody.position; Unity world coordinates");
        Vector("motion.velocity.world", "m/s", "measured", "Rigidbody.linearVelocity");
        Vector("motion.velocity.local", "m/s", "derived", "InverseTransformDirection(world velocity); right/up/forward");
        Vector("motion.angularVelocity.local", "rad/s", "derived", "InverseTransformDirection(Rigidbody.angularVelocity)");
        Vector("motion.acceleration.local", "m/s^2", "derived", "World velocity finite difference, rotated into CURRENT car frame; excludes gravity compensation");
        Vector("motion.acceleration.world", "m/s^2", "derived", "World velocity finite difference over simulation time");
        Vector("motion.accelerationG.local", "g", "derived", "Kinematic acceleration / 9.80665; NOT accelerometer specific force");
        foreach (var axis in new[] { "x", "y", "z", "w" }) Add("motion.orientation." + axis, "quaternion", "measured", "Rigidbody.rotation; Unity world frame");
        Add("motion.speed", "m/s", "derived", "Magnitude of Rigidbody.linearVelocity; Unity unit scale not yet road-validated");
        Add("motion.mass", "kg", "measured", "Rigidbody.mass");
        Add("motion.distance", "m", "derived", "Trapezoidal speed integral within continuous driving segment; resets at discontinuity");
        foreach (var field in new[] { "status", "carId", "playerIndex", "gear", "gears", "rank", "laps", "checkpoint" })
            Add("game." + field, "game integer", "raw", "MainCar; indexing/enum meanings retained without conversion");
        foreach (var field in new[] { "rpm", "trueSpeed", "speedMomentum", "steer", "throttle", "brake", "life", "fuel", "oil", "temperature", "gForce", "finalGrip", "enginePower", "idleSpeed", "actualPower", "motorTorqueScale", "steerAngleLimit", "steerValue", "gearMomentum", "lastLap", "bestLap", "penalty" })
            Add("game." + field, "game units (unvalidated)", "raw", "MainCar / nested engine, transmission, status, clock fields; see TELEMETRY.md");
        foreach (var field in new[] { "grounded", "respawning", "replay", "locked", "paused", "photoMode", "automatic", "abs", "handbrake", "shifting" })
            Add("game." + field, "boolean 0/1", "measured", "Game field; no assist changes");
        foreach (var field in new[] { "steer", "throttle", "brake" })
            Add("controls." + field, "game input units (unvalidated)", "raw", "Controls object as observed in MainCar.FixedUpdate postfix");
        foreach (var field in new[] { "steer", "throttle", "brake" })
            Add("wheelInput." + field, "normalized", "derived", "Calibrated DirectInput axis, only present when this mod applies it");
        foreach (var corner in Corners)
        {
            string p = "wheel." + corner + ".";
            Add(p + "grounded", "boolean 0/1", "measured", "WheelCollider.GetGroundHit result");
            Add(p + "rpm", "rev/min", "measured", "WheelCollider.rpm");
            Add(p + "angularSpeed", "rad/s", "derived", "WheelCollider.rpm * 2*pi/60");
            Add(p + "radius", "m", "measured", "WheelCollider.radius; unit-scale transforms required for suspension derivation");
            Add(p + "steerAngle", "deg", "measured", "WheelCollider.steerAngle");
            Add(p + "motorTorque", "N*m", "measured", "WheelCollider.motorTorque command, not engine output");
            Add(p + "brakeTorque", "N*m", "measured", "WheelCollider.brakeTorque command");
            Add(p + "suspensionDistance", "m", "measured", "WheelCollider.suspensionDistance");
            Vector(p + "hubOffset.local", "local Unity units", "measured", "Wheel centre from GetWorldPose in wheel collider transform frame; contains unknown static centre offset");
            Add(p + "hubVelocity", "local Unity units/s", "derived", "Finite difference of hubOffset.local.y during continuous sampling; not damper piston speed");
            Add(p + "suspensionExtension", "m", "derived", "Projection of GetWorldPose wheel centre from collider centre along suspension; full droop = distance");
            Add(p + "suspensionCompression", "ratio 0..1", "derived", "1 - extension/distance; outside geometry tolerance omitted");
            Add(p + "contactForce", "N", "measured", "WheelHit.force MAGNITUDE; neither signed lateral force nor rack torque");
            Add(p + "forwardSlip", "Unity slip units", "raw", "WheelHit.forwardSlip; not assumed equal to physical slip ratio");
            Add(p + "sidewaysSlip", "Unity slip units", "raw", "WheelHit.sidewaysSlip; not an angle in radians");
            Add(p + "colliderId", "session instance ID", "measured", "WheelHit.collider.GetInstanceID; not a persistent surface category");
            Vector(p + "contactPoint.world", "m", "measured", "WheelHit.point, only while grounded");
            Vector(p + "contactNormal.world", "unit vector", "measured", "WheelHit.normal, only while grounded");
        }
        return c;
    }
}
