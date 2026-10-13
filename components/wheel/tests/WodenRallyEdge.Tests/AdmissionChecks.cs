using System.Text.Json;
using System.Text.Json.Nodes;
using Dbce.Wheel.Playback;
using Dbce.Wheel.Sessions;

namespace WodenRallyEdge;

// S2: ArcadeRouteProof (StageStartup's route gate) and FullSessionAdmission, compiled from production source.
// Route cases start from the archived real Kenya SS1 stage context (references/woden-kenya-20261005, sealed
// 2026-10-05; Fixtures/kenya-ss1-stage-context.json), which observed its scenes but no native loads. Admission
// cases admit S1 originals written by the production S1 writer (FullSessionChecks.Fixture). No game or device.
internal static class AdmissionChecks
{
    static JsonObject Kenya() => JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "kenya-ss1-stage-context.json")))!.AsObject();
    static ArcadeRouteProof.Result Proof(JsonObject context)
    {
        using var document = JsonDocument.Parse(context.ToJsonString());
        return ArcadeRouteProof.Check(document.RootElement.Clone());
    }
    static JsonArray Scenes(params string[] names) => new(names.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
    static JsonObject Load(string from, string to, JsonObject selection) =>
        new() { ["sceneFrom"] = from, ["sceneTo"] = to, ["nativeMethod"] = "MenuCameraScript.LoadScene", ["selection"] = selection.DeepClone(), ["wallSeconds"] = 10.0 };

    public static void Route(Action<bool, string> check)
    {
        var kenya = Proof(Kenya());
        check(kenya.Admitted && kenya.LoadEvidence == "absent" && kenya.Route.SequenceEqual(new[] { "MapScreen", "StagePresentation", "Kenya SS1" }),
            "the archived Kenya original stays admitted, with absent native-load evidence: " + kenya.Reason);

        void Admits(string label, Action<JsonObject> edit, string evidence = "absent")
        {
            var c = Kenya(); edit(c); var r = Proof(c);
            check(r.Admitted && r.LoadEvidence == evidence, label + ": " + r.Reason);
        }
        void Refuses(string label, Action<JsonObject> edit, string reason)
        {
            var c = Kenya(); edit(c); var r = Proof(c);
            check(!r.Admitted && r.Reason.Contains(reason, StringComparison.Ordinal), label + ": admitted=" + r.Admitted + " " + r.Reason);
        }
        // Owner-path variants: only the route's end is restored; earlier menus are evidence.
        Admits("no daily message", c => c["observedScenes"] = Scenes("SplashScreen", "Title Screen", "MapScreen", "StagePresentation", "Kenya SS1"));
        Admits("menu wandering before the map", c => c["observedScenes"] = Scenes("SplashScreen", "Title Screen", "MapScreen", "Garage", "Options", "MapScreen", "StagePresentation", "Kenya SS1"));
        Admits("loading scenes between", c => c["observedScenes"] = Scenes("SplashScreen", "Title Screen", "MapScreen", "Loading Screen", "StagePresentation", "LoadingScene", "Kenya SS1"));
        Admits("observed load from the map with the restored choice", c =>
        {
            var selection = c["selection"]!.AsObject();
            c["nativeSceneRequests"] = new JsonArray(Load("Title Screen", "MapScreen", selection), Load("MapScreen", "StagePresentation", selection), Load("StagePresentation", "Kenya SS1", selection));
            c["observedScenesTruncated"] = false; c["nativeSceneRequestsTruncated"] = false;
        }, "matched");
        Admits("a field the cold start does not write may differ", c =>
        {
            var loaded = c["selection"]!.DeepClone().AsObject(); loaded["gameMasterCars"] = new JsonArray(1, 0, 0, 0);
            c["nativeSceneRequests"] = new JsonArray(Load("MapScreen", "StagePresentation", loaded));
        }, "matched");

        Refuses("the map chose another car", c =>
        {
            var loaded = c["selection"]!.DeepClone().AsObject(); loaded["arcadeCars"] = new JsonArray(9, 0, 0, 0);
            c["nativeSceneRequests"] = new JsonArray(Load("MapScreen", "StagePresentation", loaded));
        }, "differs at arcadeCars");
        Refuses("another skin", c =>
        {
            var loaded = c["selection"]!.DeepClone().AsObject(); loaded["arcadeSkins"] = new JsonArray(2, 0, 0, 0);
            c["nativeSceneRequests"] = new JsonArray(Load("MapScreen", "StagePresentation", loaded));
        }, "differs at arcadeSkins");
        Refuses("presentation loaded from elsewhere", c => c["nativeSceneRequests"] = new JsonArray(Load("Title Screen", "StagePresentation", c["selection"]!.AsObject())), "not the Arcade map");
        Refuses("a later load away from the stage", c =>
            c["nativeSceneRequests"] = new JsonArray(Load("MapScreen", "StagePresentation", c["selection"]!.AsObject()), Load("StagePresentation", "MapScreen", c["selection"]!.AsObject())), "goes to MapScreen");
        Refuses("no presentation before the stage", c => c["observedScenes"] = Scenes("SplashScreen", "Title Screen", "MapScreen", "Kenya SS1"), "through the presentation");
        Refuses("not from the Arcade map", c => c["observedScenes"] = Scenes("SplashScreen", "Title Screen", "StagePresentation", "Kenya SS1"), "from the Arcade map");
        Refuses("route ends elsewhere", c => c["observedScenes"] = Scenes("SplashScreen", "Title Screen", "MapScreen", "StagePresentation", "Kenya SS1", "StagePresentation"), "does not end at the stage");
        Refuses("truncated scenes", c => c["observedScenesTruncated"] = true, "observedScenes is truncated");
        Refuses("truncated loads", c => c["nativeSceneRequestsTruncated"] = true, "nativeSceneRequests is truncated");
        Refuses("a full legacy list may have been cut", c => c["observedScenes"] = Scenes(Enumerable.Repeat("MapScreen", 61).Concat(new[] { "MapScreen", "StagePresentation", "Kenya SS1" }).ToArray()), "truncated");
        Refuses("a later round", c => { c["arcadeRound"] = 1; c["selection"]!["arcadeRound"] = 1; }, "first round");
        Refuses("rivals", c => c["raceRivals"] = 3, "rivals");
        Refuses("ghost", c => c["ghostMode"] = true, "rivals, ghost");
        Refuses("another mode", c => c["gameMode"] = "Championship", "not an Arcade stage");
        Refuses("two players", c => c["playerCount"] = 2, "not single-player");
        Refuses("car differs from the selection", c => c["carId"] = 9, "differs from the driven car");
        Refuses("demo", c => c["selection"]!["demo"] = true, "invalid single-player selection");
        // Absent native-load evidence: the startup's own shapes are still enforced (sana 71540).
        Refuses("three skins", c => c["selection"]!["arcadeSkins"] = new JsonArray(0, 0, 0), "invalid arcadeSkins");
        Refuses("skin out of range", c => c["selection"]!["arcadeSkins"] = new JsonArray(600, 0, 0, 0), "invalid arcadeSkins");
        Refuses("fractional camera", c => c["selection"]!["playerCamera"] = new JsonArray(1.5, 1, 1, 1), "invalid playerCamera");
        Refuses("missing camera", c => c["selection"]!.AsObject().Remove("playerCamera"), "missing playerCamera");
        Refuses("transmission as numbers", c => c["selection"]!["arcadeAutoTransmission"] = new JsonArray(1, 1, 1, 1), "invalid arcadeAutoTransmission");
        Refuses("five cars", c => c["selection"]!["arcadeCars"] = new JsonArray(8, 0, 0, 0, 0), "invalid arcadeCars");
        Refuses("negative car", c => { c["carId"] = -1; c["selection"]!["arcadeCars"] = new JsonArray(-1, 0, 0, 0); }, "invalid arcadeCars");
        Refuses("negative route", c => { c["arcadeRoute"] = -1; c["selection"]!["arcadeRoute"] = -1; }, "negative route or car");
        Refuses("missing competition", c => c.Remove("competitionAsset"), "missing competitionAsset");
        Refuses("text hour", c => c["roundHour"] = "19", "missing number roundHour");
        Refuses("missing damage rule", c => c.Remove("damage"), "missing damage");
        Refuses("unknown schema", c => c["schema"] = "woden.stage-context@2", "unknown stage context schema");
        Refuses("missing selection", c => c.Remove("selection"), "missing selection");
    }

    public static void Admission(Action<bool, string> check)
    {
        using var f = new FullSessionChecks.Fixture();
        Original(f);
        string original = f.Root, derived = f.Root + ".admitted";
        var before = Hashes(original);
        var admitted = FullSessionAdmission.Admit(original, derived, FullSessionChecks.Hash);
        check(admitted.Admitted && admitted.Output == Path.GetFullPath(derived), "an S1 original is admitted: " + admitted.Reason);
        check(Same(before, Hashes(original)), "the original package stays byte-identical");
        check(!Directory.EnumerateDirectories(Path.GetDirectoryName(derived)!, "*.partial-*").Any(), "no partial output remains");

        var names = FullSessionPackageNames.FromSignal("source.jsonl");
        ArtifactSeal.Verify(derived, FullSessionAdmission.DerivedSealFiles(names));
        check(File.ReadAllLines(Path.Combine(derived, "complete.tsv")).Length == FullSessionAdmission.DerivedSealFiles(names).Length + 1, "exact derived seal inventory");
        foreach (string file in new[] { names.Signals, names.Observation, names.Timeline, "source.stage/complete.tsv", "source.stage/trajectory.tsv", "source.stage/stage-context.json" })
            check(ArtifactSeal.Hash(Path.Combine(original, file)) == ArtifactSeal.Hash(Path.Combine(derived, file)), "payload copied byte-identical: " + file);
        TrajectoryTape.Read(Path.Combine(derived, names.Stage));

        var descriptor = JsonNode.Parse(File.ReadAllText(Path.Combine(derived, "session.json")))!.AsObject();
        var source = JsonNode.Parse(File.ReadAllText(Path.Combine(original, "session.json")))!.AsObject();
        check((string?)descriptor["playbackAdapter"] == FullSessionAdmission.Adapter && (string?)descriptor["admissionEvidence"] == "route-compatible" &&
            descriptor["playbackUnavailableReason"] == null && (string?)descriptor["admission"] == "admission.json", "derived descriptor names the adapter and its evidence level");
        check((string?)descriptor["derivedFrom"]!["sessionSha256"] == ArtifactSeal.Hash(Path.Combine(original, "session.json")) &&
            (string?)descriptor["derivedFrom"]!["packageSealSha256"] == ArtifactSeal.Hash(Path.Combine(original, "complete.tsv")), "derived pins name the original");
        foreach (string field in new[] { "schema", "game", "gameBuildSha256", "recordedUtc", "origin", "outcome", "car", "biome", "signals", "observation", "timeline", "stage" })
            check(descriptor[field]?.ToJsonString() == source[field]?.ToJsonString(), "original identity kept: " + field);
        var admission = JsonNode.Parse(File.ReadAllText(Path.Combine(derived, "admission.json")))!.AsObject();
        check((string?)admission["evidence"] == "route-compatible" && (string?)admission["coldReplay"] == "not-qualified" &&
            ((string?)admission["statement"])!.Contains("does not replay every menu input", StringComparison.Ordinal), "route-compatible, not cold-replay-qualified; restores the choice only");
        check((string?)admission["loadEvidence"] == "absent" && (int?)admission["poses"] == 30 && (int?)admission["pauses"] == 0 &&
            (string?)admission["source"]!["stageSealSha256"] == ArtifactSeal.Hash(Path.Combine(original, "source.stage", "complete.tsv")), "admission records the proof and every source hash");
        using (var context = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(derived, "source.stage", "stage-context.json"))))
            check(ArcadeRouteProof.Check(context.RootElement).Admitted, "StageStartup's gate admits the derived stage");

        check(Refused(FullSessionAdmission.Admit(original, derived, FullSessionChecks.Hash), "already exists"), "never overwrites a derived package");
        check(Refused(FullSessionAdmission.Admit(derived, derived + "2", FullSessionChecks.Hash), "only an S1 original"), "a derived package is not re-admitted");
        check(Refused(FullSessionAdmission.Admit(original, Path.Combine(original, "inside"), FullSessionChecks.Hash), "inside the original"), "never writes inside the original");
        check(Refused(FullSessionAdmission.Admit(original, derived + "-other-build", new string('0', 64)), "for this build"), "another game build is refused");

        // Tampering and markers, on copies; the original is never changed.
        string copy = f.Root + ".tampered";
        Copy(original, copy);
        File.AppendAllText(Path.Combine(copy, "source.stage-session.json"), " ");
        check(Refused(FullSessionAdmission.Admit(copy, copy + ".admitted", FullSessionChecks.Hash), "changed"), "a changed payload fails the outer seal");
        string marked = f.Root + ".marked";
        Copy(original, marked);
        File.WriteAllText(Path.Combine(marked, OrdinaryFullSession.IncompleteFile), "fixture\n");
        check(Refused(FullSessionAdmission.Admit(marked, marked + ".admitted", FullSessionChecks.Hash), "marked incomplete"), "an incomplete original is refused");
        check(Same(before, Hashes(original)), "refusals leave the original byte-identical");
        check(!Directory.Exists(copy + ".admitted") && !Directory.Exists(marked + ".admitted"), "refusals write no derived package");

        // Rita F1: the copies are exactly the bytes whose hashes the admission records.
        foreach (var (file, key) in new[] { (names.Signals, "signalsSha256"), (names.Observation, "observationSha256"), (names.Timeline, "timelineSha256"), ("source.stage/complete.tsv", "stageSealSha256") })
            check(ArtifactSeal.Hash(Path.Combine(derived, file)) == (string?)admission["source"]![key], "derived " + file + " is the verified source byte for byte");
        // Rita F2: the nested stage inventory is exact; files beside the package (the companion's case files) are not copied.
        string stray = f.Root + ".stray";
        Copy(original, stray);
        File.WriteAllText(Path.Combine(stray, "source.stage", "notes.txt"), "unsealed\n");
        check(Refused(FullSessionAdmission.Admit(stray, stray + ".admitted", FullSessionChecks.Hash), "unexpected file in the stage: notes.txt"), "an unsealed stage file is refused");
        string beside = f.Root + ".beside";
        Copy(original, beside);
        File.WriteAllText(Path.Combine(beside, "case.json"), "{\"companion\":true}\n");
        var besideOutcome = FullSessionAdmission.Admit(beside, beside + ".admitted", FullSessionChecks.Hash);
        check(besideOutcome.Admitted && !File.Exists(Path.Combine(beside + ".admitted", "case.json")) &&
            Directory.GetFiles(Path.Combine(beside + ".admitted", "source.stage")).Length == FullSessionAdmission.StageFiles.Length,
            "only the named payloads and the exact stage files are copied: " + besideOutcome.Reason);
        // Rita F3: duplicate keys or non-JSON are refusals, never an unhandled exception.
        string duplicate = f.Root + ".duplicate";
        Copy(original, duplicate);
        File.WriteAllText(Path.Combine(duplicate, "session.json"), "{\"schema\":\"woden.full-session-package@1\",\"schema\":\"other\"}");
        check(!FullSessionAdmission.Admit(duplicate, duplicate + ".admitted", FullSessionChecks.Hash).Admitted, "duplicate descriptor keys are refused");
        File.WriteAllText(Path.Combine(duplicate, "session.json"), "not json");
        check(!FullSessionAdmission.Admit(duplicate, duplicate + ".admitted2", FullSessionChecks.Hash).Admitted, "a non-JSON descriptor is refused");
    }

    public static void Scope(Action<bool, string> check)
    {
        void Refuses(string label, Action<Dictionary<string, object?>> edit, string reason)
        {
            using var f = new FullSessionChecks.Fixture();
            Original(f, edit);
            var outcome = FullSessionAdmission.Admit(f.Root, f.Root + ".admitted", FullSessionChecks.Hash);
            check(Refused(outcome, reason) && !Directory.Exists(f.Root + ".admitted"), label + ": " + outcome.Reason);
        }
        Refuses("rivals", c => c["raceRivals"] = 2, "rivals");
        Refuses("second round", c => { c["arcadeRound"] = 1; }, "first round");
        Refuses("not Arcade", c => c["gameMode"] = "Championship", "not an Arcade stage");
        Refuses("route without the presentation", c => c["observedScenes"] = new[] { "SplashScreen", "Title Screen", "MapScreen", "Kenya SS1" }, "presentation");
        Refuses("context of another build", c => c["gameAssemblySha256"] = new string('1', 64), "different build");
    }

    static void Original(FullSessionChecks.Fixture f, Action<Dictionary<string, object?>>? context = null)
    {
        f.Menus(); f.Open(context: context);
        for (int i = 0; i < 30; i++) f.Tick();
        f.Session.Seal("finished", true, f.Now);
        f.CloseOrdinary(); f.Session.Stop(f.Now += .1); f.Session.Package();
        if (f.Session.Outcome != "package") throw new Exception("fixture original not packaged: " + File.ReadAllText(Path.Combine(f.Root, OrdinaryFullSession.IncompleteFile)));
    }
    static bool Refused(FullSessionAdmission.Outcome outcome, string reason) => !outcome.Admitted && outcome.Output == null && outcome.Reason.Contains(reason, StringComparison.Ordinal);
    static Dictionary<string, string> Hashes(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(p => Path.GetRelativePath(root, p), ArtifactSeal.Hash);
    static bool Same(Dictionary<string, string> a, Dictionary<string, string> b) => a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var h) && h == p.Value);
    static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string file in Directory.GetFiles(from)) File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        foreach (string directory in Directory.GetDirectories(from)) Copy(directory, Path.Combine(to, Path.GetFileName(directory)));
    }
}
