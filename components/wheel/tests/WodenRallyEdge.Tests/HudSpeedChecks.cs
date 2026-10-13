using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Replay's HUD speed (RecordedHudSpeed): the capture's own game.trueSpeed per trajectory row, never the velocity-derived
// 3.6 * |v| when the capture supplies it. Synthetic rows only; the shape mirrors the sealed Kenya reference's largest
// disagreement (live 70 against derived 105; hub artifacts/woden-hud-jitter-review-20261013).
internal static class HudSpeedChecks
{
    static Dictionary<string, double> Row(int index, double? speed)
    {
        var c = new Dictionary<string, double> { ["capture.trajectoryIndex"] = index, ["motion.speed"] = 1 };
        if (speed is { } s) c[RecordedHudSpeed.Channel] = s;
        return c;
    }
    // The earlier replay's value, exactly as StagePlayback derived it: (int)(|v| * 3.6f).
    static int Derived(float vx, float vy, float vz) => (int)(MathF.Sqrt(vx * vx + vy * vy + vz * vz) * 3.6f);
    static RecordedHudSpeedTable Load(int frames, params Dictionary<string, double>[] rows)
    {
        var speed = new RecordedHudSpeed();
        foreach (var row in rows) speed.Add(row);
        return speed.Finish(frames);
    }
    static bool Refuses(Action action, string text)
    {
        try { action(); return false; }
        catch (InvalidDataException ex) { return ex.Message.Contains(text, StringComparison.Ordinal); }
    }

    internal static void Run(Action<bool, string> check)
    {
        // Row 1 is the Kenya disagreement: the HUD showed 70 while the recorded velocity (29.2 m/s) derives 105.
        int derived = Derived(29.2f, 0f, 0.15f);
        check(derived == 105, "the velocity-derived value of the disagreement row is 105 (3.6 * |v|)");
        var table = Load(3, Row(0, 0), Row(1, 70), Row(2, 71));
        check(table.Recorded && table.Rows == 3, "a capture that supplies game.trueSpeed on every row gives a recorded table");
        check(table.Value(1, derived) == 70, "replay writes the recorded 70 for that row, not the derived 105");
        check(table.Value(0, 9) == 0 && table.Value(2, 9) == 71, "each row keeps its own recorded value, in trajectory order");
        check(Refuses(() => table.Value(3, derived), "outside the recorded HUD speed") && Refuses(() => table.Value(-1, derived), "outside"),
            "a recorded table never falls back to the derived value for a row it does not hold");

        // Alignment with capture.trajectoryIndex: a gap, a repeat or a missing index is refused.
        check(Refuses(() => Load(3, Row(0, 0), Row(2, 70), Row(3, 71)), "alignment differs at row 1"), "a skipped trajectory index is refused");
        check(Refuses(() => Load(3, Row(0, 0), Row(0, 70), Row(1, 71)), "alignment differs at row 1"), "a repeated trajectory index is refused");
        check(Refuses(() => new RecordedHudSpeed().Add(new Dictionary<string, double> { [RecordedHudSpeed.Channel] = 70 }), "alignment differs at row 0"),
            "a row without capture.trajectoryIndex is refused");
        check(Refuses(() => Load(4, Row(0, 0), Row(1, 70), Row(2, 71)), "differ from the trajectory (4)"), "a row count that differs from the trajectory is refused");

        // Only a capture with no game.trueSpeed on any row (recorded before the channel) derives the value.
        var historical = Load(2, Row(0, null), Row(1, null));
        check(!historical.Recorded && historical.Value(1, derived) == 105, "a capture without the channel replays the derived value, as before");
        check(RecordedHudSpeedTable.Derived.Value(7, 33) == 33, "the derived table returns the derived value for any row");

        // A supplied channel must be complete and well formed: never partially or silently filled in.
        check(Refuses(() => Load(3, Row(0, 0), Row(1, null), Row(2, 71)), "missing on 1 of 3 rows"), "a partial supply is refused, not filled from velocity");
        foreach (var (bad, why) in new[] { (double.NaN, "NaN"), (double.PositiveInfinity, "infinity"), (70.5, "a fraction"), (5000d, "above range"), (-5000d, "below range") })
            check(Refuses(() => Load(2, Row(0, 0), Row(1, bad)), "malformed at row 1"), "a supplied speed that is " + why + " is refused");
        check(Load(2, Row(0, -12), Row(1, RecordedHudSpeed.Maximum)).Value(0, 0) == -12, "reverse and the range ends are accepted");
    }
}
