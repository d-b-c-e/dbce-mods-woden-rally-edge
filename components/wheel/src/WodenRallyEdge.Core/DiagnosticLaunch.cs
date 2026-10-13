using System.Text.Json;

namespace WodenRallyEdge.Core;

/// <param name="InputOrigin">Who drives: "human" or "automated" when the companion states it; omitted means unknown.
/// It is never inferred from DisableForces, injection or focus.</param>
public sealed record DiagnosticLaunch(int Version, Guid Id, DateTimeOffset ExpiresUtc, bool DisableForces = true, string? CaseId = null,
    string? InputOrigin = null)
{
    public const int CurrentVersion = 2;
    public string EffectiveCaseId => Version >= 2 ? CaseId! : "woden-" + Id.ToString("N");
    /// <summary>"human", "automated", or "unknown" when the request did not state it.</summary>
    public string EffectiveInputOrigin => InputOrigin ?? "unknown";
    private static bool Alphanumeric(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';
    public static bool ValidCaseId(string? value) => value is { Length: > 0 and <= 128 } &&
        Alphanumeric(value[0]) && value.All(c => Alphanumeric(c) || c is '.' or '_' or ':' or '@' or '-');
    /// <summary>A stated origin is exactly one of the two values a request may write; "unknown" is only ever implied.</summary>
    public static bool ValidInputOrigin(string? value) => value is null or "human" or "automated";

    public static DiagnosticLaunch? Consume(string path, DateTimeOffset now)
    {
        if (!File.Exists(path)) return null;
        // Claim once before reading. An old/crashed request cannot start repeated recordings.
        string claimed = path + ".consumed-" + Guid.NewGuid().ToString("N");
        File.Move(path, claimed);
        var request = JsonSerializer.Deserialize<DiagnosticLaunch>(File.ReadAllText(claimed));
        if (request == null || request.Version is not (1 or CurrentVersion) || request.Id == Guid.Empty ||
            request.Version >= CurrentVersion && !ValidCaseId(request.CaseId) || !ValidInputOrigin(request.InputOrigin) ||
            request.ExpiresUtc <= now || request.ExpiresUtc > now.AddMinutes(20))
            throw new IOException("Invalid or expired diagnostic launch request; prepare a new launch.");
        return request;
    }
}
