using System.Text.Json;
using System.Text.Json.Nodes;
using Dbce.Wheel.Recording;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Producer input origin (deli 71803, sana 71818): the companion's optional request field, the plugin's consumption, the
// ordinary source metadata and the full-session package. Origin is never inferred and never gates playback. Production
// DiagnosticLaunch and OrdinaryFullSession; real recorders. No game or device.
internal static class OriginChecks
{
    public static void Run(Action<bool, string> check)
    {
        string temp = Path.Combine(Path.GetTempPath(), "woden-origin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            DiagnosticLaunch? Consume(string? origin, bool omit = false, string? raw = null)
            {
                string path = Path.Combine(temp, "woden-record-next-launch-" + Guid.NewGuid().ToString("N") + ".json");
                var request = new JsonObject { ["Version"] = 2, ["Id"] = Guid.NewGuid().ToString(), ["ExpiresUtc"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("o"),
                    ["DisableForces"] = true, ["CaseId"] = "woden-origin-fixture" };
                if (!omit) request["InputOrigin"] = raw != null ? JsonNode.Parse(raw) : origin;
                File.WriteAllText(path, request.ToJsonString());
                return DiagnosticLaunch.Consume(path, DateTimeOffset.UtcNow);
            }
            bool Refused(Func<DiagnosticLaunch?> consume) { try { consume(); return false; } catch (Exception ex) when (ex is IOException or JsonException) { return true; } }

            check(Consume(null, omit: true)!.EffectiveInputOrigin == "unknown", "an older request without the field is unknown");
            check(Consume("human")!.EffectiveInputOrigin == "human" && Consume("automated")!.EffectiveInputOrigin == "automated", "human and automated are accepted exactly");
            foreach (string bad in new[] { "Human", "AUTOMATED", "owner", "unknown", "" })
                check(Refused(() => Consume(bad)), "a request may not state " + (bad.Length == 0 ? "an empty origin" : bad));
            check(Refused(() => Consume(null, raw: "1")) && Refused(() => Consume(null, raw: "{}")), "a non-string origin is refused");
            check(Consume(null)!.EffectiveInputOrigin == "unknown", "an explicit null is the same as omission");
        }
        finally { try { Directory.Delete(temp, true); } catch (IOException) { } }

        // Package: the descriptor and observation say what the request said; owner only for human input.
        foreach (var (origin, expected) in new[] { ("human", "owner"), ("automated", "diagnostic"), ("unknown", "diagnostic") })
        {
            using var f = new FullSessionChecks.Fixture(properties: _ => Properties(origin), inputOrigin: origin);
            Package(f);
            check(f.Session.Outcome == "package", origin + ": packaged");
            var descriptor = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Root, "session.json")))!.AsObject();
            check((string?)descriptor["inputOrigin"] == origin && (string?)descriptor["origin"] == expected, origin + ": descriptor maps to " + expected);
            var header = JsonNode.Parse(File.ReadLines(Path.Combine(f.Root, "source.session.jsonl")).First())!["detail"]!;
            check((string?)header["inputOrigin"] == origin && (bool?)header["diagnostic"] == (expected == "diagnostic"), origin + ": observation header");
            var admitted = FullSessionAdmission.Admit(f.Root, f.Root + ".admitted", FullSessionChecks.Hash);
            check(admitted.Admitted, origin + ": origin never gates admission: " + admitted.Reason);
            // A consumer fixture (Wheelkit) can take the writer's bytes with a stated origin.
            if (origin == "human" && Environment.GetEnvironmentVariable("WODEN_ORIGIN_FIXTURE_OUT") is { Length: > 0 } export) CopyTree(f.Root, Path.Combine(export, Path.GetFileName(f.Root)));
        }
        // The source metadata and the package must state the same origin; a mismatch is never packaged.
        foreach (var (source, package) in new[] { ((string?)null, "human"), ("automated", "human"), ("human", "unknown") })
        {
            using var f = new FullSessionChecks.Fixture(properties: _ => Properties(source), inputOrigin: package);
            Package(f);
            check(f.Session.Outcome == "incomplete" && !File.Exists(Path.Combine(f.Root, "session.json")) &&
                File.ReadAllText(Path.Combine(f.Root, OrdinaryFullSession.IncompleteFile)).Contains("input origin", StringComparison.Ordinal),
                "source " + (source ?? "absent") + " versus package " + package + " is refused");
        }
    }

    static void CopyTree(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string file in Directory.GetFiles(from)) File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        foreach (string directory in Directory.GetDirectories(from)) CopyTree(directory, Path.Combine(to, Path.GetFileName(directory)));
    }
    static Dictionary<string, string> Properties(string? origin)
    {
        var properties = new Dictionary<string, string> { ["gameAssemblySha256"] = FullSessionChecks.Hash };
        if (origin != null) properties["inputOrigin"] = origin;
        return properties;
    }
    static void Package(FullSessionChecks.Fixture f)
    {
        f.Menus(); f.Open();
        for (int i = 0; i < 20; i++) f.Tick();
        f.Session.Seal("finished", true, f.Now);
        f.CloseOrdinary(); f.Session.Stop(f.Now += .1); f.Session.Package();
    }
}
