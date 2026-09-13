using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Dbce.Wheel.Telemetry;
using Dbce.Wheel.Recording;

namespace WodenRallyEdge.Core;

public sealed record OutputOptions(int ForzaPort = 8000, int DetailPort = 8001, int DetailHz = 20, string? RecordingPath = null);

// The physics thread hands off a completed, exclusively-owned snapshot. Network
// serialization and sends run on a bounded one-slot worker; old ticks may be dropped.
public sealed class TelemetryOutput : IDisposable
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly AutoResetEvent _wake = new(false);
    private readonly Thread _worker;
    private readonly OutputOptions _options;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly SessionRecorder? _recorder;
    private sealed record Pending(TelemetrySample Sample, long EnqueuedAt);
    private Pending? _pending;
    private string? _lastState;
    private string? _lastDiscontinuity;
    private string? _lastUnavailable;
    private double? _recordOrigin;
    private volatile bool _stop;
    private long _overwrites, _sent, _errors;
    public long OverwrittenTicks => Interlocked.Read(ref _overwrites);
    public long ForzaPackets => Interlocked.Read(ref _sent);
    public long SendErrors => Interlocked.Read(ref _errors);
    public string? LastError { get; private set; }
    public bool Stopped => !_worker.IsAlive;
    public string RecordingStatus => _recorder?.Status.ToString() ?? "Disabled";
    public long RecordingDrops => _recorder?.DroppedSamples ?? 0;

    public TelemetryOutput(OutputOptions options, string sessionId, string recordingSource)
    {
        if (options.ForzaPort is < 0 or > 65535 || options.DetailPort is < 0 or > 65535 || options.DetailHz is < 1 or > 60 ||
            options.ForzaPort != 0 && options.ForzaPort == options.DetailPort) throw new ArgumentException("Invalid/distinct output ports or detail rate");
        _options = options;
        if (options.RecordingPath != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.RecordingPath))!);
            _recorder = new SessionRecorder(options.RecordingPath, new SessionMetadata {
                Game = "Super Woden Rally Edge", PluginVersion = "0.2.2", ToolkitVersion = "v0.12.0 + separately pinned unpublished recording",
                StartedUtc = DateTime.UtcNow,
                Properties = new() { ["sessionId"] = sessionId, ["recordingSource"] = recordingSource,
                    ["phase"] = "MainCar.FixedUpdate.postfix.prePhysicsSolve", ["schema"] = TelemetrySchema.Name + "/1",
                    ["missing"] = "Absent channel = unavailable; never carry prior values forward" },
                ChannelUnits = TelemetrySchema.Channels.ToDictionary(x => x.Key, x => x.Value.Unit)
            }, new RecordingOptions { QueueCapacity = 512, MaxDurationSeconds = 1200, MaxFileBytes = 64L * 1024 * 1024, MaxChannelsPerSample = 512 });
        }
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
        _recordOrigin ??= sample.ElapsedSeconds;
        double recordTime = sample.ElapsedSeconds - _recordOrigin.Value;
        _recorder?.TryRecord(recordTime, sample.Channels);
        if (_lastState != sample.State) _recorder?.TryMark(recordTime, "state", sample.State);
        if (sample.Discontinuity != null && _lastDiscontinuity != sample.Discontinuity) _recorder?.TryMark(recordTime, "discontinuity", sample.Discontinuity);
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
        TelemetrySender? forza = null;
        UdpClient? detail = null;
        try
        {
            if (_options.ForzaPort > 0) forza = new("127.0.0.1", _options.ForzaPort);
            if (_options.DetailPort > 0) detail = new(AddressFamily.InterNetwork);
            double lastData = -10, lastDetail = -10, lastIdle = -10;
            while (!_stop)
            {
                _wake.WaitOne(50);
                double now = _clock.Elapsed.TotalSeconds;
                var pending = Interlocked.Exchange(ref _pending, null);
                var sample = pending?.Sample;
                if (pending != null && (Stopwatch.GetTimestamp() - pending.EnqueuedAt) / (double)Stopwatch.Frequency > .5) sample = null;
                if (sample != null)
                {
                    lastData = now;
                    var frame = ForzaProjection.Map(sample);
                    if (forza != null) { if (forza.Send(frame)) Interlocked.Increment(ref _sent); else Fail(forza.LastError); }
                    if (detail != null && now - lastDetail >= 1.0 / _options.DetailHz)
                    {
                        try
                        {
                            var bytes = JsonSerializer.SerializeToUtf8Bytes(sample, Json);
                            if (bytes.Length > 60000) Fail("Detailed datagram exceeded 60000 bytes");
                            else { detail.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, _options.DetailPort)); lastDetail = now; }
                        }
                        catch (Exception ex) { Fail(ex.Message); }
                    }
                }
                else if (now - lastData > .5 && now - lastIdle > .1)
                {
                    if (forza != null) { var idle = new TelemetryFrame(); if (forza.Send(idle)) Interlocked.Increment(ref _sent); else Fail(forza.LastError); }
                    lastIdle = now;
                }
            }
        }
        catch (Exception ex) { Fail(ex.ToString()); }
        finally
        {
            if (forza != null) { for (int i = 0; i < 3; i++) { var idle = new TelemetryFrame(); forza.Send(idle); } forza.Dispose(); }
            detail?.Dispose();
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
