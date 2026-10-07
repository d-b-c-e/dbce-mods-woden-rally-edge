using System.Text.Json;
using Dbce.Wheel.Playback;
using Dbce.Wheel.Recording;
using WodenRallyEdge.Core;

// Device-free validation of original signals. Kinematic playback cannot generate
// a replacement tyre-force baseline; re-evaluate the actual model on this stream.
internal static class StageCaptureReview
{
    internal static ForceOptions ReadOptions(string directory)
    {
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "force-config.json")));
        var c = config.RootElement;
        int version = c.GetProperty("version").GetInt32();
        var keys = new HashSet<string>(new[] { "schema", "version", "model", "strengthPercent", "peakPercent", "loadReference", "slipScale", "smoothingMs", "damping", "invert" });
        if (version == 2) { keys.Add("loadRatio"); keys.Add("gripSmoothing"); }
        foreach (var property in c.EnumerateObject())
            if (!keys.Remove(property.Name)) throw new IOException("Unknown/duplicate force config key: " + property.Name);
        if (keys.Count != 0) throw new IOException("Missing force config keys");
        if (c.GetProperty("schema").GetString() != "woden.force-config" || version is not (1 or 2) ||
            c.GetProperty("model").GetString() != (version == 2 ? ForceObservationSemantics.GripModel : ForceObservationSemantics.Model))
            throw new IOException("Unsupported force model");
        var options = new ForceOptions(c.GetProperty("strengthPercent").GetSingle(), c.GetProperty("peakPercent").GetSingle(),
            c.GetProperty("loadReference").GetSingle(), c.GetProperty("slipScale").GetSingle(),
            c.GetProperty("smoothingMs").GetSingle(), c.GetProperty("damping").GetSingle(), c.GetProperty("invert").GetBoolean());
        if (version == 2) options = options with { Model = GripSignal.ModelVersion,
            LoadRatio = c.GetProperty("loadRatio").GetSingle(), GripSmoothing = c.GetProperty("gripSmoothing").GetSingle() };
        if (new[] { options.Strength, options.PeakPercent, options.LoadReference, options.SlipScale, options.SmoothingMs, options.Damping,
            options.LoadRatio, options.GripSmoothing }.Any(x => !float.IsFinite(x)) || options.LoadReference <= 0 || options.SlipScale <= 0 ||
            options.Grip && (options.LoadRatio <= 0 || options.GripSmoothing is < 0 or > .95f))
            throw new IOException("Invalid force model tuning");
        return options;
    }

    // This state may predate the start of recording. Never relearn it from the shortened tape,
    // or round a fractional enum into a different qualification state.
    internal static FrontLoadReference ReadReference(IReadOnlyDictionary<string, double> channels)
    {
        if (!channels.TryGetValue("ffb.grip.reference", out double load) ||
            !channels.TryGetValue("ffb.grip.referenceKind", out double kind) || kind is not (0 or 1 or 2))
            throw new IOException("Missing/invalid recorded grip reference");
        var reference = new FrontLoadReference(); reference.Restore(load, (FrontLoadReferenceKind)(int)kind); return reference;
    }

    internal static object Run(string directory)
    {
        var tape = TrajectoryTape.Read(directory); // verifies every sealed artifact
        ArtifactSeal.Verify(directory, "source.jsonl", "force-config.json", "stage-context.json", "channels.json");
        if (tape.Metadata["physicalOutput"] != "false") throw new IOException("Capture was not physically muted");
        var options = ReadOptions(directory);
        var signal = new ForceSignal(); signal.Reset();
        var grip = new GripSignal(); grip.NewCar();
        int count = 0, valid = 0, nonzero = 0; double? origin = null;
        double? carEpoch = null;
        int qualified = 0;
        double maxError = 0, minForce = 0, maxForce = 0, maxSpeed = 0;
        SessionFooter? footer = null;
        foreach (var record in SessionReader.Read(Path.Combine(directory, "source.jsonl")))
        {
            if (record.Kind == SessionRecordKind.Metadata)
            {
                var properties = record.Metadata.Properties;
                if (!properties.TryGetValue("forceConfigSha256", out var hash) || hash != ArtifactSeal.Hash(Path.Combine(directory, "force-config.json")) ||
                    !properties.TryGetValue("captureSource", out var source) || source != tape.Metadata["captureSource"] ||
                    !properties.TryGetValue("physicalOutput", out var output) || output != "false")
                    throw new IOException("Original signal metadata differs from the sealed capture");
            }
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
            if (options.Grip) capturedOptions = capturedOptions with { Model = GripSignal.ModelVersion,
                LoadRatio = (float)Get("ffb.tuning.loadRatio"), GripSmoothing = (float)Get("ffb.tuning.gripSmoothing") };
            if (capturedOptions != options || Get("ffb.tuning.modelVersion") != options.Model || Get("ffb.tuning.invert") is not (0 or 1))
                throw new IOException("Capture tune changed; split into stable configuration cases");
            // Historical Classic captures predate this additive channel.
            if ((options.Grip || channels.ContainsKey("analysis.force.modelVersion")) && Get("analysis.force.modelVersion") != options.Model)
                throw new IOException("Analysis force model differs from the captured configuration");
            FrontLoadReference? reference = null;
            if (options.Grip)
            {
                reference = ReadReference(channels);
                if (Get("ffb.grip.curveEstimateVersion") != 1) throw new IOException("Unsupported grip curve estimate");
                double epoch = Get("ffb.grip.carEpoch"); carEpoch ??= epoch;
                if (epoch < 0 || epoch != Math.Truncate(epoch) || epoch != carEpoch.Value)
                    throw new IOException("Capture car changed; split into single-car cases");
            }
            var sample = new TelemetrySample { Sequence = row.Sequence, ElapsedSeconds = row.ElapsedSeconds,
                SimulationSeconds = time, State = "driving", Discontinuity = Get("sample.discontinuity") == 1 ? "recorded" : null };
            foreach (var pair in channels) sample.Channels[pair.Key] = pair.Value;
            var result = options.Grip ? grip.Evaluate(sample, options, reference) : signal.Evaluate(sample, options);
            if ((result.Valid ? 1 : 0) != Get("analysis.force.valid") || (options.Grip ? grip.ResetCount : signal.ResetCount) != Get("analysis.force.reset"))
                throw new IOException($"Model validity/reset differs at sample {count}");
            var error = Math.Abs(result.Preview - Get("analysis.force.preview"));
            maxError = Math.Max(maxError, error);
            if (error > .000002) throw new IOException($"Model preview differs at sample {count}: {error:R}");
            if (result.Valid) valid++;
            if (result.Valid && (!options.Grip || reference!.Kind == FrontLoadReferenceKind.Driving)) qualified++;
            if (result.Preview != 0) nonzero++;
            minForce = Math.Min(minForce, result.Preview); maxForce = Math.Max(maxForce, result.Preview);
            maxSpeed = Math.Max(maxSpeed, Get("motion.speed")); count++;
        }
        if (count != tape.Frames.Length || footer == null || !footer.Completed || footer.Counts.DroppedSamples != 0 ||
            footer.Counts.DroppedMarkers != 0 || footer.Counts.ErrorCount != 0 || footer.Counts.LimitCount != 0 ||
            footer.Counts.InvalidCount != 0 || footer.Counts.ContentionCount != 0 || footer.Counts.QueueFullCount != 0 ||
            footer.Counts.RejectedAfterStop != 0 || footer.Counts.WrittenSamples != count)
            throw new IOException("Original source is incomplete or dropped data");
        if (valid < 50 || nonzero < 1 || maxSpeed < 1) throw new IOException("Insufficient driving/force coverage");
        ArtifactSeal.Verify(directory, "source.jsonl", "force-config.json", "stage-context.json", "channels.json");
        return new { schema = "woden.stage-review@1", passed = true, scenario = tape.Metadata["scenario"],
            samples = count, seconds = tape.Frames[^1].Time - tape.Frames[0].Time, validForceSamples = valid,
            nonzeroForceSamples = nonzero, minimumPreview = minForce, maximumPreview = maxForce,
            maximumForceReprocessError = maxError, maximumPhysicsSpeedMps = maxSpeed,
            model = options.Grip ? ForceObservationSemantics.GripModel : ForceObservationSemantics.Model,
            qualifiedForceSamples = qualified, forceScope = "steering analysis only; crash cues and physical delivery are separate",
            physicalDeliveryAttempts = 0, source = tape.Metadata["captureSource"], normalization = "software signal only; no measured wheel torque" };
    }
}
