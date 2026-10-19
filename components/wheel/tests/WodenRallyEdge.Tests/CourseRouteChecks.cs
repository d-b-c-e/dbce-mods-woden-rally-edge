using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Offline fixtures for the read-only course route capture. Every route and sample is synthetic: no owner capture,
// game interop or Unity is used, so each expectation follows from the inputs below.
internal static class CourseRouteChecks
{
    private const string Build = "f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c";
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 3, 0, 0, TimeSpan.Zero);

    private static CourseRouteFacts Route(string track = "Kenya SS1", bool reverse = false, int points = 50,
        IReadOnlyList<Vector3>? waypoints = null, IReadOnlyList<float>? distances = null, string build = Build, int laps = 1) =>
        new(build, track, track, reverse, laps, false, points * 10f, points / 100f, 1f,
            waypoints ?? Enumerable.Range(0, points).Select(i => new Vector3(i * 10 + .125f, 5.5f + MathF.Sin(i * .1f), -i * 7.75f)).ToList(),
            distances ?? Enumerable.Range(0, points).Select(i => i * 12.6f).ToList(), true);

    private static string Root() => Path.Combine(Path.GetTempPath(), "woden-course-route-" + Guid.NewGuid().ToString("N"));

    private static CourseRouteCapture Driven(CourseRouteFacts route, int rows = 120, int maxSeconds = 600)
    {
        var capture = new CourseRouteCapture(Guid.NewGuid(), maxSeconds);
        capture.SetRoute(route);
        for (int i = 0; i < rows; i++)
            capture.AddSample(1000 + i, i / 60.0, new Vector3(i * .5f, 5.5f, -i * .4f), i * .64f, i / (float)rows, "RACE");
        capture.Complete("car END");
        return capture;
    }

    internal static void Request(Action<bool, string> check)
    {
        string dir = Root(); Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, CourseRouteRequest.FileName);
            check(CourseRouteRequest.Consume(path, Now) == null, "no request file arms nothing");
            File.WriteAllText(path, JsonSerializer.Serialize(new CourseRouteRequest(1, Guid.NewGuid(), Now.AddMinutes(5), 120)));
            var request = CourseRouteRequest.Consume(path, Now);
            check(request is { MaxSeconds: 120 }, "a valid request is accepted with its own time limit");
            check(!File.Exists(path) && Directory.GetFiles(dir, CourseRouteRequest.FileName + ".consumed-*").Length == 1,
                "the request is claimed before reading, so it cannot arm a second launch");
            var bad = new[]
            {
                new CourseRouteRequest(2, Guid.NewGuid(), Now.AddMinutes(5)), new CourseRouteRequest(1, Guid.Empty, Now.AddMinutes(5)),
                new CourseRouteRequest(1, Guid.NewGuid(), Now), new CourseRouteRequest(1, Guid.NewGuid(), Now.AddMinutes(21)),
                new CourseRouteRequest(1, Guid.NewGuid(), Now.AddMinutes(5), 9), new CourseRouteRequest(1, Guid.NewGuid(), Now.AddMinutes(5), 601),
            };
            foreach (var request2 in bad)
            {
                File.WriteAllText(path, JsonSerializer.Serialize(request2));
                bool refused = false;
                try { CourseRouteRequest.Consume(path, Now); } catch (IOException) { refused = true; }
                check(refused && !File.Exists(path), "refused, and still claimed: " + request2);
            }
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    internal static void Identity(Action<bool, string> check)
    {
        string root = Root();
        try
        {
            string forward = Driven(Route()).Write(root, Now);
            check(Path.GetFileName(forward) == "woden-kenya-ss1-fwd-20261019T030000Z", "folder names track, direction and UTC time: " + forward);
            using (var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(forward, "route.json"))))
            {
                var r = json.RootElement;
                check(r.GetProperty("schema").GetString() == "dbce.course-route@1" && r.GetProperty("game").GetString() == "Super Woden Rally Edge",
                    "schema and game");
                check(r.GetProperty("gameAssemblySha256").GetString() == Build, "the route names the exact game build");
                check(r.GetProperty("trackAsset").GetString() == "Kenya SS1" && r.GetProperty("trackName").GetString() == "Kenya SS1",
                    "identity keeps the raw asset and circuit names; the course id is derived offline by the toolkit");
                check(!r.GetProperty("reverseTrack").GetBoolean() && r.GetProperty("totalLaps").GetInt32() == 1 &&
                      !r.GetProperty("isClosedCircuit").GetBoolean(), "direction, laps and loop flag recorded as read");
                check(r.GetProperty("circuitSameAsStageProgress").GetBoolean(), "circuit cross-check recorded");
            }
            string reverse = Driven(Route(reverse: true)).Write(root, Now);
            check(Path.GetFileName(reverse) == "woden-kenya-ss1-rev-20261019T030000Z", "a reversed stage is a different capture");

            check(CourseRouteCapture.PathToken("Kenya SS1 (Night)") == "kenya-ss1-night", "punctuation runs become one dash");
            check(CourseRouteCapture.PathToken(" \t ") == "unnamed", "nothing usable is named, never empty");

            var refused = new (CourseRouteFacts Route, string Why)[]
            {
                (Route(track: " "), "track asset missing"),
                (Route(build: ""), "game assembly hash missing"),
                (Route(build: new string('g', 64)), "game assembly hash missing"),
                (Route(laps: -1), "negative lap count"),
                (Route(points: 1), "fewer than 2 waypoints"),
                (Route(waypoints: new[] { Vector3.Zero, new Vector3(float.NaN, 0, 0) }), "waypoint 1 is not finite"),
                (Route(waypoints: Enumerable.Range(0, CourseRouteCapture.MaxWaypoints + 1).Select(i => new Vector3(i, 0, 0)).ToList()),
                    "more than 20000 waypoints"),
                (Route(distances: new[] { 0f, float.PositiveInfinity }), "distances unusable"),
            };
            foreach (var (route, why) in refused)
            {
                var capture = new CourseRouteCapture(Guid.NewGuid());
                capture.SetRoute(route);
                capture.AddSample(1, 0, Vector3.Zero, 0, null, "RACE");
                check(capture.State == CourseRouteState.Refused && capture.Reason == why && capture.Rows == 0,
                    "refused: " + why + " (got " + capture.State + ": " + capture.Reason + ")");
                bool blocked = false;
                try { capture.Write(root, Now); } catch (InvalidOperationException) { blocked = true; }
                check(blocked, "a refused capture writes nothing: " + why);
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    internal static void Coordinates(Action<bool, string> check)
    {
        string root = Root();
        try
        {
            var awkward = new[]
            {
                new Vector3(-1234.5677f, .1f, 98765.43f), new Vector3(1.17549435E-38f, -3.4028235E+38f, 7f / 3f),
                new Vector3(697.1295f, 5.9963126f, 239.48135f),
            };
            var capture = new CourseRouteCapture(Guid.NewGuid());
            capture.SetRoute(Route(waypoints: awkward, distances: new[] { 0f, 1f, 2f }));
            capture.AddSample(7, 1.0 / 3.0, new Vector3(1, 2, 3), 12.5f, .25f, "RACE");
            capture.AddSample(8, 2.0 / 3.0, awkward[0], 13.75f, null, "RACE");
            capture.Complete("car END");
            string folder = capture.Write(root, Now);

            using (var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder, "route.json"))))
            {
                var r = json.RootElement;
                check(r.GetProperty("frame").GetString() == "unity-world" &&
                      r.GetProperty("axes").GetString()!.StartsWith("x east, y up, z north"),
                    "points are labelled as unconverted Unity world coordinates");
                var points = r.GetProperty("waypoints").EnumerateArray().ToList();
                check(points.Count == awkward.Length && r.GetProperty("waypointCount").GetInt32() == awkward.Length, "every waypoint, in order");
                for (int i = 0; i < awkward.Length; i++)
                {
                    var xyz = points[i].EnumerateArray().Select(e => e.GetSingle()).ToArray();
                    check(xyz.Length == 3 && Same(xyz[0], awkward[i].X) && Same(xyz[1], awkward[i].Y) && Same(xyz[2], awkward[i].Z),
                        $"waypoint {i} round-trips bit-exact as x, y, z without swapping or sign change");
                }
            }

            var rows = File.ReadAllLines(Path.Combine(folder, "progress.tsv"));
            check(rows[0] == CourseRouteCapture.ProgressHeader && rows.Length == 3, "header plus one row per accepted tick");
            var first = rows[1].Split('\t');
            check(first[0] == "7" && double.Parse(first[1], CultureInfo.InvariantCulture) == 1.0 / 3.0,
                "tick and simulation seconds round-trip exactly");
            check(first[2] == "1" && first[3] == "2" && first[4] == "3", "car position is written px, py, pz in Unity order");
            check(first[5] == "12.5" && first[6] == "0.25" && first[7] == "RACE", "game progress, HUD percentage and status");
            var second = rows[2].Split('\t');
            check(Same(float.Parse(second[2], CultureInfo.InvariantCulture), awkward[0].X) &&
                  Same(float.Parse(second[4], CultureInfo.InvariantCulture), awkward[0].Z), "awkward positions round-trip bit-exact");
            check(second[6] == "", "a missing HUD percentage is empty, never zero");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    internal static void Lifecycle(Action<bool, string> check)
    {
        var early = new CourseRouteCapture(Guid.NewGuid());
        early.AddSample(1, 0, Vector3.Zero, 0, null, "WARMING");
        check(early.State == CourseRouteState.WaitingForRoute && early.Rows == 0, "no row before the route is read");
        early.Complete("car END");
        check(early.State == CourseRouteState.Refused && early.Reason == "no route before car END", "a stage that ends first is refused");

        var skips = new CourseRouteCapture(Guid.NewGuid());
        skips.SetRoute(Route());
        skips.AddSample(1, 1.0, Vector3.One, 1, null, "RACE");
        skips.AddSample(2, 2.0, new Vector3(float.NaN, 0, 0), 1, null, "RACE");
        skips.AddSample(3, 0.5, Vector3.One, 1, null, "RACE");
        skips.AddSample(4, 3.0, Vector3.One, float.PositiveInfinity, null, "RACE");
        skips.AddSample(5, 4.0, Vector3.One, 1, float.NaN, "RACE");
        skips.AddSample(6, 5.0, Vector3.One, 1, null, "RA\tCE");
        check(skips.Rows == 1 && skips.SkippedRows == 5, "non-finite, backwards-in-time and unsafe rows are counted, never written");

        var timed = new CourseRouteCapture(Guid.NewGuid(), maxSeconds: 10);
        timed.SetRoute(Route());
        for (int i = 0; i <= 12 * 60; i++) timed.AddSample(i, i / 60.0, Vector3.One, i, null, "RACE");
        check(timed.State == CourseRouteState.LimitReached && timed.Reason == "10 s" && timed.Rows == 601,
            "the request's seconds bound the rows: " + timed.Rows);
        timed.Complete("car END");
        check(timed.State == CourseRouteState.LimitReached, "a limit is final");

        var capped = new CourseRouteCapture(Guid.NewGuid());
        capped.SetRoute(Route());
        for (int i = 0; i <= CourseRouteCapture.MaxRows; i++) capped.AddSample(i, i / 100.0, Vector3.One, i, null, "RACE");
        check(capped.State == CourseRouteState.LimitReached && capped.Reason == "36000 rows" && capped.Rows == CourseRouteCapture.MaxRows,
            "rows stop at the cap");

        var faulted = new CourseRouteCapture(Guid.NewGuid());
        faulted.SetRoute(Route());
        faulted.AddSample(1, 0, Vector3.One, 0, null, "RACE");
        faulted.Fault("NullReferenceException: tracker");
        faulted.Complete("car END");
        check(faulted.State == CourseRouteState.Faulted && faulted.Terminal, "a read failure abandons the capture");
        bool faultWrite = false;
        try { faulted.Write(Root(), Now); } catch (InvalidOperationException) { faultWrite = true; }
        check(faultWrite, "a faulted capture writes nothing");

        string root = Root();
        try
        {
            // Worst case on disk: the most waypoints and rows, with long numbers everywhere.
            var huge = Route(waypoints: Enumerable.Range(0, CourseRouteCapture.MaxWaypoints)
                    .Select(i => new Vector3(-12345.679f - i / 7f, 123.45679f + i / 9f, -98765.43f + i / 11f)).ToList(),
                distances: Enumerable.Range(0, CourseRouteCapture.MaxWaypoints + 1).Select(i => i * 1.2345679f).ToList());
            var full = new CourseRouteCapture(Guid.NewGuid());
            full.SetRoute(huge);
            for (int i = 0; i < CourseRouteCapture.MaxRows; i++)
                full.AddSample(100000 + i, i / 60.0 + 1e-7, new Vector3(-12345.679f + i / 3f, 123.45679f, -98765.43f + i / 7f),
                    9876.5432f + i / 13f, i / 36000f, "DESTROYED");
            full.Complete("car DESTROYED");
            string folder = full.Write(root, Now);
            long bytes = Directory.GetFiles(folder).Sum(f => new FileInfo(f).Length);
            check(bytes <= CourseRouteCapture.MaxBytes, "worst case fits the disk bound: " + bytes);
            var seal = File.ReadAllLines(Path.Combine(folder, "complete.tsv"));
            check(seal[0] == "dbce-playback-seal-v1" && seal.Length == 3, "seal lists both files");
            foreach (var line in seal.Skip(1))
            {
                var parts = line.Split('\t');
                byte[] content = File.ReadAllBytes(Path.Combine(folder, parts[0]));
                check(long.Parse(parts[1], CultureInfo.InvariantCulture) == content.Length &&
                      parts[2] == Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(), "seal matches " + parts[0]);
            }
            check(Directory.GetDirectories(root, ".*partial*").Length == 0, "no partial folder is left after a write");

            bool twice = false;
            try { full.Write(root, Now); } catch (InvalidOperationException) { twice = true; }
            check(twice, "a capture is written once");

            var clash = Driven(Route());
            Directory.CreateDirectory(Path.Combine(root, CourseRouteCapture.FolderName(clash.Route!, Now)));
            bool kept = false;
            try { clash.Write(root, Now); } catch (IOException) { kept = true; }
            check(kept && Directory.GetDirectories(root, ".*partial*").Length == 0, "an existing capture is never overwritten, and nothing partial remains");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    private static bool Same(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
}
