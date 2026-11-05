using System.Text;
using WodenRallyEdge;
using WodenRallyEdge.Core;

internal static class Program
{
    private static int checks, sequence;
    private static string evidence = "";
    private static readonly CameraSlot Bonnet = new(MountedView.Bonnet);
    private static void Check(bool value, string why) { checks++; if (!value) throw new Exception(why); }
    private static void Near(double value, double expected, string why) => Check(Math.Abs(value - expected) < 1e-8, why);
    private static string Fresh(string extra = "")
    {
        string path = Path.Combine(evidence, "camera-" + (++sequence) + ".cfg");
        byte[] text = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(
            "# owner keeper\r\n[Triple]\r\nBonnetRealistic = false\r\nBonnetGameOffsetDegrees  =  0  \n# keeper\n" +
            "BonnetRealisticOffsetDegrees = -100\r\nCar1.Bonnet.Game.SideOffset = 12\r\n[ForceFeedback]\r\nStrength = 50\r\n" + extra)).ToArray();
        File.WriteAllBytes(path, text); return path;
    }
    private static void Refused(Action action, string why)
    { bool refused = false; try { action(); } catch (Exception) { refused = true; } Check(refused, why); }
    private static int Main(string[] args)
    {
        if (args.Length < 1) throw new ArgumentException("Fresh evidence directory required.");
        evidence = Path.GetFullPath(args[0]); if (Directory.Exists(evidence)) throw new IOException("Preserve existing evidence.");
        Directory.CreateDirectory(evidence);
        var modes = args.Length > 1 ? new[] { args[1] } : new[] { "lens", "store", "projection", "consumer" };
        string mode = "";
        try
        {
            foreach (string item in modes)
            {
                mode = item;
                switch (mode) { case "lens": Lens(); break; case "store": Storage(); break; case "projection": Projection(); break; case "consumer": Consumer(); break; default: throw new ArgumentException("Unknown witness."); }
            }
            Console.WriteLine("PASS " + checks + " actual FOV store/edit/lens/projection assertions; native camera/notice simulated."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("REGRESSION " + mode + ": " + ex); return 1; }
    }
    private static void Lens()
    {
        Near(CameraLens.Fov(70, 200), 179, "finite high request clamps only at final lens");
        Near(CameraLens.Fov(34, -200), 1, "finite low request clamps only at final lens");
        foreach (double start in new[] { 34d, 70d, 90d })
        foreach (double delta in new[] { -2d, 0d, 2d }) Near(CameraLens.Fov(start, delta), start + delta, "ordinary lens retained");
        Near(CameraLens.Fov(70, float.MaxValue), 179, "largest finite saved offset stays representable");
        Near(CameraLens.Fov(70, -float.MaxValue), 1, "lowest finite saved offset stays representable");
        Refused(() => CameraLens.Fov(double.NaN, 0), "invalid native measurement refused");
        Refused(() => CameraLens.Fov(70, double.PositiveInfinity), "nonfinite delta refused");
    }
    private static void Storage()
    {
        string path = Fresh(); TripleView.Open(path, Bonnet);
        byte[] before = File.ReadAllBytes(path); string original = Encoding.UTF8.GetString(before);
        TripleView.Store.SetFov(Bonnet, false, 70, 250);
        Near(TripleView.Store.FovOffset(Bonnet, false), 180, "finite requested delta retained");
        Check(Encoding.UTF8.GetString(File.ReadAllBytes(path)) == original.Replace("BonnetGameOffsetDegrees  =  0  \n", "BonnetGameOffsetDegrees  =  180  \n"), "one owned scalar only; BOM/mixed endings/comments/force retained");
        TripleView.Open(path, Bonnet); Near(TripleView.Store.FovOffset(Bonnet, false), 180, "real restart retains unrestricted delta");
        Near(TripleView.Store.FovOffset(Bonnet, true), -100, "other set retained");
        var pose = TripleView.Store.Position(Bonnet, 1, false); Near(pose.Side, 12, "pose retained");
        var stable = File.ReadAllBytes(path);
        Refused(() => TripleView.Store.SetFov(Bonnet, false, 70, double.NaN), "nonfinite request refused");
        Check(stable.SequenceEqual(File.ReadAllBytes(path)), "invalid request leaves bytes exact");
        TripleView.Store.SetFov(Bonnet, false, 70, -250); TripleView.Open(path, Bonnet);
        Near(TripleView.Store.FovOffset(Bonnet, false), -320, "negative raw delta survives restart");
        TripleView.Store.Reset(Bonnet, 1, false);
        Near(TripleView.Store.FovOffset(Bonnet, false), 0, "reset saved FOV zero");
        Near(TripleView.Store.Position(Bonnet, 1, false).Side, 0, "reset current pose");
        Near(TripleView.Store.FovOffset(Bonnet, true), -100, "reset retains other set");
    }
    private static void Projection()
    {
        foreach (bool game in new[] { true, false })
        foreach (double offset in new[] { 200d, -200d, (double)float.MaxValue, -float.MaxValue })
        {
            double lens = offset > 0 ? 179 : 1;
            double expected = TripleGeometry.EyeDistanceForVerticalFov(398.5, lens);
            double eye = TripleGeometry.EyeDistanceForView(398.5, 660, game, game, MountedView.Stock, 70, null, offsetDegrees: offset);
            Near(eye, expected, "saturated projection uses same final angle as lens");
        }
        Near(TripleGeometry.EyeDistanceForView(398.5, 660, false, false, MountedView.Stock, 70, null), 660, "zero Realistic geometry exactly retained");
        Near(TripleGeometry.EyeDistanceForView(398.5, 660, true, true, MountedView.Stock, 70, null), TripleGeometry.EyeDistanceForVerticalFov(398.5, 70), "ordinary Game geometry retained");
        Near(TripleGeometry.EyeDistanceForView(398.5, 660, true, true, MountedView.Stock, double.NaN, null), 660, "unknown native start retains physical fallback");
    }
    private static void Consumer()
    {
        string path = Fresh(); string raw = File.ReadAllText(path);
        File.WriteAllText(path, raw.Replace("BonnetGameOffsetDegrees  =  0  \n", "BonnetGameOffsetDegrees  =  180  \n"), new UTF8Encoding(false));
        TripleView.Open(path, Bonnet);
        TripleView.AdjustFov(-2); Near(TripleView.Store.FovOffset(Bonnet, false), 178, "actual key edits raw saturated offset instead of clipped display");
        Check(TripleView.ViewsInvalidated, "actual editing handler invalidates cached projection");
        Check(TripleView.TryGetPanelFov(out float shown) && shown == 179, "display remains final lens");
        TripleView.Native(90); TripleView.AdjustFov(2); Near(TripleView.Store.FovOffset(Bonnet, false), 180, "native lens change does not consume saved request");
        TripleView.Store.SetRealistic(Bonnet, true); TripleView.AdjustFov(2);
        Near(TripleView.Store.FovOffset(Bonnet, true), -98, "Realistic low saturation edits raw offset");
        Check(TripleView.TryGetPanelFov(out shown) && shown == 1, "Realistic display uses final low lens");
        TripleView.Store.SetRealistic(Bonnet, false); TripleView.Native(70);
        TripleView.SetFovFromPanel(130); Near(TripleView.Store.FovOffset(Bonnet, false), 60, "actual absolute panel request uses native start");
        TripleView.AdjustFov(-2); Near(TripleView.Store.FovOffset(Bonnet, false), 58, "ordinary nudge unchanged");
        var before = File.ReadAllBytes(path);
        TripleView.CameraEditAllowed = false; TripleView.AdjustFov(2); Check(before.SequenceEqual(File.ReadAllBytes(path)), "inactive consumer cannot save");
        TripleView.CameraEditAllowed = true; TripleView.SourceAvailable(false); TripleView.AdjustFov(2); Check(before.SequenceEqual(File.ReadAllBytes(path)), "unknown camera cannot save");
        TripleView.SourceAvailable(true);
        using (var held = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        { TripleView.AdjustFov(2); Check(TripleView.Notice.Contains("Couldn't save"), "actual consumer reports locked writer"); }
        Check(before.SequenceEqual(File.ReadAllBytes(path)), "consumer failure preserves bytes");
        Near(TripleView.Store.FovOffset(Bonnet, false), 58, "consumer failure preserves cache");
        TripleView.AdjustFov(2); TripleView.Open(path, Bonnet); Near(TripleView.Store.FovOffset(Bonnet, false), 60, "fresh explicit retry saves latest nudge");
        before = File.ReadAllBytes(path); TripleView.AdjustFov(float.NaN);
        Check(before.SequenceEqual(File.ReadAllBytes(path)), "nonfinite consumer delta never writes");
    }
}
