using System.Numerics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Dbce.Wheel.Telemetry;
using Dbce.Wheel.Recording;
using WodenRallyEdge.Core;

if (args.Length > 0 && args[0] == "--ffb-summary")
    return WodenRallyEdge.FfbSummaryCommand.Run(args, Console.Error);
int passed = 0, failed = 0, checks = 0;
void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
void Near(double actual, double expected, string name, double tolerance = .0001) => Check(Math.Abs(actual - expected) < tolerance, $"{name}: {actual} != {expected}");
void Test(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex); } }
TelemetrySample Sample(double time, float velocity = 10, float position = 0, int car = 1, string state = "driving", double? wall = null) {
    var s = new TelemetrySample { SessionId = "synthetic-fixture", ElapsedSeconds = wall ?? time, SimulationSeconds = time, CarInstanceId = car, State = state };
    s.Vector("motion.position.world", new(position, 0, 0)); s.Vector("motion.velocity.world", new(velocity, 0, 0)); return s;
}

Test("calibration endpoints, asymmetric centre, inverted pedals and deadzone", () => {
    var steer = new AxisCalibration(0, 65535, 30000, .02);
    Near(steer.Normalize(0), -1, "left"); Near(steer.Normalize(30000), 0, "centre"); Near(steer.Normalize(65535), 1, "right");
    Near(steer.Normalize(30050), 0, "deadzone");
    var invert = new AxisCalibration(65535, 0, 30000); Near(invert.Normalize(0), 1, "inverted right"); Near(invert.Normalize(65535), -1, "inverted left");
    var pedal = new AxisCalibration(60000, 1000); Near(pedal.Normalize(60000), 0, "rest"); Near(pedal.Normalize(1000), 1, "full"); Near(pedal.Normalize(30500), .5, "half");
    Check(!new AxisCalibration(1, 1).Valid, "zero span rejected"); Check(!new AxisCalibration(0, 100, 100).Valid, "centre endpoint rejected");
    Check(!new AxisCalibration(0, 100, null, double.NaN).Valid, "NaN deadzone rejected");
});
Test("world finite difference rotated into current local frame", () => {
    var processor = new MotionProcessor(); var q = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
    var first = Sample(1, 10, 0); processor.Process(first, q);
    Check(!first.Channels.ContainsKey("motion.acceleration.local.z"), "first sample has no invented acceleration");
    var second = Sample(1.02, 10.08f, .2008f); processor.Process(second, q);
    Near(second.Get("motion.velocity.local.z"), 10.08, "local forward"); Near(second.Get("motion.acceleration.local.z"), 4, "forward acceleration");
    Near(second.Get("motion.acceleration.local.x"), 0, "lateral acceleration"); Near(second.Get("motion.accelerationG.local.z"), 4 / 9.80665, "g conversion");
    Near(second.Get("motion.distance"), .2008, "distance integral");
    var wire = ForzaProjection.Map(second); Near(wire.VelocityZ, 10.08, "Forza local forward"); Near(wire.AccelerationZ, 4, "Forza local acceleration");
});
Test("rotation alone cannot fabricate acceleration", () => {
    var p = new MotionProcessor(); p.Process(Sample(1), Quaternion.Identity);
    var s = Sample(1.02, 10, .2f); p.Process(s, Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1));
    Near(s.Get("motion.acceleration.local.x"), 0, "turning frame x"); Near(s.Get("motion.acceleration.local.z"), 0, "turning frame z");
});
Test("teleport, car change, pause, duplicate time and wall gap reset derivatives", () => {
    foreach (var scenario in new[] { "teleport", "car", "pause", "duplicate", "wallgap", "simgap" }) {
        var p = new MotionProcessor(); p.Process(Sample(1), Quaternion.Identity);
        var s = Sample(scenario == "duplicate" ? 1 : scenario == "simgap" ? 2 : 1.02, 20, scenario == "teleport" ? 100 : .3f,
            scenario == "car" ? 2 : 1, scenario == "pause" ? "paused" : "driving", scenario == "wallgap" ? 3 : null);
        p.Process(s, Quaternion.Identity);
        Check(s.Discontinuity != null, scenario + " marked"); Check(!s.Channels.ContainsKey("motion.acceleration.local.x"), scenario + " no spike");
        Near(s.Get("motion.distance"), 0, scenario + " distance reset");
    }
});
Test("nonfinite channels omitted, unknown channels rejected, invalid orientation reset", () => {
    var s = Sample(1); s.Add("game.rpm", 123); s.Add("game.rpm", double.NaN);
    Check(!s.Channels.ContainsKey("game.rpm"), "NaN absent"); Check(s.Unavailable.Contains("game.rpm:nonfinite"), "reason retained");
    bool thrown = false; try { s.Add("typo.channel", 1); } catch (ArgumentException) { thrown = true; } Check(thrown, "schema typo rejected");
    new MotionProcessor().Process(s, new Quaternion(float.NaN, 0, 0, 0)); Check(s.Discontinuity != null, "bad quaternion resets");
    Check(!ForzaProjection.Map(s).IsRaceOn, "no valid motion parks Forza");
    Check(!s.Driving, "invalid motion state not advertised as driving");
});
Test("rev fraction maps to a nominal rpm scale; fuel and tyre slip stay unguessed", () => {
    var s = Sample(1); new MotionProcessor().Process(s, Quaternion.Identity);
    s.Add("game.rpm", .8); s.Add("game.idleSpeed", .08); s.Add("game.fuel", 92); s.Add("wheel.fl.sidewaysSlip", .75); s.Add("game.gear", 0);
    s.Add("wheel.fl.angularSpeed", 42); s.Add("wheel.fr.angularSpeed", 43); s.Add("wheel.rl.angularSpeed", 44); s.Add("wheel.rr.angularSpeed", 45);
    var f = ForzaProjection.Map(s);
    Near(f.CurrentEngineRpm, 6400, "rev fraction on nominal scale"); Near(f.EngineMaxRpm, 8000, "nominal redline"); Near(f.EngineIdleRpm, 640, "idle on nominal scale");
    Check(f.Gear == 0, "gear 0 stays 0"); Near(f.Fuel, 0, "no fuel scale guess"); Near(f.TireSlipAngle.FrontLeft, 0, "no Unity slip as angle");
    Near(f.WheelRotationSpeed.FrontLeft, 42, "FL"); Near(f.WheelRotationSpeed.FrontRight, 43, "FR"); Near(f.WheelRotationSpeed.RearLeft, 44, "RL"); Near(f.WheelRotationSpeed.RearRight, 45, "RR");
    var bytes = ForzaPacket.CreateBuffer(); ForzaPacket.Write(f, bytes);
    Check(bytes.Length == 324, "exact FH5 packet length"); Check(BitConverter.ToInt32(bytes, 0) == 1, "race active"); Near(BitConverter.ToSingle(bytes, 256), 10, "speed byte offset");
});
Test("full detailed sample is finite JSON below UDP budget", () => {
    var s = Sample(1);
    foreach (var channel in TelemetrySchema.Channels.Keys) s.Add(channel, 1.123456789);
    var bytes = JsonSerializer.SerializeToUtf8Bytes(s, TelemetryOutput.Json);
    Check(bytes.Length < 60000, "bounded datagram"); using var doc = JsonDocument.Parse(bytes);
    Check(doc.RootElement.GetProperty("version").GetInt32() == 1, "schema version");
    Check(doc.RootElement.GetProperty("channels").EnumerateObject().Count() == TelemetrySchema.Channels.Count, "all channels preserved");
});
Test("loopback packet delivery, stale idle, bounded stop, completed recording", () => {
    using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)); receiver.Client.ReceiveTimeout = 2000;
    int port = ((IPEndPoint)receiver.Client.LocalEndPoint!).Port;
    string dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/tests")); Directory.CreateDirectory(dir);
    string path = Path.Combine(dir, "synthetic-" + Guid.NewGuid().ToString("N") + ".jsonl");
    var output = new TelemetryOutput(new(port, 0, 20, path), "synthetic-fixture", "test-created fixture, not gameplay", "fixture-version", new Dictionary<string,string>{{"fixtureIdentity","yes"}});
    var s = Sample(.02); new MotionProcessor().Process(s, Quaternion.Identity); output.Publish(s);
    IPEndPoint endpoint = new(IPAddress.Any, 0); var bytes = receiver.Receive(ref endpoint);
    Check(bytes.Length == 324 && BitConverter.ToInt32(bytes, 0) == 1, "active packet received");
    bool idle = false; var timeout = System.Diagnostics.Stopwatch.StartNew();
    while (timeout.Elapsed.TotalSeconds < 2 && !idle) idle = BitConverter.ToInt32(receiver.Receive(ref endpoint), 0) == 0;
    Check(idle, "stale source parks dashboard independently of Unity Update");
    output.Dispose(); output.Dispose(); Check(output.Stopped, "worker joined"); Check(output.RecordingDrops == 0, "no recording drops");
    var records = SessionReader.Read(path).ToArray(); Check(records.Last().Kind == SessionRecordKind.Footer, "validated footer");
    Check(records.Last().Footer.Completed, "normal completion");
    Check(records[0].Metadata.PluginVersion == "fixture-version" && records[0].Metadata.Properties["fixtureIdentity"] == "yes", "runtime recording identity is not hard-coded");
    var recorded = records.Single(x => x.Kind == SessionRecordKind.Sample).Sample;
    Check(recorded.Channels["sample.driving"] == 1, "recorded validity"); Check(recorded.Channels["sample.simulationSeconds"] == .02, "recorded simulation clock");
    Console.WriteLine("  synthetic recording: " + path);
});
Test("invalid clocks are reported without killing output", () => {
    using var output = new TelemetryOutput(new(0, 0), "synthetic", "fixture");
    output.Publish(Sample(double.NaN)); Check(output.SendErrors == 1, "invalid clock counted");
});
Test("binding capture rejects ambiguity and half steering sweeps; reversed pedals survive save/reload", () => {
    Guid guid = Guid.NewGuid(); var baseline = new Dictionary<(Guid, int), int> { [(guid, 0)] = 32000, [(guid, 2)] = 65535 };
    var capture = new AxisCapture(baseline, true);
    capture.Observe(new() { [(guid, 0)] = 65535, [(guid, 2)] = 65535 });
    Check(capture.Finish() == null, "half steering sweep refused");
    capture.Observe(new() { [(guid, 0)] = 0, [(guid, 2)] = 65535 });
    var steering = capture.Finish()!; Near(steering.Calibration.Normalize(32000), 0, "captured centre"); Near(steering.Calibration.Normalize(65535), 1, "right first");
    var ambiguous = new AxisCapture(baseline, false); ambiguous.Observe(new() { [(guid, 0)] = 0, [(guid, 2)] = 0 });
    Check(ambiguous.Ambiguous && ambiguous.Finish() == null, "multiple moving axes refused");
    var pedal = new AxisCapture(baseline, false); pedal.Observe(new() { [(guid, 0)] = 32000, [(guid, 2)] = 0 });
    var binding = pedal.Finish()!; Near(binding.Calibration.Normalize(0), 1, "inverted full pedal");
    var bindings = new Bindings { Steer = steering, Throttle = binding, Brake = binding, Buttons = new() { ["Gear up"] = new(guid, 10) } };
    string path = Path.Combine(Path.GetTempPath(), "woden-binding-test-" + Guid.NewGuid() + ".json");
    bindings.Save(path); bindings.Save(path); var read = Bindings.Load(path);
    Check(read.DrivingAxesReady && read.Buttons["Gear up"].DeviceGuid == guid, "identity and roles preserved");
    Near(read.Throttle!.Calibration.Normalize(65535), 0, "released after reload"); Check(File.Exists(path + ".bak"), "previous file backed up");
    File.Delete(path); File.Delete(path + ".bak");
});
TelemetrySample Contact(double time, double slip = .3, double speed = 20, string state = "driving") {
    var s = Sample(time, state: state); s.Add("motion.speed", speed); s.Add("motion.velocity.local.z", speed);
    foreach (var c in new[] { "fl", "fr" }) { s.Add("wheel." + c + ".grounded", 1); s.Add("wheel." + c + ".contactForce", 3000); s.Add("wheel." + c + ".sidewaysSlip", slip); }
    s.Add("wheelInput.steer", 0); return s;
}
TelemetrySample GripSample(double time, double slip, double frontLoad = 10000, double speed = 25, string state = "driving", double extremum = .2, double stiffness = 1) {
    var s = Sample(time, state: state); s.Add("motion.speed", speed); s.Add("motion.velocity.local.z", speed);
    foreach (var c in new[] { "fl", "fr" }) {
        string p = "wheel." + c + "."; s.Add(p + "grounded", 1); s.Add(p + "contactForce", frontLoad / 2); s.Add(p + "sidewaysSlip", slip);
        s.Add(p + "sideFriction.extremumSlip", extremum); s.Add(p + "sideFriction.extremumValue", 1); s.Add(p + "sideFriction.asymptoteSlip", extremum * 2.5);
        s.Add(p + "sideFriction.asymptoteValue", .75); s.Add(p + "sideFriction.stiffness", stiffness);
    }
    s.Add("wheelInput.steer", 0); return s;
}
GripSignal DrivenGrip(double load = 10000) { var g = new GripSignal(); for (int i = 0; i < 110; i++) g.Evaluate(GripSample(i * .02, 0, load, 25), new(Model: 4)); return g; }
Test("grip model v4: art of rally's shared curve on the rebuilt front lateral force, 50 means art's 50", () => {
    Near(GripSignal.Curve(.1, .2, 1, .5, .75), .5, "curve rises linearly to the extremum");
    Near(GripSignal.Curve(-.35, .2, 1, .5, .75), .875, "curve falls toward the asymptote");
    Near(GripSignal.Curve(2, .2, 1, .5, .75), .75, "curve holds the asymptote");
    var options = new ForceOptions(Model: 4, GripSmoothing: 0, Damping: 0);
    var g = DrivenGrip();
    Check(g.Reference.Kind == FrontLoadReferenceKind.Driving && Math.Abs(g.Reference.Load - 10000) < 1e-6, "2 s of driving give the driving front load");
    var grid = new GripSignal(); for (int i = 0; i < 60; i++) grid.Evaluate(GripSample(i * .02, 0, 13560, 0, "inactive"), options);
    Check(grid.Reference.Kind == FrontLoadReferenceKind.None, "the grid load (Woden loads the fronts differently there) is not the reference");
    var atPeak = g.Evaluate(GripSample(2.20, .2), options);
    // Fy = 10000 x 1 at the curve peak; trail 0.8 there; full scale 2 x 10000; Strength 50 = gain 1: 0.4, sign -sign(slip).
    Near(atPeak.Preview, -.4, "peak-grip force at Strength 50", 1e-5); Check(atPeak.Valid && atPeak.Reason == "grip model", "valid grip reason");
    var twicePeak = g.Evaluate(GripSample(2.22, .4), options);
    Near(twicePeak.Preview, -(10000 * (1 - .25 * 2 / 3.0 * 1) * .6) / 20000 * 1, "past the peak the force falls and the trail lightens", 1e-4);
    Check(Math.Abs(twicePeak.Preview) < Math.Abs(atPeak.Preview), "the wheel lightens as the front slides");
    Near(g.Evaluate(GripSample(2.24, -.2), options).Preview, .4, "symmetric", 1e-5);
    Near(g.Evaluate(GripSample(2.26, .2), options with { Strength = 100 }).Preview, -.8, "Strength 100 doubles it, no 25% cap", 1e-5);
    Near(g.Evaluate(GripSample(2.28, .2), options with { Invert = true }).Preview, .4, "invert", 1e-5);
    Near(g.Evaluate(GripSample(2.30, .2, stiffness: .5), options).Preview, -.2, "the game's stiffness scales the rebuilt force", 1e-5);
    Near(g.Evaluate(GripSample(2.32, .2, speed: .7), options).Preview, 0, "faded below 3 km/h (0.7 m/s)");
});
Test("grip model v4: reference rules, measuring, missing friction curve and model selection", () => {
    var options = new ForceOptions(Model: 4, GripSmoothing: 0, Damping: 0);
    var fresh = new GripSignal();
    var first = fresh.Evaluate(GripSample(0, .2, 1000, 30), options);
    Check(first.Valid && first.Preview == 0 && first.Reason == "measuring front load" && fresh.Reference.Kind == FrontLoadReferenceKind.None, "a moving first sample is no reference");
    for (int i = 1; i <= 50; i++) fresh.Evaluate(GripSample(i * .02, .2, i % 2 == 0 ? 1000 : 3000, 30), options);
    Check(fresh.Reference.Kind == FrontLoadReferenceKind.Provisional && Math.Abs(fresh.Reference.Load - 2000) < 100, "under 2 s of driving: provisional mean");
    for (int i = 51; i <= 110; i++) fresh.Evaluate(GripSample(i * .02, .2, i % 2 == 0 ? 1000 : 3000, 30), options);
    Check(fresh.Reference.Kind == FrontLoadReferenceKind.Driving && Math.Abs(fresh.Reference.Load - 2000) < 100, "2 s of driving: driving mean");
    for (int i = 111; i <= 140; i++) fresh.Evaluate(GripSample(i * .02, .2, 9000, 2), options);
    Check(Math.Abs(fresh.Reference.Load - 2000) < 100, "driving below 15 km/h does not move the reference");
    var g = DrivenGrip();
    var noCurve = GripSample(2.2, .2); noCurve.Channels.Remove("wheel.fl.sideFriction.extremumSlip");
    var missing = g.Evaluate(noCurve, options);
    Check(!missing.Valid && missing.Reason == "friction curve unavailable" && ForceObservationSemantics.ModelReason(missing.Reason) == 19, "missing friction curve stops the grip model");
    Check(new ForceOptions().Model == 3 && !new ForceOptions().Grip && options.Grip, "ForceOptions defaults to the classic v3 record; Model 4 is grip");
    var settings = new WodenRallyEdge.Settings(new BepInEx.Configuration.ConfigFile(Path.Combine(Path.GetTempPath(), "woden-grip-" + Guid.NewGuid().ToString("N") + ".cfg"), false));
    Check(settings.ForceOptions.Grip && settings.FfbModel == "Grip" && settings.ForceOptions.LoadRatio == 2 && settings.ForceOptions.GripSmoothing == .2f, "the mod defaults to the grip model");
    settings.FfbModel = "Classic"; Check(settings.ForceOptions.Model == 3, "Classic selects the unchanged v3 model");
    settings.FfbModel = "anything"; settings.Validate(); Check(settings.FfbModel == "Grip", "unknown model names fall back to Grip");
});Test("shared force shaping: symmetric sign, literal gain, cap, ramp and low-speed fade", () => {
    var left = new ForceSignal(); var right = new ForceSignal(); var inverted = new ForceSignal(); float final = 0;
    for (int i = 1; i <= 100; i++) {
        var a = left.Evaluate(Contact(i * .02), new()); var b = right.Evaluate(Contact(i * .02, -.3), new());
        var inv = inverted.Evaluate(Contact(i * .02), new(Invert: true));
        Check(a.Valid && Math.Abs(a.Preview) <= .25001, "50% default remains within peak cap"); Near(a.Preview, -b.Preview, "symmetric slip"); Near(a.Preview, -inv.Preview, "invert sign");
        if (i == 1) Check(Math.Abs(a.Preview) < .01, "starts near zero"); final = a.Preview;
    }
    Check(final < -.01, "preview builds when not output-armed");
    var cap = new ForceSignal(); float peak = 0;
    for (int i = 1; i <= 80; i++) peak = Math.Max(peak, Math.Abs(cap.Evaluate(Contact(i * .02, 100), new(Strength: 100, PeakPercent: 5)).Preview));
    Check(peak <= .05001 && peak > .01, "hard cap still permits signal");
    Near(new ForceSignal().Evaluate(Contact(1, speed: 0), new()).Preview, 0, "stationary has no force");
    Near(new ForceSignal().Evaluate(Contact(1), new(PeakPercent: 0)).Preview, 0, "zero peak disables force");
});
Test("50% default matches the original output waveform, including capped peaks", () => {
    Near(new ForceOptions().Strength, 50, "default strength");
    var previous = new Dbce.Wheel.Ffb.ForceShaper { Strength = 50, SmoothingMs = 35, SoftSaturation = .5f, SlewPerSecond = 1.5f,
        FadeStartKmh = 3, FadeFullKmh = 12, RampSeconds = .5f, PeakLimit = .25f };
    var signal = new ForceSignal(); bool hitCap = false;
    for (int i = 1; i <= 150; i++) {
        double slip = i < 70 ? 100 : -.12; double speed = i < 110 ? 20 : 2;
        float old = previous.Shape((float)-Math.Tanh(slip / .35f) * .5f, (float)(speed * 3.6), .02f);
        var result = signal.Evaluate(Contact(i * .02, slip, speed), new());
        Check(result.Valid, "comparison uses valid contacts"); Near(result.Preview, old, "original output restored");
        hitCap |= Math.Abs(old) >= .2499f;
    }
    Check(hitCap, "comparison actually includes capped output");
});
Test("camera fit, manual tuning, bounds and key bindings survive legacy and new settings", () => {
    var pose = CameraPose.FitBonnet(new(-.9f, 0, -2), new(.9f, 1.5f, 2));
    Check(pose.Height > 1 && pose.Height < 1.5 && pose.Forward > 0 && pose.Forward < 1, "body-relative windscreen placement leaves bonnet ahead");
    Check(pose.Pitch > 0 && pose.Fov == 70, "default looks down with explicit FOV");
    Check(CameraPose.FitBonnet(Vector3.Zero, Vector3.Zero) == CameraPose.Bonnet, "missing bounds fallback");
    Check(CameraPose.FitBonnet(Vector3.Zero, new(float.NaN)) == CameraPose.Bonnet, "nonfinite bounds fallback");
    Near(CameraTuning.Adjust(pose, "Camera up").Height, pose.Height + .02, "move up (default 0.02 m step)");
    Near(CameraTuning.Adjust(pose, "Camera back").Forward, pose.Forward - .02, "move back");
    Near(CameraTuning.Adjust(pose, "Camera left").Side, -.02, "move left");
    var fine = new CameraSteps(.005f, .25f, .5f);
    Near(CameraTuning.Adjust(pose, "Camera forward", fine).Forward, pose.Forward + .005, "fine move step"); Near(CameraTuning.Adjust(pose, "Camera pitch down", fine).Pitch, pose.Pitch + .25, "fine tilt step");
    Near(CameraTuning.Adjust(pose, "Camera wider", fine).Fov, pose.Fov + .5, "fine FOV step"); Near(new CameraSteps(0, float.NaN, 99).Bounded().Move, .005, "step bounds");
    Near(CameraTuning.Adjust(pose, "Camera pitch up").Pitch, pose.Pitch - 1, "pitch up");
    Near(CameraTuning.Adjust(pose with { Fov = 110 }, "Camera wider").Fov, 110, "FOV clamp");
    Near(CameraTuning.Adjust(pose with { Forward = -2 }, "Camera back").Forward, -2, "position clamp");
    string path = Path.Combine(Path.GetTempPath(), "woden-camera-bindings-" + Guid.NewGuid() + ".json");
    File.WriteAllText(path, "{\"Version\":1,\"Buttons\":{}}");
    var bindings = Bindings.Load(path); Check(bindings.CameraKeys["Camera up"] == "Numpad9", "legacy bindings acquire numpad defaults");
    bindings.CameraKeys["Camera up"] = "U"; bindings.CameraKeys["Camera down"] = "None";
    bindings.Buttons["Camera down"] = new(Guid.NewGuid(), 5);
    bindings.Save(path); var restored = Bindings.Load(path);
    Check(restored.CameraKeys["Camera up"] == "U" && restored.CameraKeys["Camera down"] == "None", "custom keys and clears persist");
    Check(restored.Buttons["Camera down"].Button == 5, "wheel camera shortcut persists");
    File.Delete(path); File.Delete(path + ".bak");
});
Test("real settings migrate untouched bonnet defaults and preserve custom camera/FFB choices", () => {
    string path = Path.Combine(Path.GetTempPath(), "woden-camera-config-" + Guid.NewGuid() + ".cfg");
    var fresh = new WodenRallyEdge.Settings(new BepInEx.Configuration.ConfigFile(path, false));
    Near(fresh.FfbStrength, 50, "new config default 50"); Check(fresh.CameraAutoFit, "new config body fit");
    File.WriteAllText(path, "[Camera]\nHeight = 0.85\nForward = 0.75\nPitchDegrees = 3\n[ForceFeedback]\nStrengthPercent = 42\nEnabled = false\n");
    var migrated = new WodenRallyEdge.Settings(new BepInEx.Configuration.ConfigFile(path, false));
    Near(migrated.CameraHeight, CameraPose.Bonnet.Height, "old default migrates"); Check(migrated.CameraAutoFit, "old default opts into body fit");
    Near(migrated.FfbStrength, 42, "custom strength survives"); Check(!migrated.FfbEnabled, "saved Off survives");
    migrated.CameraAutoFit = false; migrated.SetCameraPose(false, new(.1f, 1.3f, .2f, 5, 80)); migrated.Save();
    var restored = new WodenRallyEdge.Settings(new BepInEx.Configuration.ConfigFile(path, false));
    Check(!restored.CameraAutoFit && restored.GetCameraPose(false) == migrated.GetCameraPose(false), "manual pose and auto-fit Off survive restart");
    File.WriteAllText(path, "[Camera]\nHeight = 1.25\nForward = 0.3\nPitchDegrees = 6\n");
    var custom = new WodenRallyEdge.Settings(new BepInEx.Configuration.ConfigFile(path, false));
    Check(!custom.CameraAutoFit, "preexisting customized camera stays manual"); Near(custom.CameraHeight, 1.25, "custom height retained");
    File.Delete(path);
});
Test("bonnet defaults match the owner's raised/forward correction while preserving saved manual views", () => {
    var fitted = CameraPose.FitBonnet(new(-1.0630412f, -.5870567f, -2.460395f), new(1.0630587f, .91786325f, 2.4637225f));
    Near(fitted.Height, .7367809, "owner's corrected height"); Near(fitted.Forward, .9380049, "owner's corrected forward position");
    Near(fitted.Pitch, 8, "pitch retained"); Near(fitted.Fov, 70, "FOV retained");
    Near(CameraPose.Bonnet.Height, fitted.Height, "fallback uses observed height"); Near(CameraPose.Bonnet.Forward, fitted.Forward, "fallback uses observed forward");
    var otherCar = CameraPose.FitBonnet(new(-1.0647197f, -.5397808f, -2.1166487f), new(1.0647128f, .84692115f, 2.1351051f));
    Near(otherCar.Height, .5418467 + .15, "correction follows the first car's geometry");
    Near(otherCar.Forward, .77454394 + .05, "forward correction follows the first car");
    string path = Path.Combine(Path.GetTempPath(), "woden-bonnet-v2-" + Guid.NewGuid() + ".cfg");
    try {
        var fresh = new WodenRallyEdge.Settings(new BepInEx.Configuration.ConfigFile(path, false));
        Check(fresh.CameraAutoFit && fresh.GetCameraPose(false) == CameraPose.Bonnet, "fresh config uses corrected fit and fallback");
        File.WriteAllText(path, "[Camera]\nDefaultsVersion = 1\nAutoFitBonnet = false\nHeight = 0.7367809\nForward = 0.9380049\nSide = 0.000008761883\nPitchDegrees = 8\nFov = 70\n");
        var saved = new WodenRallyEdge.Settings(new BepInEx.Configuration.ConfigFile(path, false)); var manual = saved.GetCameraPose(false);
        Check(!saved.CameraAutoFit && Math.Abs(manual.Side - .000008761883f) < .0000001, "owner's exact manual view is retained");
        Near(manual.Height, .7367809, "saved height unchanged"); Near(manual.Forward, .9380049, "saved forward unchanged");
        saved.Save(); var reloaded = new WodenRallyEdge.Settings(new BepInEx.Configuration.ConfigFile(path, false));
        Check(!reloaded.CameraAutoFit && reloaded.GetCameraPose(false) == manual, "migration persists without another correction");
        reloaded.ResetCamera(false); Check(reloaded.CameraAutoFit && reloaded.GetCameraPose(false) == CameraPose.Bonnet, "reset selects corrected default fit");
    } finally { File.Delete(path); }
});
Test("bumper view sits in front of each car's body and keeps its adjustment across cars", () => {
    var saved = CameraPose.Bumper;
    // Body bounds observed in game: the owner's 2026-10-06 car (front 2.307 m) and two earlier cars.
    var owners = (lo: new Vector3(-1.018577f, -.47877452f, -2.3073466f), hi: new Vector3(1.0185976f, 1.0041455f, 2.3073452f));
    var longer = (lo: new Vector3(-1.0630412f, -.5870567f, -2.460395f), hi: new Vector3(1.0630587f, .91786325f, 2.4637225f));
    var shorter = (lo: new Vector3(-1.0647197f, -.5397808f, -2.1166487f), hi: new Vector3(1.0647128f, .84692115f, 2.1351051f));
    Check(saved.Forward < owners.hi.Z && saved.Forward < longer.hi.Z, "the old fixed 2.2 m offset was inside both longer bodies");
    foreach (var car in new[] { owners, longer, shorter })
    {
        var pose = CameraPose.FitBumper(car.lo, car.hi, saved, CameraPose.BumperAheadDefault);
        Near(pose.Forward, car.hi.Z + CameraPose.BumperAheadDefault, "view just ahead of this body's front");
        Check(pose.Height == saved.Height && pose.Side == saved.Side && pose.Pitch == saved.Pitch && pose.Fov == saved.Fov, "only forward follows the body");
    }
    var moved = CameraTuning.Adjust(CameraPose.FitBumper(owners.lo, owners.hi, saved, CameraPose.BumperAheadDefault), "Camera forward");
    float ahead = CameraPose.BumperAhead(owners.hi, moved);
    Near(ahead, CameraPose.BumperAheadDefault + .02, "a forward press is saved relative to the body front");
    Near(CameraPose.FitBumper(shorter.lo, shorter.hi, saved, ahead).Forward, shorter.hi.Z + ahead, "the adjustment carries to a shorter car");
    Check(CameraPose.FitBumper(Vector3.Zero, Vector3.Zero, saved with { Forward = 2.54f }, ahead).Forward == 2.54f, "unmeasured body keeps the saved pose");
    Check(CameraPose.FitBumper(owners.lo, new(float.NaN), saved, ahead) == saved, "nonfinite bounds keep the saved pose");
    string path = Path.Combine(Path.GetTempPath(), "woden-bumper-" + Guid.NewGuid() + ".cfg");
    try {
        File.WriteAllText(path, "[Camera]\nBumperForward = 2.5399997\nBumperHeight = 0.37\nBumperFov = 38\n");
        var cfg = new WodenRallyEdge.Settings(new BepInEx.Configuration.ConfigFile(path, false));
        Near(cfg.BumperAhead, CameraPose.BumperAheadDefault, "an existing config gains the default distance");
        Near(cfg.BumperHeight, .37, "saved bumper height kept"); Near(cfg.BumperFov, 38, "saved bumper FOV kept");
        cfg.BumperAhead = .3f; cfg.Save(); var reloaded = new WodenRallyEdge.Settings(new BepInEx.Configuration.ConfigFile(path, false));
        Near(reloaded.BumperAhead, .3, "distance persists"); reloaded.ResetCamera(true); Near(reloaded.BumperAhead, CameraPose.BumperAheadDefault, "reset restores the distance");
        reloaded.BumperAhead = float.NaN; reloaded.Validate(); Near(reloaded.BumperAhead, CameraPose.BumperAheadDefault, "invalid distance falls back");
    } finally { File.Delete(path); }
});
Test("game window policy excludes foreign, console, hidden and unrepresentable handles", () => {
    uint self = (uint)Environment.ProcessId;
    Check(WodenRallyEdge.GameWindow.Eligible(0x1234, self, "UnityWndClass", true), "visible owned Unity window accepted");
    Check(WodenRallyEdge.GameWindow.Eligible(unchecked((int)0x80001234), self, "UnityWndClass", true), "signed 32-bit native HWND ABI retained");
    Check(!WodenRallyEdge.GameWindow.Eligible(0x1234, self + 1, "UnityWndClass", true), "foreign process excluded");
    Check(!WodenRallyEdge.GameWindow.Eligible(0x1234, self, "ConsoleWindowClass", true), "console excluded");
    Check(!WodenRallyEdge.GameWindow.Eligible(0x1234, self, "UnityWndClass", false), "hidden window excluded");
    Check(!WodenRallyEdge.GameWindow.Eligible(0, self, "UnityWndClass", true), "null window excluded");
    Check(!WodenRallyEdge.GameWindow.Eligible(0x100001234, self, "UnityWndClass", true), "unrepresentable HWND not truncated");
});
Test("actual toolkit adapter pins the game window and requires exit guards", () => WodenRallyEdge.ToolkitForceChecks.Run(Check));
Test("waiting for the game window never closes readers or latches a startup failure", () => {
    var (controller, device) = WodenRallyEdge.ForceControllerChecks.Create(); device.CanOpen = false;
    for (int i = 0; i < 100; i++) controller.Prepare();
    Check(device.Opens == 0 && WodenRallyEdge.Runtime.Devices!.Closes == 0 && WodenRallyEdge.Runtime.Devices.Refreshes == 0, "waiting does no native open/reader churn");
    Check(controller.Failures == 0 && controller.Armed, "missing window is retryable without a saved On toggle");
    device.CanOpen = true; controller.Prepare(); controller.Prepare();
    Check(device.Opens == 1 && controller.Connected, "window readiness permits one normal zero-only open");
    controller.Shutdown();
});
Test("countdown rate handles invalid clocks, expired budgets and speed limits", () => {
    foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity }) {
        Check(CountdownRate.Prepare(bad, 1, 60, 75) == null, "invalid anchor ignored");
        Check(CountdownRate.Prepare(0, bad, 60, 75) == null, "invalid current time ignored");
        Check(CountdownRate.Prepare(0, 1, bad, 75) == null, "invalid budget ignored");
        Check(CountdownRate.Prepare(0, 1, 60, bad) == null, "invalid speed ignored");
    }
    foreach (float remaining in new[] { 0f, -1f }) Check(CountdownRate.Prepare(0, 1, remaining, 75) == null, "expired budget never refunded");
    Check(CountdownRate.Prepare(2, 1, 60, 75) == null && CountdownRate.Prepare(1, 1, 60, 75) == null, "clock restart and pause ignored");
    Check(CountdownRate.Prepare(-float.MaxValue, float.MaxValue, 60, 75) == null, "overflow ignored");
    Check(CountdownRate.Prepare(0, 1, 60, 100) == null && CountdownRate.Prepare(0, 1, 60, 110) == null, "normal rate unchanged");
    Near(CountdownRate.Prepare(0, 1, 60, 50)!.Value.AdjustedAnchor, .5, "half rate");
    Near(CountdownRate.Prepare(0, 1, 60, 0)!.Value.AdjustedAnchor, .75, "minimum quarter rate");
});
Test("actual countdown hook preserves native expiry, ownership, bonuses and cleanup", () => WodenRallyEdge.CountdownChecks.Run(Check));
Test("real difficulty settings default Off and persist bounded speed without changing FFB", () => {
    string path = Path.Combine(Path.GetTempPath(), "woden-difficulty-" + Guid.NewGuid() + ".cfg");
    try {
        File.WriteAllText(path, "[ForceFeedback]\nStrengthPercent = 42\nEnabled = false\n");
        var settings = new WodenRallyEdge.Settings(new BepInEx.Configuration.ConfigFile(path, false));
        Check(!settings.CountdownAssistEnabled && settings.CountdownSpeed == 75, "existing config gains an opt-in assist at 75%");
        settings.CountdownAssistEnabled = true; settings.CountdownSpeed = 50; settings.Save();
        var restored = new WodenRallyEdge.Settings(new BepInEx.Configuration.ConfigFile(path, false));
        Check(restored.CountdownAssistEnabled && restored.CountdownSpeed == 50, "saved enabled/speed survive restart");
        Check(!restored.FfbEnabled && restored.FfbStrength == 42, "saved force preference unchanged");
        restored.CountdownSpeed = 0; restored.Validate(); Near(restored.CountdownSpeed, 25, "minimum speed");
        restored.CountdownSpeed = 150; restored.Validate(); Near(restored.CountdownSpeed, 100, "maximum speed");
        restored.CountdownSpeed = float.NaN; restored.Validate(); Near(restored.CountdownSpeed, 75, "invalid speed fallback");
        restored.CountdownAssistEnabled = false; restored.Save();
        Check(!new WodenRallyEdge.Settings(new BepInEx.Configuration.ConfigFile(path, false)).CountdownAssistEnabled, "Off persists");
    } finally { File.Delete(path); }
});
Test("countdown wheel/camera eligibility preserves every existing driving and FFB exclusion", () => {
    var ready = new PlayerControlState(PlayerPhase.Countdown, true, true, false, false, false, false, false, true);
    Check(ready.WheelAvailable && ready.CameraAvailable && ready.PreRace, "locked start-line permits calibrated controls and camera");
    Check(!ready.Driving, "countdown never becomes a force/driving sample");
    var blocked = new[] { ready with { Selected = false }, ready with { Focused = false }, ready with { PanelOpen = true }, ready with { Paused = true },
        ready with { Replay = true }, ready with { Respawning = true }, ready with { PhotoMode = true }, ready with { Phase = PlayerPhase.Finished },
        ready with { Phase = PlayerPhase.Destroyed }, ready with { Phase = PlayerPhase.Unavailable }, ready with { Phase = PlayerPhase.Racing, Locked = true } };
    foreach (var state in blocked) Check(!state.WheelAvailable && !state.CameraAvailable && !state.Driving, "unsafe/inactive context stays excluded: " + state);
    var green = ready with { Phase = PlayerPhase.Racing, Locked = false };
    Check(green.WheelAvailable && green.CameraAvailable && green.Driving && !green.PreRace, "green enables existing driving path");
    var cycle = new CameraCycle(); cycle.StockChanged(4, 0, true, true);
    Check(cycle.View == MountedView.Bonnet && !cycle.Reconcile(0, true, true), "preselected bonnet survives normal countdown-to-race without preset change");
});
Test("actual action-table lease supplies countdown-capable controls and restores boxed native values", () => WodenRallyEdge.InputLeaseChecks.Run(Check));
Test("active countdown camera cannot enable physical force", () => {
    var (controller, device) = WodenRallyEdge.ForceControllerChecks.Create(); controller.Prepare();
    for (int i = 1; i <= 30; i++) controller.Tick(Contact(i * .02, state: "countdown"));
    Check(device.Opens == 1 && device.Writes.Count == 0 && controller.Sent == 0, "saved On and player camera still cannot deliver countdown force");
    for (int i = 31; i <= 80; i++) controller.Tick(Contact(i * .02));
    Check(device.Writes.Any(x => Math.Abs(x) > .01), "green resumes ordinary ramped force without reconnecting");
    controller.Shutdown();
});
Test("force rejects stale, paused, replay, reverse, airborne and incomplete or nonfinite contacts", () => {
    foreach (string state in new[] { "paused", "replay", "inactive", "respawning", "unfocused", "invalid-motion" })
        Check(!new ForceSignal().Evaluate(Contact(1, state: state), new()).Valid, state + " blocked");
    var signal = new ForceSignal(); signal.Evaluate(Contact(1), new()); Check(!signal.Evaluate(Contact(1.3), new()).Valid, "stale signal reset");
    var missing = Contact(1); missing.Channels.Remove("wheel.fl.sidewaysSlip"); Check(!new ForceSignal().Evaluate(missing, new()).Valid, "missing contact refuses estimate");
    var bad = Contact(1); bad.Add("wheel.fl.contactForce", double.NaN); Check(!new ForceSignal().Evaluate(bad, new()).Valid, "nonfinite refuses estimate");
    var airborne = Contact(1); airborne.Add("wheel.fl.grounded", 0); airborne.Add("wheel.fr.grounded", 0); Check(!new ForceSignal().Evaluate(airborne, new()).Valid, "airborne zero");
    Check(!new ForceSignal().Evaluate(Contact(1, speed: -2), new()).Valid, "reverse blocked");
    Check(!new ForceSignal().Evaluate(Contact(1), new(Strength: float.NaN)).Valid, "bad tuning blocked");
});
Test("live capture starts at zero even hours after game launch", () => {
    string path = Path.Combine(Path.GetTempPath(), "woden-late-capture-" + Guid.NewGuid() + ".jsonl");
    using (var output = new TelemetryOutput(new(0, 0, 20, path), "fixture", "synthetic")) { output.Publish(Sample(7200)); output.Publish(Sample(7200.02)); }
    var rows = SessionReader.Read(path).ToArray(); Check(rows.Count(x => x.Kind == SessionRecordKind.Sample) == 2, "late recording is not instantly duration-limited");
    Check(rows.Last().Footer.Completed, "late capture complete"); File.Delete(path);
});
Test("camera button extends the stock cycle without saving invalid native indices and yields on takeover", () => {
    var cycle = new CameraCycle();
    cycle.StockChanged(0, 1, true, true); Check(cycle.View == MountedView.Stock, "stock intermediate view retained");
    cycle.StockChanged(4, 0, true, true); Check(cycle.View == MountedView.Bonnet && cycle.ExpectedStockPreset == 0, "wrap inserts bonnet while native index remains legal");
    Check(cycle.Advance(true, true) && cycle.View == MountedView.Bumper, "same action advances bumper");
    Check(cycle.Advance(true, true) && cycle.View == MountedView.Stock, "same action returns to stock zero");
    Check(!cycle.Advance(true, true), "next press delegated to stock");
    cycle.StockChanged(4, 0, false, true); Check(cycle.View == MountedView.Bumper, "disabled bonnet skipped");
    Check(cycle.Reconcile(2, true, true) && cycle.View == MountedView.Stock, "unexpected game preset wins");
    cycle.StockChanged(4, 0, true, true); cycle.Handoff(); Check(cycle.View == MountedView.Stock, "photo/replay handoff drops mounted view");
    cycle.StockChanged(0, 0, false, false); Check(cycle.View == MountedView.Stock, "all disabled leaves normal cycle");
});
Test("FFB saved preference, transient stop recovery and panic using actual consumer policy", () => WodenRallyEdge.ForceControllerChecks.Recovery(Check, t => Contact(t)));
Test("FFB init/write failures and diagnostic launch cannot reconnect-loop or choose another wheel", () => WodenRallyEdge.ForceControllerChecks.Failures(Check, t => Contact(t)));
Test("crash kick: art of rally's rules and cue on a body contact; continuity, road and delivery gates (toolkit CrashCue@2)", () => WodenRallyEdge.ForceControllerChecks.Crash(Check, t => Contact(t)));
Test("handbrake axis/button selection, proportional values and legacy binding persistence", () => {
    Near(HandbrakeInput.Amount(true, true, .25f, false, false), .25, "quarter pull");
    Near(HandbrakeInput.Amount(true, false, 1, false, false), 0, "disconnected axis releases");
    Near(HandbrakeInput.Amount(true, true, float.NaN, false, false), 0, "invalid axis releases");
    Near(HandbrakeInput.Amount(true, true, .25f, false, true), 1, "stock button remains full");
    Near(HandbrakeInput.Amount(false, true, .25f, true, false), 1, "button mode is full");
    Near(HandbrakeInput.Amount(false, true, 1, false, false), 1, "legacy mode never disables additive axis");
    Near(HandbrakeInput.Scale(2000, .25f), 500, "quarter native brake command");
    Near(HandbrakeInput.Scale(.7f, .5f), .35, "half native grip loss");
    Guid guid = Guid.NewGuid(); var bindings = new Bindings { Handbrake = new(guid, 2, new(65535, 0)), HandbrakeUsesAxis = true, Buttons = new() { ["Handbrake"] = new(guid, 7) } };
    string path = Path.Combine(Path.GetTempPath(), "woden-handbrake-" + Guid.NewGuid() + ".json");
    bindings.Save(path); var loaded = Bindings.Load(path);
    Check(loaded.HandbrakeUsesAxis && loaded.Buttons["Handbrake"].Button == 7, "axis selection preserves old button");
    Near(loaded.Handbrake!.Calibration.Normalize(65535), 0, "reversed rest survives");
    Near(loaded.Handbrake.Calibration.Normalize(0), 1, "reversed full survives");
    File.WriteAllText(path, "{\"Version\":1,\"Buttons\":{}}");
    Check(!Bindings.Load(path).HandbrakeUsesAxis, "legacy file loads in button mode"); File.Delete(path);
});
Test("diagnostic launch is bounded, consumed once and rejects expired requests", () => {
    string path = Path.Combine(Path.GetTempPath(), "woden-launch-" + Guid.NewGuid() + ".json");
    DateTimeOffset now = DateTimeOffset.UtcNow;
    File.WriteAllText(path, JsonSerializer.Serialize(new DiagnosticLaunch(2, Guid.NewGuid(), now.AddMinutes(15), true, "fixture-drive:baseline")));
    var accepted = DiagnosticLaunch.Consume(path, now);
    Check(accepted?.DisableForces == true && accepted.EffectiveCaseId == "fixture-drive:baseline", "v2 case identity and unattended no-force default retained");
    Check(DiagnosticLaunch.Consume(path, now) == null, "second launch cannot repeat capture");
    File.WriteAllText(path, JsonSerializer.Serialize(new DiagnosticLaunch(1, Guid.NewGuid(), now.AddMinutes(-1))));
    bool rejected = false; try { DiagnosticLaunch.Consume(path, now); } catch (IOException) { rejected = true; }
    Check(rejected && !File.Exists(path), "expired request rejected and consumed");
    File.WriteAllText(path, JsonSerializer.Serialize(new DiagnosticLaunch(2, Guid.NewGuid(), now.AddMinutes(15), true, "../escape")));
    rejected = false; try { DiagnosticLaunch.Consume(path, now); } catch (IOException) { rejected = true; }
    Check(rejected && !File.Exists(path), "unsafe case identity rejected and consumed");
    foreach (string file in Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".consumed-*")) File.Delete(file);
});
Test("recorded driving reruns actual ForceSignal into a bound observation case", () => {
    string directory = Path.Combine(Path.GetTempPath(), "woden-reprocess-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
    try
    {
        var options = new ForceOptions();
        string config = RecordingArtifacts.WriteForceConfig(Path.Combine(directory, "force-config.json"), options);
        string profile = RecordingArtifacts.WriteCaptureProfile(Path.Combine(directory, "capture-profile.json"), new string('1',64), new string('2',64), new string('3',64), new string('4',64), true, true, true, "fixture-guid");
        var properties = new Dictionary<string,string> { ["recordingContract"]="dbce.wheel.replay-case@1", ["capability"]="signal-reprocess",
            ["requestId"]=Guid.NewGuid().ToString(), ["caseId"]="fixture-drive", ["disableForces"]="false", ["gameAssemblySha256"]=new string('1',64),
            ["pluginSha256"]=new string('2',64), ["pluginProductVersion"]="0.2.12+fixture-source", ["runtimeSource"]="fixture-source",
            ["forceConfigSha256"]=config, ["captureProfileSha256"]=profile };
        var signal = new ForceSignal();
        using (var output = new TelemetryOutput(new(0,0,20,Path.Combine(directory,"source.jsonl")), "fixture-session", "synthetic", "0.2.12", properties))
        {
            for (int i=1;i<=100;i++)
            {
                var sample=Contact(i*.02, slip: i<60?.3:-.2); long before=signal.ResetCount; var result=signal.Evaluate(sample,options);
                sample.Add("ffb.frontLoad",result.FrontLoad);sample.Add("ffb.alignmentEstimate",result.Alignment);sample.Add("ffb.dampingEstimate",result.Damping);sample.Add("ffb.preview",result.Preview);
                sample.Add("ffb.modelValid",result.Valid?1:0);sample.Add("ffb.modelReason",ForceObservationSemantics.ModelReason(result.Reason));
                sample.Add("ffb.modelResetBefore",before);sample.Add("ffb.modelResetAfter",signal.ResetCount);sample.Add("ffb.gate",0);
                sample.Add("ffb.tuning.strengthPercent",options.Strength);sample.Add("ffb.tuning.peakPercent",options.PeakPercent);sample.Add("ffb.tuning.loadReference",options.LoadReference);
                sample.Add("ffb.tuning.slipScale",options.SlipScale);sample.Add("ffb.tuning.smoothingMs",options.SmoothingMs);sample.Add("ffb.tuning.damping",options.Damping);
                sample.Add("ffb.tuning.invert",options.Invert?1:0);sample.Add("ffb.tuning.modelVersion",3); output.Publish(sample);
            }
        }
        var prepared=RecordedForceReplay.Reprocess(directory);
        Check(prepared.DrivingSamples==100&&prepared.ModelSamples==100&&prepared.DrivingSeconds>1,"driving coverage retained");
        Check(File.Exists(prepared.CasePath)&&File.Exists(prepared.ObservationPath),"case and observation written");
        Check(RecordedForceReplay.Compare(prepared.ObservationPath,prepared.ObservationPath).Equal,"baseline self-comparison is exact");
        using var header=JsonDocument.Parse(File.ReadLines(prepared.ObservationPath).First());
        Check(header.RootElement.GetProperty("caseSha256").GetString()==RecordingArtifacts.Sha256(prepared.CasePath),"observation binds exact case bytes");
        string candidatePath=Path.Combine(directory,"candidate-20-percent.json"),candidateObservation=Path.Combine(directory,"force-observation.trial-20.jsonl");
        string candidateHash=RecordingArtifacts.WriteForceConfig(candidatePath,options with { Strength=20 });
        var protectedHashes=new[]{"source.jsonl","force-config.json","capture-profile.json","case.json","force-observation.jsonl"}
            .ToDictionary(name=>name,name=>RecordingArtifacts.Sha256(Path.Combine(directory,name)));
        var trial=RecordedForceReplay.Trial(directory,candidatePath,candidateObservation);
        Check(!trial.Comparison.Equal&&trial.Comparison.Differences>0&&trial.CandidateConfigSha256==candidateHash,"separate tuning trial changes magnitudes under candidate config identity");
        Check(protectedHashes.All(pair=>RecordingArtifacts.Sha256(Path.Combine(directory,pair.Key))==pair.Value),"trial preserves source, case, baseline and original config/profile bytes");
        using(var trialHeader=JsonDocument.Parse(File.ReadLines(candidateObservation).First()))
            Check(trialHeader.RootElement.GetProperty("caseSha256").GetString()==RecordingArtifacts.Sha256(prepared.CasePath)&&
                trialHeader.RootElement.GetProperty("configSha256").GetString()==candidateHash,"trial retains original case identity with candidate config identity");

        string Mutated(string name,Func<string[],string[]> change)
        { string path=Path.Combine(directory,name);File.WriteAllLines(path,change(File.ReadAllLines(prepared.ObservationPath)),new System.Text.UTF8Encoding(false));return path; }
        bool Invalid(string path){try{RecordedForceReplay.Compare(prepared.ObservationPath,path);return false;}catch{return true;}}
        Check(Invalid(Mutated("invalid-no-footer.jsonl",lines=>lines[..^1])),"missing observation footer rejects");
        Check(Invalid(Mutated("invalid-case-id.jsonl",lines=>{lines[0]=lines[0].Replace("\"caseId\":\"fixture-drive\"","\"caseId\":\"../escape\"");return lines;})),"invalid case identifier rejects");
        Check(Invalid(Mutated("invalid-model-id.jsonl",lines=>{lines[0]=lines[0].Replace("\"model\":\"woden-force-signal@3\"","\"model\":\"bad/model\"");return lines;})),"invalid model identifier rejects");
        Check(Invalid(Mutated("invalid-negative-tick.jsonl",lines=>{lines[1]=System.Text.RegularExpressions.Regex.Replace(lines[1],"\"tick\":\\d+","\"tick\":-1");return lines;})),"negative first tick rejects");
        Check(Invalid(Mutated("invalid-shifted-timeline.jsonl",lines=>{lines[1]=System.Text.RegularExpressions.Regex.Replace(lines[1],"\"tick\":\\d+","\"tick\":1");return lines;})),"shifted timeline is structurally unavailable before magnitude comparison");

        void CopyCase(string target){Directory.CreateDirectory(target);foreach(string file in Directory.GetFiles(directory))File.Copy(file,Path.Combine(target,Path.GetFileName(file)));}
        string alteredSource=Path.Combine(directory,"altered-source");CopyCase(alteredSource);File.AppendAllText(Path.Combine(alteredSource,"source.jsonl")," ");
        bool sourceRefused=false;try{RecordedForceReplay.Trial(alteredSource,Path.Combine(alteredSource,"candidate-20-percent.json"),Path.Combine(alteredSource,"new-trial.jsonl"));}catch{sourceRefused=true;}
        Check(sourceRefused,"altered source cannot start a tuning trial");
        string alteredBaseline=Path.Combine(directory,"altered-baseline");CopyCase(alteredBaseline);
        string alteredBaselinePath=Path.Combine(alteredBaseline,"force-observation.jsonl");var alteredLines=File.ReadAllLines(alteredBaselinePath);
        alteredLines[1]=System.Text.RegularExpressions.Regex.Replace(alteredLines[1],"\"magnitude\":-?[0-9.Ee+]+","\"magnitude\":0.123");File.WriteAllLines(alteredBaselinePath,alteredLines,new System.Text.UTF8Encoding(false));
        bool baselineRefused=false;try{RecordedForceReplay.Trial(alteredBaseline,Path.Combine(alteredBaseline,"candidate-20-percent.json"),Path.Combine(alteredBaseline,"new-trial.jsonl"));}catch{baselineRefused=true;}
        Check(baselineRefused,"altered baseline cannot start a tuning trial");
        string idle=Path.Combine(directory,"idle");Directory.CreateDirectory(idle);
        string idleConfig=RecordingArtifacts.WriteForceConfig(Path.Combine(idle,"force-config.json"),options);
        string idleProfile=RecordingArtifacts.WriteCaptureProfile(Path.Combine(idle,"capture-profile.json"),new string('1',64),new string('2',64),new string('3',64),new string('4',64),false,false,true,"");
        var idleProperties=new Dictionary<string,string>(properties){["caseId"]="fixture-idle",["forceConfigSha256"]=idleConfig,["captureProfileSha256"]=idleProfile};
        using(var output=new TelemetryOutput(new(0,0,20,Path.Combine(idle,"source.jsonl")),"idle","synthetic","0.2.12",idleProperties)) output.Publish(Sample(0,state:"inactive"));
        bool idleRefused=false;try{RecordedForceReplay.Reprocess(idle);}catch(IOException ex){idleRefused=ex.Message.Contains("Insufficient driving coverage");}
        Check(idleRefused,"structurally valid idle-only recording cannot claim driving readiness");
    }
    finally
    {
        if (Environment.GetEnvironmentVariable("WODEN_KEEP_REPLAY_FIXTURE") == "1") Console.WriteLine("  replay fixture: " + directory);
        else Directory.Delete(directory,true);
    }
});
Test("actual force controller capture replays active, pause, camera, discontinuity and no-force gates", () => {
    string directory=Path.Combine(Path.GetTempPath(),"woden-controller-reprocess-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
    var options=new ForceOptions();string config=RecordingArtifacts.WriteForceConfig(Path.Combine(directory,"force-config.json"),options);
    string profile=RecordingArtifacts.WriteCaptureProfile(Path.Combine(directory,"capture-profile.json"),new string('1',64),new string('2',64),new string('3',64),new string('4',64),true,true,true,"fixture-guid");
    var properties=new Dictionary<string,string>{{"recordingContract","dbce.wheel.replay-case@1"},{"capability","signal-reprocess"},{"requestId",Guid.NewGuid().ToString()},
        {"caseId","controller-lifecycle"},{"disableForces","false"},{"gameAssemblySha256",new string('1',64)},{"pluginSha256",new string('2',64)},
        {"pluginProductVersion","0.2.12+fixture-source"},{"runtimeSource","fixture-source"},{"forceConfigSha256",config},{"captureProfileSha256",profile}};
    var (controller,_)=WodenRallyEdge.ForceControllerChecks.Create();double time=0;controller.Prepare();
    try
    {
        using(var output=new TelemetryOutput(new(0,0,20,Path.Combine(directory,"source.jsonl")),"controller-fixture","synthetic","0.2.12",properties))
        {
            void Emit(TelemetrySample sample){controller.Tick(sample);output.Publish(sample);}
            for(int i=0;i<60;i++)Emit(Contact(time+=.02));
            Emit(Contact(time+=.02,state:"paused"));
            WodenRallyEdge.MountedCamera.PlayerOwned=false;Emit(Contact(time+=.02));WodenRallyEdge.MountedCamera.PlayerOwned=true;
            var discontinuity=Contact(time+=.02);discontinuity.Discontinuity="wall-time-gap";Emit(discontinuity);
            for(int i=0;i<60;i++)Emit(Contact(time+=.02,slip:-.2));
            WodenRallyEdge.Runtime.DiagnosticNoForce=true;for(int i=0;i<3;i++)Emit(Contact(time+=.02));
        }
        var rows=SessionReader.Read(Path.Combine(directory,"source.jsonl")).Where(x=>x.Kind==SessionRecordKind.Sample).Select(x=>x.Sample).ToArray();
        Check(rows.Any(x=>x.Channels["ffb.gate"]==0)&&rows.Any(x=>x.Channels["ffb.gate"]==9)&&rows.Any(x=>x.Channels["ffb.gate"]==7)&&rows.Any(x=>x.Channels["ffb.gate"]==1),"recording retains active, invalid-model, camera and diagnostic gates");
        Check(rows.Any(x=>x.Channels["ffb.modelReason"]==11&&x.Channels["ffb.modelResetAfter"]-x.Channels["ffb.modelResetBefore"]==2),"discontinuity retains model and gate reset ordering");
        Check(rows.Where(x=>x.Channels["ffb.gate"]==1).All(x=>x.Channels["ffb.modelResetAfter"]-x.Channels["ffb.modelResetBefore"]==1),"default no-force gate resets shaping after every valid model sample");
        var prepared=RecordedForceReplay.Reprocess(directory);Check(prepared.DrivingSamples>=120&&prepared.ModelSamples==rows.Length,"actual controller lifecycle recording reprocesses completely");
    }
    finally
    {
        WodenRallyEdge.Runtime.DiagnosticNoForce=false;WodenRallyEdge.MountedCamera.PlayerOwned=true;controller.Shutdown();
        Directory.Delete(directory,true);
    }
});
Test("native handbrake adaptation restores boxed tuning and preserves game transient state", () => WodenRallyEdge.HandbrakeChecks.Run(Check));
Test("UX view migration, scoped camera defaults, additive bindings and inversion persistence", () => WodenRallyEdge.UxChecks.SettingsAndBindings(Check));
Test("strict follow/override output selection and camera release/repeat gates", () => WodenRallyEdge.UxChecks.SelectionAndRepeat(Check));
Test("atomic telemetry reconfiguration preserves capture and independent Stop", () => WodenRallyEdge.UxChecks.NetworkCapture(Check));
Test("actual controller drains producers, serializes shutdown and requires explicit restart", () => WodenRallyEdge.LifecycleChecks.Run(Check, t => Contact(t)));
Test("versioned production FFB reference envelopes, cadence and terminal zero", () => WodenRallyEdge.FfbRegressionChecks.Run(Check));
Test("concurrent summary creation preserves the other creator's exact bytes", () => WodenRallyEdge.FfbRegressionChecks.ConcurrentCreation(Check));
Test("bounded canonical command capture uses actual controller and pure fake replay", () => WodenRallyEdge.CommandCaptureChecks.Run(Check, t => Contact(t)));
Test("summary CLI bounds ordinary failures and malformed requests without running fixtures", () => WodenRallyEdge.FfbSummaryCommandChecks.Run(Check));
Console.WriteLine($"{passed} suites passed; {failed} failed; {checks} assertions.");
return failed == 0 ? 0 : 1;
