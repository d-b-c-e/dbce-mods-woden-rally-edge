using System.Text.Json;

namespace WodenRallyEdge;

// What a sealed stage context must show before native-arcade-v1 restores its recorded stage choice: single-player
// Arcade, round 0, and an observed route ending at the Arcade map, the stage presentation and the stage. It proves the
// restored choice is the recorded one, not that every menu input is replayed; scenes before the map are evidence only.
// A native load into the presentation, where observed, must come from the map with the same restored fields. Older
// contexts observed no native loads; that is reported as absent load evidence. Engine-free for fixtures and tools.
internal static class ArcadeRouteProof
{
    internal sealed record Result(bool Admitted, string Reason, string LoadEvidence, string[] Route);
    /// <summary>The selection fields native-arcade-v1 writes at the map screen (StageStartup.NativeStep).</summary>
    internal static readonly string[] RestoredFields = { "gameMode", "subMode", "playerCount", "arcadeRoute", "arcadeRound",
        "arcadeCars", "arcadeSkins", "arcadeAutoTransmission", "playerCamera", "demo" };
    private static readonly HashSet<string> LoadingScenes = new(StringComparer.Ordinal) { "LoadingScene", "Loading Screen" };
    private const int ListLimit = 64; // StageCaptureContext's own bound

    internal static Result Check(JsonElement context)
    {
        try { return Admit(context); }
        catch (Exception ex) when (ex is InvalidDataException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { return new Result(false, ex.Message, "unknown", Array.Empty<string>()); }
    }

    private static Result Admit(JsonElement c)
    {
        if (Text(c, "schema") != "woden.stage-context@1") throw new InvalidDataException("unknown stage context schema");
        string scene = Text(c, "scene");
        if (scene != Text(c, "trackName")) throw new InvalidDataException("scene and track differ");
        if (Text(c, "gameMode") != "Arcade" || Text(c, "subMode") != "Arcade") throw new InvalidDataException("not an Arcade stage");
        if (Int(c, "playerIndex") != 0 || Int(c, "playerCount") != 1) throw new InvalidDataException("not single-player");
        if (Int(c, "arcadeRound") != 0) throw new InvalidDataException("not the first round of an Arcade route");
        if (Int(c, "raceRivals") != 0 || Bool(c, "rollingStart") || Bool(c, "ghostMode")) throw new InvalidDataException("rivals, ghost or rolling start");
        var selection = Get(c, "selection");
        if (Int(selection, "playerCount") != 1 || Bool(selection, "demo")) throw new InvalidDataException("invalid single-player selection");
        if (Text(selection, "gameMode") != "Arcade" || Text(selection, "subMode") != "Arcade" ||
            Int(selection, "arcadeRoute") != Int(c, "arcadeRoute") || Int(selection, "arcadeRound") != 0)
            throw new InvalidDataException("selection differs from the stage's mode, route or round");
        // The startup's own shapes (StageStartup MapScreen step): four entries; ints 0..512; transmission booleans.
        foreach (string key in new[] { "arcadeCars", "arcadeSkins", "playerCamera" }) Ints(selection, key);
        Bools(selection, "arcadeAutoTransmission");
        if (Int(c, "arcadeRoute") < 0 || Int(c, "carId") < 0) throw new InvalidDataException("negative route or car");
        if (Get(selection, "arcadeCars")[0].GetInt32() != Int(c, "carId"))
            throw new InvalidDataException("selected Arcade car differs from the driven car");
        // Every other field the startup and its round/race checks read must be present with its type.
        foreach (string key in new[] { "competitionAsset", "trackAsset", "roundWeather" }) Text(c, key);
        foreach (string key in new[] { "roundLaps", "roundTimeSpeed", "raceLaps" }) Int(c, key);
        foreach (string key in new[] { "roundHour", "fuelConsumptionScale" }) Number(c, key);
        foreach (string key in new[] { "reverseTrack", "damage" }) Bool(c, key);

        var scenes = List(c, "observedScenes").Select(x => x.GetString() ?? throw new InvalidDataException("unnamed scene")).ToArray();
        var requests = List(c, "nativeSceneRequests");
        // Walk back from the stage: (loading)* presentation (loading)* map.
        int i = scenes.Length - 1;
        if (i < 0 || scenes[i] != scene) throw new InvalidDataException("observed route does not end at the stage");
        i--; while (i >= 0 && LoadingScenes.Contains(scenes[i])) i--;
        if (i < 0 || scenes[i] != "StagePresentation") throw new InvalidDataException("observed route does not reach the stage through the presentation");
        i--; while (i >= 0 && LoadingScenes.Contains(scenes[i])) i--;
        if (i < 0 || scenes[i] != "MapScreen") throw new InvalidDataException("observed route does not start the stage from the Arcade map");
        var route = scenes[i..];

        string evidence = "absent";
        int last = -1;
        for (int r = 0; r < requests.Length; r++) if (Text(requests[r], "sceneTo") == "StagePresentation") last = r;
        if (last >= 0)
        {
            var load = requests[last];
            if (Text(load, "sceneFrom") != "MapScreen") throw new InvalidDataException("the presentation was loaded from " + Text(load, "sceneFrom") + ", not the Arcade map");
            var loaded = Get(load, "selection");
            foreach (string field in RestoredFields)
                if (!Same(Get(loaded, field), Get(selection, field))) throw new InvalidDataException("the map's selection differs at " + field);
            for (int r = last + 1; r < requests.Length; r++)
            {
                string to = Text(requests[r], "sceneTo");
                if (to != scene && !LoadingScenes.Contains(to)) throw new InvalidDataException("a native load after the presentation goes to " + to);
            }
            evidence = "matched";
        }
        return new Result(true, "", evidence, route);
    }

    private static JsonElement[] List(JsonElement c, string name)
    {
        var list = Get(c, name);
        if (list.ValueKind != JsonValueKind.Array) throw new InvalidDataException(name + " is not a list");
        bool truncated = c.TryGetProperty(name + "Truncated", out var flag)
            ? flag.ValueKind != JsonValueKind.False // anything but an explicit false fails closed
            : list.GetArrayLength() >= ListLimit; // contexts before the flag: a full list may have been cut
        if (truncated) throw new InvalidDataException(name + " is truncated");
        return list.EnumerateArray().ToArray();
    }
    private static JsonElement Get(JsonElement c, string name) =>
        c.ValueKind == JsonValueKind.Object && c.TryGetProperty(name, out var value) ? value : throw new InvalidDataException("missing " + name);
    private static string Text(JsonElement c, string name) =>
        Get(c, name) is { ValueKind: JsonValueKind.String } value ? value.GetString()! : throw new InvalidDataException("missing text " + name);
    private static int Int(JsonElement c, string name) =>
        Get(c, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out int n) ? n : throw new InvalidDataException("missing integer " + name);
    private static double Number(JsonElement c, string name) =>
        Get(c, name) is { ValueKind: JsonValueKind.Number } value && double.IsFinite(value.GetDouble()) ? value.GetDouble() : throw new InvalidDataException("missing number " + name);
    private static void Ints(JsonElement c, string name)
    {
        var a = Get(c, name);
        if (a.ValueKind != JsonValueKind.Array || a.GetArrayLength() != 4 ||
            a.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.Number || !x.TryGetInt32(out int n) || n < 0 || n > 512))
            throw new InvalidDataException("invalid " + name);
    }
    private static void Bools(JsonElement c, string name)
    {
        var a = Get(c, name);
        if (a.ValueKind != JsonValueKind.Array || a.GetArrayLength() != 4 || a.EnumerateArray().Any(x => x.ValueKind is not (JsonValueKind.True or JsonValueKind.False)))
            throw new InvalidDataException("invalid " + name);
    }
    private static bool Bool(JsonElement c, string name) => Get(c, name).ValueKind switch
    {
        JsonValueKind.True => true, JsonValueKind.False => false, _ => throw new InvalidDataException("missing boolean " + name),
    };
    /// <summary>Value equality, independent of formatting and property order.</summary>
    internal static bool Same(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind) return false;
        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
                var left = a.EnumerateObject().ToArray(); var right = b.EnumerateObject().ToArray();
                return left.Length == right.Length && left.All(p => b.TryGetProperty(p.Name, out var q) && Same(p.Value, q));
            case JsonValueKind.Array:
                return a.GetArrayLength() == b.GetArrayLength() && a.EnumerateArray().Zip(b.EnumerateArray()).All(p => Same(p.First, p.Second));
            case JsonValueKind.Number: return a.GetDecimal() == b.GetDecimal();
            case JsonValueKind.String: return a.GetString() == b.GetString();
            default: return true; // true, false, null
        }
    }
}
