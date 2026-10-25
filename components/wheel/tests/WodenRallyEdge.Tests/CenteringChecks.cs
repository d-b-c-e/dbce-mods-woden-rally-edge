using WodenRallyEdge;
using WodenRallyEdge.Core;

// Grip v7 centering from the wheel position, damping that resists the wheel's motion, and v8's owner-accepted base gain
// (2026-10-25: the owner's v7 Strength 74.745544 / GripLoadRatio 1.5029 / Damping 0.05 is v8 at Strength 50). Synthetic
// rows with Woden's native flat front curve (2/1/2/1, stiffness 1); no game, device or owner recording.
internal static class CenteringChecks
{
    const float OwnerStrength = 74.745544f, OwnerLoadRatio = 1.5029262f;
    static readonly ForceOptions Defaults = new(Model: GripSignal.ModelVersion, LoadRatio: 1.5f, Damping: .05f);
    static readonly ForceOptions OwnerV7 = new(Strength: OwnerStrength, Model: GripSignal.CenteringVersion, LoadRatio: OwnerLoadRatio, Damping: .05f);

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

    // A drive-like sequence: steering sweeps, slip that follows and lags it, speed from 0 to 130 km/h, a pause and a
    // discontinuity. Returns every preview and pre-clamp term.
    static List<(float Preview, float Steering, float Damping, float Centering)> Drive(ForceOptions o)
    {
        var g = new GripSignal(); var reference = Reference(); var rows = new List<(float, float, float, float)>();
        for (int i = 0; i < 900; i++)
        {
            double t = i / 60.0, steer = .6 * Math.Sin(t * 1.3) * Math.Sin(t * .37), kmh = Math.Min(130, i * .25);
            var s = Row(t, steer, kmh, -1.2 * .6 * Math.Sin((t - .08) * 1.3) * Math.Sin((t - .08) * .37) + .03 * Math.Sin(t * 17), 5000 + 900 * Math.Sin(t * 3));
            if (i == 400) s.State = "paused";
            if (i == 600) s.Discontinuity = "recorded-discontinuity";
            var r = g.Evaluate(s, o, reference);
            rows.Add((r.Preview, g.LastSteering, g.LastDamping, g.LastCentering));
        }
        return rows;
    }

    public static void Run(Action<bool, string> check)
    {
        check(GripSignal.ModelVersion == 8 && GripSignal.CenteringVersion == 7 && GripSignal.BaseGain == OwnerStrength / 50f &&
            ForceObservationSemantics.ModelId(8) == "woden-grip-signal@8" && ForceObservationSemantics.ModelId(7) == "woden-grip-signal@7" &&
            ForceObservationSemantics.ConfigVersion(8) == 6 && ForceObservationSemantics.ModelForConfigVersion(6) == 8 &&
            ForceObservationSemantics.ConfigVersion(7) == 5 && ForceObservationSemantics.ModelForConfigVersion(5) == 7,
            "v8 has its own model and config identity; v7 keeps config 5");

        // Owner-accepted v7 at 50%: v8 at Strength 50 is the owner's v7 tune (74.745544 / 1.5029 / 0.05), bit for bit, on
        // every row of a drive, including each pre-clamp term.
        var accepted = Drive(OwnerV7);
        var v8 = Drive(Defaults with { LoadRatio = OwnerLoadRatio });
        check(accepted.SequenceEqual(v8) && accepted.Any(x => Math.Abs(x.Centering) > .05) && accepted.Any(x => Math.Abs(x.Steering) > .05) &&
            accepted.Any(x => Math.Abs(x.Damping) > .005), "owner-accepted v7 at 50%: v8 defaults reproduce the owner's v7 tune exactly");
        foreach (double steer in new[] { .035, .075, .15 })
            check(Held(steer, 80, Defaults) == Held(steer, 80, OwnerV7 with { LoadRatio = 1.5f }),
                "owner-accepted v7 at 50% near center at |steer| " + steer);

        // Near-center slope: restoring from the first degrees, BaseGain x 2.4 per unit steer at 80 km/h.
        double slope = (Held(.005, 80, Defaults) - Held(-.005, 80, Defaults)) / .01;
        check(slope > 3.2 && slope < 4, "near-center slope at Strength 50, 80 km/h: " + slope);

        // Builds up monotonically and symmetrically from center, with the tyre term's restoring sign (the steer's own sign).
        float last = 0;
        for (int i = 1; i <= 100; i++)
        {
            double steer = i / 100.0; float right = Held(steer, 80, Defaults), left = Held(-steer, 80, Defaults);
            // Strictly rising through the near-center range; at the tanh plateau float steps may be equal.
            check((steer > .2 ? right >= last : right > last) && right > 0 && Math.Abs(right + left) < 1e-6, "monotonic, restoring and symmetric at steer " + steer);
            last = right;
        }
        check(last < .12f * GripSignal.BaseGain + 1e-4, "the centering alone stays at its plateau: " + last);

        // Speed: no force at standstill or below the 3 km/h fade start, (speed / 60)^2 below 60 km/h, full above.
        foreach (double kmh in new[] { 0, 1, 2.9 })
            check(Held(.3, kmh, Defaults) == 0, "no force at " + kmh + " km/h");
        double quarter = Held(.1, 30, Defaults) / Held(.1, 80, Defaults);
        check(Math.Abs(quarter - .25) < .01, "30 km/h carries a quarter of the centering: " + quarter);
        check(Math.Abs(Held(.1, 120, Defaults) - Held(.1, 80, Defaults)) < 1e-6, "full from 60 km/h");

        // Strength scales the centering like the tyre force; Invert flips it; v6 has none; v7 has no base gain.
        check(Math.Abs(Held(.03, 80, Defaults with { Strength = 100 }) - 2 * Held(.03, 80, Defaults)) < 1e-5 && Held(.2, 80, Defaults with { Strength = 0 }) == 0,
            "Steering strength scales the centering");
        check(Math.Abs(Held(.1, 80, Defaults with { Invert = true }) + Held(.1, 80, Defaults)) < 1e-6, "Invert flips the centering");
        check(Held(.1, 80, Defaults with { Model = GripSignal.EqualKnotsVersion }) == 0, "v6 has no centering, so its recordings replay exactly");
        check(Math.Abs(Held(.1, 80, Defaults) - GripSignal.BaseGain * Held(.1, 80, Defaults with { Model = GripSignal.CenteringVersion })) < 1e-6,
            "v8 is v7 times the base gain, so v7 recordings replay exactly as v7");

        // It adds to the tyre force with the same sign: front slip of the steer's restoring direction raises the force.
        check(Held(.1, 80, Defaults, slip: -.2) > Held(.1, 80, Defaults), "tyre force and centering add when the slip follows the steer");

        // Damping resists the wheel's motion from v7 (the rate's sign, as the restoring force of that side); v6 keeps its sign.
        foreach (int model in new[] { GripSignal.EqualKnotsVersion, GripSignal.CenteringVersion, GripSignal.ModelVersion })
        {
            var g = new GripSignal(); var reference = Reference(); var o = Defaults with { Model = model };
            for (int i = 0; i <= 40; i++) g.Evaluate(Row(i * .02, i * .005, 80), o, reference);
            check(model >= GripSignal.CenteringVersion ? g.LastDamping > 0 : g.LastDamping < 0, "damping sign for a wheel moving toward +steer, model " + model);
        }
    }

    // The one-time v8 tune migration of a saved config.
    public static void Migration(Action<bool, string> check)
    {
        Settings Load(string? text, out string path)
        {
            path = Path.Combine(Path.GetTempPath(), "woden-v8-" + Guid.NewGuid().ToString("N") + ".cfg");
            if (text != null) File.WriteAllText(path, text);
            return new Settings(new BepInEx.Configuration.ConfigFile(path, false));
        }
        string Grip(string strength, string ratio, string damping, string extra = "") =>
            $"[ForceFeedback]\nModel = Grip\nStrengthPercent = {strength}\nGripLoadRatio = {ratio}\nDamping = {damping}\n{extra}";

        // The owner: v7 at 74.745544 / 1.5029262 / 0.05 becomes Strength 50 with the rest kept, and feels identical.
        var owner = Load(Grip("74.745544", "1.5029262", "0.05"), out string ownerPath);
        check(owner.FfbStrength == 50 && owner.FfbLoadRatio == OwnerLoadRatio && owner.FfbDamping == .05f && owner.ForceOptions.Model == GripSignal.ModelVersion,
            $"owner tune migrates to Strength 50: {owner.FfbStrength} {owner.FfbLoadRatio} {owner.FfbDamping}");
        check(owner.FfbMigration != null && owner.FfbMigration.Contains("StrengthPercent 74.745544 -> 50", StringComparison.Ordinal) &&
            !owner.FfbMigration.Contains("GripLoadRatio", StringComparison.Ordinal) && !owner.FfbMigration.Contains("Damping", StringComparison.Ordinal),
            "the migration is logged with exactly what changed: " + owner.FfbMigration);
        check(Drive(owner.ForceOptions).SequenceEqual(Drive(OwnerV7)), "the owner's feel is unchanged by the migration, bit for bit");
        owner.Save();
        var reloaded = new Settings(new BepInEx.Configuration.ConfigFile(ownerPath, false));
        check(reloaded.FfbMigration == null && reloaded.FfbStrength == 50 && File.ReadAllText(ownerPath).Contains("DefaultsVersion = 1", StringComparison.Ordinal),
            "the migration is saved once with its marker and never repeats");

        // Earlier defaults (v6: 50 / 2 / 0.05; v7: 50 / 3 / 0.02) take the new defaults: the new feel at 50.
        foreach (var (ratio, damping) in new[] { ("2", "0.05"), ("3", "0.02") })
        {
            var d = Load(Grip("50", ratio, damping), out _);
            check(d.FfbStrength == 50 && d.FfbLoadRatio == 1.5f && d.FfbDamping == .05f && !d.CustomFfb, $"earlier defaults 50/{ratio}/{damping} become the v8 defaults");
        }
        // A custom tune keeps its felt tyre force and centering: Strength / BaseGain, other values kept.
        var custom = Load(Grip("80", "2.5", "0.1"), out _);
        check(Math.Abs(custom.FfbStrength - 80 / GripSignal.BaseGain) < .001f && custom.FfbLoadRatio == 2.5f && custom.FfbDamping == .1f, "a custom tune keeps its feel: " + custom.FfbStrength);
        // Already migrated, Classic and fresh configs are left alone.
        var done = Load(Grip("74.745544", "1.5029262", "0.05", "DefaultsVersion = 1\n"), out _);
        check(done.FfbStrength == 74.745544f && done.FfbMigration == null, "an already migrated config is not migrated again");
        var classic = Load("[ForceFeedback]\nModel = Classic\nStrengthPercent = 70\nDamping = 0.05\n", out _);
        check(classic.FfbModel == "Classic" && classic.FfbStrength == 70 && classic.FfbMigration == null, "Classic has no base gain and is not migrated");
        var fresh = Load(null, out _);
        check(fresh.FfbStrength == 50 && fresh.FfbLoadRatio == 1.5f && fresh.FfbDamping == .05f && fresh.FfbMigration == null && !fresh.CustomFfb, "a fresh config starts on the v8 defaults");
    }

    // The controller's off and mute gates are unchanged: no write while saved Off or diagnostic-muted, force when On.
    public static void Gates(Action<bool, string> check)
    {
        var (controller, device) = ForceControllerChecks.Create();
        Runtime.Settings.Options = Defaults;
        double time = 1;
        TelemetrySample Drive() => Row(time += .02, .15, 80);
        for (int i = 0; i < 160; i++) { controller.Prepare(); controller.Tick(Drive()); }
        check(device.Writes.Count > 0 && device.Writes.Last() > .05, "On: the centering reaches the device: " + device.Writes.LastOrDefault());
        controller.SetEnabled(false); int writes = device.Writes.Count;
        for (int i = 0; i < 20; i++) { controller.Prepare(); var s = Drive(); controller.Tick(s); check(s.Get("ffb.deliveredOutput") == 0, "Off delivers zero"); }
        check(device.Writes.Count == writes, "saved Off writes nothing");
        (controller, device) = ForceControllerChecks.Create(); Runtime.DiagnosticNoForce = true;
        Runtime.Settings.Options = Defaults;
        for (int i = 0; i < 40; i++) { controller.Prepare(); controller.Tick(Drive()); }
        check(device.Writes.Count == 0 && device.Opens == 0, "diagnostic mute writes nothing and opens nothing");
        Runtime.DiagnosticNoForce = false;
    }
}
