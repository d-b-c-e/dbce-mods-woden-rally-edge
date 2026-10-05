using System.Text.Json;
using UnityEngine.SceneManagement;

namespace WodenRallyEdge;

// Read-only evidence for a future native startup adapter. This is not permission
// to replay arbitrary static fields or overwrite the owner's save/presentation.
internal static class StageCaptureContext
{
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
        };
        File.WriteAllText(Path.Combine(directory, "stage-context.json"),
            JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = true }));
    }
}
