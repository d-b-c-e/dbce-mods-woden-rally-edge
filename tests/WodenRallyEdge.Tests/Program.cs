using System.Numerics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Dbce.Wheel.Telemetry;
using Dbce.Wheel.Recording;
using WodenRallyEdge.Core;

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
Test("raw game units never masquerade as RPM, fuel percent or tyre slip radians", () => {
    var s = Sample(1); new MotionProcessor().Process(s, Quaternion.Identity);
    s.Add("game.rpm", .8); s.Add("game.fuel", 92); s.Add("wheel.fl.sidewaysSlip", .75); s.Add("game.gear", 0);
    s.Add("wheel.fl.angularSpeed", 42); s.Add("wheel.fr.angularSpeed", 43); s.Add("wheel.rl.angularSpeed", 44); s.Add("wheel.rr.angularSpeed", 45);
    var f = ForzaProjection.Map(s);
    Near(f.CurrentEngineRpm, 0, "no guessed rpm"); Near(f.EngineMaxRpm, 0, "no redline guess"); Near(f.Fuel, 0, "no fuel scale guess"); Near(f.TireSlipAngle.FrontLeft, 0, "no Unity slip as angle");
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
    var output = new TelemetryOutput(new(port, 0, 20, path), "synthetic-fixture", "test-created fixture, not gameplay");
    var s = Sample(.02); new MotionProcessor().Process(s, Quaternion.Identity); output.Publish(s);
    IPEndPoint endpoint = new(IPAddress.Any, 0); var bytes = receiver.Receive(ref endpoint);
    Check(bytes.Length == 324 && BitConverter.ToInt32(bytes, 0) == 1, "active packet received");
    bool idle = false; var timeout = System.Diagnostics.Stopwatch.StartNew();
    while (timeout.Elapsed.TotalSeconds < 2 && !idle) idle = BitConverter.ToInt32(receiver.Receive(ref endpoint), 0) == 0;
    Check(idle, "stale source parks dashboard independently of Unity Update");
    output.Dispose(); output.Dispose(); Check(output.Stopped, "worker joined"); Check(output.RecordingDrops == 0, "no recording drops");
    var records = SessionReader.Read(path).ToArray(); Check(records.Last().Kind == SessionRecordKind.Footer, "validated footer");
    Check(records.Last().Footer.Completed, "normal completion");
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
Test("shared force shaping: symmetric sign, literal gain, cap, ramp and low-speed fade", () => {
    var left = new ForceSignal(); var right = new ForceSignal(); var inverted = new ForceSignal(); float final = 0;
    for (int i = 1; i <= 100; i++) {
        var a = left.Evaluate(Contact(i * .02), new()); var b = right.Evaluate(Contact(i * .02, -.3), new());
        var inv = inverted.Evaluate(Contact(i * .02), new(Invert: true));
        Check(a.Valid && Math.Abs(a.Preview) <= .10001, "default gain remains low"); Near(a.Preview, -b.Preview, "symmetric slip"); Near(a.Preview, -inv.Preview, "invert sign");
        if (i == 1) Check(Math.Abs(a.Preview) < .01, "starts near zero"); final = a.Preview;
    }
    Check(final < -.01, "preview builds when not output-armed");
    var cap = new ForceSignal(); float peak = 0;
    for (int i = 1; i <= 80; i++) peak = Math.Max(peak, Math.Abs(cap.Evaluate(Contact(i * .02, 100), new(Strength: 100, PeakPercent: 5)).Preview));
    Check(peak <= .05001 && peak > .01, "hard cap still permits signal");
    Near(new ForceSignal().Evaluate(Contact(1, speed: 0), new()).Preview, 0, "stationary has no force");
    Near(new ForceSignal().Evaluate(Contact(1), new(PeakPercent: 0)).Preview, 0, "zero peak disables force");
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
Console.WriteLine($"{passed} suites passed; {failed} failed; {checks} assertions.");
return failed == 0 ? 0 : 1;
