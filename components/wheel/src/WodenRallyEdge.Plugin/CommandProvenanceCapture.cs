using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Bounded opt-in SOFTWARE observation only. No native imports, file writes during
// callbacks, or cadence adoption. Production construction does not instantiate it.
internal sealed record CommandCaptureEvent(long Sequence, string Kind, string Code, string ClockBits,
    string? Device, string? PreviousDevice, int? CommandBits, bool? Result,
    long ResetBefore, long ResetAfter, string? OptionsBits, string? OptionsSha256, int State,
    string? SimulationBits, string? SourceElapsedBits);
internal sealed record CommandCaptureFooter(bool Complete, long Observed, int Stored, long Dropped, string Reason);
internal sealed record CommandCaptureDocument(string Schema, string ClockRole, string ControllerBase,
    string ProducerAssemblySha256, string Scope, string[] Unrecorded, CommandCaptureEvent[] Events, CommandCaptureFooter Footer);

internal sealed class CommandProvenanceCapture
{
    internal const string Schema = "dbce.woden.canonical-command-capture@1";
    internal const int MaximumBytes = 1024 * 1024;
    private readonly object _sync = new();
    private readonly Func<double> _clock;
    private readonly int _capacity;
    private readonly List<CommandCaptureEvent> _events = new();
    private readonly Dictionary<Guid, string> _tokens = new(); // Never serialized.
    private Action<CommandCaptureEvent>? _sink;
    private long _observed, _dropped;
    private double _lastClock = -1;
    private bool _finished, _terminal;
    private string _reason = "none";
    internal string? SelectedToken { get; private set; }
    private readonly string _assemblyHash;

    internal CommandProvenanceCapture(Func<double> clock, int capacity = 1024, Action<CommandCaptureEvent>? sink = null)
    {
        if (capacity is < 16 or > 1024) throw new ArgumentOutOfRangeException(nameof(capacity));
        _clock = clock; _capacity = capacity; _sink = sink;
        _assemblyHash = Hash(File.ReadAllBytes(typeof(ForceController).Assembly.Location));
    }
    private void Incomplete(string reason) { if (_reason == "none") _reason = reason; }
    internal void Restart() { lock (_sync) { if (_terminal) Incomplete("restart-unsupported"); } Emit("invocation", "beginruntime"); }
    internal string? Token(Guid guid)
    {
        if (guid == Guid.Empty) return null;
        lock (_sync) {
            if (_tokens.TryGetValue(guid, out string? token)) return token;
            if (_tokens.Count == 64) { Incomplete("token-overflow"); return null; }
            token = "d" + (_tokens.Count + 1); _tokens.Add(guid, token); return token;
        }
    }
    internal void Selection(Guid? selected, Guid previous, string code, bool ready)
    {
        SelectedToken = selected.HasValue ? Token(selected.Value) : null;
        Emit("target", code, SelectedToken, Token(previous), result: ready);
    }
    internal static string TargetCode(ForceTarget target) => target.Ready ? "none" : target.Reason switch {
        "Bind Steering in Controls to select its wheel." => "steering-unbound",
        "Select an FFB device." => "explicit-target-missing",
        "Saved device disconnected. Reconnect it and Refresh." => "target-not-present",
        "Device identity is ambiguous. Refresh or select another wheel." => "target-ambiguous",
        "Virtual devices cannot receive FFB. Select a physical wheel." => "target-virtual",
        "Selected device has no FFB. Select a force-feedback wheel." => "target-no-ffb",
        _ => "unknown"
    };
    internal void Emit(string kind, string code, string? device = null, string? previous = null,
        int? command = null, bool? result = null, long before = 0, long after = 0,
        string? options = null, int state = 0, double? simulation = null, double? elapsed = null)
    {
        // Observer faults never escape into control flow or request extra device calls.
        lock (_sync) {
            _observed++;
            if (_finished || _events.Count == _capacity) {
                _dropped++; Incomplete(_finished ? "after-finish" : "event-overflow"); return;
            }
            if (!CommandCaptureAudit.ValidTag(kind, code)) { _dropped++; Incomplete("invalid-event"); return; }
            double now;
            try { now = _clock(); } catch { _dropped++; Incomplete("clock-failed"); return; }
            if (!double.IsFinite(now) || now < 0 || now < _lastClock) Incomplete("clock-invalid");
            _lastClock = now;
            var row = new CommandCaptureEvent(_observed, kind, code,
                unchecked((ulong)BitConverter.DoubleToInt64Bits(now)).ToString("x16"),
                device, previous, command, result, before, after, options,
                options == null ? null : Hash(Encoding.ASCII.GetBytes(options)), state, Bits(simulation), Bits(elapsed));
            _events.Add(row);
            if (kind == "state" && code == "shutdown-ended" && result == true) _terminal = true;
            try { _sink?.Invoke(row); } catch { Incomplete("sink-failed"); _sink = null; }
        }
    }
    internal void Options(ForceOptions options)
    {
        string Bits(float x) => unchecked((uint)BitConverter.SingleToInt32Bits(x)).ToString("x8");
        Emit("settings", "options", options: string.Join(":", Bits(options.Strength), Bits(options.PeakPercent),
            Bits(options.LoadReference), Bits(options.SlipScale), Bits(options.SmoothingMs), Bits(options.Damping), options.Invert ? "1" : "0"));
    }
    internal T Call<T>(string code, string? token, Func<T> action, int? bits = null)
    {
        Emit("call-begin", code, token, command: bits);
        try {
            T value = action();
            Emit("call-end", code, token, command: bits, result: value is bool b ? b : null);
            return value;
        } catch {
            Emit("call-end", code, token, command: bits, result: null, state: 1); // Bounded exception indicator only.
            throw;
        }
    }
    internal void Call(string code, string? token, Action action)
        => Call(code, token, () => { action(); return 0; });
    internal CommandCaptureDocument Finish()
    {
        lock (_sync) {
            if (!_terminal) Incomplete("missing-terminal");
            _finished = true;
            return new(Schema, "injected-callback-observation-not-model-or-cadence-clock",
                "7e806bb986e11166deca4bb03ead326837ba835d", _assemblyHash,
                "one-controller-software-decisions-and-call-sequence-only",
                new[] { "full-model-source-channel-recompute", "cadence-clock-and-epochs", "native-quantization-and-entry-timing",
                    "native-watchdog-or-driver-interventions", "config-profile-source-attestation", "concurrent-runtime-actor-correlation" },
                _events.ToArray(), new(_reason == "none", _observed, _events.Count, _dropped, _reason));
        }
    }
    private static string? Bits(double? value) => value.HasValue ? unchecked((ulong)BitConverter.DoubleToInt64Bits(value.Value)).ToString("x16") : null;
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal static void WriteNew(string path, CommandCaptureDocument document)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document);
        if (bytes.Length > MaximumBytes) throw new IOException("capture-size-limit");
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        file.Write(bytes);
    }
}

// Decorates existing calls only when explicitly injected; Error remains a pass-through
// and is never read a second time or serialized as raw exception/device text.
internal sealed class ProvenanceForceDevice : IForceDevice
{
    private readonly IForceDevice _inner;
    private readonly CommandProvenanceCapture _capture;
    private string? _token;
    internal ProvenanceForceDevice(IForceDevice inner, CommandProvenanceCapture capture) { _inner = inner; _capture = capture; }
    public string? Error => _inner.Error;
    public bool CanOpen => _capture.Call("can-open", _capture.SelectedToken, () => _inner.CanOpen);
    public bool Open(Guid guid) { _token = _capture.Token(guid); return _capture.Call("open", _token, () => _inner.Open(guid)); }
    public bool Write(float force) => _capture.Call("write", _token, () => _inner.Write(force), BitConverter.SingleToInt32Bits(force));
    public void ZeroAndStop() => _capture.Call("zero-stop", _token, _inner.ZeroAndStop);
    public void Panic() => _capture.Call("panic", _token, _inner.Panic);
    public void Close() { _capture.Call("close", _token, _inner.Close); _token = null; }
}

internal static class CommandCaptureAudit
{
    private static readonly HashSet<string> Calls = new() { "can-open", "open", "write", "zero-stop", "panic", "close", "reader-close", "refresh" };
    private static readonly HashSet<string> Invocations = new() { "prepare", "tick", "setenabled", "disarm", "reconnect", "panic", "suspend", "beginruntime", "shutdown" };
    private static readonly HashSet<string> Gates = new() { "active", "diagnostic-force-disabled", "saved-off", "faulted", "settings-open", "unfocused", "stock-owner-unready", "camera-not-owned", "wheel-input-unavailable", "model-invalid", "wheel-not-connected", "write-failed" };
    internal static bool ValidTag(string kind, string code) => kind switch {
        "call-begin" or "call-end" => Calls.Contains(code),
        "invocation" or "terminal-reject" => Invocations.Contains(code),
        "gate" => Gates.Contains(code),
        "guard" => code is "diagnostic" or "enabled" or "faulted" or "focused" or "stock-ready" or "hub-present",
        "target" => code is "none" or "steering-unbound" or "explicit-target-missing" or "target-not-present" or "target-ambiguous" or "target-virtual" or "target-no-ffb" or "unknown",
        "target-change" => code == "changed",
        "reader" => code == "is-reading",
        "settings" => code == "options",
        "reset" => code == "explicit",
        "model" => code == "evaluated",
        "state" => code is "prepare-ended" or "shutdown-ended" or "terminal-stop-published",
        _ => false
    };
    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool value, string code) { if (!value) throw new IOException(code); }
    internal static void Validate(CommandCaptureDocument d)
    {
        try { ValidateCore(d); }
        catch (IOException) { throw; }
        catch { throw new IOException("capture-invalid"); }
    }
    private static void ValidateCore(CommandCaptureDocument d)
    {
        Require(d.Schema == CommandProvenanceCapture.Schema && d.ClockRole == "injected-callback-observation-not-model-or-cadence-clock" &&
            d.ControllerBase == "7e806bb986e11166deca4bb03ead326837ba835d" && Regex.IsMatch(d.ProducerAssemblySha256, "^[a-f0-9]{64}$"), "capture-identity");
        Require(d.Footer != null && d.Events != null && d.Footer.Complete && d.Footer.Reason == "none" &&
            d.Footer.Dropped == 0 && d.Footer.Stored == d.Events.Length && d.Footer.Observed == d.Events.Length && d.Events.Length <= 1024, "capture-incomplete");
        Require(d.Scope == "one-controller-software-decisions-and-call-sequence-only" && d.Unrecorded != null && d.Unrecorded.SequenceEqual(new[] {
            "full-model-source-channel-recompute", "cadence-clock-and-epochs", "native-quantization-and-entry-timing",
            "native-watchdog-or-driver-interventions", "config-profile-source-attestation", "concurrent-runtime-actor-correlation" }), "capture-scope");
        CommandCaptureEvent? pending = null;
        string? connected = null;
        bool terminal = false;
        double clock = -1;
        long reset = 0;
        for (int i = 0; i < d.Events.Length; i++) {
            var e = d.Events[i]; Require(e.Sequence == i + 1 && ValidTag(e.Kind, e.Code), "capture-event");
            Require(e.State is 0 or 1, "capture-state");
            Require(e.State != 1 || (e.Kind == "call-end" && e.Result == null), "capture-error");
            Require(e.Device == null || Regex.IsMatch(e.Device, "^d([1-9]|[1-5][0-9]|6[0-4])$"), "capture-device-token");
            Require(e.PreviousDevice == null || Regex.IsMatch(e.PreviousDevice, "^d([1-9]|[1-5][0-9]|6[0-4])$"), "capture-device-token");
            Require(Regex.IsMatch(e.ClockBits, "^[a-f0-9]{16}$"), "capture-clock");
            double now = BitConverter.Int64BitsToDouble(unchecked((long)Convert.ToUInt64(e.ClockBits, 16)));
            Require(double.IsFinite(now) && now >= 0 && now >= clock, "capture-clock"); clock = now;
            if (e.Kind is "reset" or "model") { Require(e.ResetBefore >= reset && e.ResetAfter >= e.ResetBefore, "capture-reset"); reset = e.ResetAfter; }
            Require(e.SimulationBits == null || Regex.IsMatch(e.SimulationBits, "^[a-f0-9]{16}$"), "capture-model-clock");
            Require(e.SourceElapsedBits == null || Regex.IsMatch(e.SourceElapsedBits, "^[a-f0-9]{16}$"), "capture-model-clock");
            Require(e.Kind == "settings" ? e.OptionsBits != null && e.OptionsSha256 != null : e.OptionsBits == null && e.OptionsSha256 == null, "capture-settings");
            Require(e.Kind != "model" || (e.SimulationBits != null && e.SourceElapsedBits != null), "capture-model-clock");
            if (e.OptionsBits != null) Require(Regex.IsMatch(e.OptionsBits, "^([a-f0-9]{8}:){6}[01]$") &&
                e.OptionsSha256 == CommandProvenanceCapture.Hash(Encoding.ASCII.GetBytes(e.OptionsBits)), "capture-settings");
            if (e.Kind == "call-begin") {
                Require(pending == null && !terminal, "capture-call-order");
                if (e.Code == "open") Require(connected == null && e.Device != null, "capture-open-state");
                if (e.Code == "write") {
                    Require(connected != null && connected == e.Device && e.CommandBits.HasValue, "capture-write-state");
                    float value = BitConverter.Int32BitsToSingle(e.CommandBits!.Value);
                    Require(float.IsFinite(value) && Math.Abs(value) <= 1f, "capture-command");
                }
                pending = e;
            } else if (e.Kind == "call-end") {
                Require(pending != null && pending.Code == e.Code && pending.Device == e.Device && pending.CommandBits == e.CommandBits, "capture-call-order");
                if (e.Code == "open" && e.Result == true) connected = e.Device;
                if (e.Code == "close" && e.State == 0) connected = null;
                pending = null;
            }
            if (e.Kind == "state" && e.Code == "shutdown-ended" && e.Result == true) { Require(connected == null && pending == null, "capture-terminal-state"); terminal = true; }
        }
        Require(terminal && pending == null && connected == null, "capture-missing-terminal");
    }
    // Projection into a pure fake sink, never dispatch to a device or reconstruct math.
    internal static void ReplayCalls(CommandCaptureDocument document, Action<CommandCaptureEvent> fakeSink)
    {
        Validate(document);
        foreach (var e in document.Events) if (e.Kind == "call-end") fakeSink(e);
    }
    internal static CommandCaptureDocument Read(string path)
    {
        try {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > CommandProvenanceCapture.MaximumBytes) throw new IOException("capture-size-limit");
            byte[] bytes = new byte[(int)file.Length];
            int read = 0;
            while (read < bytes.Length) {
                int count = file.Read(bytes, read, bytes.Length - read);
                if (count == 0) throw new IOException("capture-truncated");
                read += count;
            }
            if (file.ReadByte() != -1) throw new IOException("capture-size-changed");
            using var json = JsonDocument.Parse(bytes);
            CheckObject(json.RootElement, typeof(CommandCaptureDocument));
            foreach (var e in json.RootElement.GetProperty("Events").EnumerateArray()) CheckObject(e, typeof(CommandCaptureEvent));
            CheckObject(json.RootElement.GetProperty("Footer"), typeof(CommandCaptureFooter));
            var d = JsonSerializer.Deserialize<CommandCaptureDocument>(json.RootElement.GetRawText()) ?? throw new IOException("capture-json");
            Validate(d); return d;
        } catch (IOException) { throw; } catch { throw new IOException("capture-json"); }
    }
    private static void CheckObject(JsonElement value, Type type)
    {
        var expected = type.GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in value.EnumerateObject()) Require(expected.Contains(p.Name) && seen.Add(p.Name), "capture-json-shape");
        Require(seen.SetEquals(expected), "capture-json-shape");
    }
}
