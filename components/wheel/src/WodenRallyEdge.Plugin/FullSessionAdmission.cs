using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dbce.Wheel.Playback;
using Dbce.Wheel.Sessions;

namespace WodenRallyEdge;

// Offline admission of an S1 original (woden.full-session-package@1) for native-arcade-v1 cold playback. It writes a
// derived sibling package and never changes the original. The derived package restores the recorded stage choice
// through Woden's native Arcade startup and plays the recorded stage; it does not claim every menu input is replayed.
// Its evidence is "route-compatible": the compiler's proof only. A passed cold replay is separate evidence, recorded by
// the replay run against the derived package's seal; this class never claims it. Engine-free for fixtures and tools.
internal static class FullSessionAdmission
{
    internal const string Adapter = "woden.native-arcade-v1@1", Schema = "woden.full-session-admission@1", AdmissionFile = "admission.json";
    internal const string Statement = "Restores the recorded stage choice through Woden's native Arcade startup and plays the recorded stage; it does not replay every menu input.";
    internal sealed record Outcome(bool Admitted, string Reason, string? Output);
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    internal static Outcome Admit(string original, string output, string gameHash)
    {
        original = Path.GetFullPath(original); output = Path.GetFullPath(output);
        if (Directory.Exists(output) || File.Exists(output)) return Refused("the derived output already exists");
        if (output.StartsWith(original.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return Refused("the derived package may not live inside the original");
        Dictionary<string, string> before;
        JsonObject descriptor, admission;
        FullSessionPackageNames.Names names;
        try
        {
            (descriptor, names, admission) = Verify(original, gameHash);
            before = Inventory(original);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { return Refused(ex.Message); }

        string partial = output + ".partial-" + Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(partial);
            foreach (string file in new[] { names.Signals, names.Observation, names.Timeline }) CopyExact(Path.Combine(original, file), Path.Combine(partial, file));
            CopyTree(Path.Combine(original, names.Stage), Path.Combine(partial, names.Stage));
            WriteNew(Path.Combine(partial, AdmissionFile), admission.ToJsonString(Indented));
            var derived = new JsonObject();
            foreach (var pair in descriptor) if (pair.Key != "playbackUnavailableReason") derived[pair.Key] = JsonNode.Parse(pair.Value?.ToJsonString() ?? "null");
            derived["playbackAdapter"] = Adapter;
            derived["admission"] = AdmissionFile;
            derived["admissionEvidence"] = "route-compatible";
            derived["derivedFrom"] = new JsonObject { ["sessionSha256"] = (string?)admission["source"]!["sessionSha256"],
                ["packageSealSha256"] = (string?)admission["source"]!["packageSealSha256"] };
            WriteNew(Path.Combine(partial, FullSessionPackageNames.Descriptor), derived.ToJsonString(Indented));
            var sealed_ = DerivedSealFiles(names);
            ArtifactSeal.Complete(partial, sealed_);
            ArtifactSeal.Verify(partial, sealed_);
            TrajectoryTape.Read(Path.Combine(partial, names.Stage));
            var after = Inventory(original);
            if (after.Count != before.Count || after.Any(p => !before.TryGetValue(p.Key, out var h) || h != p.Value))
                throw new InvalidDataException("the original package changed during admission");
            Directory.Move(partial, output);
            return new Outcome(true, "", output);
        }
        catch (Exception ex)
        {
            // Only this call's own partial folder is removed; the original and any existing output are never touched.
            try { if (Directory.Exists(partial)) Directory.Delete(partial, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return Refused("derived package not written: " + ex.Message);
        }
    }

    /// <summary>The derived outer seal: the admission and every payload, with the copied nested stage seal.</summary>
    internal static string[] DerivedSealFiles(FullSessionPackageNames.Names names) =>
        new[] { FullSessionPackageNames.Descriptor, AdmissionFile, names.Signals, names.Observation, names.Timeline, names.Stage + "/complete.tsv" };

    private static (JsonObject Descriptor, FullSessionPackageNames.Names Names, JsonObject Admission) Verify(string root, string gameHash)
    {
        var descriptor = ReadObject(Path.Combine(root, FullSessionPackageNames.Descriptor), 64 * 1024);
        if ((string?)descriptor["schema"] != OrdinaryFullSession.PackageSchema || (string?)descriptor["game"] != OrdinaryFullSession.Game ||
            (string?)descriptor["gameBuildSha256"] != gameHash) throw new InvalidDataException("not an original Woden full-session package for this build");
        if (descriptor["playbackAdapter"] != null || descriptor["derivedFrom"] != null) throw new InvalidDataException("only an S1 original can be admitted");
        var names = FullSessionPackageNames.FromSignal((string?)descriptor["signals"] ?? "");
        if ((string?)descriptor["observation"] != names.Observation || (string?)descriptor["timeline"] != names.Timeline || (string?)descriptor["stage"] != names.Stage)
            throw new InvalidDataException("payload names disagree");
        foreach (string marker in new[] { OrdinaryFullSession.IncompleteFile, names.Stage + "/incomplete.txt" })
            if (File.Exists(Path.Combine(root, marker))) throw new InvalidDataException("the original is marked incomplete");
        var outer = File.ReadAllLines(Path.Combine(root, "complete.tsv"));
        if (outer.Length != names.OuterSealFiles().Length + 1) throw new InvalidDataException("unexpected outer seal inventory");
        ArtifactSeal.Verify(root, names.OuterSealFiles());

        if (new FileInfo(Path.Combine(root, names.Timeline)).Length > FullSessionTimeline.MaxDocumentBytes) throw new InvalidDataException("timeline too large");
        var timeline = JsonSerializer.Deserialize<FullSessionTimeline>(File.ReadAllBytes(Path.Combine(root, names.Timeline)))
            ?? throw new InvalidDataException("missing timeline");
        timeline.Validate(OrdinaryFullSession.TimelineSchema, forPlayback: true);
        var stage = timeline.Stage!;
        if (timeline.GameBuild != gameHash || timeline.AdapterVersion != OrdinaryFullSession.AdapterVersion || stage.Directory != names.Stage)
            throw new InvalidDataException("timeline build, adapter or stage reference differs");
        string stageDirectory = Path.Combine(root, names.Stage);
        OrdinaryFullSession.VerifyStage(stageDirectory, stage, gameHash);
        OrdinaryFullSession.ReadSource(Path.Combine(root, names.Signals), gameHash);

        var contextPath = Path.Combine(stageDirectory, "stage-context.json");
        if (new FileInfo(contextPath).Length > 65536) throw new InvalidDataException("stage context too large");
        using var context = JsonDocument.Parse(File.ReadAllBytes(contextPath));
        var c = context.RootElement;
        if (c.GetProperty("gameAssemblySha256").GetString() != gameHash) throw new InvalidDataException("stage context names a different build");
        var proof = ArcadeRouteProof.Check(c);
        if (!proof.Admitted) throw new InvalidDataException("route not admitted: " + proof.Reason);
        var selection = timeline.Steps.First(s => s.State?.Phase == FullSessionPhase.Driving).State!.Selection!;
        int carId = c.GetProperty("carId").GetInt32();
        string scene = c.GetProperty("scene").GetString()!;
        if (selection.Scene != scene || selection.Car != "car=" + carId.ToString(CultureInfo.InvariantCulture) ||
            TrajectoryTape.Read(stageDirectory).Metadata["scenario"] != scene + "|car=" + carId.ToString(CultureInfo.InvariantCulture))
            throw new InvalidDataException("timeline, tape and context name different stages or cars");

        string Hash(string name) => ArtifactSeal.Hash(Path.Combine(root, name));
        var admission = new JsonObject
        {
            ["schema"] = Schema, ["evidence"] = "route-compatible", ["coldReplay"] = "not-qualified", ["adapter"] = Adapter, ["statement"] = Statement,
            ["scope"] = new JsonObject { ["mode"] = "Arcade/Arcade", ["scene"] = scene, ["competitionAsset"] = c.GetProperty("competitionAsset").GetString(),
                ["arcadeRoute"] = c.GetProperty("arcadeRoute").GetInt32(), ["arcadeRound"] = 0, ["carId"] = carId, ["players"] = 1 },
            ["route"] = new JsonArray(proof.Route.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
            ["loadEvidence"] = proof.LoadEvidence,
            ["restoredFields"] = new JsonArray(ArcadeRouteProof.RestoredFields.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
            ["poses"] = stage.PoseCount,
            ["pauses"] = timeline.Steps.Count(s => s.State?.Phase == FullSessionPhase.Paused),
            ["source"] = new JsonObject {
                ["sessionSha256"] = Hash(FullSessionPackageNames.Descriptor), ["packageSealSha256"] = Hash("complete.tsv"),
                ["stageSealSha256"] = stage.SealSha256, ["timelineSha256"] = Hash(names.Timeline),
                ["observationSha256"] = Hash(names.Observation), ["signalsSha256"] = Hash(names.Signals) },
        };
        return (descriptor, names, admission);
    }

    private static Dictionary<string, string> Inventory(string root)
    {
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(f => f[prefix.Length..], ArtifactSeal.Hash, StringComparer.OrdinalIgnoreCase);
    }
    private static JsonObject ReadObject(string path, long limit)
    {
        if (new FileInfo(path).Length > limit) throw new InvalidDataException("descriptor too large");
        return JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? throw new InvalidDataException("missing descriptor");
    }
    private static void CopyExact(string from, string to)
    {
        if ((File.GetAttributes(from) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("linked payload refused");
        File.Copy(from, to);
        if (ArtifactSeal.Hash(from) != ArtifactSeal.Hash(to)) throw new IOException("copy readback differs: " + Path.GetFileName(from));
    }
    private static void CopyTree(string from, string to)
    {
        if ((File.GetAttributes(from) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("linked stage refused");
        Directory.CreateDirectory(to);
        foreach (string file in Directory.GetFiles(from)) CopyExact(file, Path.Combine(to, Path.GetFileName(file)));
        if (Directory.GetDirectories(from).Length != 0) throw new InvalidDataException("unexpected folder inside the stage");
    }
    private static void WriteNew(string path, string text)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(System.Text.Encoding.UTF8.GetBytes(text)); stream.Flush(true);
    }
    private static Outcome Refused(string reason) => new(false, reason, null);
}
