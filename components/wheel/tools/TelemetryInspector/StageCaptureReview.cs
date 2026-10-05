using System.Text.Json;
using Dbce.Wheel.Playback;
using Dbce.Wheel.Recording;
using WodenRallyEdge.Core;

// Device-free validation of original signals. Kinematic playback cannot generate
// a replacement tyre-force baseline; re-evaluate the actual model on this stream.
internal static class StageCaptureReview
{
    internal static object Run(string directory)
    {
        var tape = TrajectoryTape.Read(directory); // verifies every sealed artifact
        ArtifactSeal.Verify(directory, "source.jsonl", "force-config.json", "stage-context.json", "channels.json");
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "force-config.json")));
        var c = config.RootElement;
        if (c.GetProperty("schema").GetString() != "woden.force-config" || c.GetProperty("version").GetInt32() != 1 ||
            c.GetProperty("model").GetString() != "woden-force-signal@3") throw new IOException("Unsupported force model");
        var options = new ForceOptions(c.GetProperty("strengthPercent").GetSingle(), c.GetProperty("peakPercent").GetSingle(),
            c.GetProperty("loadReference").GetSingle(), c.GetProperty("slipScale").GetSingle(),
            c.GetProperty("smoothingMs").GetSingle(), c.GetProperty("damping").GetSingle(), c.GetProperty("invert").GetBoolean());
        var signal = new ForceSignal(); signal.Reset();
        int count = 0, valid = 0, nonzero = 0; double? origin = null;
        double maxError = 0, minForce = 0, maxForce = 0, maxSpeed = 0;
        SessionFooter? footer = null;
        foreach (var record in SessionReader.Read(Path.Combine(directory, "source.jsonl")))
        {
            if (record.Kind == SessionRecordKind.Footer) footer = record.Footer;
            if (record.Kind != SessionRecordKind.Sample) continue;
            var row = record.Sample; var channels = row.Channels;
            double Get(string key) => channels.TryGetValue(key, out double value) && double.IsFinite(value) ? value :
                throw new IOException($"Missing/nonfinite {key} at sample {count}");
            if (count >= tape.Frames.Length || row.Sequence != count || Get("capture.trajectoryIndex") != count)
                throw new IOException("Original signal/trajectory row alignment differs");
            var time = Get("sample.simulationSeconds"); origin ??= time;
            if (Math.Abs(time - origin.Value - tape.Frames[count].Time) > 1e-6)
                throw new IOException("Original signal/trajectory physics clocks differ");
            if (Get("sample.driving") != 1 || Get("sample.discontinuity") is not (0 or 1))
                throw new IOException("Capture is not a driving trajectory");
            foreach (string key in new[] { "ffb.sent", "ffb.connectionAttempts", "ffb.deliveryAttempts" })
                if (Get(key) != 0) throw new IOException("Capture was not physically muted: " + key);
            var capturedOptions = new ForceOptions((float)Get("ffb.tuning.strengthPercent"), (float)Get("ffb.tuning.peakPercent"),
                (float)Get("ffb.tuning.loadReference"), (float)Get("ffb.tuning.slipScale"), (float)Get("ffb.tuning.smoothingMs"),
                (float)Get("ffb.tuning.damping"), Get("ffb.tuning.invert") == 1);
            if (capturedOptions != options || Get("ffb.tuning.modelVersion") != 3)
                throw new IOException("Capture tune changed; split into stable configuration cases");
            var sample = new TelemetrySample { Sequence = row.Sequence, ElapsedSeconds = row.ElapsedSeconds,
                SimulationSeconds = time, State = "driving", Discontinuity = Get("sample.discontinuity") == 1 ? "recorded" : null };
            foreach (var pair in channels) sample.Channels[pair.Key] = pair.Value;
            var result = signal.Evaluate(sample, options);
            if ((result.Valid ? 1 : 0) != Get("analysis.force.valid") || signal.ResetCount != Get("analysis.force.reset"))
                throw new IOException($"Model validity/reset differs at sample {count}");
            var error = Math.Abs(result.Preview - Get("analysis.force.preview"));
            maxError = Math.Max(maxError, error);
            if (error > .000002) throw new IOException($"Model preview differs at sample {count}: {error:R}");
            if (result.Valid) valid++;
            if (result.Preview != 0) nonzero++;
            minForce = Math.Min(minForce, result.Preview); maxForce = Math.Max(maxForce, result.Preview);
            maxSpeed = Math.Max(maxSpeed, Get("motion.speed")); count++;
        }
        if (count != tape.Frames.Length || footer == null || !footer.Completed || footer.Counts.DroppedSamples != 0 ||
            footer.Counts.DroppedMarkers != 0 || footer.Counts.ErrorCount != 0 || footer.Counts.LimitCount != 0 ||
            footer.Counts.InvalidCount != 0 || footer.Counts.ContentionCount != 0 || footer.Counts.WrittenSamples != count)
            throw new IOException("Original source is incomplete or dropped data");
        if (valid < 50 || nonzero < 1 || maxSpeed < 1) throw new IOException("Insufficient real driving/force coverage");
        ArtifactSeal.Verify(directory, "source.jsonl", "force-config.json", "stage-context.json", "channels.json");
        return new { schema = "woden.stage-review@1", passed = true, scenario = tape.Metadata["scenario"],
            samples = count, seconds = tape.Frames[^1].Time - tape.Frames[0].Time, validForceSamples = valid,
            nonzeroForceSamples = nonzero, minimumPreview = minForce, maximumPreview = maxForce,
            maximumForceReprocessError = maxError, maximumPhysicsSpeedMps = maxSpeed,
            physicalDeliveryAttempts = 0, source = "original live physics", normalization = "software signal only; no measured wheel torque" };
    }
}
