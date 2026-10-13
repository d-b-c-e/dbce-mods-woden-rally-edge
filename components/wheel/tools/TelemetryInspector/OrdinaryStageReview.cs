using System.Text.Json.Nodes;
using Dbce.Wheel.Playback;
using Dbce.Wheel.Sessions;
using WodenRallyEdge;

// S2b: the review an admitted ordinary package gets before muted cold playback. An owner drive may have had forces on,
// so the capture's own stage-start output flag is accepted when consistent and its per-row delivery counters are truthful.
// The route's structure is required; the force analysis (tune, model, history replay, coverage) is reported as qualified or
// unqualified with its reason, never refusing the route. Raw stage recordings keep the muted-capture stage-review.
// Playback itself is always muted.
internal static class OrdinaryStageReview
{
    internal static object Run(string derived)
    {
        derived = Path.GetFullPath(derived);
        var descriptor = Read(Path.Combine(derived, FullSessionPackageNames.Descriptor));
        if ((string?)descriptor["schema"] != OrdinaryFullSession.PackageSchema || (string?)descriptor["playbackAdapter"] != FullSessionAdmission.Adapter ||
            (string?)descriptor["admissionEvidence"] != "route-compatible" || (string?)descriptor["admission"] != FullSessionAdmission.AdmissionFile ||
            descriptor["derivedFrom"] is not JsonObject from)
            throw new IOException("Not an admitted ordinary package");
        var names = FullSessionPackageNames.FromSignal((string?)descriptor["signals"] ?? "");
        if ((string?)descriptor["observation"] != names.Observation || (string?)descriptor["timeline"] != names.Timeline || (string?)descriptor["stage"] != names.Stage)
            throw new IOException("Derived payload names disagree");
        var sealedFiles = FullSessionAdmission.DerivedSealFiles(names);
        if (File.ReadAllLines(Path.Combine(derived, "complete.tsv")).Length != sealedFiles.Length + 1) throw new IOException("Unexpected derived seal inventory");
        ArtifactSeal.Verify(derived, sealedFiles);
        var admission = Read(Path.Combine(derived, FullSessionAdmission.AdmissionFile));
        var source = admission["source"] as JsonObject ?? throw new IOException("Admission has no source hashes");
        string stage = Path.Combine(derived, names.Stage);
        if ((string?)admission["schema"] != FullSessionAdmission.Schema || (string?)admission["evidence"] != "route-compatible" ||
            (string?)admission["adapter"] != FullSessionAdmission.Adapter ||
            (string?)source["sessionSha256"] != (string?)from["sessionSha256"] || (string?)source["packageSealSha256"] != (string?)from["packageSealSha256"] ||
            (string?)source["stageSealSha256"] != ArtifactSeal.Hash(Path.Combine(stage, "complete.tsv")))
            throw new IOException("Admission differs from the derived descriptor or its stage");
        // Provenance survives later consumers: every copied payload is still the verified source byte for byte (Rita 72210).
        foreach (var (file, key) in new[] { (names.Signals, "signalsSha256"), (names.Observation, "observationSha256"), (names.Timeline, "timelineSha256") })
            if ((string?)source[key] != ArtifactSeal.Hash(Path.Combine(derived, file))) throw new IOException("Derived " + file + " differs from the admitted source");
        string stageSeal = ArtifactSeal.Hash(Path.Combine(stage, "complete.tsv"));
        var review = StageCaptureReview.Run(stage, StageReviewPolicy.OrdinaryOutputRetained);
        return new { schema = "woden.ordinary-stage-review@1", passed = true, package = derived, stage = names.Stage, stageSealSha256 = stageSeal,
            sessionSha256 = ArtifactSeal.Hash(Path.Combine(derived, FullSessionPackageNames.Descriptor)),
            packageSealSha256 = ArtifactSeal.Hash(Path.Combine(derived, "complete.tsv")),
            derivedFrom = new { sessionSha256 = (string?)from["sessionSha256"], packageSealSha256 = (string?)from["packageSealSha256"] },
            admissionEvidence = "route-compatible", coldReplay = "not-qualified", review };
    }

    private static JsonObject Read(string path)
    {
        if (new FileInfo(path).Length > 64 * 1024) throw new IOException("Oversized package document: " + Path.GetFileName(path));
        try { return JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? throw new IOException("Empty package document"); }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or ArgumentException or InvalidOperationException)
        { throw new IOException("Malformed package document " + Path.GetFileName(path) + ": " + ex.Message); }
    }
}
