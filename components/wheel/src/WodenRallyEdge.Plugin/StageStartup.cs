using System.Text.Json;
using Dbce.Wheel.Playback;
using Dbce.Wheel.Recording;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.SceneManagement;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Game-specific semantic startup. Requests are prepared with Woden closed; each
// native action runs once after its actual scene/controller is ready. No OS input,
// display changes, career progress restoration or arbitrary scene names.
internal static class StageStartup
{
    private static JsonElement _context;
    private static bool _requested, _done, _raceVerified;
    private static string? _failure, _scene, _issuedFor;
    private static double _enteredAt;
    private static readonly List<(int Mode, int Preset, MountedView View)> Cameras = new();
    internal static string? Status { get; private set; }

    internal static void Initialize(string? source)
    {
        if (source == null) return;
        _requested = true;
        try
        {
            var tape = TrajectoryTape.Read(source);
            ArtifactSeal.Verify(source, "stage-context.json", "source.jsonl");
            string path = Path.Combine(source, "stage-context.json");
            if (new FileInfo(path).Length > 65536) throw new IOException("Stage context exceeds limit");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            _context = doc.RootElement.Clone();
            if (Text("schema") != "woden.stage-context@1" || Text("gameAssemblySha256") != Runtime.GameAssemblyHash ||
                Text("gameMode") != "Arcade" || Text("subMode") != "Arcade" || Number("playerIndex") != 0 || Number("playerCount") != 1 ||
                Number("arcadeRound") != 0 || Number("raceRivals") != 0 || _context.GetProperty("rollingStart").GetBoolean() ||
                _context.GetProperty("ghostMode").GetBoolean())
                throw new IOException("Cold startup currently requires a single-player Arcade first stage, without rivals/ghost/rolling start");
            if (Text("scene") != Text("trackName") || tape.Metadata["scenario"] != Text("scene") + "|car=" + Number("carId") ||
                tape.Metadata["gameSha256"] != Runtime.GameAssemblyHash)
                throw new IOException("Stage identity differs between sealed artifacts");
            string[] required = { "SplashScreen", "DailyMessageScreen", "Title Screen", "MapScreen", "StagePresentation", Text("scene") };
            if (!_context.GetProperty("observedScenes").EnumerateArray().Select(x => x.GetString()).SequenceEqual(required))
                throw new IOException("Unsupported recorded scene path");
            var selection = _context.GetProperty("selection");
            if (selection.GetProperty("playerCount").GetInt32() != 1 || selection.GetProperty("demo").GetBoolean() ||
                selection.GetProperty("arcadeCars")[0].GetInt32() != Number("carId")) throw new IOException("Invalid single-player selection");
            foreach (var row in SessionReader.Read(Path.Combine(source, "source.jsonl")))
            {
                if (row.Kind != SessionRecordKind.Sample) continue;
                var c = row.Sample.Channels;
                int Integer(string name, int min, int max)
                {
                    if (!c.TryGetValue(name, out double n) || !double.IsFinite(n) || n < min || n > max || n != Math.Truncate(n))
                        throw new IOException("Unavailable recorded presentation: " + name);
                    return (int)n;
                }
                if (Integer("capture.trajectoryIndex", 0, 216000) != Cameras.Count) throw new IOException("Presentation row alignment differs");
                Cameras.Add((Integer("camera.mode", 0, 1), Integer("camera.stockPreset", 0, 31),
                    (MountedView)Integer("camera.mountedView", 0, 2)));
            }
            if (Cameras.Count != tape.Frames.Length) throw new IOException("Presentation sample count differs");
            Status = "PLAYBACK - starting recorded Arcade stage";
        }
        catch (Exception ex) { _failure = ex.Message; }
    }

    private static string Text(string key) => _context.GetProperty(key).GetString() ?? throw new IOException("Missing " + key);
    private static int Number(string key) => _context.GetProperty(key).GetInt32();
    private static T? Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectOfType<T>();
    internal static void Tick(bool active, bool playing, double now)
    {
        if (!_requested || _done) return;
        if (_failure != null) { _done = true; throw new IOException(_failure); }
        if (!active) { _done = true; return; }
        if (playing) { _done = true; Status = null; return; }
        string scene = SceneManager.GetActiveScene().name;
        if (scene != _scene) { _scene = scene; _enteredAt = now; _issuedFor = null; }
        if (now - _enteredAt > 55) throw new IOException("Startup timed out in " + scene);
        if (now - _enteredAt < 1 || _issuedFor == scene) return;
        if (scene == "SplashScreen" || scene == "LoadingScene" || scene == "Loading Screen") return;
        if (scene == Text("scene")) { Status = "PLAYBACK - waiting for the native countdown"; return; }
        if (Find<MenuCameraScript>() == null || MenuCameraScript.Exiting) return;
        if (scene == "DailyMessageScreen")
        {
            var daily = Find<DailyMessage>();
            if (daily == null || !daily.field_Private_Boolean_0) return;
            daily.field_Private_Boolean_0 = false;
            Load("Title Screen", false);
        }
        else if (scene == "Title Screen")
        {
            var title = Find<TitleScreenScript>();
            if (title == null || title.MyControls == null || title.Starting) return;
            if (title.SceneToLoad != "MapScreen") throw new IOException("Unexpected title destination: " + title.SceneToLoad);
            title.Starting = true;
            Load(title.SceneToLoad, true);
        }
        else if (scene == "MapScreen")
        {
            var progress = Find<Progress>();
            var stats = Progress.StatsData_;
            if (progress?.ArcadeRoutes == null || stats == null) return;
            int route = Number("arcadeRoute"), round = Number("arcadeRound");
            if (route < 0 || route >= progress.ArcadeRoutes.Length) throw new IOException("Recorded Arcade route is absent");
            var competition = progress.ArcadeRoutes[route];
            if (competition == null || competition.name != Text("competitionAsset") || competition.Rounds == null || round >= competition.Rounds.Length)
                throw new IOException("Recorded competition differs");
            ValidateRound(competition.Rounds[round]);
            var selected = _context.GetProperty("selection");
            int[] Ints(string key)
            {
                var values = selected.GetProperty(key).EnumerateArray().Select(x => x.GetInt32()).ToArray();
                if (values.Length != 4 || values.Any(x => x < 0 || x > 512)) throw new IOException("Invalid " + key);
                return values;
            }
            bool[] Bools(string key)
            {
                var values = selected.GetProperty(key).EnumerateArray().Select(x => x.GetBoolean()).ToArray();
                if (values.Length != 4) throw new IOException("Invalid " + key);
                return values;
            }
            // Only transient Arcade choices. Progression, video, audio and tune stay owned by the player.
            var cars = Ints("arcadeCars"); var skins = Ints("arcadeSkins");
            var automatic = Bools("arcadeAutoTransmission"); var cameras = Ints("playerCamera");
            var catalog = progress.CarContainer?.CarIndex;
            if (catalog == null || cars[0] >= catalog.Length || catalog[cars[0]] == null)
                throw new IOException("Recorded Arcade car is absent");
            var carStats = catalog[cars[0]].GetComponent<CarStats>();
            if (carStats?.Skins == null || skins[0] >= carStats.Skins.Length)
                throw new IOException("Recorded Arcade skin is absent");
            stats.Gamemode = StatsData.Mode.Arcade; stats.SubGamemode = StatsData.SubMode.Arcade;
            stats.NrOfPlayers = 1; stats.ArcadeRoute = route; stats.ArcadeRound = round;
            stats.ArcadeCars = new Il2CppStructArray<int>(cars); stats.ArcadeSkins = new Il2CppStructArray<int>(skins);
            stats.Arcade_Auto_Transmission = new Il2CppStructArray<bool>(automatic);
            stats.PlayerCamera = new Il2CppStructArray<int>(cameras);
            GameMaster.Demo = false; GameMaster.NrOfPlayers = 1;
            Load("StagePresentation", true);
        }
        else if (scene == "StagePresentation")
        {
            var presentation = Find<StagePresentation>();
            if (presentation == null || !presentation.field_Private_Boolean_0 || presentation.field_Private_GameRound_0 == null) return;
            if (presentation.field_Private_Boolean_1) throw new IOException("Stage presentation is a results screen");
            ValidateRound(presentation.field_Private_GameRound_0);
            _issuedFor = scene;
            Runtime.Log.LogInfo("Stage startup: native ProceedToStage -> " + Text("scene"));
            presentation.field_Private_Boolean_0 = false;
            presentation.ProceedToStage();
        }
        else throw new IOException("Unexpected startup scene: " + scene);
    }
    private static void Load(string target, bool showLoading)
    {
        _issuedFor = _scene;
        Runtime.Log.LogInfo("Stage startup: native menu load " + _scene + " -> " + target);
        MenuCameraScript.LoadScene(target, showLoading, false);
    }
    private static void ValidateRound(GameRound round)
    {
        if (round?.Track == null || round.Track.name != Text("trackAsset") || round.Track.CircuitName != Text("trackName") ||
            Math.Abs(round.Hour - _context.GetProperty("roundHour").GetSingle()) > .000001 || round.Laps != Number("roundLaps") || round.TimeSpeed != Number("roundTimeSpeed") ||
            round.Weather.ToString() != Text("roundWeather")) throw new IOException("Native stage setup differs from recording");
    }
    internal static bool ValidateRace(MainCar car)
    {
        if (!_requested || _raceVerified) return true;
        if (_failure != null) return false;
        var race = car.field_Private_RaceConditions_0;
        if (SceneManager.GetActiveScene().name != Text("scene") || car.CarId != Number("carId")) return false;
        if (race == null || race.MyGameround == null) return false;
        ValidateRound(race.MyGameround);
        if (race.MyCompetition?.name != Text("competitionAsset") || race.PlayerCarList?.Count != 1 || GameMaster.Demo ||
            Progress.StatsData_?.Gamemode != StatsData.Mode.Arcade || RaceConditions.TotalRivals != Number("raceRivals") ||
            RaceConditions.TotalLaps != Number("raceLaps") || RaceConditions.ReverseTrack != _context.GetProperty("reverseTrack").GetBoolean() ||
            RaceConditions.Damage != _context.GetProperty("damage").GetBoolean() || RaceConditions.Rollingstart || RaceConditions.GhostMode ||
            Math.Abs(RaceConditions.FuelConsumptionScale - _context.GetProperty("fuelConsumptionScale").GetSingle()) > .000001)
            throw new IOException("Native race rules differ from recording");
        _raceVerified = true;
        Runtime.Log.LogInfo("Stage startup: actual single-player car, stage and rules verified");
        return true;
    }
    internal static void ApplyCamera(Car_Cam camera, int row)
    {
        if (!_requested || row < 0 || row >= Cameras.Count) return;
        var desired = Cameras[row];
        MountedCamera.RecordedView(camera, desired.Mode, desired.Preset, desired.View);
    }
}
