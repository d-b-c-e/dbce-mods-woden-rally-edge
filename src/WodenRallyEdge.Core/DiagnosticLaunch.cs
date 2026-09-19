using System.Text.Json;

namespace WodenRallyEdge.Core;

public sealed record DiagnosticLaunch(int Version, Guid Id, DateTimeOffset ExpiresUtc, bool DisableForces = true)
{
    public static DiagnosticLaunch? Consume(string path, DateTimeOffset now)
    {
        if (!File.Exists(path)) return null;
        // Claim once before reading. An old/crashed request cannot start repeated recordings.
        string claimed = path + ".consumed-" + Guid.NewGuid().ToString("N");
        File.Move(path, claimed);
        var request = JsonSerializer.Deserialize<DiagnosticLaunch>(File.ReadAllText(claimed));
        if (request == null || request.Version != 1 || request.Id == Guid.Empty ||
            request.ExpiresUtc <= now || request.ExpiresUtc > now.AddMinutes(20))
            throw new IOException("Invalid or expired diagnostic launch request; prepare a new launch.");
        return request;
    }
}
