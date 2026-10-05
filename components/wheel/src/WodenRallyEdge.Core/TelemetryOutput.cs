using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Dbce.Wheel.Telemetry;
using Dbce.Wheel.Recording;

namespace WodenRallyEdge.Core;

public sealed record OutputOptions(int ForzaPort = 8000, int DetailPort = 8001, int DetailHz = 20, string? RecordingPath = null, bool Enabled = true);

// The physics thread hands off a completed, exclusively-owned snapshot. Network
// serialization and sends run on a bounded one-slot worker; old ticks may be dropped.
public sealed class TelemetryOutput : IDisposable
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly AutoResetEvent _wake = new(false);
    private readonly Thread _worker;
    private OutputOptions _options;
    private readonly object _networkGate = new();
    private TelemetrySender? _forza;
    private UdpClient? _detail;
    public OutputOptions ActiveOptions { get { lock (_networkGate) return _options; } }
    public bool RecordingActive => RecordingStatus == "Recording";
    public void StopRecording() => _recorder?.Stop(TimeSpan.FromMilliseconds(500));
    private static void Validate(OutputOptions options)
    {
        if (options.ForzaPort is < 0 or > 65535 || options.DetailPort is < 0 or > 65535 || options.DetailHz is < 1 or > 60 ||
            options.ForzaPort != 0 && options.ForzaPort == options.DetailPort) throw new ArgumentException("Invalid/distinct output ports or detail rate");
    }
    // Construct a replacement before touching the active pair. The recorder and
    // worker stay alive across destination and On/Off changes.
    public void ConfigureNetwork(OutputOptions options)
    {
        Validate(options);
        TelemetrySender? forza = null; UdpClient? detail = null;
        try
        {
            if (options.Enabled && options.ForzaPort > 0) forza = new("127.0.0.1", options.ForzaPort);
            if (options.Enabled && options.DetailPort > 0) detail = new(AddressFamily.InterNetwork);
            lock (_networkGate)
            {
                if (_stop) throw new ObjectDisposedException(nameof(TelemetryOutput));
                var oldForza = _forza; var oldDetail = _detail;
                _forza = forza; _detail = detail; forza = null; detail = null;
                _options = options with { RecordingPath = _options.RecordingPath };
                oldForza?.Dispose(); oldDetail?.Dispose(); LastError = null; Interlocked.Exchange(ref _lastSentAt, 0);
            }
        }
        finally { forza?.Dispose(); detail?.Dispose(); }
    }
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly SessionRecorder? _recorder;
    private sealed record Pending(TelemetrySample Sample, long EnqueuedAt);
    private Pending? _pending;
    private string? _lastState;
    private string? _lastDiscontinuity;
    private string? _lastUnavailable;
    private string? _lastForceStatus;
    private string? _lastForceGate;
    private string? _lastForceModelReason;
    private double? _recordOrigin;
    private volatile bool _stop;
    private long _overwrites, _sent, _errors, _detailSent, _lastSentAt;
    public long OverwrittenTicks => Interlocked.Read(ref _overwrites);
    public long ForzaPackets => Interlocked.Read(ref _sent);
    public long DetailPackets => Interlocked.Read(ref _detailSent);
    public bool Sending => Interlocked.Read(ref _lastSentAt) > 0 && (Stopwatch.GetTimestamp() - Interlocked.Read(ref _lastSentAt)) / (double)Stopwatch.Frequency < 1;
    private void SentPacket(bool detail = false) { if (detail) Interlocked.Increment(ref _detailSent); else Interlocked.Increment(ref _sent); Interlocked.Exchange(ref _lastSentAt, Stopwatch.GetTimestamp()); }
    public long SendErrors => Interlocked.Read(ref _errors);
    public string? LastError { get; private set; }
    public bool Stopped => !_worker.IsAlive;
    public string? RecordingError => _recorder?.LastError;
    public string RecordingStatus => _recorder?.Status.ToString() ?? "Disabled";
    public long RecordingDrops => _recorder?.DroppedSamples ?? 0;

    public TelemetryOutput(OutputOptions options, string sessionId, string recordingSource, string pluginVersion = "unknown",
        IReadOnlyDictionary<string, string>? recordingProperties = null, RecordingOptions? recordingOptions = null)
    {
        Validate(options);
        _options = options;
        if (options.RecordingPath != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.RecordingPath))!);
            var properties = new Dictionary<string, string> { ["sessionId"] = sessionId, ["recordingSource"] = recordingSource,
                ["phase"] = "MainCar.FixedUpdate.postfix.prePhysicsSolve", ["schema"] = TelemetrySchema.Name + "/1",
                ["missing"] = "Absent channel = unavailable; never carry prior values forward" };
            if (recordingProperties != null) foreach (var pair in recordingProperties)
            {
                if (properties.ContainsKey(pair.Key)) throw new ArgumentException("Reserved recording property: " + pair.Key);
                properties.Add(pair.Key, pair.Value);
            }
            _recorder = new SessionRecorder(options.RecordingPath, new SessionMetadata {
                Game = "Super Woden Rally Edge", PluginVersion = pluginVersion, ToolkitVersion = "v0.12.0 + separately pinned unpublished recording",
                StartedUtc = DateTime.UtcNow,
                Properties = properties,
                ChannelUnits = TelemetrySchema.Channels.ToDictionary(x => x.Key, x => x.Value.Unit)
            }, recordingOptions ?? new RecordingOptions { QueueCapacity = 512, MaxDurationSeconds = 1200, MaxFileBytes = 64L * 1024 * 1024, MaxChannelsPerSample = 512 });
        }
        try { ConfigureNetwork(options); } catch { _recorder?.Stop(TimeSpan.FromMilliseconds(500)); throw; }
        _worker = new Thread(Run) { IsBackground = true, Name = "Woden telemetry output" };
        _worker.Start();
    }

    public void Publish(TelemetrySample sample)
    {
        if (_stop) return;
        if (!double.IsFinite(sample.ElapsedSeconds) || sample.ElapsedSeconds < 0 || !double.IsFinite(sample.SimulationSeconds) || sample.SimulationSeconds < 0)
        { Fail("Invalid sample clock"); return; }
        sample.Add("sample.simulationSeconds", sample.SimulationSeconds);
        sample.Add("sample.sequence", sample.Sequence);
        sample.Add("sample.driving", sample.Driving ? 1 : 0);
        sample.Add("sample.discontinuity", sample.Discontinuity == null ? 0 : 1);
        _recordOrigin ??= sample.ElapsedSeconds;
        double recordTime = sample.ElapsedSeconds - _recordOrigin.Value;
        _recorder?.TryRecord(recordTime, sample.Channels);
        if (_lastState != sample.State) _recorder?.TryMark(recordTime, "state", sample.State);
        if (sample.Discontinuity != null && _lastDiscontinuity != sample.Discontinuity) _recorder?.TryMark(recordTime, "discontinuity", sample.Discontinuity);
        if (sample.ForceStatus != null && sample.ForceStatus != _lastForceStatus) _recorder?.TryMark(recordTime, "ffb", sample.ForceStatus);
        if (sample.ForceGate != null && sample.ForceGate != _lastForceGate) _recorder?.TryMark(recordTime, "ffb-gate", sample.ForceGate);
        if (sample.ForceModelReason != null && sample.ForceModelReason != _lastForceModelReason) _recorder?.TryMark(recordTime, "ffb-model", sample.ForceModelReason);
        _lastForceStatus = sample.ForceStatus;
        _lastForceGate = sample.ForceGate; _lastForceModelReason = sample.ForceModelReason;
        string unavailable = string.Join(",", sample.Unavailable);
        if (unavailable != _lastUnavailable && (unavailable.Length > 0 || _lastUnavailable?.Length > 0))
            _recorder?.TryMark(recordTime, "unavailable", unavailable.Length <= 4096 ? unavailable : unavailable[..4096]);
        _lastUnavailable = unavailable;
        _lastState = sample.State; _lastDiscontinuity = sample.Discontinuity;
        if (Interlocked.Exchange(ref _pending, new(sample, Stopwatch.GetTimestamp())) != null) Interlocked.Increment(ref _overwrites);
        _wake.Set();
    }

    private void Run()
    {
        try
        {
            double lastData = -10, lastDetail = -10, lastIdle = -10;
            while (!_stop)
            {
                _wake.WaitOne(50);
                lock (_networkGate)
                {
                var forza = _forza; var detail = _detail;
                double now = _clock.Elapsed.TotalSeconds;
                var pending = Interlocked.Exchange(ref _pending, null);
                var sample = pending?.Sample;
                if (pending != null && (Stopwatch.GetTimestamp() - pending.EnqueuedAt) / (double)Stopwatch.Frequency > .5) sample = null;
                if (sample != null)
                {
                    lastData = now;
                    var frame = ForzaProjection.Map(sample);
                    if (forza != null) { if (forza.Send(frame)) SentPacket(); else Fail(forza.LastError); }
                    if (detail != null && now - lastDetail >= 1.0 / _options.DetailHz)
                    {
                        try
                        {
                            var bytes = JsonSerializer.SerializeToUtf8Bytes(sample, Json);
                            if (bytes.Length > 60000) Fail("Detailed datagram exceeded 60000 bytes");
                            else { detail.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, _options.DetailPort)); lastDetail = now; SentPacket(true); }
                        }
                        catch (Exception ex) { Fail(ex.Message); }
                    }
                }
                else if (now - lastData > .5 && now - lastIdle > .1)
                {
                    if (forza != null) { var idle = new TelemetryFrame(); if (forza.Send(idle)) SentPacket(); else Fail(forza.LastError); }
                    lastIdle = now;
                }
                }
            }
        }
        catch (Exception ex) { Fail(ex.ToString()); }
        finally
        {
            lock (_networkGate)
            {
                if (_forza != null) { for (int i = 0; i < 3; i++) { var idle = new TelemetryFrame(); _forza.Send(idle); } _forza.Dispose(); }
                _detail?.Dispose(); _forza = null; _detail = null;
            }
        }
    }
    private void Fail(string? error) { LastError = error; Interlocked.Increment(ref _errors); }
    public void Dispose()
    {
        if (_stop) return;
        _stop = true;
        _wake.Set();
        // Keep the event alive if the OS is stalled; never dispose it under the worker.
        if (_worker.Join(500)) _wake.Dispose();
        _recorder?.Stop(TimeSpan.FromMilliseconds(500));
    }
}
