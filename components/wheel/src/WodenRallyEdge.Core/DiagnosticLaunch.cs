using System.Text.Json;

namespace WodenRallyEdge.Core;

public sealed record DiagnosticLaunch(int Version, Guid Id, DateTimeOffset ExpiresUtc, bool DisableForces = true, string? CaseId = null)
{
    public const int CurrentVersion = 2;
    public string EffectiveCaseId => Version >= 2 ? CaseId! : "woden-" + Id.ToString("N");
    private static bool Alphanumeric(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';
    public static bool ValidCaseId(string? value) => value is { Length: > 0 and <= 128 } &&
        Alphanumeric(value[0]) && value.All(c => Alphanumeric(c) || c is '.' or '_' or ':' or '@' or '-');

    public static DiagnosticLaunch? Consume(string path, DateTimeOffset now)
    {
        if (!File.Exists(path)) return null;
        // Claim once before reading. An old/crashed request cannot start repeated recordings.
        string claimed = path + ".consumed-" + Guid.NewGuid().ToString("N");
        File.Move(path, claimed);
        var request = JsonSerializer.Deserialize<DiagnosticLaunch>(File.ReadAllText(claimed));
        if (request == null || request.Version is not (1 or CurrentVersion) || request.Id == Guid.Empty ||
            request.Version >= CurrentVersion && !ValidCaseId(request.CaseId) ||
            request.ExpiresUtc <= now || request.ExpiresUtc > now.AddMinutes(20))
            throw new IOException("Invalid or expired diagnostic launch request; prepare a new launch.");
        return request;
    }
}
