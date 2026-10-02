using System.Security.Cryptography;
using System.Text.Json;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Observe production ForceController -> ForceSignal -> pinned ForceShaper.
// This contains input generation/measurement only, never a replacement force model.
internal static class FfbRegressionChecks
{
    const int Frames = 300;
    const double Dt = .02;
    static readonly string[] Shapes = { "steady-load", "sine-1hz", "sine-4hz", "sign-step", "lifecycle-gates" };
    static readonly float[] Strengths = { 0, 25, 50, 100 };
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    internal sealed record Row(string Input, float StrengthPercent, float PeakPercent, int Frames, double DurationSeconds,
        double Minimum, double Maximum, double Peak, double Rms, double AbsoluteImpulse,
        int AtOutputCapFrames, double AtOutputCapSeconds, int UnconditionedDemandAboveCapFrames,
        int NonzeroCommandFrames, double NonzeroCommandDuty, int AcceptedFakeWrites, double RequestWritesPerSecond,
        int SignChanges, double SignChangesPerSecond, double MaximumObservedSlew,
        int ZeroStopCallsBeforeShutdown, int ShutdownZeroCalls, bool ShutdownZeroBeforeClose,
        bool TerminalProducersRejected, string InputSha256, string CommandsSha256);
    internal sealed record StrengthComparison(string Input, float FromStrengthPercent, float ToStrengthPercent,
        double PeakDelta, double RmsDelta, double DutyDelta, double AtCapSecondsDelta);
    internal sealed record Summary(string Schema, string Kind, int ModelVersion, double SampleSeconds, ForceOptions FixedOptionsExceptStrength,
        string Units, string Collision, string NativePeriodicEffectCadence, string RigAssumptions,
        bool HardwareOpened, bool PhysicalTorqueMeasured, bool CrossGameFeelEquivalence,
        string SharedFfbSha256, Row[] Rows, StrengthComparison[] StrengthComparisons);
    sealed class Device : IForceDevice
    {
        internal readonly List<string> Calls = new();
        internal int Writes, Zeros;
        public string? Error => null;
        public bool CanOpen => true;
        public bool Open(Guid guid) { Calls.Add("open"); return true; }
        public bool Write(float force) { Calls.Add("write"); Writes++; return true; }
        public void ZeroAndStop() { Calls.Add("zero-stop"); Zeros++; }
        public void Panic() => Calls.Add("panic");
        public void Close() => Calls.Add("close");
    }
    internal static Summary Generate(Action<bool, string> check)
    {
        var rows = new List<Row>();
        foreach (string shape in Shapes)
        foreach (float strength in Strengths)
        {
            var a = Observe(shape, strength, 1, check);
            var repeated = Observe(shape, strength, 1, check);
            var mirror = Observe(shape, strength, -1, check);
            check(JsonSerializer.Serialize(a) == JsonSerializer.Serialize(repeated), "repeat summary differs: " + shape);
            check(Math.Abs(a.Peak - mirror.Peak) < .000002 && Math.Abs(a.Rms - mirror.Rms) < .000002,
                "mirrored envelope differs: " + shape);
            check(a.Minimum == -mirror.Maximum && a.Maximum == -mirror.Minimum, "mirrored extrema differ");
            check(strength != 0 || a.Peak == 0, "zero strength produced force");
            rows.Add(a);
        }
        var comparisons = new List<StrengthComparison>();
        foreach (string shape in Shapes)
        {
            var group = rows.Where(r => r.Input == shape).ToArray();
            for (int i = 1; i < group.Length; i++) {
                var from = group[i - 1]; var to = group[i];
                comparisons.Add(new(shape, from.StrengthPercent, to.StrengthPercent, to.Peak - from.Peak,
                    to.Rms - from.Rms, to.NonzeroCommandDuty - from.NonzeroCommandDuty, to.AtOutputCapSeconds - from.AtOutputCapSeconds));
                // Do not assume monotonic RMS across dynamically slew-limited waveforms.
                check(double.IsFinite(to.Rms - from.Rms), "nonfinite strength comparison");
            }
        }
        var dll = typeof(Dbce.Wheel.Ffb.ForceShaper).Assembly.Location;
        return new("dbce.woden.ffb-regression-summary@1", "synthetic-production-controller-requests", 3, Dt, new(),
            "normalized signed command [-1,1]; no Nm or delivered torque",
            "unavailable: no verified collision producer; no collision waveform synthesized",
            "unavailable: constant-force request path only; request timing below is an injected sample schedule, not native/driver effect timing",
            "MOZA R12 is historical rig identity only; driver gain, calibration and physical torque unknown; synthetic inputs are not measured rig data",
            false, false, false, Hash(File.ReadAllBytes(dll)), rows.ToArray(), comparisons.ToArray());
    }
    static Row Observe(string shape, float strength, int sign, Action<bool, string> check)
    {
        ForceControllerChecks.Create(); // Reset only fake context, preserving production source.
        Runtime.Settings.FfbGuid = "00000000-0000-0000-0000-000000000001";
        Runtime.Settings.Options = new(Strength: strength); // Test-only options; fixed production defaults otherwise.
        var device = new Device(); var controller = new ForceController(device);
        controller.Prepare();
        var inputs = new List<object>(); var commands = new List<float>();
        int capFrames = 0, demandAbove = 0, nonzero = 0, changes = 0, lastSign = 0;
        double square = 0, impulse = 0, slew = 0; float previous = 0;
        for (int i = 0; i < Frames; i++)
        {
            double t = (i + 1) * Dt;
            double slip = sign * (shape switch {
                "sine-1hz" => .6 * Math.Sin(2 * Math.PI * t),
                "sine-4hz" => .6 * Math.Sin(8 * Math.PI * t),
                "sign-step" => t < 3 ? 1 : -1,
                _ => 1 });
            var sample = new TelemetrySample { SessionId = "ffb-reference-v1", Sequence = i,
                SimulationSeconds = t, ElapsedSeconds = t, State = "driving", CarInstanceId = 1 };
            sample.Add("motion.speed", 20); sample.Add("motion.velocity.local.z", 20);
            sample.Add("wheelInput.steer", 0);
            foreach (string corner in new[] { "fl", "fr" }) {
                sample.Add("wheel." + corner + ".grounded", 1);
                sample.Add("wheel." + corner + ".contactForce", 3000);
                sample.Add("wheel." + corner + ".sidewaysSlip", slip);
            }
            Runtime.Focused = true; MountedCamera.PlayerOwned = true;
            if (shape == "lifecycle-gates") {
                if (i is >= 50 and < 60) sample.State = "paused";
                if (i is >= 70 and < 80) Runtime.Focused = false;
                if (i is >= 90 and < 100) MountedCamera.PlayerOwned = false;
                if (i == 110) sample.Discontinuity = "respawn";
                if (i == 130) sample.Discontinuity = "stale-source";
            }
            inputs.Add(new { tick = i, seconds = t, channels = new Dictionary<string, double>(sample.Channels), sample.State, focused = Runtime.Focused,
                cameraOwned = MountedCamera.PlayerOwned, sample.Discontinuity });
            controller.Tick(sample);
            float value = controller.Sent;
            check(float.IsFinite(value) && Math.Abs(value) <= .250001f, "command outside unchanged 25% cap");
            if (sample.ForceGate != "active") check(value == 0, "gated controller retained output");
            check(sample.Get("ffb.sent") == value && sample.Get("ffb.tuning.strengthPercent") == strength,
                "recording channels differ from observed production output/options");
            if (Math.Abs(value) >= .25 - .000001) capFrames++;
            // This is demand exceedance, NOT proof of internal hard clipping: shaping is nonlinear.
            if (Math.Abs((controller.Last.Alignment + controller.Last.Damping) * strength / 100) > .25) demandAbove++;
            if (Math.Abs(value) > .0015) { nonzero++; int nextSign = Math.Sign(value); if (lastSign != 0 && lastSign != nextSign) changes++; lastSign = nextSign; }
            square += value * (double)value; impulse += Math.Abs(value) * Dt;
            slew = Math.Max(slew, Math.Abs(value - previous) / Dt); previous = value; commands.Add(value);
        }
        int writes = device.Writes, zerosBefore = device.Zeros, calls = device.Calls.Count;
        controller.Shutdown();
        bool order = device.Calls.Skip(calls).SequenceEqual(new[] { "zero-stop", "close" });
        check(order && controller.Sent == 0 && !controller.Connected, "shutdown did not zero before close");
        calls = device.Calls.Count;
        controller.Prepare(); controller.SetEnabled(true); controller.Tick(new TelemetrySample()); controller.Shutdown();
        bool terminal = device.Calls.Count == calls;
        check(terminal && controller.Sent == 0, "late producer reopened stopped output");
        check(Runtime.Settings.Saves == 0, "offline fixture changed saved preferences");
        return new(shape, strength, 25, Frames, Frames * Dt, commands.Min(), commands.Max(), commands.Max(v => Math.Abs(v)),
            Math.Sqrt(square / Frames), impulse, capFrames, capFrames * Dt, demandAbove, nonzero, nonzero / (double)Frames,
            writes, writes / (Frames * Dt), changes, changes / (Frames * Dt), slew,
            zerosBefore, device.Zeros - zerosBefore, order, terminal,
            Hash(JsonSerializer.SerializeToUtf8Bytes(inputs)), Hash(JsonSerializer.SerializeToUtf8Bytes(commands)));
    }
    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    static void Compare(Summary actual, string baseline, Action<bool, string> check)
    {
        var expected = JsonSerializer.Deserialize<Summary>(File.ReadAllText(baseline)) ?? throw new Exception("missing summary");
        check(expected.Schema == actual.Schema && expected.ModelVersion == actual.ModelVersion &&
            expected.SampleSeconds == actual.SampleSeconds && expected.SharedFfbSha256 == actual.SharedFfbSha256,
            "reference identity/model/dependency differs");
        check(JsonSerializer.Serialize(expected.Rows) == JsonSerializer.Serialize(actual.Rows), "reference metrics or command/input hashes changed");
        check(JsonSerializer.Serialize(expected.StrengthComparisons) == JsonSerializer.Serialize(actual.StrengthComparisons), "strength comparisons changed");
        check(JsonSerializer.Serialize(expected) == JsonSerializer.Serialize(actual), "summary options, units or evidence boundary changed");
    }
    internal static void Run(Action<bool, string> check)
        => Compare(Generate(check), Path.Combine(AppContext.BaseDirectory, "Fixtures/ffb-reference-v1.json"), check);
    internal static int Write(string path, string? baseline)
    {
        if (File.Exists(path)) throw new IOException("Preserve existing regression artifact; choose a new path");
        int checks = 0; void Check(bool b, string why) { checks++; if (!b) throw new Exception(why); }
        var result = Generate(Check); if (baseline != null) Compare(result, baseline, Check);
        File.WriteAllText(path, JsonSerializer.Serialize(result, Json) + "\n");
        Console.WriteLine($"PASS FFB reference: {checks} checks, {result.Rows.Length} summaries; production code, fake device only.");
        return 0;
    }
}
