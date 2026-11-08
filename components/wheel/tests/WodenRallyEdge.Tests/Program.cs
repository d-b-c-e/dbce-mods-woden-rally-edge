using System.Numerics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Dbce.Wheel.Telemetry;
using Dbce.Wheel.Ui;
using WodenRallyEdge.Core;

if (args.Length > 0 && args[0] == "--ffb-summary")
    return WodenRallyEdge.FfbSummaryCommand.Run(args, Console.Error);
string? only = args.Length == 2 && args[0] == "--only" ? args[1] : null;
if (args.Length > 0 && only == null) { Console.Error.WriteLine("Use --only with one exact suite name."); return 2; }
int passed = 0, failed = 0, checks = 0;
void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
void Near(double actual, double expected, string name, double tolerance = .0001) => Check(Math.Abs(actual - expected) < tolerance, $"{name}: {actual} != {expected}");
void Test(string name, Action action) { if (only != null && name != only) return; try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex); } }
TelemetrySample Sample(double time, float velocity = 10, float position = 0, int car = 1, string state = "driving", double? wall = null) {
    var s = new TelemetrySample { SessionId = "synthetic-fixture", ElapsedSeconds = wall ?? time, SimulationSeconds = time, CarInstanceId = car, State = state };
    s.Vector("motion.position.world", new(position, 0, 0)); s.Vector("motion.velocity.world", new(velocity, 0, 0)); return s;
}

Test("single-key saves preserve file bytes and refuse ambiguous input", () => ConfigLineChecks.Run(Check));

Test("calibration endpoints, asymmetric centre, inverted pedals and deadzone", () => {
    var steer = new AxisCalibration(0, 65535, 30000, .02);
    Near(steer.Normalize(0), -1, "left"); Near(steer.Normalize(30000), 0, "centre"); Near(steer.Normalize(65535), 1, "right");
    Near(steer.Normalize(30050), 0, "deadzone");
    var invert = new AxisCalibration(65535, 0, 30000); Near(invert.Normalize(0), 1, "inverted right"); Near(invert.Normalize(65535), -1, "inverted left");
    var pedal = new AxisCalibration(60000, 1000); Near(pedal.Normalize(60000), 0, "rest"); Near(pedal.Normalize(1000), 1, "full"); Near(pedal.Normalize(30500), (.5 - .03) / .97, "half");
    Check(new AxisCalibration(0, 65535).Normalize(1966) == 0 && new AxisCalibration(0, 65535).Normalize(1967) > 0, "zero-deadzone pedal gets the 3% rest floor");
    Check(!new AxisCalibration(1, 1).Valid, "zero span rejected"); Check(!new AxisCalibration(0, 100, 100).Valid, "centre endpoint rejected");
    Check(!new AxisCalibration(0, 100, null, double.NaN).Valid, "NaN deadzone rejected");
    var quietBrake = new AxisCalibration(0, 65535, Deadzone: .03);
    foreach (int raw in new[] { 0, 830, 913, 927, 1966 })
        Check(quietBrake.Normalize(raw) == 0, "pedal rest/noise maps to exact zero: " + raw);
    Check(quietBrake.Normalize(1967) > 0, "input above explicit deadzone remains available");
    Check(quietBrake.Normalize(65535) == 1, "deadzone preserves full pedal travel");
    Near(quietBrake.Normalize(32768), (32768d / 65535 - .03) / .97, "pedal travel rescales above deadzone");
    Check(new AxisCalibration(65535, 0, Deadzone: .03).Normalize(65535 - 927) == 0,
        "reversed pedal uses the same explicit rest deadzone");
Check(string.Join("|", TripleLayoutChoice.Labels) == "Off|Surround|Separate monitors", "STD-022 selector labels"); Check(TripleLayoutChoice.Current(true, false) == 0 && TripleLayoutChoice.Current(true, true) == 0, "views off wins over a saved span"); Check(TripleLayoutChoice.Current(false, false) == 1 && TripleLayoutChoice.Current(false, true) == 2, "Surround vs Separate"); Check(TripleLayoutChoice.Apply(0) == (true, false) && TripleLayoutChoice.Apply(1) == (false, false) && TripleLayoutChoice.Apply(2) == (false, true), "Off also ends the span at the next start"); for (int tl = 0; tl < 3; tl++) { var tc = TripleLayoutChoice.Apply(tl); Check(TripleLayoutChoice.Current(tc.Off, tc.Span) == tl, "selector round trip " + tl); }
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
Test("Forza gear shows R exactly when the HUD does: TrueSpeed below -10 km/h", () => {
    foreach (var (hud, expected) in new[] { (-36.0, 0), (-11.0, 0), (-10.0, 1), (36.0, 1) }) {
        var s = Sample(1); new MotionProcessor().Process(s, Quaternion.Identity); s.Add("game.gear", 1); s.Add("game.trueSpeed", hud);
        Check(ForzaProjection.Map(s).Gear == expected, $"TrueSpeed {hud} gear {expected}");
    }
});
Test("Forza gear shows the emulated H-pattern selection: out of gear is N (11), the R gate R (0)", () => {
    foreach (var (gate, gear, hud, expected) in new[] { (0, 3, 40.0, 11), (HPatternShifter.Reverse, 1, 0.0, 0), (HPatternShifter.Reverse, 1, -36.0, 0), (3, 2, 40.0, 2) }) {
        var s = Sample(1); new MotionProcessor().Process(s, Quaternion.Identity); s.Add("game.gear", gear); s.Add("game.trueSpeed", hud); s.Add("wheelInput.gate", gate);
        Check(ForzaProjection.Map(s).Gear == expected, $"gate {gate} gear {gear} shows {expected}");
    }
});
Test("Forza speed is the HUD's abs(TrueSpeed) in m/s, reverse included", () => {
    foreach (var hud in new[] { 36.0, -36.0 }) {
        var s = Sample(1); new MotionProcessor().Process(s, Quaternion.Identity); s.Add("game.trueSpeed", hud);
        Near(ForzaProjection.Map(s).Speed, 10, $"TrueSpeed {hud} shows 36 km/h");
    }
});
Test("Forza lap fields: 0-based lap, 1-based rank, seconds, no-lap sentinel and segment distance are 0", () => {
    var s = Sample(1); new MotionProcessor().Process(s, Quaternion.Identity);
    s.Add("motion.distance", 500); s.Add("game.laps", 2); s.Add("game.rank", 1);
    s.Add("game.lastLap", 105.083); s.Add("game.bestLap", 5999.9990234375); s.Add("game.lapTime", 12.5); s.Add("game.raceTime", 117.583);
    var f = ForzaProjection.Map(s);
    Check(f.LapNumber == 1 && f.RacePosition == 1, "second lap is Forza lap 1; rank 1 is P1");
    Near(f.LastLap, 105.083, "last lap s"); Near(f.BestLap, 0, "no lap yet is 0"); Near(f.CurrentLap, 12.5, "lap time"); Near(f.CurrentRaceTime, 117.583, "race time");
    Near(f.DistanceTraveled, 0, "segment distance is not race distance");
});
Test("full detailed sample is finite JSON below UDP budget", () => {
    var s = Sample(1);
    foreach (var channel in TelemetrySchema.Channels.Keys) s.Add(channel, 1.123456789);
    var bytes = JsonSerializer.SerializeToUtf8Bytes(s, TelemetryOutput.Json);
    Check(bytes.Length < 60000, "bounded datagram"); using var doc = JsonDocument.Parse(bytes);
    Check(doc.RootElement.GetProperty("version").GetInt32() == 1, "schema version");
    Check(doc.RootElement.GetProperty("channels").EnumerateObject().Count() == TelemetrySchema.Channels.Count, "all channels preserved");
});
Test("loopback packet delivery, stale idle and bounded stop", () => {
    using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)); receiver.Client.ReceiveTimeout = 2000;
    int port = ((IPEndPoint)receiver.Client.LocalEndPoint!).Port;
    var output = new TelemetryOutput(new(port, 0, 20));
    var s = Sample(.02); new MotionProcessor().Process(s, Quaternion.Identity); output.Publish(s);
    IPEndPoint endpoint = new(IPAddress.Any, 0); var bytes = receiver.Receive(ref endpoint);
    Check(bytes.Length == 324 && BitConverter.ToInt32(bytes, 0) == 1, "active packet received");
    bool idle = false; var timeout = System.Diagnostics.Stopwatch.StartNew();
    while (timeout.Elapsed.TotalSeconds < 2 && !idle) idle = BitConverter.ToInt32(receiver.Receive(ref endpoint), 0) == 0;
    Check(idle, "stale source parks dashboard independently of Unity Update");
    output.Dispose(); output.Dispose(); Check(output.Stopped, "worker joined");
});
Test("disabled network delivery sends no packets", () => {
    using var output = new TelemetryOutput(new(8000, 8001, 20, Enabled: false));
    output.Publish(Sample(.02)); Thread.Sleep(120);
    Check(output.ForzaPackets == 0 && output.DetailPackets == 0, "no loopback packets are sent");
});
Test("invalid clocks are reported without killing output", () => {
    using var output = new TelemetryOutput(new(0, 0));
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
Test("grip model v4: art of rally's shared curve on the estimated front lateral force, art's strength scale", () => {
    Near(GripSignal.Curve(.1, .2, 1, .5, .75), .75, "first piece eases out to a flat extremum");
    Near(GripSignal.Curve(.2, .2, 1, .5, .75), 1, "extremum value"); Near(GripSignal.Curve(.19999, .2, 1, .5, .75), 1, "flat tangent at the extremum", 1e-6);
    Near(GripSignal.Curve(-.35, .2, 1, .5, .75), .875, "second piece: smoothstep toward the asymptote");
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
    Near(twicePeak.Preview, -(10000 * GripSignal.Curve(.4, .2, 1, .5, .75) * .6) / 20000, "past the peak the force falls and the trail lightens", 1e-5);
    Check(Math.Abs(twicePeak.Preview) < Math.Abs(atPeak.Preview), "the wheel lightens as the front slides");
    Near(g.Evaluate(GripSample(2.24, -.2), options).Preview, .4, "symmetric", 1e-5);
    Near(g.Evaluate(GripSample(2.26, .2), options with { Strength = 100 }).Preview, -.8, "Strength 100 doubles it, no 25% cap", 1e-5);
    Near(g.Evaluate(GripSample(2.28, .2), options with { Invert = true }).Preview, .4, "invert", 1e-5);
    Near(g.Evaluate(GripSample(2.30, .2, stiffness: .5), options).Preview, -.2, "the game's stiffness scales the rebuilt force", 1e-5);
    Near(g.Evaluate(GripSample(2.32, .2, speed: .7), options).Preview, 0, "faded below 3 km/h (0.7 m/s)");
});
Test("grip model v4: signed per-wheel sum, one wheel, airborne, start ramp (Codex design review)", () => {
    var options = new ForceOptions(Model: 4, GripSmoothing: 0, Damping: 0);
    var g = DrivenGrip();
    TelemetrySample Wheels(double time, double slipL, double slipR, double loadL = 5000, double loadR = 5000, bool groundL = true, bool groundR = true)
    {
        var s = GripSample(time, slipL);
        s.Channels["wheel.fr.sidewaysSlip"] = slipR; s.Channels["wheel.fl.contactForce"] = loadL; s.Channels["wheel.fr.contactForce"] = loadR;
        s.Channels["wheel.fl.grounded"] = groundL ? 1 : 0; s.Channels["wheel.fr.grounded"] = groundR ? 1 : 0; return s;
    }
    Near(g.Evaluate(Wheels(2.20, .2, -.2), options).Preview, 0, "opposed front tyres cancel", 1e-6);
    Near(g.Evaluate(Wheels(2.22, .2, .1), options).Preview, -(5000 * 1 + 5000 * .75) * AxleTrail(.75) / 20000, "unequal slips add signed", 1e-5);
    Near(g.Evaluate(Wheels(2.24, .2, -.2, 8000, 2000), options).Preview, -6000 * .8 / 20000, "unequal loads: the heavier side wins", 1e-5);
    Near(g.Evaluate(Wheels(2.26, .2, 0, groundR: false), options).Preview, -5000 * .8 / 20000, "one wheel grounded", 1e-5);
    var airborne = g.Evaluate(Wheels(2.28, .2, .2, groundL: false, groundR: false), options);
    Check(!airborne.Valid && airborne.Reason == "front wheels airborne", "both airborne stops the model");
    var r = DrivenGrip(); r.Reset();
    float start = r.Evaluate(GripSample(2.30, .2), options).Preview; float quarter = 0;
    for (int i = 1; i <= 12; i++) quarter = r.Evaluate(GripSample(2.30 + i * .02, .2), options).Preview;
    Check(start == 0 && Math.Abs(quarter - -.4 * .24 / GripSignal.RampSeconds) < 1e-4, $"0.5 s start ramp after a reset: {start}, {quarter}");
});
Test("grip model v4: Codex review of 82f2812 (stationary damping, poisoned reference, negative curve)", () => {
    var still = new ForceOptions(Model: 4, GripSmoothing: 0);   // default damping 0.05
    var g = DrivenGrip();
    for (int i = 0; i <= 30; i++) g.Evaluate(GripSample(2.20 + i * .02, 0), still);   // past the start ramp
    var rest = GripSample(2.82, 0, speed: 0); rest.Channels["wheelInput.steer"] = .2;
    Near(g.Evaluate(rest, still).Preview, 0, "a steering motion at rest writes no damping force (fades with the tyre force)", 1e-6);
    var poisoned = new GripSignal(); var options = new ForceOptions(Model: 4, GripSmoothing: 0, Damping: 0);
    for (int i = 0; i < 110; i++) { var bad = GripSample(i * .02, .1, speed: 25); bad.Channels["wheel.fl.contactForce"] = -4999; bad.Channels["wheel.fr.contactForce"] = 5000;
        Check(!poisoned.Evaluate(bad, options).Valid, "a negative load is rejected"); }
    Check(poisoned.Reference.Kind == FrontLoadReferenceKind.None && poisoned.Reference.Load == 0, "rejected rows do not enter the reference mean");
    for (int i = 110; i < 230; i++) poisoned.Evaluate(GripSample(i * .02, 0, speed: 25), options);
    Check(Math.Abs(poisoned.Reference.Load - 10000) < 1e-6, "good rows then measure the true load");
    var neg = GripSample(2.20, .2); foreach (var c in new[] { "fl", "fr" }) { neg.Channels["wheel." + c + ".sideFriction.extremumValue"] = -1; neg.Channels["wheel." + c + ".sideFriction.asymptoteValue"] = -.75; }
    var refused = DrivenGrip().Evaluate(neg, options);
    Check(!refused.Valid && refused.Reason == "friction curve unavailable", "negative curve magnitudes are refused");
    var zero = GripSample(2.20, .2); foreach (var c in new[] { "fl", "fr" }) zero.Channels["wheel." + c + ".sideFriction.stiffness"] = 0;
    var z = DrivenGrip().Evaluate(zero, options); Check(z.Valid && z.Preview == 0, "zero stiffness stays a valid zero-force observation");
});
double AxleTrail(double relative) => Dbce.Wheel.Ffb.AxleForceCurve.Trail((float)(relative * 8), 8);
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
    Check(settings.ForceOptions.Grip && settings.ForceOptions.Model == GripSignal.ModelVersion && settings.ForceOptions.LoadRatio == 1.5f && settings.ForceOptions.GripSmoothing == .2f && settings.ForceOptions.Damping == .05f, "the mod defaults to the grip model at the owner-accepted tune");
    string old = Path.Combine(Path.GetTempPath(), "woden-old-" + Guid.NewGuid().ToString("N") + ".cfg"); File.WriteAllText(old, "[ForceFeedback]\nStrengthPercent = 49.583332\nModel = Classic\nPeakPercent = 25\n");
    var oldSettings = new WodenRallyEdge.Settings(new BepInEx.Configuration.ConfigFile(old, false));
    Check(oldSettings.ForceOptions.Model == GripSignal.ModelVersion && oldSettings.FfbStrength == 49.583332f, "a saved Model or PeakPercent key is ignored: Grip only, Strength kept");
});
Test("grip model v5: Steering strength scales the tyre force only; v4 keeps its coupled damping for replay", () => {
    Check(GripSignal.ModelVersion == 8 && GripSignal.IsGripModel(4) && GripSignal.IsGripModel(5) && GripSignal.IsGripModel(6) && GripSignal.IsGripModel(7) && GripSignal.IsGripModel(8) && !GripSignal.IsGripModel(3), "grip models are 4 to 8; 8 is the latest");
    Check(new ForceOptions(Model: 5).Grip && new ForceOptions(Model: 4).Grip && !new ForceOptions(Model: 3).Grip, "ForceOptions.Grip covers v4 and v5");
    Check(ForceObservationSemantics.ModelId(5) == "woden-grip-signal@5" && ForceObservationSemantics.ModelId(4) == "woden-grip-signal@4" &&
        ForceObservationSemantics.ConfigVersion(5) == 3 && ForceObservationSemantics.ModelForConfigVersion(2) == 4 && ForceObservationSemantics.ModelForConfigVersion(3) == 5, "v5 has its own identity and force-config version");
    bool refused = false; try { ForceObservationSemantics.ModelId(9); } catch (IOException) { refused = true; } Check(refused, "an unknown model version is refused");
    // The same tyre inputs with the wheel moving every tick: at 50 the versions agree; at 100 only v4 doubles the damping.
    (float Preview, float Steering, float Damping) Run(int model, float strength)
    {
        var g = new GripSignal(); var o = new ForceOptions(Strength: strength, Model: model, GripSmoothing: 0, Damping: .2f); ForceResult? r = null;
        for (int i = 0; i < 160; i++) { var s = GripSample(i * .02, .12, 10000, 40); s.Channels["wheelInput.steer"] = Math.Sin(i * .3) * .5; r = g.Evaluate(s, o); }
        Check(r!.Valid, "the fixture drives a valid grip model");
        return (r.Preview, g.LastSteering, g.LastDamping);
    }
    var v4at50 = Run(4, 50); var v5at50 = Run(5, 50); var v4at100 = Run(4, 100); var v5at100 = Run(5, 100);
    Check(v4at50 == v5at50, "at Steering strength 50 v4 and v5 are identical");
    Check(Math.Abs(v5at50.Damping) > 1e-4 && Math.Abs(v5at50.Steering) > 1e-3, "the fixture exercises both terms");
    Near(v5at100.Steering, 2 * v5at50.Steering, "the tyre term scales with Steering strength", 1e-5);
    Near(v5at100.Damping, v5at50.Damping, "v5: Steering strength does not scale the damping", 1e-6);
    Near(v4at100.Damping, 2 * v4at50.Damping, "v4: Strength still scales the damping (exact replay of v4 recordings)", 1e-6);
    Near(v5at100.Preview, Math.Clamp(v5at100.Steering + v5at100.Damping, -1, 1), "the preview is the clamped sum of the two recorded terms", 1e-5);
    var fresh = new WodenRallyEdge.Settings(new BepInEx.Configuration.ConfigFile(Path.Combine(Path.GetTempPath(), "woden-v5-" + Guid.NewGuid().ToString("N") + ".cfg"), false));
    Check(fresh.ForceOptions.Model == 8 && fresh.FfbLoadRatio == 1.5f && fresh.FfbDamping == .05f, "a fresh config runs grip v8 at the owner-accepted defaults");
});
Test("H-pattern: a gate steps the game's own shift actions, one press at a time, never during a change", () => {
    var h = new HPatternShifter(); double now = 0; int gear = 1, presses = 0; bool held = false;
    // Drive the policy against a fake gearbox that changes gear on each new press after a 0.1 s change.
    double changingUntil = -1;
    void Frame(int gate, int gears = 5, bool automatic = false)
    {
        now += .02; bool changing = now < changingUntil;
        var (up, down) = h.Step(gate, gear, gears, automatic, changing, now);
        bool press = up || down;
        if (press && !held) { presses++; gear += up ? 1 : -1; changingUntil = now + .1; }
        held = press;
    }
    for (int i = 0; i < 100; i++) Frame(4);
    Check(gear == 4 && presses == 3, $"gate 4 from first gear: three separate up presses ({gear}, {presses})");
    for (int i = 0; i < 100; i++) Frame(2);
    Check(gear == 2 && presses == 5, "gate 2: two down presses");
    for (int i = 0; i < 100; i++) Frame(6, gears: 5);
    Check(gear == 5, "a gate beyond the car's gears selects its top gear");
    int before = presses; for (int i = 0; i < 50; i++) Frame(0);
    Check(presses == before && h.Status.Contains("Out of gear"), "out of gear never shifts");
    for (int i = 0; i < 200; i++) Frame(HPatternShifter.Reverse);
    Check(gear == 1 && h.Status.Contains("R"), "R steps down to first gear");
    before = presses; for (int i = 0; i < 50; i++) Frame(3, automatic: true);
    Check(presses == before && h.Status.Contains("manual"), "an automatic gearbox is left alone with a status");
    var (t1, b1) = HPatternShifter.Pedals(HPatternShifter.Reverse, .7f, .2f); var (t0, b0) = HPatternShifter.Pedals(0, .7f, .2f); var (t3, b3) = HPatternShifter.Pedals(3, .7f, .2f);
    Check(t1 == .2f && b1 == .7f && t0 == 0 && b0 == .2f && t3 == .7f && b3 == .2f, "R swaps the pedals, out of gear cuts the throttle, a gear passes them through");
});
Test("H-pattern: automatic pedals ignore neutral and reverse while manual emulation is retained", () => {
    foreach (var (gate, manualThrottle, manualBrake) in new[] { (0, 0f, .2f), (HPatternShifter.Reverse, .2f, .7f), (3, .7f, .2f) })
    {
        var automatic = HPatternShifter.Pedals(gate, .7f, .2f, automatic: true);
        Check(automatic == (.7f, .2f), $"automatic gate {gate} preserves throttle and brake");
        var manual = HPatternShifter.Pedals(gate, .7f, .2f, automatic: false);
        Check(manual == (manualThrottle, manualBrake), $"manual gate {gate} retains neutral/reverse/forward pedals");
        var h = new HPatternShifter();
        h.Step(6, 1, 6, false, false, 0); // Start a manual shift before switching to automatic.
        var shift = h.Step(gate, 1, 6, true, false, .01);
        Check(!shift.Up && !shift.Down && h.Status.Contains("manual"), $"automatic gate {gate} stops shifting and keeps the manual-mode hint");
    }
});
Test("shared force shaping: symmetric sign, literal gain, cap, ramp and low-speed fade", () => {
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
    Check(FovLockText.Angle(33.6) == "33.6° vertical" && FovLockText.Angle(null) == "the rig settings" && FovLockText.Angle(double.NaN) == "the rig settings" &&
          FovLockText.KeyMessage(33.6).Contains(FovLockText.SwitchPath) && FovLockText.ViewHint(true, null).StartsWith("Bumper FOV is set by the rig settings (the rig settings"),
          "FOV lock texts name the actual angle, fall back without one, and give the one switch");
    string shipped = FovLockText.RigSettings(708.4, 398.5, 660, 70, 8, true), mine = FovLockText.RigSettings(597, 336, 620, 55, 6, false);
    Check(shipped.StartsWith("708.4 × 398.5 mm screens, eyes 660 mm from the center screen, sides turned 70°, bezel 8 mm") && shipped.Contains("shipped defaults, not your measurements") &&
          new[] { "PanelWidthMm", "PanelHeightMm", "EyeDistanceMm", "SideAngle", "BezelMm", FovLockText.ConfigFile, "Game field of view" }.All(s => shipped.Contains(s)) &&
          mine.StartsWith("597 × 336 mm screens, eyes 620 mm") && !mine.Contains("default") && mine.Contains(FovLockText.ConfigFile) &&
          !FovLockText.KeyMessage(33.6).Contains("your monitors") && !FovLockText.ViewHint(false, 33.6).Contains("your rig"),
          "Measured-perspective text shows the geometry in use and names untouched values as shipped defaults, never as the player's measurements");
    Check(FovModeSwitch.AdjustableStart(33.62, true, true) == 33.6f && FovModeSwitch.AdjustableStart(33.6, false, true) == null &&
          FovModeSwitch.AdjustableStart(33.6, true, false) == null && FovModeSwitch.AdjustableStart(null, true, true) == null &&
          FovModeSwitch.AdjustableStart(double.NaN, true, true) == null && FovModeSwitch.AdjustableStart(20, true, true) == 30f &&
          FovModeSwitch.AdjustableStart(130, true, true) == 110f,
          "Adjustable starts at the shown angle only for a saved mounted lens on screen, within the lens bounds");
    // Owner rig (97BBB): 708.4 x 398.5 mm panels, 660 mm eye, 70 degree sides, 8 mm bezel; saved bonnet lens 30 (the floor).
    var rig = WodenRallyEdge.TripleGeometry.Compute(708.4, 398.5, 660, 70, 8);
    double shown = (Math.Atan(rig[1].Top) - Math.Atan(rig[1].Bottom)) * 180 / Math.PI;
    var start = FovModeSwitch.AdjustableStart(shown, true, true);
    Check(start is > 30f and < 40f, $"the owner's Physical center angle is the start, not the stale 30 floor: {start}");
    Near(WodenRallyEdge.TripleGeometry.EyeDistanceForView(398.5, 660, true, true, MountedView.Bonnet, 70, start), 660, "switching to Adjustable keeps the shown projection", 1.5);
    Check(WodenRallyEdge.TripleGeometry.EyeDistanceForView(398.5, 660, true, true, MountedView.Bonnet, 70, 30) > 700,
          "adopting the stale 30 degree lens would have narrowed the bonnet on the switch");
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
Test("triple FOV policy preserves physical geometry and coherently follows mounted lenses", () => {
    const double width = 708.4, height = 398.5, physicalEye = 660;
    var baseline = WodenRallyEdge.TripleGeometry.Compute(width, height, physicalEye, 70, 8);
    double Eye(bool match, bool chase, MountedView view, double game, double? mounted) =>
        WodenRallyEdge.TripleGeometry.EyeDistanceForView(height, physicalEye, match, chase, view, game, mounted);
    foreach (var view in new[] { MountedView.Bonnet, MountedView.Bumper })
    {
        foreach (double lens in new[] { 30d, 110d })
        foreach (double game in new[] { 45d, 100d })
        {
            double eye = Eye(false, true, view, game, lens);
            Near(eye, physicalEye, "physical eye remains measured");
            Check(WodenRallyEdge.TripleGeometry.Compute(width, height, eye, 70, 8).SequenceEqual(baseline),
                "physical mode keeps all three original frusta");
        }
        WodenRallyEdge.PanelFrustum[]? previous = null;
        foreach (double lens in new[] { 50d, 80d })
        {
            double eye = Eye(true, true, view, 45, lens);
            Near(Eye(true, true, view, 100, lens), eye, "saved mounted lens avoids game-field feedback");
            var views = WodenRallyEdge.TripleGeometry.Compute(width, height, eye, 70, 0);
            Near((Math.Atan(views[1].Top) - Math.Atan(views[1].Bottom)) * 180 / Math.PI,
                lens, "center vertical angle follows saved mounted lens");
            for (int i = 0; i < 3; i++)
            {
                Near(views[i].YawDegrees, (i - 1) * 70, "panel yaw retained");
                if (previous != null) Check(views[i].Left != previous[i].Left && views[i].Top != previous[i].Top,
                    "lens adjustment recalculates each projection");
            }
            // Zero-bezel adjoining corners must represent the same world-space ray.
            (double X, double Y, double Z) Ray(WodenRallyEdge.PanelFrustum panel, double x, double y)
            {
                double angle = panel.YawDegrees * Math.PI / 180;
                double rx = Math.Cos(angle) * x + Math.Sin(angle), rz = -Math.Sin(angle) * x + Math.Cos(angle);
                double length = Math.Sqrt(rx * rx + y * y + rz * rz);
                return (rx / length, y / length, rz / length);
            }
            void SameRay((double X, double Y, double Z) a, (double X, double Y, double Z) b)
            { Near(a.X, b.X, "seam ray x", 1e-9); Near(a.Y, b.Y, "seam ray y", 1e-9); Near(a.Z, b.Z, "seam ray z", 1e-9); }
            foreach (bool top in new[] { false, true })
            {
                SameRay(Ray(views[1], views[1].Right, top ? views[1].Top : views[1].Bottom),
                    Ray(views[2], views[2].Left, top ? views[2].Top : views[2].Bottom));
                SameRay(Ray(views[1], views[1].Left, top ? views[1].Top : views[1].Bottom),
                    Ray(views[0], views[0].Right, top ? views[0].Top : views[0].Bottom));
            }
            previous = views;
        }
    }
    double gameEye = WodenRallyEdge.TripleGeometry.EyeDistanceForVerticalFov(height, 45);
    Near(Eye(false, true, MountedView.Stock, 45, 80), gameEye, "stock chase retains game lens");
    Near(Eye(false, false, MountedView.Stock, 45, 80), physicalEye, "stock physical override retained");
    Near(Eye(true, false, MountedView.Stock, 45, 80), physicalEye, "chase Realistic is independent of a mounted Game choice");
    Near(Eye(true, true, MountedView.Bonnet, 45, null), gameEye, "unowned mount retains game fallback");
    Near(Eye(true, true, MountedView.Bumper, 45, double.NaN), physicalEye, "invalid lens safely keeps measured eye");
});
Test("curved rig: radius 0 reproduces TripleGeometry, curvature widens the source, the session key alternates", () => {
    var reference = WodenRallyEdge.TripleGeometry.Compute(708.4, 398.5, 660, 70, 8);
    var flat = WodenRallyEdge.CurvedRig.Compute(708.4, 398.5, 660, 70, 8, 0);
    Check(flat.Warps == null, "radius 0 has no warps");
    for (int i = 0; i < 3; i++)
    {
        Near(flat.Views[i].YawDegrees, reference[i].YawDegrees, "toolkit rig yaw " + i, 1e-9);
        Near(flat.Views[i].Left, reference[i].Left, "toolkit rig left " + i, 1e-9); Near(flat.Views[i].Right, reference[i].Right, "toolkit rig right " + i, 1e-9);
        Near(flat.Views[i].Bottom, reference[i].Bottom, "toolkit rig bottom " + i, 1e-9); Near(flat.Views[i].Top, reference[i].Top, "toolkit rig top " + i, 1e-9);
    }
    var curved = WodenRallyEdge.CurvedRig.Compute(708.4, 398.5, 660, 70, 8, 1500);
    Check(curved.Warps is { Length: 3 } && curved.Warps.All(w => !w.IsFlat), "1500R gives three warps");
    // Shared 74c5eda defines distance at the panel-edge plane, not the glass
    // middle. Centre boundary rays therefore remain equal to the flat panel;
    // the interior UVs account for the glass receding behind those edges.
    Near(curved.Views[1].Top, reference[1].Top, "curved centre top stays on the measured edge plane", 1e-9);
    Near(curved.Views[1].Right, reference[1].Right, "curved centre right stays on the measured edge plane", 1e-9);
    Near(curved.Views[0].YawDegrees, -70, "curved left yaw", 1e-9); Near(curved.Views[2].YawDegrees, 70, "curved right yaw", 1e-9);
    curved.Warps![1].SourceUv(0.5, 0.5, out double cu, out double cv);
    Near(cu, 0.5, "centre column samples the source centre", 1e-9); Near(cv, 0.5, "centre row samples the source centre", 1e-9);
    for (int i = 0; i < 3; i++) for (int gu = 0; gu <= 8; gu++) for (int gv = 0; gv <= 8; gv++)
    { curved.Warps[i].SourceUv(gu / 8d, gv / 8d, out double su, out double sv); Check(su >= -1e-9 && su <= 1 + 1e-9 && sv >= -1e-9 && sv <= 1 + 1e-9, "every panel point samples inside its source " + i); }
    bool refused = false; try { WodenRallyEdge.CurvedRig.Compute(708.4, 398.5, 660, 70, 8, 300); } catch (ArgumentOutOfRangeException) { refused = true; }
    Check(refused, "a radius under half the panel width is refused");
    // F7 comparison cycle (toolkit policy revision 2): from what is shown now, 1 original, 2 angle, 3 angle + curve when available.
    {
        var g = typeof(WodenRallyEdge.TripleGeometry);
        Check(WodenRallyEdge.TripleGeometry.CurrentStep(null, false) == 2 && WodenRallyEdge.TripleGeometry.CurrentStep(null, true) == 3, "saved views show angle correction, with the curve when configured");
        Check(WodenRallyEdge.TripleGeometry.NextStep(null, false) == 1 && WodenRallyEdge.TripleGeometry.NextStep(1, false) == 2 && WodenRallyEdge.TripleGeometry.NextStep(2, false) == 1, "flat rig: two steps, original then angle");
        Check(WodenRallyEdge.TripleGeometry.NextStep(null, true) == 1 && WodenRallyEdge.TripleGeometry.NextStep(1, true) == 2 && WodenRallyEdge.TripleGeometry.NextStep(2, true) == 3 && WodenRallyEdge.TripleGeometry.NextStep(3, true) == 1, "curved rig: three steps");
        Check(WodenRallyEdge.TripleGeometry.NextStep(3, false) == 1 && WodenRallyEdge.TripleGeometry.StepCount(false) == 2, "a curve that went away shortens the cycle from the last shown step");
        Check(WodenRallyEdge.TripleGeometry.ComparisonLabel(3, 3, true) == "Angle + curve correction (3/3) · Game FOV" &&
              WodenRallyEdge.TripleGeometry.ComparisonLabel(1, 2, false) == "Original game view (1/2) · Measured FOV" &&
              WodenRallyEdge.TripleGeometry.ComparisonLabel(2, 3, true, saved: false) == "Angle correction (2/3) · Game FOV · Couldn't save view", "labels name the actual mode, step of count and framing source; only a failed save is reported");
        Check(WodenRallyEdge.TripleGeometry.FromSaved(1) == null && WodenRallyEdge.TripleGeometry.FromSaved(2) == null && WodenRallyEdge.TripleGeometry.FromSaved(3) == null &&
              WodenRallyEdge.TripleGeometry.FromSaved(0) == null && WodenRallyEdge.TripleGeometry.FromSaved(4) == null && WodenRallyEdge.TripleGeometry.FromSaved(99) == null && WodenRallyEdge.TripleGeometry.FromSaved(-1) == null,
              "STD-041 retires all saved correction picks; normal startup is always corrected");
        // The pick is written as one line (ConfigLine): comments, unknown keys and absent defaults stay; a failure keeps the file.
        string dir = Path.Combine(Path.GetTempPath(), "woden-f7-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            string cfg = Path.Combine(dir, "test.cfg"), nl = Environment.NewLine;
            string before = "## comment" + nl + "[Force]" + nl + "Unknown = 7" + nl + nl + "[Triple]" + nl + "MatchGameFov = false" + nl;
            File.WriteAllText(cfg, before);
            ConfigLine.Write(cfg, "Triple", "CompareView", "2");
            Check(File.ReadAllText(cfg) == before + "CompareView = 2" + nl, "the pick adds one line; nothing else is written or materialised");
            ConfigLine.Write(cfg, "Triple", "CompareView", "3");
            Check(File.ReadAllText(cfg) == before + "CompareView = 3" + nl, "a new pick replaces its line in place");
            ConfigLine.Write(Path.Combine(dir, "new.cfg"), "Triple", "CompareView", "1");
            Check(File.ReadAllText(Path.Combine(dir, "new.cfg")) == nl + "[Triple]" + nl + "CompareView = 1" + nl, "a missing file gets the section and the pick");
            string kept = File.ReadAllText(cfg); bool threw = false;
            using (File.Open(cfg, FileMode.Open, FileAccess.Read, FileShare.None))
                try { ConfigLine.Write(cfg, "Triple", "CompareView", "1"); } catch (IOException) { threw = true; }
            Check(threw && File.ReadAllText(cfg) == kept && Directory.GetFiles(dir, "*.tmp-*").Length == 0, "a failed write throws, keeps the file and leaves no temp");
        }
        finally { Directory.Delete(dir, true); }
        Check(g.GetMethod("NextSessionGameFov") == null, "the measured/game framing A/B is no longer a comparison step");
    }
    // The saved framing policy still decides the eye; the game lens is the default for new settings (MatchGameFov true).
    Near(WodenRallyEdge.TripleGeometry.EyeDistanceForView(398.5, 660, true, true, MountedView.Stock, 45, null, false), 660, "measured session overrides saved game FOV in chase", 1e-9);
    Near(WodenRallyEdge.TripleGeometry.EyeDistanceForView(398.5, 660, false, false, MountedView.Bonnet, 45, 60, true), WodenRallyEdge.TripleGeometry.EyeDistanceForVerticalFov(398.5, 60), "game session uses the mounted lens", 1e-9);
    Near(WodenRallyEdge.TripleGeometry.EyeDistanceForView(398.5, 660, false, true, MountedView.Bonnet, 45, 60, null), 660, "no session keeps the saved policy", 1e-9);
    // Texture size bucket (TripleCurve.SizeBucket is Unity-bound; the same arithmetic): the owner rig at 1500R keeps one
    // bucket while a game FOV sweep moves the derived eye between 500 and 900 mm, so no per-frame texture rebuild.
    (double X, double Y) Bucket(double eye)
    {
        var w = WodenRallyEdge.CurvedRig.Compute(708.4, 398.5, eye, 70, 8, 1500).Warps!;
        double x = 1, y = 1;
        foreach (var warp in w) { x = Math.Max(x, warp.SourceSurface.Width / warp.Panel.Width); y = Math.Max(y, warp.SourceSurface.Height / warp.Panel.Height); }
        return (Math.Ceiling(x * 8) / 8, Math.Ceiling(y * 8) / 8);
    }
    var buckets = new HashSet<(double, double)>();
    for (double eye = 500; eye <= 900; eye += 25) buckets.Add(Bucket(eye));
    Check(buckets.Count <= 2, "a 500-900 mm eye sweep at 1500R touches at most two texture size buckets: " + buckets.Count);
    Check(Bucket(660).X >= 1 && Bucket(660).X <= 1.5 && Bucket(660).Y >= 1 && Bucket(660).Y <= 1.5, "owner rig bucket is modest: " + Bucket(660));
    // Presentation evidence in source frames: textures alone are not presented; a presenter that never draws or stops
    // drawing is reported after a bounded number of source frames; evidence resets per build.
    var evidence = new WodenRallyEdge.CurveEvidence();
    Check(!evidence.Presented && evidence.SourceFrame() == null, "nothing is presented before the first draw");
    evidence.Drew();
    Check(evidence.Presented && evidence.Drawn == 1, "a draw in the current source frame is presented");
    for (int f = 0; f < 2; f++) Check(evidence.SourceFrame() == null && evidence.Presented, "two source frames of grace " + f);
    Check(evidence.SourceFrame() == null && !evidence.Presented, "a third source frame without a draw is no longer presented");
    string? stall = null;
    for (int f = 0; f <= WodenRallyEdge.CurveEvidence.MissingFrames && stall == null; f++) stall = evidence.SourceFrame();
    Check(stall == "the presenter stopped drawing", "a stalled presenter is reported within the bounded source frames: " + stall);
    evidence.Reset(); stall = null;
    for (int f = 0; f <= WodenRallyEdge.CurveEvidence.MissingFrames && stall == null; f++) stall = evidence.SourceFrame();
    Check(stall == "the presenter never drew a frame" && evidence.Drawn == 0 && !evidence.Presented, "a presenter that never draws is reported and the evidence reset per build");
});
Test("real settings keep custom camera/FFB choices across restart", () => {
    string path = Path.Combine(Path.GetTempPath(), "woden-camera-config-" + Guid.NewGuid() + ".cfg");
    var fresh = new WodenRallyEdge.Settings(new BepInEx.Configuration.ConfigFile(path, false));
    Near(fresh.FfbStrength, 50, "new config default 50"); Check(fresh.CameraAutoFit, "new config body fit");
    File.WriteAllText(path, "[ForceFeedback]\nStrengthPercent = 42\nEnabled = false\n");
    var migrated = new WodenRallyEdge.Settings(new BepInEx.Configuration.ConfigFile(path, false));
    Near(migrated.FfbStrength, 42, "custom strength survives"); Check(!migrated.FfbEnabled, "saved Off survives");
    migrated.CameraAutoFit = false; migrated.SetCameraPose(false, new(.1f, 1.3f, .2f, 5, 80)); migrated.Save();
    var restored = new WodenRallyEdge.Settings(new BepInEx.Configuration.ConfigFile(path, false));
    Check(!restored.CameraAutoFit && restored.GetCameraPose(false) == migrated.GetCameraPose(false), "manual pose and auto-fit Off survive restart");
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
Test("shim main-window fallback never trusts console, foreign, hidden or stale windows", () => {
    uint self=(uint)Environment.ProcessId; int lookups=0;
    var candidates=new Dictionary<long,(uint owner,string kind,bool visible,bool live)> {
        [123]=(self,"UnityWndClass",true,true), [124]=(self,"ConsoleWindowClass",true,true),
        [125]=(self+1,"UnityWndClass",true,true), [126]=(self,"UnityWndClass",false,true),
        [127]=(self,"UnityWndClass",true,false)
    };
    bool Owned(IntPtr h) => candidates.TryGetValue(h.ToInt64(),out var c) && c.live && WodenRallyEdge.GameWindow.Eligible(h.ToInt64(),c.owner,c.kind,c.visible);
    IntPtr Main(long h) {lookups++;return new(h);}
    Check(WodenRallyEdge.GameWindow.SelectShimWindow(new(123),()=>Main(124),Owned)==123&&lookups==0,"verified Unity foreground wins without fallback lookup");
    foreach(long foreground in new long[]{0,124,125})
        Check(WodenRallyEdge.GameWindow.SelectShimWindow(new(foreground),()=>Main(123),Owned)==123,"verified current-process Unity main window can open shim behind non-Unity foreground");
    foreach(long main in new long[]{0,124,125,126,127,0x100001234})
        Check(WodenRallyEdge.GameWindow.SelectShimWindow(new(124),()=>Main(main),Owned)==0,"fallback refuses unavailable/non-owned/non-Unity/nonvisible/dead candidate "+main);
    Check(!WodenRallyEdge.GameWindow.IsOwned(0)&&!WodenRallyEdge.GameWindow.IsFocused(0),"zero is never owned or focused at the actual Windows boundary");
});
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
    foreach (var state in new[] { ready, green })
    {
        var panel = state with { PanelOpen = true };
        Check(panel.CameraShown && !panel.CameraAvailable && !panel.WheelAvailable && !panel.Driving, "open F6 keeps the mounted view only: " + panel);
    }
    foreach (var state in blocked.Where(s => !s.PanelOpen))
        Check(!(state with { PanelOpen = true }).CameraShown, "open F6 never shows the mounted view in an excluded context: " + state);
    var cycle = new CameraCycle(); cycle.StockChanged(4, 0, true, true);
    Check(cycle.View == MountedView.Bonnet && !cycle.Reconcile(0, true, true), "preselected bonnet survives normal countdown-to-race without preset change");
});
Test("actual action-table lease supplies countdown-capable controls and restores boxed native values", () => WodenRallyEdge.InputLeaseChecks.Run(Check));
Test("shim steering identity preserves force lifecycle and tune", () => WodenRallyEdge.ShimForceChecks.Run(Check));
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
Test("UX view migration, scoped camera defaults, additive bindings and inversion persistence", () => WodenRallyEdge.UxChecks.SettingsAndBindings(Check));
Test("strict follow/override output selection and camera release/repeat gates", () => WodenRallyEdge.UxChecks.SelectionAndRepeat(Check));
Test("atomic telemetry reconfiguration preserves capture and independent Stop", () => WodenRallyEdge.UxChecks.NetworkCapture(Check));
Test("actual controller drains producers, serializes shutdown and requires explicit restart", () => WodenRallyEdge.LifecycleChecks.Run(Check, t => Contact(t)));
Test("versioned production FFB reference envelopes, cadence and terminal zero", () => WodenRallyEdge.FfbRegressionChecks.Run(Check));
Test("concurrent summary creation preserves the other creator's exact bytes", () => WodenRallyEdge.FfbRegressionChecks.ConcurrentCreation(Check));
Test("bounded canonical command capture uses actual controller and pure fake replay", () => WodenRallyEdge.CommandCaptureChecks.Run(Check, t => Contact(t)));
Test("summary CLI bounds ordinary failures and malformed requests without running fixtures", () => WodenRallyEdge.FfbSummaryCommandChecks.Run(Check));
Test("grip v6 admits only finite continuous equal-knot curves; legacy arithmetic and admission stay exact", () => GripV6Checks.Curves(Check));
Test("grip v6 learns front load only from forward-valid rows; recorded replay reference stays exact", () => GripV6Checks.ReferenceDirection(Check));
Test("grip v8 centering: owner-accepted v7 at 50%, restoring from the first degrees, monotonic, none at standstill; damping resists", () => CenteringChecks.Run(Check));
Test("grip v8 through the controller: On reaches the device; saved Off and diagnostic mute write nothing", () => CenteringChecks.Gates(Check));
Test("ordinary HUD depth preserves ideal pixels and bounds isolated-world float precision", () => WodenRallyEdge.HudDepthChecks.Run(Check));
Test("native gearbox preference sets only the selected player's saved choice, once, never for tooling or malformed records", () => WodenRallyEdge.NativeGearboxChecks.Run(Check));
Test("the separate-monitor span takes the toolkit racing row: the four-display rig spans only the racing displays", () => WodenRallyEdge.SpanTargetChecks.Run(Check));
Test("a game window outside the racing row moves to its primary display, never to a dashboard; Unity's start monitor is saved only when its index is certain", () => WodenRallyEdge.WindowPlacementChecks.Run(Check));
Console.WriteLine($"{passed} suites passed; {failed} failed; {checks} assertions.");
if (only != null && passed + failed != 1) { Console.Error.WriteLine("Exact suite was not found."); return 2; }
return failed == 0 ? 0 : 1;
