using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WodenRallyEdge.Core;

/// <summary>
/// One-launch request for a private course-route capture. It exists only when an operator creates it; like
/// <see cref="DiagnosticLaunch"/> it is claimed before reading, so a stale or crashed request cannot repeat.
/// </summary>
public sealed record CourseRouteRequest(int Version, Guid Id, DateTimeOffset ExpiresUtc, int MaxSeconds = CourseRouteCapture.MaxSecondsLimit)
{
    public const int CurrentVersion = 1;
    public const string FileName = "woden-course-route-next-launch.json";

    public static CourseRouteRequest? Consume(string path, DateTimeOffset now)
    {
        if (!File.Exists(path)) return null;
        string claimed = path + ".consumed-" + Guid.NewGuid().ToString("N");
        File.Move(path, claimed);
        var request = JsonSerializer.Deserialize<CourseRouteRequest>(File.ReadAllText(claimed));
        if (request == null || request.Version != CurrentVersion || request.Id == Guid.Empty ||
            request.ExpiresUtc <= now || request.ExpiresUtc > now.AddMinutes(20) ||
            request.MaxSeconds is < 10 or > CourseRouteCapture.MaxSecondsLimit)
            throw new IOException("Invalid or expired course route request; prepare a new one.");
        return request;
    }
}

/// <summary>
/// The game's own route for the current stage, read once: identity, the WaypointCircuit's points and facts.
/// Points are Unity world coordinates, unconverted; the toolkit's CourseAxes.FromUnity maps them offline.
/// </summary>
public sealed record CourseRouteFacts(
    string GameAssemblySha256, string TrackAsset, string? TrackName, bool ReverseTrack, int TotalLaps,
    bool IsClosedCircuit, float Length, float Kms, float UnitConversionFactor,
    IReadOnlyList<Vector3> Waypoints, IReadOnlyList<float> Distances, bool? CircuitSameAsStageProgress);

public enum CourseRouteState { WaitingForRoute, Sampling, Completed, LimitReached, Refused, Faulted }

/// <summary>
/// Bounded, read-only capture of one stage's route and the game's own progress, written once to a private
/// folder as <c>route.json</c>, <c>progress.tsv</c> and a <c>complete.tsv</c> seal.
/// </summary>
/// <remarks>
/// <para>
/// Woden-specific capture only. Course identity, axes, projection and the SimHub layout are toolkit code
/// (Dbce.Wheel.Telemetry Course types); they are applied offline to these files, so nothing here duplicates
/// them and the installed telemetry output is untouched.
/// </para>
/// <para>
/// Limits: at most <see cref="MaxWaypoints"/> points (more is refused, not truncated), <see cref="MaxRows"/>
/// rows and the request's seconds (then <see cref="CourseRouteState.LimitReached"/>), <see cref="MaxBytes"/> on
/// disk. Files are written to a temporary folder and renamed into place, so a failure leaves nothing partial,
/// and an existing capture is never overwritten.
/// </para>
/// </remarks>
public sealed class CourseRouteCapture
{
    public const string Schema = "dbce.course-route@1";
    public const string Game = "Super Woden Rally Edge";
    public const int MaxWaypoints = 20_000;
    public const int MaxRows = 36_000;
    public const int MaxSecondsLimit = 600;
    public const long MaxBytes = 8L * 1024 * 1024;
    public const string ProgressHeader = "tick\tsim_s\tpx\tpy\tpz\tprogress_distance\tplayer_percentage\tstatus";

    private static readonly char[] RowBreaks = { '\t', '\r', '\n' };
    private readonly StringBuilder _rows = new();
    private readonly int _maxSeconds;
    private double _firstSeconds = double.NaN, _lastSeconds = double.NegativeInfinity;
    private bool _written;

    public CourseRouteCapture(Guid requestId, int maxSeconds = MaxSecondsLimit)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("request id required", nameof(requestId));
        if (maxSeconds is < 10 or > MaxSecondsLimit) throw new ArgumentOutOfRangeException(nameof(maxSeconds));
        RequestId = requestId;
        _maxSeconds = maxSeconds;
    }

    public Guid RequestId { get; }
    public CourseRouteState State { get; private set; } = CourseRouteState.WaitingForRoute;
    public string? Reason { get; private set; }
    public CourseRouteFacts? Route { get; private set; }
    public int Rows { get; private set; }
    public int SkippedRows { get; private set; }
    public bool Terminal => State is CourseRouteState.Completed or CourseRouteState.LimitReached
        or CourseRouteState.Refused or CourseRouteState.Faulted;

    /// <summary>Accepts the route once, while waiting for it; anything unusable refuses the whole capture.</summary>
    public void SetRoute(CourseRouteFacts facts)
    {
        if (State != CourseRouteState.WaitingForRoute) return;
        string? problem = Problem(facts);
        if (problem != null) { State = CourseRouteState.Refused; Reason = problem; return; }
        Route = facts;
        State = CourseRouteState.Sampling;
    }

    /// <summary>One physics tick. Ignored unless sampling; unusable rows are counted, never written.</summary>
    public void AddSample(long tick, double simSeconds, Vector3 position, float progressDistance, float? playerPercentage, string status)
    {
        if (State != CourseRouteState.Sampling) return;
        if (!double.IsFinite(simSeconds) || !Finite(position) || !float.IsFinite(progressDistance) ||
            playerPercentage is float p && !float.IsFinite(p) || simSeconds < _lastSeconds || string.IsNullOrEmpty(status) ||
            status.IndexOfAny(RowBreaks) >= 0)
        { SkippedRows++; return; }
        if (double.IsNaN(_firstSeconds)) _firstSeconds = simSeconds;
        if (Rows >= MaxRows || simSeconds - _firstSeconds > _maxSeconds)
        {
            State = CourseRouteState.LimitReached;
            Reason = Rows >= MaxRows ? MaxRows + " rows" : _maxSeconds + " s";
            return;
        }
        _lastSeconds = simSeconds;
        _rows.Append(tick.ToString(CultureInfo.InvariantCulture)).Append('\t')
            .Append(simSeconds.ToString("R", CultureInfo.InvariantCulture)).Append('\t')
            .Append(F(position.X)).Append('\t').Append(F(position.Y)).Append('\t').Append(F(position.Z)).Append('\t')
            .Append(F(progressDistance)).Append('\t')
            .Append(playerPercentage is float pp ? F(pp) : "").Append('\t')
            .Append(status).Append('\n');
        Rows++;
    }

    /// <summary>The stage ended (car finished or destroyed) or the plugin stopped.</summary>
    public void Complete(string reason)
    {
        if (State == CourseRouteState.Sampling) { State = CourseRouteState.Completed; Reason = reason; }
        else if (State == CourseRouteState.WaitingForRoute) { State = CourseRouteState.Refused; Reason = "no route before " + reason; }
    }

    /// <summary>A read failed. The capture is abandoned and nothing is written.</summary>
    public void Fault(string reason)
    {
        if (Terminal) return;
        State = CourseRouteState.Faulted;
        Reason = reason;
    }

    /// <summary>Writes a completed or limit-reached capture once, under <paramref name="root"/>; returns its folder.</summary>
    public string Write(string root, DateTimeOffset utcNow)
    {
        if (_written) throw new InvalidOperationException("capture already written");
        if (State is not (CourseRouteState.Completed or CourseRouteState.LimitReached) || Route == null)
            throw new InvalidOperationException("only a completed or limit-reached capture is written (state " + State + ")");
        _written = true;

        byte[] route = RouteJson(Route, utcNow);
        byte[] progress = Encoding.UTF8.GetBytes(ProgressHeader + "\n" + _rows);
        if (route.LongLength + progress.LongLength > MaxBytes)
            throw new IOException("capture exceeds " + MaxBytes + " bytes; nothing written");

        string name = FolderName(Route, utcNow);
        string final = Path.Combine(root, name);
        if (Directory.Exists(final) || File.Exists(final)) throw new IOException("capture folder already exists: " + name);
        Directory.CreateDirectory(root);
        string partial = Path.Combine(root, "." + name + ".partial-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(partial);
        try
        {
            WriteNew(Path.Combine(partial, "route.json"), route);
            WriteNew(Path.Combine(partial, "progress.tsv"), progress);
            var seal = new StringBuilder("dbce-playback-seal-v1\n");
            seal.Append("progress.tsv\t").Append(progress.Length).Append('\t').Append(Sha256(progress)).Append('\n');
            seal.Append("route.json\t").Append(route.Length).Append('\t').Append(Sha256(route)).Append('\n');
            WriteNew(Path.Combine(partial, "complete.tsv"), Encoding.UTF8.GetBytes(seal.ToString()));
            Directory.Move(partial, final);
        }
        catch
        {
            try { Directory.Delete(partial, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
        return final;
    }

    /// <summary><c>woden-&lt;track&gt;-&lt;fwd|rev&gt;-&lt;yyyyMMddTHHmmssZ&gt;</c>: a folder name, not a course id.</summary>
    public static string FolderName(CourseRouteFacts route, DateTimeOffset utcNow) =>
        "woden-" + PathToken(route.TrackAsset) + "-" + (route.ReverseTrack ? "rev" : "fwd") + "-" +
        utcNow.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    /// <summary>Lowercase ASCII letters and digits; every other run of characters becomes one '-'.</summary>
    public static string PathToken(string text)
    {
        var sb = new StringBuilder();
        bool dash = false;
        foreach (char raw in text)
        {
            char c = char.ToLowerInvariant(raw);
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (dash && sb.Length > 0) sb.Append('-');
                dash = false;
                sb.Append(c);
            }
            else dash = true;
        }
        string token = sb.Length > 40 ? sb.ToString(0, 40).TrimEnd('-') : sb.ToString();
        return token.Length == 0 ? "unnamed" : token;
    }

    private byte[] RouteJson(CourseRouteFacts r, DateTimeOffset utcNow)
    {
        using var memory = new MemoryStream();
        using (var w = new Utf8JsonWriter(memory, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteString("schema", Schema);
            w.WriteString("game", Game);
            w.WriteString("requestId", RequestId.ToString("N"));
            w.WriteString("gameAssemblySha256", r.GameAssemblySha256);
            w.WriteString("trackAsset", r.TrackAsset);
            if (r.TrackName == null) w.WriteNull("trackName"); else w.WriteString("trackName", r.TrackName);
            w.WriteBoolean("reverseTrack", r.ReverseTrack);
            w.WriteNumber("totalLaps", r.TotalLaps);
            w.WriteBoolean("isClosedCircuit", r.IsClosedCircuit);
            w.WriteNumber("length", r.Length);
            w.WriteNumber("kms", r.Kms);
            w.WriteNumber("unitConversionFactor", r.UnitConversionFactor);
            w.WriteString("frame", "unity-world");
            w.WriteString("axes", "x east, y up, z north (toolkit CourseAxes.FromUnity)");
            w.WriteNumber("waypointCount", r.Waypoints.Count);
            w.WriteStartArray("waypoints");
            foreach (var p in r.Waypoints)
            {
                w.WriteStartArray();
                w.WriteNumberValue(p.X); w.WriteNumberValue(p.Y); w.WriteNumberValue(p.Z);
                w.WriteEndArray();
            }
            w.WriteEndArray();
            w.WriteStartArray("distances");
            foreach (float d in r.Distances) w.WriteNumberValue(d);
            w.WriteEndArray();
            if (r.CircuitSameAsStageProgress is bool same) w.WriteBoolean("circuitSameAsStageProgress", same);
            else w.WriteNull("circuitSameAsStageProgress");
            w.WriteString("state", State.ToString());
            w.WriteString("reason", Reason);
            w.WriteNumber("rows", Rows);
            w.WriteNumber("skippedRows", SkippedRows);
            w.WriteString("capturedUtc", utcNow.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            w.WriteEndObject();
        }
        return memory.ToArray();
    }

    private static string? Problem(CourseRouteFacts f)
    {
        if (f.GameAssemblySha256 is not { Length: 64 } || !f.GameAssemblySha256.All(Uri.IsHexDigit)) return "game assembly hash missing";
        if (string.IsNullOrWhiteSpace(f.TrackAsset) || f.TrackAsset.Length > 128) return "track asset missing";
        if (f.TotalLaps < 0) return "negative lap count";
        if (f.Waypoints == null || f.Waypoints.Count < 2) return "fewer than 2 waypoints";
        if (f.Waypoints.Count > MaxWaypoints) return "more than " + MaxWaypoints + " waypoints";
        for (int i = 0; i < f.Waypoints.Count; i++) if (!Finite(f.Waypoints[i])) return "waypoint " + i + " is not finite";
        if (f.Distances == null || f.Distances.Count > MaxWaypoints + 1 || f.Distances.Any(d => !float.IsFinite(d))) return "distances unusable";
        if (!float.IsFinite(f.Length) || !float.IsFinite(f.Kms) || !float.IsFinite(f.UnitConversionFactor)) return "route facts not finite";
        return null;
    }

    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
    private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void WriteNew(string path, byte[] bytes)
    {
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(bytes);
        output.Flush(true);
    }
}
