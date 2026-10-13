using Dbce.Wheel.Display;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// SpanWindow.Target's decision (SpanTarget) over the vendored toolkit racing-row policy, with fixed monitor rectangles:
// the owner's four-display rig, the saved opt-out and triple gate, the wide-primary path, refusals and a failed read.
// No Windows call, game or display change.
internal static class SpanTargetChecks
{
    static MonitorRect M(int x, int y, int w, int h, bool primary = false) => new(x, y, w, h, primary);
    static readonly MonitorRect Left = M(-2560, 0, 2560, 1440), Centre = M(0, 0, 2560, 1440, true), Right = M(2560, 0, 2560, 1440),
        Diagnostic = M(314, -1080, 1920, 1080);

    static SpanTarget Over(params MonitorRect[] monitors) => new(() => RacingRowPolicy.Select(monitors));

    public static void Run(Action<bool, string> check)
    {
        // The rig since 2026-10-13: the diagnostic display above made the virtual desktop 7680x2520 at (-2560,-1080),
        // which the former virtual-desktop test refused. The row is the three racing displays only.
        var rig = Over(Diagnostic, Right, Centre, Left);
        var span = rig.Decide(enabled: true, triplesAllowed: true, out bool changed);
        check(span == (-2560, 0, 7680, 1440) && changed, "the four-display rig spans the racing row only: " + span + " " + rig.Reason);
        rig.Decide(true, true, out changed);
        check(!changed, "an unchanged decision is not logged again");

        check(Over(Left, Centre, Right).Decide(true, true, out _) == (-2560, 0, 7680, 1440), "the former three-display row spans as before");

        var off = Over(Left, Centre, Right, Diagnostic);
        check(off.Decide(enabled: false, triplesAllowed: true, out changed) == null && changed && off.Reason.Contains("saved setting", StringComparison.Ordinal),
            "the saved opt-out keeps the game's window");
        check(off.Decide(enabled: true, triplesAllowed: false, out changed) == null && changed && off.Reason.Contains("triple views are off", StringComparison.Ordinal),
            "triples off keeps the game's window");
        check(off.Decide(true, true, out changed) == (-2560, 0, 7680, 1440) && changed, "turning both back on spans again, logged once");

        var wide = Over(M(0, 0, 7680, 1440, true), Diagnostic);
        check(wide.Decide(true, true, out _) == null && wide.Reason.Length > 0, "a wide primary keeps its own path, never a span: " + wide.Reason);

        foreach (var (label, layout) in new (string, MonitorRect[])[]
        {
            ("four panels in the row", new[] { Left, Centre, Right, M(5120, 0, 2560, 1440) }),
            ("the primary at an end", new[] { M(0, 0, 2560, 1440, true), M(2560, 0, 2560, 1440), M(5120, 0, 2560, 1440) }),
            ("a missing side", new[] { Centre, Right, Diagnostic }),
            ("a single display with the diagnostic", new[] { Centre, Diagnostic }),
        })
        {
            var target = Over(layout);
            check(target.Decide(true, true, out _) == null && target.Reason.Length > 0, label + " keeps the game's window: " + target.Reason);
        }

        var failed = new SpanTarget(() => throw new InvalidOperationException("EnumDisplayMonitors failed"));
        check(failed.Decide(true, true, out changed) == null && changed && failed.Reason.Contains("could not be read", StringComparison.Ordinal),
            "a failed monitor query is no row, never an exception: " + failed.Reason);

        // One observation serves every check for RefreshSeconds (a per-frame caller does not enumerate each frame);
        // a later topology change is picked up at the next refresh, and the gates never read the displays.
        double clock = 100; int reads = 0;
        var monitors = new List<MonitorRect> { Left, Centre, Right, Diagnostic };
        var cached = new SpanTarget(() => { reads++; return RacingRowPolicy.Select(monitors.ToArray()); }, () => clock);
        for (int frame = 0; frame < 120; frame++) { cached.Decide(true, true, out _); clock += 1.0 / 60; }
        check(reads == 1, "two seconds of per-frame checks read the displays once: " + reads);
        cached.Decide(false, true, out _); cached.Decide(true, false, out _);
        check(reads == 1, "the saved gates are decided without reading the displays");
        monitors[3] = M(314, -200, 1920, 1080); // the diagnostic display moved down into the row's band
        clock += SpanTarget.RefreshSeconds;
        check(cached.Decide(true, true, out changed) == null && changed && reads == 2, "the next refresh sees the new topology and refuses: " + cached.Reason);

        // The vendored selector is the toolkit's bytes (DEPENDENCIES.md): git blob ids of the checked-in files.
        string display = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/WodenRallyEdge.Core/Toolkit/Display"));
        foreach (var (file, blob) in new[] { ("RacingRow.cs", "3d43d868a58b619a75f622481325b700d168f5ad"), ("WindowsMonitors.cs", "cf20f97be06ba43b26a95c4edbd74d3dff58efd5") })
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(display, file));
            byte[] header = System.Text.Encoding.ASCII.GetBytes("blob " + bytes.Length + "\0");
            string actual = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(header.Concat(bytes).ToArray())).ToLowerInvariant();
            check(actual == blob, file + " is the pinned toolkit blob: " + actual);
        }
    }
}
