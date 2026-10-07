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
        Add("capture.trajectoryIndex", "integer", "measured", "Developer stage capture: zero-based row in trajectory.tsv, same physics callback, prefix pose and postfix signals");
        Add("sample.driving", "boolean 0/1", "derived", "Selected player, RACE state, focused, unpaused, not replay/photo/respawning/locked");
        Add("sample.discontinuity", "boolean 0/1", "derived", "Current sample reset motion/model continuity; reason is retained as an ordered marker");
        Add("ffb.frontLoad", "Unity force units", "derived", "Sum of measured front WheelHit.force magnitudes");
        Add("ffb.alignmentEstimate", "normalized estimate", "derived", "Contact-weighted tanh(sidewaysSlip/slipScale), divided by reference load; NOT measured rack torque");
        Add("ffb.dampingEstimate", "normalized estimate", "derived", "Calibrated steering velocity damping");
        Add("ffb.preview", "normalized -1..1", "derived", "Toolkit-shaped force preview before output permission/ownership gates");
        Add("ffb.grip.preview", "normalized -1..1", "derived", "Grip model (v4) result this tick, whether or not it is the selected model");
        Add("ffb.grip.valid", "bool", "derived", "The grip model had valid inputs this tick");
        Add("ffb.grip.previousOutput", "normalized -1..1", "derived", "Grip model smoothed output before this tick");
        Add("ffb.grip.reference", "N", "derived", "Front load reference the grip model used (0 until measured)");
        Add("ffb.grip.referenceKind", "enum", "derived", "0 none, 1 provisional (moving mean), 2 measured at rest");
        Add("ffb.grip.referenceChanges", "count", "derived", "Changes of the grip reference kind or resting load");
        Add("ffb.grip.carEpoch", "count", "derived", "Cars seen; a change measures a new reference");
        Add("ffb.tuning.loadRatio", "multiplier", "setting", "Grip: full scale as a multiple of the resting front load");
        Add("ffb.tuning.gripSmoothing", "0..0.95", "setting", "Grip: per-update smoothing factor");
        Add("crash.cue", "normalized -1..1", "derived", "Crash cue calculated this tick (toolkit CrashCue fallback crash-constant-fallback@2: art of rally's push + 25 Hz rattle averaged per update), whether or not it was delivered");
        Add("crash.count", "count", "derived", "Crash cues played since the car went live");
        Add("crash.delivered", "bool", "derived", "The crash cue was added to the written force this tick");
        Add("crash.epoch", "count", "derived", "Crash detector epochs (motion continuity breaks start a new one)");
        Add("crash.discontinuous", "bool", "derived", "This tick started a new crash detector epoch");
        Add("crash.enabled", "bool", "setting", "Effective [ForceFeedback] CrashEnabled");
        Add("crash.strength", "percent", "setting", "Effective [ForceFeedback] CrashStrengthPercent");
        Add("crash.modelVersion", "integer", "derived", "2 = Core CrashStage over Dbce.Wheel.Ffb CrashCue, constant-force fallback crash-constant-fallback@2");
        Add("crash.contacts", "count", "measured", "Body contacts observed since the previous force tick (recorded below in arrival order, at most 8)");
        Add("crash.contactsDropped", "count", "measured", "Contacts beyond 8 since the previous tick that were not recorded; the cue cannot be replayed across them");
        for (int i = 0; i < CrashStage.MaxContactsPerTick; i++)
        {
            string k = "crash.contact" + i + ".";
            Add(k + "time", "s", "measured", "Unity physics time of the contact");
            Add(k + "speed", "m/s", "measured", "Relative speed along the contact normal (strongest of the collision's contact points)");
            Add(k + "share", "0..1", "measured", "|normal.y| / |normal| of that contact point");
            Add(k + "road", "bool", "derived", "The other collider was classified as road by tag/object name");
            Add(k + "classified", "bool", "derived", "The collider had a tag other than Untagged for that classification");
            Add(k + "intensity", "0..1", "derived", "Crash detector intensity for the contact (0 when rejected)");
        }        Add("ffb.deliveredOutput", "normalized -1..1", "derived", "Force written to the wheel: ffb.preview plus the crash cue when delivered");
        Add("analysis.force.preview", "normalized -1..1", "derived", "Developer stage capture only: independent history of the selected model (ForceSignal@3 or GripSignal@4, see analysis.force.modelVersion) without device delivery gates; not physical output");
        Add("analysis.force.valid", "boolean 0/1", "derived", "Independent analysis force model accepted the recorded inputs");
        Add("analysis.force.reset", "count", "derived", "Independent analysis force model reset epoch");
        Add("analysis.force.modelVersion", "integer", "setting", "3 = classic ForceSignal, 4 = GripSignal");
        Add("ffb.modelValid", "boolean 0/1", "derived", "Actual ForceSignal accepted the current recorded model inputs");
        Add("ffb.modelReason", "enum", "derived", "Stable ForceSignal reason code; see RECORDED-PLAYBACK.md");
        Add("ffb.modelResetBefore", "count", "measured", "ForceSignal reset epoch immediately before evaluating this sample");
        Add("ffb.modelResetAfter", "count", "measured", "ForceSignal reset epoch after evaluation and lifecycle gate handling");
        Add("ffb.gate", "enum", "measured", "Stable output-gate code after model evaluation; zero means a native write was eligible");
        Add("ffb.sent", "normalized -1..1", "measured", "Force request accepted by native API; does not establish physical feel");
        Add("ffb.armed", "boolean 0/1", "measured", "Saved On preference with no latched output error or diagnostic force suppression; legacy name, no session-start step");
        Add("ffb.connected", "boolean 0/1", "measured", "Toolkit device open; not proof of force delivery");
        Add("ffb.connectionAttempts", "count", "measured", "Device initialization attempts this launch");
        Add("ffb.lastConnectionMs", "ms", "measured", "Last zero-force open plus input-reader refresh duration");
        foreach (var timing in new[] { "frameMs", "devicePollMs", "carMs", "samplerMs", "forceMs", "controlsMs" })
            Add("timing." + timing, "ms", "measured", "Stopwatch duration; frameMs is Update interval, other channels describe last completed named work; not GPU time");
        Add("timing.hitches", "count", "measured", "Update intervals of at least 100 ms with a local car, including expected pauses/loading");
        Add("ffb.accepted", "boolean 0/1", "measured", "Last native force write outcome; absent when no write attempted");
        Add("ffb.deliveryAttempts", "count", "measured", "Cumulative non-initialization force API writes");
        Add("ffb.deliveryFailures", "count", "measured", "Cumulative output/init failures");
        foreach (var setting in new[] { "strengthPercent", "peakPercent", "loadReference", "slipScale", "smoothingMs", "damping", "invert", "modelVersion" })
            Add("ffb.tuning." + setting, "model setting (see source)", "measured", "Exact active estimate/conditioning setting; modelVersion 3 restores the original provisional contact-weighted slip output; no post-shaping reduction");
        foreach (var axis in new[] { "steer", "throttle", "brake", "handbrake" })
            Add("wheelRaw." + axis, "DirectInput 0..65535", "measured", "Current bound device axis before calibration; absent on read failure");
        Add("camera.mode", "game enum", "raw", "Car_Cam mode: Chase=0, IsoMetric=1, Photo=2, Replay=3");
        Add("camera.mountedView", "enum", "measured", "Mod cycle: Stock=0, Bonnet=1, Bumper=2; never written to stock save data");
        Add("camera.playerOwned", "boolean 0/1", "measured", "Live player camera authority; false suppresses mod FFB");
        Add("camera.changing", "boolean 0/1", "measured", "Game camera transition state");
        Add("camera.stockPreset", "game index", "raw", "Original game preset index");
        Add("wheelInput.appliedTicks", "count", "measured", "Successful scoped Controls.FixedUpdate action-table overrides");
        Add("wheelInput.preRaceTicks", "count", "measured", "Successful wheel action-table overrides during native WARMING/countdown; not FFB permission");
        Add("assist.countdown.speedPercent", "percent", "configured", "Requested countdown drain speed; 100 when optional assist is Off; does not alter elapsed lap/stage timing");
        Add("assist.countdown.adjustedUpdates", "count", "measured", "CountDown.Update timestamp adjustments applied before native timeout checks");
        Add("assist.countdown.timeLeft", "s", "raw", "Native CountDown.TimeLeft; omitted without a recent observed single-player race timer");
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
        foreach (var field in new[] { "steer", "throttle", "brake", "handbrake" })
            Add("wheelInput." + field, "normalized", "derived", "Calibrated applied input; handbrake includes full stock/button override");
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
            // Sideways friction curve in force on this wheel (the game varies stiffness): the evidence for a grip model on the shared AxleForceCurve (STD-025).
            Add(p + "sideFriction.extremumSlip", "Unity slip units", "raw", "WheelCollider.sidewaysFriction extremum slip (peak of the friction curve)");
            Add(p + "sideFriction.extremumValue", "curve units", "raw", "WheelCollider.sidewaysFriction value at the extremum");
            Add(p + "sideFriction.asymptoteSlip", "Unity slip units", "raw", "WheelCollider.sidewaysFriction asymptote slip");
            Add(p + "sideFriction.asymptoteValue", "curve units", "raw", "WheelCollider.sidewaysFriction value at and beyond the asymptote");
            Add(p + "sideFriction.stiffness", "multiplier", "raw", "WheelCollider.sidewaysFriction stiffness multiplier");
            Add(p + "colliderId", "session instance ID", "measured", "WheelHit.collider.GetInstanceID; not a persistent surface category");
            Vector(p + "contactPoint.world", "m", "measured", "WheelHit.point, only while grounded");
            Vector(p + "contactNormal.world", "unit vector", "measured", "WheelHit.normal, only while grounded");
        }
        return c;
    }
}
