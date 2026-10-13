using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Woden.ControlsTesting;

// Device/engine-free admission rules. The addon separately verifies the resident
// module and exact installed/configuration hashes before it can submit raw input.
public sealed record ColdRequest(int Schema, string Nonce, DateTimeOffset ExpiresUtc,
    string PluginSha256, string CoreSha256, string ConfigSha256, string BindingsSha256,
    string NativeSha256, string OutputDirectory, string Mode = "raw", string? ControlsSha256 = null, string? ShimSha256 = null,
    string? StageReference = null, string? WorkloadDirectory = null,
    string? ProfileSha256 = null, string? WorkloadSha256 = null, string? ProfileRevision = null,
    string? ApplyReadbackSha256 = null);
public sealed record RawRequest(int Sequence, string Nonce, string Operation, string? Raw);
public static class Protocol
{
    public static bool MenuTrial(ColdRequest? request) => request?.Mode == "shim-menu-trial";
    public static bool HudOrdinary(ColdRequest? request) => request is {Schema:6, Mode:"shim-hud-ordinary"};
    public static bool ForceCapture(ColdRequest? request) => request?.Mode is "shim-force-capture" or "shim-hud-ordinary";
    public static bool DynamicCarTrial(ColdRequest? request) => request?.Schema==7 && request.Mode=="shim-applied-car-trial";
    public static bool CarTrial(ColdRequest? request) => request?.Mode is "shim-car-trial" or "shim-force-capture" or "shim-hud-ordinary" || DynamicCarTrial(request);
    public static bool LatchedTrial(ColdRequest? request) => MenuTrial(request) || CarTrial(request);
    public static bool ShimAdmission(ColdRequest? request) => request?.Mode is "shim-admission" or "shim-menu-trial" or "shim-car-trial" or "shim-force-capture" or "shim-hud-ordinary" || DynamicCarTrial(request);
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    static readonly Regex Hash = new("^[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant);
    static readonly Regex Nonce = new("^[a-f0-9]{32}$", RegexOptions.CultureInvariant);
    public static T Decode<T>(byte[] bytes, int limit) where T : class
    {
        if (bytes.Length == 0 || bytes.Length > limit) throw new InvalidDataException("Request size refused.");
        // Reject duplicates rather than allowing JSON's last-property-wins rule.
        using var doc = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
        if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected an object.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in doc.RootElement.EnumerateObject())
            if (!names.Add(p.Name)) throw new InvalidDataException("Duplicate request property.");
        return JsonSerializer.Deserialize<T>(bytes, Json) ?? throw new InvalidDataException("Empty request.");
    }
    public static ColdRequest Admit(byte[] bytes, DateTimeOffset now, string marker)
    {
        var r = Decode<ColdRequest>(bytes, 8192);
        if (!(r.Schema == 2 && r.Mode == "raw" && r.ControlsSha256 is null && r.ShimSha256 is null ||
              (r.Schema == 3 && r.Mode == "shim-admission" || r.Schema == 4 && MenuTrial(r) || r.Schema == 5 && r.Mode == "shim-car-trial" || r.Schema == 6 && ForceCapture(r) || DynamicCarTrial(r)) && r.ControlsSha256 is not null && Hash.IsMatch(r.ControlsSha256) && r.ShimSha256 is not null && Hash.IsMatch(r.ShimSha256)) ||
            !ValidNonce(r.Nonce) || marker != r.Nonce || r.ExpiresUtc <= now || r.ExpiresUtc > now.AddMinutes(5))
            throw new InvalidDataException("Cold request identity or expiry refused.");
        foreach (var value in new[] { r.PluginSha256, r.CoreSha256, r.ConfigSha256, r.BindingsSha256, r.NativeSha256 })
            if (value is null || !Hash.IsMatch(value)) throw new InvalidDataException("Missing artifact hash.");
        if (string.IsNullOrEmpty(r.OutputDirectory) || !Path.IsPathFullyQualified(r.OutputDirectory))
            throw new InvalidDataException("Evidence directory must be absolute.");
        if (CarTrial(r) ? string.IsNullOrEmpty(r.StageReference) || !Path.IsPathFullyQualified(r.StageReference) ||
                string.IsNullOrEmpty(r.WorkloadDirectory) || !Path.IsPathFullyQualified(r.WorkloadDirectory) :
                r.StageReference is not null || r.WorkloadDirectory is not null)
            throw new InvalidDataException("Car-trial sources require schema 5/6 and absolute paths.");
        if(DynamicCarTrial(r) ? r.ProfileSha256 is null || !Hash.IsMatch(r.ProfileSha256) ||
            r.WorkloadSha256 is null || !Hash.IsMatch(r.WorkloadSha256) ||
            r.ProfileRevision is null || !Hash.IsMatch(r.ProfileRevision) ||
            r.ApplyReadbackSha256 is null || !Hash.IsMatch(r.ApplyReadbackSha256) :
            r.ProfileSha256 is not null || r.WorkloadSha256 is not null || r.ProfileRevision is not null || r.ApplyReadbackSha256 is not null)
            throw new InvalidDataException("Dynamic car sources require schema 7 and four frozen pins.");
        return r;
    }
    public static RawRequest Command(byte[] bytes, string nonce, int previous, DateTimeOffset created, DateTimeOffset processStart, bool shimAdmission = false,bool menuTrial=false)
    {
        var r = Decode<RawRequest>(bytes, 2048);
        if (!ValidNonce(nonce) || r.Nonce != nonce || r.Sequence != previous + 1 || r.Sequence > 1024 || created <= processStart)
            throw new InvalidDataException("Stale or mismatched command refused.");
        if (r.Operation is not ("status" or "stop") && (menuTrial || !(shimAdmission ? r.Operation is "panel" or "shot" : r.Operation == "raw")))
            throw new InvalidDataException("Operation unavailable in this test mode.");
        if (r.Operation == "raw")
        {
            if (string.IsNullOrEmpty(r.Raw) || !r.Raw.StartsWith("inject raw ", StringComparison.Ordinal) ||
                r.Raw.Length > 512 || r.Raw.Any(c => c < 32 || c > 126)) throw new InvalidDataException("Raw command syntax refused.");
        }
        else if (r.Raw is not null) throw new InvalidDataException("Unexpected raw input.");
        return r;
    }
    static bool ValidNonce(string? s) => s is not null && Nonce.IsMatch(s) && s != new string('0', 32);
}
