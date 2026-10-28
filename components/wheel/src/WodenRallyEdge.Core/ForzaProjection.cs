using Dbce.Wheel.Telemetry;

namespace WodenRallyEdge.Core;

public static class ForzaProjection
{
    public const float NominalRedline = 8000;

    public static TelemetryFrame Map(TelemetrySample sample)
    {
        var frame = new TelemetryFrame { TimestampMs = unchecked((uint)(long)(sample.ElapsedSeconds * 1000)) };
        if (!sample.Driving || !sample.Channels.ContainsKey("motion.speed")) return frame;
        float F(string key) { double x = sample.Get(key); return x >= -float.MaxValue && x <= float.MaxValue ? (float)x : 0; }
        WheelValues Wheels(string suffix) => new(F("wheel.fl." + suffix), F("wheel.fr." + suffix), F("wheel.rl." + suffix), F("wheel.rr." + suffix));
        frame.IsRaceOn = true;
        // Dashboards should agree with the HUD. The HUD shows TrueSpeed, an arcade number about
        // 3x the physics m/s (2026-10-04 capture), read as km/h; physics speed is the fallback.
        // HUD.FixedUpdate prints abs(TrueSpeed), which is negative in reverse; Forza Speed is a magnitude.
        frame.Speed = sample.Channels.ContainsKey("game.trueSpeed") ? Math.Abs(F("game.trueSpeed")) / 3.6f : F("motion.speed");
        frame.PositionX = F("motion.position.world.x"); frame.PositionY = F("motion.position.world.y"); frame.PositionZ = F("motion.position.world.z");
        frame.VelocityX = F("motion.velocity.local.x"); frame.VelocityY = F("motion.velocity.local.y"); frame.VelocityZ = F("motion.velocity.local.z");
        frame.AccelerationX = F("motion.acceleration.local.x"); frame.AccelerationY = F("motion.acceleration.local.y"); frame.AccelerationZ = F("motion.acceleration.local.z");
        frame.AngularVelocityX = F("motion.angularVelocity.local.x"); frame.AngularVelocityY = F("motion.angularVelocity.local.y"); frame.AngularVelocityZ = F("motion.angularVelocity.local.z");
        frame.WheelRotationSpeed = Wheels("angularSpeed");
        frame.NormalizedSuspensionTravel = Wheels("suspensionCompression");
        // Forza's travel is extension from full compression, in metres.
        frame.SuspensionTravelMeters = Wheels("suspensionExtension");
        // motion.distance restarts at every pause/respawn, so it is not Forza's race distance; no proven odometer yet.
        // CarClock times are seconds (two finished Kenya stages: LastLapValue 105.083/116.339 vs 105.100/116.350 s RACE->END);
        // 5999.999 (99:59.999) is the game's "no lap yet" value and Forza's is 0.
        float Lap(string key) { float x = F(key); return x < 5999 ? x : 0; }
        frame.LastLap = Lap("game.lastLap"); frame.BestLap = Lap("game.bestLap");
        frame.CurrentLap = F("game.lapTime"); frame.CurrentRaceTime = F("game.raceTime");
        // MainCar.Laps is the 1-based lap being driven (1 for the whole stage, 2 at END); Forza's LapNumber is 0-based.
        frame.LapNumber = (ushort)Math.Max(0, F("game.laps") - 1);
        // Rank is 1-based (0 only before the first racing tick), as is Forza's RacePosition.
        frame.RacePosition = (byte)Math.Clamp(F("game.rank"), 0, 255);

        // These are normalized only when OUR calibrated input route supplies them.
        frame.Accel = TelemetryFrame.ToPedal(F("wheelInput.throttle"));
        frame.Brake = TelemetryFrame.ToPedal(F("wheelInput.brake"));
        frame.Steer = TelemetryFrame.ToSteer(F("wheelInput.steer"));
        // The game has no engine rpm, only a 0..1 rev fraction (idle about 0.08). Dashboards need
        // a rev range, so it is shown on a nominal 8000 rpm scale; the rich stream keeps the raw value.
        if (sample.Channels.ContainsKey("game.rpm"))
        {
            frame.EngineMaxRpm = NominalRedline;
            frame.EngineIdleRpm = F("game.idleSpeed") * NominalRedline;
            frame.CurrentEngineRpm = Math.Clamp(F("game.rpm"), 0, 1) * NominalRedline;
        }
        float gear = F("game.gear");
        // Woden reverses in first gear; HUD.FixedUpdate shows "R" while TrueSpeed < -10 km/h, and Forza's 0 is R.
        bool reversing = sample.Channels.ContainsKey("game.trueSpeed") && F("game.trueSpeed") < -10;
        // The mod's H-pattern emulates neutral (gate 0, drive cut) and reverse (R gate) on a gearbox with neither, so the
        // dash shows that selection: Forza's 11 is N, 0 is R. Without the emulation the HUD rule above stands.
        float? gate = sample.Channels.ContainsKey("wheelInput.gate") ? F("wheelInput.gate") : null;
        frame.Gear = gate == 0 ? (byte)11 : !reversing && gate != HPatternShifter.Reverse && gear >= 1 && gear <= 10 ? (byte)gear : (byte)0;
        // No fabricated tyre heat, fuel %, surface rumble, power, drivetrain identity or
        // slip-angle conversions. Rich stream retains raw data.
        return frame;
    }
}
