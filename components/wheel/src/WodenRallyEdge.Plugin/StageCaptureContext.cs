using System.Text.Json;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WodenRallyEdge;

// Read-only evidence for a future native startup adapter. This is not permission
// to replay arbitrary static fields or overwrite the owner's save/presentation.
internal static class StageCaptureContext
{
    private static readonly List<object> SceneRequests = new();
    private static readonly List<string> Scenes = new();
    private static string? _scene;
    internal static void ObserveScene()
    {
        if (!StagePlayback.OutputMuted) return;
        string scene = SceneManager.GetActiveScene().name;
        if (scene == _scene) return;
        _scene = scene;
        if (Scenes.Count < 64) Scenes.Add(scene);
        Runtime.Log.LogInfo("Stage scene: " + scene);
    }
    internal static void ObserveLoad(LoadScene loader, string scene)
    {
        if (!StagePlayback.OutputMuted || StagePlayback.Playing || SceneRequests.Count >= 64) return;
        try
        {
            SceneRequests.Add(new { sceneFrom = SceneManager.GetActiveScene().name, sceneTo = scene,
                loaderObject = loader.gameObject.name, overrideScene = loader.OverrideScene,
                selection = ReadSelection(), wallSeconds = Runtime.Clock.Elapsed.TotalSeconds });
            Runtime.Log.LogInfo("Stage native load: " + SceneManager.GetActiveScene().name + " -> " + scene);
        }
        catch (Exception ex) { Runtime.Log.LogWarning("Stage load context unavailable: " + ex.Message); }
    }
    internal static Dictionary<string, object?> ReadSelection()
    {
        var stats = Progress.StatsData_;
        return new Dictionary<string, object?> {
            ["gameMode"] = stats?.Gamemode.ToString(), ["subMode"] = stats?.SubGamemode.ToString(),
            ["playerCount"] = stats?.NrOfPlayers, ["arcadeRoute"] = stats?.ArcadeRoute,
            ["arcadeRound"] = stats?.ArcadeRound, ["arcadeCars"] = stats?.ArcadeCars?.ToArray(),
            ["arcadeSkins"] = stats?.ArcadeSkins?.ToArray(), ["arcadeAutoTransmission"] = stats?.Arcade_Auto_Transmission?.ToArray(),
            ["autoTransmission"] = stats?.Auto_Transmission?.ToArray(), ["playerCamera"] = stats?.PlayerCamera?.ToArray(),
            ["competitionIndex"] = stats?.RE_CompetitionIndex, ["competitionRound"] = stats?.RE_CompetitionRound,
            ["competingCarSlotId"] = stats?.CompetingCarSlotID, ["difficulty"] = stats?.Difficulty,
            ["gameMasterPlayerCount"] = GameMaster.NrOfPlayers, ["gameMasterCars"] = GameMaster.PlayerCar?.ToArray(),
            ["gameMasterSkins"] = GameMaster.PlayerSkin?.ToArray(), ["demo"] = GameMaster.Demo,
        };
    }
    internal static void Write(string directory, MainCar car)
    {
        var race = car.field_Private_RaceConditions_0
            ?? throw new InvalidOperationException("Cannot capture the stage's race context");
        var stats = Progress.StatsData_
            ?? throw new InvalidOperationException("Cannot capture the stage's game mode");
        var round = race.MyGameround;
        var context = new Dictionary<string, object?>
        {
            ["schema"] = "woden.stage-context@1",
            ["role"] = "observed launch context; native startup restoration is not yet qualified",
            ["scene"] = SceneManager.GetActiveScene().name,
            ["gameAssemblySha256"] = Runtime.GameAssemblyHash,
            ["carId"] = car.CarId,
            ["playerIndex"] = car.PlayerIndex,
            ["playerCount"] = race.PlayerCarList?.Count,
            ["gameMode"] = stats.Gamemode.ToString(),
            ["subMode"] = stats.SubGamemode.ToString(),
            ["arcadeRoute"] = stats.ArcadeRoute,
            ["arcadeRound"] = stats.ArcadeRound,
            ["competitionIndex"] = stats.RE_CompetitionIndex,
            ["competitionRound"] = stats.RE_CompetitionRound,
            ["competitionAsset"] = race.MyCompetition?.name,
            ["trackAsset"] = round?.Track?.name,
            ["trackName"] = round?.Track?.CircuitName,
            ["roundHour"] = round?.Hour,
            ["roundTimeSpeed"] = round?.TimeSpeed,
            ["roundLaps"] = round?.Laps,
            ["roundWeather"] = round?.Weather.ToString(),
            ["raceLaps"] = RaceConditions.TotalLaps,
            ["raceRivals"] = RaceConditions.TotalRivals,
            ["reverseTrack"] = RaceConditions.ReverseTrack,
            ["damage"] = RaceConditions.Damage,
            ["rollingStart"] = RaceConditions.Rollingstart,
            ["ghostMode"] = RaceConditions.GhostMode,
            ["fuelConsumptionScale"] = RaceConditions.FuelConsumptionScale,
            ["selection"] = ReadSelection(),
            ["observedScenes"] = Scenes.ToArray(),
            ["nativeSceneRequests"] = SceneRequests.ToArray(),
        };
        File.WriteAllText(Path.Combine(directory, "stage-context.json"),
            JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = true }));
    }
}

[HarmonyPatch(typeof(LoadScene), nameof(LoadScene.LoadScene_))]
internal static class StageNativeLoadObserver
{
    private static void Prefix(LoadScene __instance, string __0) => StageCaptureContext.ObserveLoad(__instance, __0);
}
