using System.Runtime.CompilerServices;
using Dbce.Wheel.Recording;

namespace WodenRallyEdge.Core;

/// <summary>
/// An optional destination for the samples <see cref="TelemetryOutput"/> publishes. Developer recording implements it.
/// Player builds have none, so <see cref="TelemetryOutput"/> never touches <c>Dbce.Wheel.Recording</c> and that DLL
/// does not need to be beside the plugin. Nothing here may mention a <c>Dbce.Wheel.Recording</c> type.
/// </summary>
public interface ITelemetryRecorder
{
    /// <summary>The recorder's own status: Recording, Stopping, Completed, LimitReached, ...</summary>
    string Status { get; }
    string? Error { get; }
    long Drops { get; }
    /// <summary>Recording-relative time of the last accepted sample; frozen after stop.</summary>
    double ElapsedSeconds { get; }
    void Offer(TelemetrySample sample);
    void Stop();
    /// <summary>Marks the source failed at this time and stops it.</summary>
    void Fail(double elapsedSeconds, string reason);
}

/// <summary>Developer recording: the published samples, written with the toolkit <see cref="SessionRecorder"/>.</summary>
public sealed class SessionRecordingSink : ITelemetryRecorder
{
    private readonly SessionRecorder _recorder;
    private string? _lastState, _lastDiscontinuity, _lastUnavailable, _lastForceStatus, _lastForceGate, _lastForceModelReason;
    private double? _origin;
    private double _elapsed;

    private SessionRecordingSink(SessionRecorder recorder) => _recorder = recorder;

    /// <summary>The ordinary profile (<see cref="RecordingProfiles.Ordinary"/>). Kept out of line so a caller that never
    /// records never compiles a <c>Dbce.Wheel.Recording</c> reference.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ITelemetryRecorder Open(string path, string sessionId, string recordingSource, string pluginVersion,
        IReadOnlyDictionary<string, string>? recordingProperties) =>
        Open(path, sessionId, recordingSource, pluginVersion, recordingProperties, RecordingProfiles.Ordinary());

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ITelemetryRecorder Open(string path, string sessionId, string recordingSource, string pluginVersion,
        IReadOnlyDictionary<string, string>? recordingProperties, RecordingOptions? recordingOptions)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var properties = new Dictionary<string, string> { ["sessionId"] = sessionId, ["recordingSource"] = recordingSource,
            ["phase"] = "MainCar.FixedUpdate.postfix.prePhysicsSolve", ["schema"] = TelemetrySchema.Name + "/1",
            ["missing"] = "Absent channel = unavailable; never carry prior values forward" };
        if (recordingProperties != null) foreach (var pair in recordingProperties)
        {
            if (properties.ContainsKey(pair.Key)) throw new ArgumentException("Reserved recording property: " + pair.Key);
            properties.Add(pair.Key, pair.Value);
        }
        return new SessionRecordingSink(new SessionRecorder(path, new SessionMetadata {
            Game = "Super Woden Rally Edge", PluginVersion = pluginVersion, ToolkitVersion = "v0.12.0 + separately pinned unpublished recording",
            StartedUtc = DateTime.UtcNow,
            Properties = properties,
            ChannelUnits = TelemetrySchema.Channels.ToDictionary(x => x.Key, x => x.Value.Unit)
        }, recordingOptions ?? RecordingProfiles.Ordinary()));
    }

    public RecordingCounters Counters => _recorder.GetCounters();
    public string Status => _recorder.Status.ToString();
    public string? Error => _recorder.LastError;
    public long Drops => _recorder.DroppedSamples;
    public double ElapsedSeconds => Volatile.Read(ref _elapsed);
    public void Stop() => _recorder.Stop(TimeSpan.FromMilliseconds(500));

    public void Fail(double elapsedSeconds, string reason)
    {
        // This semantic failure is part of the source, even if a complete prefix/footer can be saved.
        // A rejected marker is also refused by the existing dropped/invalid-counter checks.
        if (_recorder.Status == RecorderStatus.Recording)
            _recorder.TryMark(Math.Max(0, elapsedSeconds - (_origin ?? elapsedSeconds)), "capture-failed", reason.Length > 4096 ? reason[..4096] : reason);
        _recorder.Stop(TimeSpan.FromMilliseconds(500));
    }

    public void Offer(TelemetrySample sample)
    {
        // Stop may arrive while the game keeps producing samples. Never offer a
        // post-Stop record to SessionRecorder: it counts rejected-after-stop
        // attempts and makes an otherwise complete source unusable.
        if (_recorder.Status != RecorderStatus.Recording) return;
        _origin ??= sample.ElapsedSeconds;
        double recordTime = sample.ElapsedSeconds - _origin.Value;
        Volatile.Write(ref _elapsed, Math.Max(0, recordTime));
        _recorder.TryRecord(recordTime, sample.Channels);
        if (_recorder.Status != RecorderStatus.Recording) return;
        if (_lastState != sample.State) _recorder.TryMark(recordTime, "state", sample.State);
        if (sample.Discontinuity != null && _lastDiscontinuity != sample.Discontinuity) _recorder.TryMark(recordTime, "discontinuity", sample.Discontinuity);
        if (sample.ForceStatus != null && sample.ForceStatus != _lastForceStatus) _recorder.TryMark(recordTime, "ffb", sample.ForceStatus);
        if (sample.ForceGate != null && sample.ForceGate != _lastForceGate) _recorder.TryMark(recordTime, "ffb-gate", sample.ForceGate);
        if (sample.ForceModelReason != null && sample.ForceModelReason != _lastForceModelReason) _recorder.TryMark(recordTime, "ffb-model", sample.ForceModelReason);
        string unavailable = string.Join(",", sample.Unavailable);
        if (unavailable != _lastUnavailable && (unavailable.Length > 0 || _lastUnavailable?.Length > 0))
            _recorder.TryMark(recordTime, "unavailable", unavailable.Length <= 4096 ? unavailable : unavailable[..4096]);
        _lastUnavailable = unavailable;
        _lastForceStatus = sample.ForceStatus;
        _lastForceGate = sample.ForceGate; _lastForceModelReason = sample.ForceModelReason;
        _lastState = sample.State; _lastDiscontinuity = sample.Discontinuity;
    }
}
