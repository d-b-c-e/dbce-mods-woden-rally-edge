using Dbce.Wheel.Telemetry;

namespace WodenRallyEdge.Core;

public static class ForzaProjection
{
    public static TelemetryFrame Map(TelemetrySample sample)
    {
        var frame = new TelemetryFrame { TimestampMs = unchecked((uint)(long)(sample.ElapsedSeconds * 1000)) };
        if (!sample.Driving || !sample.Channels.ContainsKey("motion.speed")) return frame;
        float F(string key) { double x = sample.Get(key); return x >= -float.MaxValue && x <= float.MaxValue ? (float)x : 0; }
        WheelValues Wheels(string suffix) => new(F("wheel.fl." + suffix), F("wheel.fr." + suffix), F("wheel.rl." + suffix), F("wheel.rr." + suffix));
        frame.IsRaceOn = true;
        frame.Speed = F("motion.speed");
        frame.PositionX = F("motion.position.world.x"); frame.PositionY = F("motion.position.world.y"); frame.PositionZ = F("motion.position.world.z");
        frame.VelocityX = F("motion.velocity.local.x"); frame.VelocityY = F("motion.velocity.local.y"); frame.VelocityZ = F("motion.velocity.local.z");
        frame.AccelerationX = F("motion.acceleration.local.x"); frame.AccelerationY = F("motion.acceleration.local.y"); frame.AccelerationZ = F("motion.acceleration.local.z");
        frame.AngularVelocityX = F("motion.angularVelocity.local.x"); frame.AngularVelocityY = F("motion.angularVelocity.local.y"); frame.AngularVelocityZ = F("motion.angularVelocity.local.z");
        frame.WheelRotationSpeed = Wheels("angularSpeed");
        frame.NormalizedSuspensionTravel = Wheels("suspensionCompression");
        // Forza's travel is extension from full compression, in metres.
        frame.SuspensionTravelMeters = Wheels("suspensionExtension");
        frame.DistanceTraveled = F("motion.distance");
        // These are normalized only when OUR calibrated input route supplies them.
        frame.Accel = TelemetryFrame.ToPedal(F("wheelInput.throttle"));
        frame.Brake = TelemetryFrame.ToPedal(F("wheelInput.brake"));
        frame.Steer = TelemetryFrame.ToSteer(F("wheelInput.steer"));
        // No fabricated redline/RPM, gear offsets, tyre heat, fuel %, surface rumble,
        // power, drivetrain identity or slip-angle conversions. Rich stream retains raw data.
        return frame;
    }
}
