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

    internal static void ReferenceDirection(Action<bool, string> check)
    {
        var options = new ForceOptions(Model: GripSignal.ModelVersion);
        var boundary = new GripSignal();
        var edge = Sample(0);
        edge.Channels["motion.velocity.local.z"] = -.5;
        check(boundary.Evaluate(edge, options).Valid, "signed reverse boundary is still admitted by the model");
        edge = Sample(.02);
        edge.Channels["motion.velocity.local.z"] = -.500001;
        check(!boundary.Evaluate(edge, options).Valid && boundary.Reference.Kind == FrontLoadReferenceKind.None,
            "a signed row just beyond the reverse boundary is refused before reference acquisition");
        var grip = new GripSignal();
        for (int i = 0; i < 120; i++)
        {
            var reverse = Sample(i * .02);
            reverse.Channels["motion.speed"] = 25; // unsigned magnitude remains high in reverse
            reverse.Channels["motion.velocity.local.z"] = -25;
            var result = grip.Evaluate(reverse, options);
            check(!result.Valid && result.Reason == "reverse: signal not validated", "model refuses signed reverse row");
        }
        check(grip.Reference.Kind == FrontLoadReferenceKind.None && grip.Reference.Load == 0,
            "two seconds of fast reverse travel cannot qualify a live front-load reference");
        for (int i = 120; i < 240; i++)
        {
            var forward = Sample(i * .02);
            var result = grip.Evaluate(forward, options);
            check(result.Valid, "forward row remains valid after reverse travel");
        }
        check(grip.Reference.Kind == FrontLoadReferenceKind.Driving && Math.Abs(grip.Reference.Load - 10000) < 1e-6,
            "forward-only rows qualify the unchanged reference load");

        var historical = new FrontLoadReference();
        historical.Restore(12345, FrontLoadReferenceKind.Driving);
        var replay = new GripSignal();
        var recordedReverse = Sample(0);
        recordedReverse.Channels["motion.velocity.local.z"] = -25;
        replay.Evaluate(recordedReverse, options, historical);
        check(historical.Kind == FrontLoadReferenceKind.Driving && historical.Load == 12345 &&
            replay.Reference.Kind == FrontLoadReferenceKind.None,
            "shared recorded reference remains exact and is never relearned during replay");
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
}
