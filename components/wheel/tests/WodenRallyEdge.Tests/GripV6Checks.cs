using Dbce.Wheel.Recording;
using WodenRallyEdge.Core;

internal static class GripV6Checks
{
    private static TelemetrySample Sample(double time, double slip = 1)
    {
        var s = new TelemetrySample { SimulationSeconds = time, State = "driving" };
        s.Channels["motion.speed"] = s.Channels["motion.velocity.local.z"] = 25;
        s.Channels["wheelInput.steer"] = Math.Sin(time * 9) * .4;
        foreach (string corner in new[] { "fl", "fr" })
        {
            string p = "wheel." + corner + ".";
            s.Channels[p + "grounded"] = 1; s.Channels[p + "contactForce"] = 5000;
            s.Channels[p + "sidewaysSlip"] = slip;
            s.Channels[p + "sideFriction.extremumSlip"] = s.Channels[p + "sideFriction.asymptoteSlip"] = 2;
            s.Channels[p + "sideFriction.extremumValue"] = s.Channels[p + "sideFriction.asymptoteValue"] = 1;
            s.Channels[p + "sideFriction.stiffness"] = 1;
        }
        return s;
    }

    internal static void Curves(Action<bool, string> check)
    {
        check(ForceObservationSemantics.ModelId(6) == "woden-grip-signal@6" &&
            ForceObservationSemantics.ConfigVersion(6) == 4 && ForceObservationSemantics.ModelForConfigVersion(4) == 6,
            "model6/config4 are distinct from legacy model identities");
        var reference = new FrontLoadReference(); reference.Restore(10000, FrontLoadReferenceKind.Driving);
        var options = new ForceOptions(Model: 6, Damping: 0, GripSmoothing: 0);
        foreach (double slip in new[] { 0, 1, 1.999999, 2, 2.000001, 4, -1, -2, -4 })
        {
            var g = new GripSignal(); ForceResult r = g.Evaluate(Sample(0, slip), options, reference);
            for (int i = 1; i <= 30; i++) r = g.Evaluate(Sample(i * .02, slip), options, reference);
            check(r.Valid && float.IsFinite(r.Preview) && float.IsFinite(r.Alignment), "coincident native knots produce finite force at slip " + slip);
            check(slip == 0 ? r.Preview == 0 : Math.Sign(r.Preview) == -Math.Sign(slip), "flat-knot sign remains the existing direction");
            foreach (int old in new[] { 3, 4, 5 })
                check(!new GripSignal().Evaluate(Sample(0, slip), options with { Model = old }, reference).Valid,
                    "old/Classic-shadow curve admission stays strict for model " + old);
        }
        check(GripSignal.Curve(1, 2, 1, 2, 1) == .75 && GripSignal.Curve(2, 2, 1, 2, 1) == 1 &&
            GripSignal.Curve(4, 2, 1, 2, 1) == 1, "the unchanged equation rises to the coincident knot and stays flat");
        check(Math.Abs(GripSignal.Curve(2 - 1e-7, 2, 1, 2, 1) - GripSignal.Curve(2 + 1e-7, 2, 1, 2, 1)) < 1e-12,
            "the admitted curve is continuous across the knot");
        foreach (bool zeroStiffness in new[] { false, true })
        {
            var g = new GripSignal(); ForceResult? r = null;
            for (int i = 0; i <= 30; i++)
            {
                var s = Sample(i * .02);
                foreach (string corner in new[] { "fl", "fr" })
                {
                    string p = "wheel." + corner + ".sideFriction.";
                    if (zeroStiffness) s.Channels[p + "stiffness"] = 0;
                    else s.Channels[p + "extremumValue"] = s.Channels[p + "asymptoteValue"] = 0;
                }
                r = g.Evaluate(s, options, reference);
            }
            check(r!.Valid && r.Preview == 0, "genuine zero curve/stiffness stays valid");
        }
        foreach (var mutation in new (string Field, double? Value)[] {
            ("asymptoteValue", .9), ("asymptoteValue", Math.BitIncrement(1d)), ("asymptoteSlip", 1.9),
            ("extremumSlip", 0), ("extremumSlip", -1), ("asymptoteSlip", 0), ("asymptoteSlip", -1),
            ("extremumValue", -1), ("asymptoteValue", -1), ("stiffness", -1) })
        {
            var s = Sample(0); s.Channels["wheel.fl.sideFriction." + mutation.Field] = mutation.Value!.Value;
            var r = new GripSignal().Evaluate(s, options, reference);
            check(!r.Valid && r.Reason == "friction curve unavailable" && r.Preview == 0, "invalid flat curve refused: " + mutation);
        }
        foreach (string field in new[] { "extremumSlip", "extremumValue", "asymptoteSlip", "asymptoteValue", "stiffness" })
        foreach (double? value in new double?[] { null, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var s = Sample(0); string key = "wheel.fr.sideFriction." + field;
            if (value.HasValue) s.Channels[key] = value.Value; else s.Channels.Remove(key);
            var r = new GripSignal().Evaluate(s, options, reference);
            check(!r.Valid && r.Reason == "friction curve unavailable", "missing/nonfinite flat curve refused: " + field);
        }
        foreach (var pair in new (string Field, double Value)[] {
            ("Slip", 0), ("Slip", -1), ("Slip", double.NaN), ("Slip", double.PositiveInfinity), ("Slip", double.NegativeInfinity),
            ("Value", -1), ("Value", double.NaN), ("Value", double.PositiveInfinity), ("Value", double.NegativeInfinity) })
        {
            var s = Sample(0);
            s.Channels["wheel.fl.sideFriction.extremum" + pair.Field] = s.Channels["wheel.fl.sideFriction.asymptote" + pair.Field] = pair.Value;
            var r = new GripSignal().Evaluate(s, options, reference);
            check(!r.Valid && r.Reason == "friction curve unavailable", "coincident invalid pair remains refused despite equality: " + pair);
        }
        foreach (float strength in new[] { 0f, 49.583332f, 50f, 100f })
        foreach (bool invert in new[] { false, true })
        {
            var old = new GripSignal(); var current = new GripSignal();
            for (int i = 0; i < 160; i++)
            {
                var s = Sample(i * .02, Math.Sin(i * .03));
                foreach (string corner in new[] { "fl", "fr" })
                { s.Channels["wheel." + corner + ".sideFriction.asymptoteSlip"] = 5; s.Channels["wheel." + corner + ".sideFriction.asymptoteValue"] = .75; }
                if (i == 60) s.State = "paused";
                if (i == 100) s.Discontinuity = "recorded-discontinuity";
                var o = options with { Strength = strength, Invert = invert, Damping = .2f, GripSmoothing = .2f };
                check(old.Evaluate(s, o with { Model = 5 }, reference) == current.Evaluate(s, o, reference) &&
                    old.LastSteering == current.LastSteering && old.LastDamping == current.LastDamping && old.ResetCount == current.ResetCount,
                    "ordered curves preserve all v5 terms and resets exactly");
            }
        }
    }

    // Optional private input; no owner capture is committed. All generated replay artifacts go in a fresh temp copy.
    internal static void NativeCapture(Action<bool, string> check, string capture)
    {
        string[] files = { "source.jsonl", "force-config.json", "capture-profile.json" };
        var pins = files.ToDictionary(f => f, f => RecordingArtifacts.Sha256(Path.Combine(capture, f)));
        check(pins["source.jsonl"] == "932e1d4dbd9da5da043963e1c5c3c9668eba85d0e1da7faa6efbdb22ea43ce4e", "exact native trial02 source pin");
        string directory = Path.Combine(Path.GetTempPath(), "woden-native-grip6-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (string f in files) File.Copy(Path.Combine(capture, f), Path.Combine(directory, f));
            var options = StageCaptureReview.ReadOptions(directory);
            check(options.Model == 5 && options.Strength == 49.583332f && options.PeakPercent == 25 && options.LoadRatio == 2,
                "actual saved Grip5 tune is retained");
            var baseline = RecordedForceReplay.Reprocess(directory);
            check(baseline.SourceSamples == 1895 && baseline.ModelSamples == 1696, "every native legacy model row replays unchanged");
            var protectedPins = files.Concat(new[] { "case.json", "force-observation.jsonl" })
                .ToDictionary(f => f, f => RecordingArtifacts.Sha256(Path.Combine(directory, f)));
            string candidate = Path.Combine(directory, "candidate-grip6.json");
            RecordingArtifacts.WriteForceConfig(candidate, options with { Model = 6 });
            var trial = RecordedForceReplay.Trial(directory, candidate, Path.Combine(directory, "trial-grip6.jsonl"));
            check(trial.ModelSamples == 1696 && trial.Comparison.BaselineModel == "woden-grip-signal@5" &&
                trial.Comparison.CandidateModel == "woden-grip-signal@6", "actual immutable producer dispatches an explicit model6/config4 trial");
            var grip = new GripSignal(); var reference = new FrontLoadReference();
            int valid = 0, repaired = 0, reverse = 0, discontinuity = 0; long? previousReset = null;
            foreach (var record in SessionReader.Read(Path.Combine(directory, "source.jsonl")))
            {
                if (record.Kind != SessionRecordKind.Sample) continue;
                var row = record.Sample; var c = row.Channels;
                if (!c.ContainsKey("ffb.modelResetBefore")) continue;
                if (previousReset.HasValue && c["ffb.modelResetBefore"] != previousReset.Value) grip.Reset();
                var s = new TelemetrySample { Sequence = row.Sequence, ElapsedSeconds = row.ElapsedSeconds,
                    SimulationSeconds = c["sample.simulationSeconds"], State = c["sample.driving"] == 1 ? "driving" : "inactive",
                    Discontinuity = c["sample.discontinuity"] == 1 ? "recorded-discontinuity" : null };
                foreach (var p in c) s.Channels[p.Key] = p.Value;
                reference.Restore(c["ffb.grip.reference"], (FrontLoadReferenceKind)(int)c["ffb.grip.referenceKind"]);
                var r = grip.Evaluate(s, options with { Model = 6 }, reference);
                check(c["ffb.modelValid"] == 0 && c["ffb.gate"] == 1 && c["ffb.deliveryAttempts"] == 0, "original invalid/muted production evidence stays explicit");
                if (r.Valid) valid++;
                if (c["ffb.modelReason"] == 19)
                {
                    foreach (string corner in new[] { "fl", "fr" })
                    {
                        string p = "wheel." + corner + ".sideFriction.";
                        check(c[p + "extremumSlip"] == 2 && c[p + "asymptoteSlip"] == 2 && c[p + "extremumValue"] == 1 &&
                            c[p + "asymptoteValue"] == 1 && c[p + "stiffness"] == 1, "actual five-field native front curve, no defaults");
                    }
                    check(r.Valid, "only curve admission repairs the captured friction rejection"); repaired++;
                }
                else
                {
                    int recordedReason = s.Discontinuity != null ? 11 : ForceObservationSemantics.ModelReason(r.Reason);
                    check(!r.Valid && recordedReason == c["ffb.modelReason"], $"other native refusals remain refused at row {row.Sequence}: {recordedReason} vs {c["ffb.modelReason"]}");
                    if (c["ffb.modelReason"] == 13) reverse++;
                    if (c["ffb.modelReason"] == 11) discontinuity++;
                }
                // Preserve diagnostic lifecycle: no ramp recovery is inferred from the validity repair.
                check(r.Preview == 0, "no-force reset ordering keeps the actual muted preview zero");
                if (c["ffb.gate"] != 0) grip.Reset(); previousReset = (long)c["ffb.modelResetAfter"];
            }
            check(valid == 1151 && repaired == 1151 && reverse == 230 && discontinuity == 315,
                $"native counterfactual validity: valid={valid}, repaired={repaired}, reverse={reverse}, discontinuity={discontinuity}");
            foreach (var pin in protectedPins) check(RecordingArtifacts.Sha256(Path.Combine(directory, pin.Key)) == pin.Value, "trial preserves " + pin.Key);
            Console.WriteLine("Native trial02: Grip5 exact replay 1696; Grip6 counterfactual valid 1151; reverse 230; discontinuity 315; muted preview remains zero.");
        }
        finally
        {
            foreach (var pin in pins) check(RecordingArtifacts.Sha256(Path.Combine(capture, pin.Key)) == pin.Value, "private original unchanged: " + pin.Key);
            Directory.Delete(directory, true);
        }
    }
}
