using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Dbce.Wheel.Telemetry;

namespace WodenRallyEdge.Core;

public sealed record OutputOptions(int ForzaPort = 8000, int DetailPort = 8001, int DetailHz = 20, bool Enabled = true);

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
    private static void Validate(OutputOptions options)
    {
        if (options.ForzaPort is < 0 or > 65535 || options.DetailPort is < 0 or > 65535 || options.DetailHz is < 1 or > 60 ||
            options.ForzaPort != 0 && options.ForzaPort == options.DetailPort) throw new ArgumentException("Invalid/distinct output ports or detail rate");
    }
    // Construct a replacement before touching the active pair. The worker stays
    // alive across destination and On/Off changes.
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
                _options = options;
                oldForza?.Dispose(); oldDetail?.Dispose(); LastError = null; Interlocked.Exchange(ref _lastSentAt, 0);
            }
        }
        finally { forza?.Dispose(); detail?.Dispose(); }
    }
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private sealed record Pending(TelemetrySample Sample, long EnqueuedAt);
    private Pending? _pending;
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

    public TelemetryOutput(OutputOptions options)
    {
        Validate(options);
        _options = options;
        ConfigureNetwork(options);
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
    }
}
