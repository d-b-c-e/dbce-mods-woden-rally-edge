using BepInEx;
using BepInEx.Logging;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Read-only, one-launch, private capture of the stage's own route and progress for the toolkit course seam
// (toolkit docs/COURSE-TELEMETRY.md, "Woden route extraction handoff"). Armed only by a claimed request file;
// otherwise every call returns at the first check. It never writes game state, never skips game code and
// never throws into the sampler, so force, telemetry and recording behave exactly as without it.
internal static class CourseRouteDump
{
    private static CourseRouteCapture? _capture;
    private static ManualLogSource? _log;
    private static StageProgress? _stage;
    private static int _finished;

    internal static string Status => _capture == null ? "off" : $"{_capture.State} rows={_capture.Rows} skipped={_capture.SkippedRows}";

    internal static void Initialize(ManualLogSource log)
    {
        _log = log;
        try
        {
            var request = CourseRouteRequest.Consume(Path.Combine(Paths.ConfigPath, CourseRouteRequest.FileName), DateTimeOffset.UtcNow);
            if (request == null) return;
            _capture = new CourseRouteCapture(request.Id, request.MaxSeconds);
            log.LogInfo($"Course route capture {request.Id:N} armed for one stage: read-only, private, at most {request.MaxSeconds} s");
        }
        catch (Exception ex) { log.LogError("Course route request rejected; no capture this launch: " + ex.Message); }
    }

    // Called from the MainCar.FixedUpdate postfix for the selected local car, after the sampler.
    internal static void Observe(MainCar car, TelemetrySample sample)
    {
        var capture = _capture;
        if (capture == null || capture.Terminal) return;
        try
        {
            var status = car.Status;
            if (capture.State == CourseRouteState.WaitingForRoute)
            {
                if (status != MainCar.CarStatus.RACE) return;
                capture.SetRoute(ReadRoute(car));
                if (capture.Terminal) { Finish(capture, synchronous: false); return; }
            }
            var tracker = car.field_Private_WaypointProgressTracker_0;
            var position = car.transform.position;
            float? percentage = _stage != null ? _stage.PlayerPercentage : null;
            capture.AddSample(Runtime.LocalTicks, sample.SimulationSeconds,
                new System.Numerics.Vector3(position.x, position.y, position.z),
                tracker != null ? tracker.progressDistance : float.NaN, percentage, status.ToString());
            if (status is MainCar.CarStatus.END or MainCar.CarStatus.DESTROYED) capture.Complete("car " + status);
            if (capture.Terminal) Finish(capture, synchronous: false);
        }
        catch (Exception ex)
        {
            capture.Fault(ex.GetType().Name + ": " + ex.Message);
            Finish(capture, synchronous: false);
        }
    }

    // Plugin shutdown, after the activity gate has drained: no Observe can run concurrently.
    internal static void Stop()
    {
        var capture = _capture;
        if (capture == null || capture.Terminal) return;
        capture.Complete("plugin stopped");
        Finish(capture, synchronous: true);
    }

    private static CourseRouteFacts ReadRoute(MainCar car)
    {
        var tracker = car.field_Private_WaypointProgressTracker_0 ?? throw new InvalidOperationException("car has no progress tracker");
        var circuit = tracker.circuit ?? throw new InvalidOperationException("progress tracker has no circuit");
        var items = circuit.waypointList?.items ?? throw new InvalidOperationException("circuit has no waypoint list");
        // Read at most one point past the limit so an oversized route is refused, not truncated or read unbounded.
        var points = new List<System.Numerics.Vector3>(Math.Min(items.Length, CourseRouteCapture.MaxWaypoints + 1));
        for (int i = 0; i < items.Length && i <= CourseRouteCapture.MaxWaypoints; i++)
        {
            var point = items[i] ?? throw new InvalidOperationException("waypoint " + i + " is missing");
            var p = point.position;
            points.Add(new System.Numerics.Vector3(p.x, p.y, p.z));
        }
        var distances = new List<float>();
        var cumulative = circuit.distances;
        if (cumulative != null)
            for (int i = 0; i < cumulative.Length && i <= CourseRouteCapture.MaxWaypoints + 1; i++) distances.Add(cumulative[i]);

        _stage = UnityEngine.Object.FindObjectOfType<StageProgress>();
        var stageCircuit = _stage != null ? _stage.MyCircuit : null;
        bool? same = stageCircuit == null ? null : stageCircuit.Pointer == circuit.Pointer;
        var track = car.field_Private_RaceConditions_0?.MyGameround?.Track;
        return new CourseRouteFacts(Runtime.GameAssemblyHash, track?.name ?? "", track?.CircuitName,
            RaceConditions.ReverseTrack, RaceConditions.TotalLaps, circuit.isClosedCircuit, circuit.length, circuit.Kms,
            circuit.unitConversionFactor, points, distances, same);
    }

    private static void Finish(CourseRouteCapture capture, bool synchronous)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0) return;
        if (capture.State is not (CourseRouteState.Completed or CourseRouteState.LimitReached))
        {
            _log?.LogWarning($"Course route capture {capture.RequestId:N} ended {capture.State}: {capture.Reason}; nothing written");
            return;
        }
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dbce", "CourseExtraction");
        void Write()
        {
            try
            {
                string folder = capture.Write(root, DateTimeOffset.UtcNow);
                _log?.LogInfo($"Course route capture {capture.RequestId:N} {capture.State} ({capture.Reason}): {capture.Rows} rows, " +
                              $"{capture.SkippedRows} skipped, {capture.Route?.Waypoints.Count} waypoints -> {folder}");
            }
            catch (Exception ex) { _log?.LogError($"Course route capture {capture.RequestId:N} not written: {ex.Message}"); }
        }
        // The capture is terminal, so nothing mutates it while the file work leaves the physics thread.
        if (synchronous) Write(); else Task.Run(Write);
    }
}
