using WodenRallyEdge;
using WodenRallyEdge.Core;

// Grip v7 (2026-10-24): centering from the wheel position and damping that resists the wheel's motion. Synthetic rows
// with Woden's native flat front curve (2/1/2/1, stiffness 1); no game, device or owner recording.
internal static class CenteringChecks
{
    // art of rally, owner's Norway recording 2026-10-13 at Strength 50, 60+ km/h: signed restoring output by |steer| band.
    static readonly (double Steer, double Art)[] ArtNearCenter = { (.035, .071), (.075, .127) };

    static TelemetrySample Row(double time, double steer, double kmh, double slip = 0, double load = 5000)
    {
        var s = new TelemetrySample { SimulationSeconds = time, ElapsedSeconds = time, State = "driving", CarInstanceId = 1 };
        s.Channels["motion.speed"] = s.Channels["motion.velocity.local.z"] = kmh / 3.6;
        s.Channels["wheelInput.steer"] = steer;
        foreach (string corner in new[] { "fl", "fr" })
        {
            string p = "wheel." + corner + ".";
            s.Channels[p + "grounded"] = 1; s.Channels[p + "contactForce"] = load; s.Channels[p + "sidewaysSlip"] = slip;
            s.Channels[p + "sideFriction.extremumSlip"] = s.Channels[p + "sideFriction.asymptoteSlip"] = 2;
            s.Channels[p + "sideFriction.extremumValue"] = s.Channels[p + "sideFriction.asymptoteValue"] = 1;
            s.Channels[p + "sideFriction.stiffness"] = 1;
        }
        return s;
    }

    static FrontLoadReference Reference() { var r = new FrontLoadReference(); r.Restore(10000, FrontLoadReferenceKind.Driving); return r; }

    // Steady wheel past the 0.5 s ramp, so the damping is zero and the preview is tyre + centering.
    static float Held(double steer, double kmh, ForceOptions o, double slip = 0)
    {
        var g = new GripSignal(); var reference = Reference(); ForceResult r = null!;
        for (int i = 0; i <= 40; i++) r = g.Evaluate(Row(i * .02, steer, kmh, slip), o, reference);
        return r.Preview;
    }

    public static void Run(Action<bool, string> check)
    {
        var v7 = new ForceOptions(Model: GripSignal.ModelVersion, LoadRatio: 3, Damping: .02f);
        check(GripSignal.ModelVersion == 7 && ForceObservationSemantics.ModelId(7) == "woden-grip-signal@7" &&
            ForceObservationSemantics.ConfigVersion(7) == 5 && ForceObservationSemantics.ModelForConfigVersion(5) == 7 &&
            ForceObservationSemantics.ModelForConfigVersion(4) == GripSignal.EqualKnotsVersion, "v7 has its own model and config identity; v6 keeps config 4");

        // Near-center slope: a real restoring force from the first degrees, about 2.4 per unit steer at 80 km/h.
        double slope = (Held(.005, 80, v7) - Held(-.005, 80, v7)) / .01;
        check(slope > 2 && slope < 2.8, "near-center slope at Strength 50, 80 km/h: " + slope);

        // Builds up monotonically and symmetrically from center, with the tyre term's restoring sign (the steer's own sign).
        float last = 0;
        for (int i = 1; i <= 100; i++)
        {
            double steer = i / 100.0; float right = Held(steer, 80, v7), left = Held(-steer, 80, v7);
            // Strictly rising through the near-center range; at the tanh plateau float steps may be equal.
            check((steer > .2 ? right >= last : right > last) && right > 0 && Math.Abs(right + left) < 1e-6, "monotonic, restoring and symmetric at steer " + steer);
            last = right;
        }
        check(last < .13, "the centering alone stays below its 0.12 plateau times the load factor: " + last);

        // Parity with art of rally where the centering carries the force (Woden's front slip is near zero there).
        foreach (var (steer, art) in ArtNearCenter)
        {
            double ratio = Held(steer, 80, v7) / art;
            check(ratio > .8 && ratio < 1.2, $"Art parity at |steer| {steer}: Woden/Art {ratio:0.00}");
        }

        // Speed: no force at standstill or below the 3 km/h fade start, (speed / 60)^2 below 60 km/h, full above.
        foreach (double kmh in new[] { 0, 1, 2.9 })
            check(Held(.3, kmh, v7) == 0, "no force at " + kmh + " km/h");
        double quarter = Held(.1, 30, v7) / Held(.1, 80, v7);
        check(Math.Abs(quarter - .25) < .01, "30 km/h carries a quarter of the centering: " + quarter);
        check(Math.Abs(Held(.1, 120, v7) - Held(.1, 80, v7)) < 1e-6, "full from 60 km/h");

        // Strength scales the centering like the tyre force; Invert flips it; v6 has none.
        check(Math.Abs(Held(.05, 80, v7 with { Strength = 100 }) - 2 * Held(.05, 80, v7)) < 1e-5 && Held(.2, 80, v7 with { Strength = 0 }) == 0,
            "Steering strength scales the centering");
        check(Math.Abs(Held(.1, 80, v7 with { Invert = true }) + Held(.1, 80, v7)) < 1e-6, "Invert flips the centering");
        check(Held(.1, 80, v7 with { Model = GripSignal.EqualKnotsVersion }) == 0, "v6 has no centering, so its recordings replay exactly");

        // It adds to the tyre force with the same sign: front slip of the steer's restoring direction raises the force.
        check(Held(.1, 80, v7, slip: -.2) > Held(.1, 80, v7), "tyre force and centering add when the slip follows the steer");

        // Damping resists the wheel's motion in v7 (the rate's sign, as the restoring force of that side); v6 keeps its sign.
        foreach (int model in new[] { GripSignal.EqualKnotsVersion, GripSignal.ModelVersion })
        {
            var g = new GripSignal(); var reference = Reference(); var o = v7 with { Model = model, Damping = .05f };
            for (int i = 0; i <= 40; i++) g.Evaluate(Row(i * .02, i * .005, 80), o, reference);
            check(model == GripSignal.ModelVersion ? g.LastDamping > 0 : g.LastDamping < 0, "damping sign for a wheel moving toward +steer, model " + model);
        }
    }

    // The controller's off and mute gates are unchanged: no write while saved Off or diagnostic-muted, force when On.
    public static void Gates(Action<bool, string> check)
    {
        var (controller, device) = ForceControllerChecks.Create();
        Runtime.Settings.Options = new ForceOptions(Model: GripSignal.ModelVersion, LoadRatio: 3, Damping: .02f);
        double time = 1;
        TelemetrySample Drive() => Row(time += .02, .15, 80);
        for (int i = 0; i < 160; i++) { controller.Prepare(); controller.Tick(Drive()); }
        check(device.Writes.Count > 0 && device.Writes.Last() > .05, "On: the centering reaches the device: " + device.Writes.LastOrDefault());
        controller.SetEnabled(false); int writes = device.Writes.Count;
        for (int i = 0; i < 20; i++) { controller.Prepare(); var s = Drive(); controller.Tick(s); check(s.Get("ffb.deliveredOutput") == 0, "Off delivers zero"); }
        check(device.Writes.Count == writes, "saved Off writes nothing");
        (controller, device) = ForceControllerChecks.Create(); Runtime.DiagnosticNoForce = true;
        Runtime.Settings.Options = new ForceOptions(Model: GripSignal.ModelVersion, LoadRatio: 3, Damping: .02f);
        for (int i = 0; i < 40; i++) { controller.Prepare(); controller.Tick(Drive()); }
        check(device.Writes.Count == 0 && device.Opens == 0, "diagnostic mute writes nothing and opens nothing");
        Runtime.DiagnosticNoForce = false;
    }
}
