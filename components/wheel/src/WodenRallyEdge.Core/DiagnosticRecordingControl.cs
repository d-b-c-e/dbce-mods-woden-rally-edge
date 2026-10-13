using System.Globalization;
using System.Text.Json;

namespace WodenRallyEdge.Core;

// Narrow control for a prepared ordinary signal recording. StageSession and its
// replayable trajectory keep their separate FileSessionControl contract.
public sealed record DiagnosticFinalization(int Version, Guid RequestId, string State, string? Error);

public sealed class DiagnosticRecordingControl
{
    public const int CurrentVersion = 1;
    private readonly string _controlRoot;
    private readonly Guid _requestId;
    private readonly int _processId;
    private readonly long _processStartUtcTicks;
    private readonly TelemetryOutput _output;
    private string? _lastPublished;
    private string? _lastError;
    private DateTimeOffset _nextPoll;
    private DiagnosticFinalization? _finalization;
    public DiagnosticFinalization? Finalization => _finalization;
    public string? LastError => _lastError;

    public DiagnosticRecordingControl(string directory, Guid requestId, int processId,
        long processStartUtcTicks, TelemetryOutput output)
    {
        if (requestId == Guid.Empty || processId <= 0 || processStartUtcTicks <= 0)
            throw new ArgumentException("Invalid ordinary recording identity");
        string captureRoot = Path.GetFullPath(directory);
        _requestId = requestId;
        _processId = processId;
        _processStartUtcTicks = processStartUtcTicks;
        _output = output;
        if (!string.Equals(Path.GetFileName(captureRoot), "request-" + requestId.ToString("N"), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Recording control requires its consumed request identity");
        _controlRoot = Path.Combine(captureRoot, "control");
        try
        {
            if (!Directory.Exists(captureRoot)) throw new DirectoryNotFoundException("Capture directory is missing");
            Directory.CreateDirectory(_controlRoot);
            PublishStatus();
        }
        catch (Exception ex) { _lastError = "Recording control storage: " + ex.Message; }
    }

    public void Tick(DateTimeOffset? at = null)
    {
        DateTimeOffset now = at ?? DateTimeOffset.UtcNow;
        if (now < _nextPoll) return;
        _nextPoll = now.AddMilliseconds(250);
        Flush();
    }

    public void Flush()
    {
        try { TickCore(); }
        catch (Exception ex) { _lastError = "Recording control storage: " + ex.Message; }
    }

    private void TickCore()
    {
        string stopPath = Path.Combine(_controlRoot, "request.txt");
        if (File.Exists(stopPath))
        {
            try
            {
                if (new FileInfo(stopPath).Length > 8192) throw new InvalidDataException("Request too large");
                var fields = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (string line in File.ReadAllLines(stopPath))
                {
                    int equal = line.IndexOf('=');
                    if (equal <= 0 || !fields.TryAdd(line[..equal], line[(equal + 1)..]))
                        throw new InvalidDataException("Malformed request");
                }
                if (fields.Count != 3 || !fields.TryGetValue("id", out string? id) ||
                    !Guid.TryParseExact(id, "N", out Guid parsed) || parsed != _requestId ||
                    !fields.TryGetValue("action", out string? action) || action != "stop" ||
                    !fields.TryGetValue("expiresUtc", out string? expiry) ||
                    !DateTimeOffset.TryParseExact(expiry, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var until) ||
                    until < DateTimeOffset.UtcNow || until > DateTimeOffset.UtcNow.AddMinutes(5) ||
                    !_output.RecordingActive) throw new InvalidDataException("Wrong, expired or inactive Stop request");
                _output.StopRecording();
                _lastError = null;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            { _lastError = ex.Message; }
            finally { try { File.Delete(stopPath); } catch (Exception ex) { _lastError = ex.Message; } }
        }
        PublishStatus();
        if (_finalization == null && _output.RecordingStatus == "Completed")
        {
            string noticePath = Path.Combine(_controlRoot, "finalization.json");
            if (File.Exists(noticePath))
            {
                try
                {
                    var notice = JsonSerializer.Deserialize<DiagnosticFinalization>(File.ReadAllText(noticePath));
                    if (notice?.Version == CurrentVersion && notice.RequestId == _requestId &&
                        (notice.State == "saved" || notice.State == "incomplete") &&
                        (notice.State != "saved" || File.Exists(Path.Combine(Path.GetDirectoryName(_controlRoot)!, "case.json")) &&
                            File.Exists(Path.Combine(Path.GetDirectoryName(_controlRoot)!, "force-observation.jsonl"))))
                        _finalization = notice;
                }
                catch (IOException) { /* The finalizer may still be publishing the notice. */ }
                catch (JsonException) { /* No HUD claim from malformed status. */ }
            }
        }
    }

    private void PublishStatus()
    {
        string state = _output.RecordingStatus, error = _output.RecordingError ?? _lastError ?? "";
        string key = state + "\n" + error;
        if (key == _lastPublished) return;
        string path = Path.Combine(_controlRoot, "status.txt");
        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllLines(temporary, new[] {
                "id=" + _requestId.ToString("N"), "utc=" + DateTime.UtcNow.ToString("O"),
                "pid=" + _processId.ToString(CultureInfo.InvariantCulture),
                "startedUtcTicks=" + _processStartUtcTicks.ToString(CultureInfo.InvariantCulture),
                "active=" + _output.RecordingActive,
                "status=" + state, "error=" + error.Replace('\r', ' ').Replace('\n', ' ') });
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
            _lastPublished = key;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
