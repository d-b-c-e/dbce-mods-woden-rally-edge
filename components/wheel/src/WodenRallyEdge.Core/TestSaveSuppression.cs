using System.Globalization;

namespace WodenRallyEdge.Core;

/// <summary>
/// One test launch that must not write the game's save (sana 98059): the 2026-10-14 native gearbox check saved
/// Supersave0.sav on reaching MapScreen, and Steam Cloud uploaded it, which a local restore cannot undo.
/// The qualification runner writes suppress-saves.txt in the stage control folder before its launch and removes it
/// after. The plugin reads it once at start. Once its prefix on the game's central Save is confirmed installed, that
/// Save is skipped for the launch. MapScreen.OnEnable, StagePresentation, ResultScreen and others call it directly;
/// SaveOnEnable does not cover them. Muted stage playback skips the same central Save (SaveAllowed).
/// It is deliberately not stage tooling, so the native gearbox adapter still acts. An ordinary player launch never has
/// the file. A stale, oversize, malformed, repeated-key or wrong-purpose file suppresses nothing, which is the same
/// fail-safe direction as an ordinary launch; the runner then refuses because the plugin never confirms suppression.
/// </summary>
public static class TestSaveSuppression
{
    public const string FileName = "suppress-saves.txt", Purpose = "native-gearbox-check";
    public const int MaxBytes = 8192;
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromMinutes(5);

    public readonly record struct Request(bool Active, string? Id, string Reason);

    /// <summary>
    /// Whether the game's central Save may run (sana 98439). It is skipped while muted stage playback owns the launch,
    /// which no earlier guard covered on the MapScreen, StagePresentation or ResultScreen routes, or while one confirmed
    /// runner request is active. Otherwise it runs, as in every ordinary launch.
    /// </summary>
    public static bool SaveAllowed(bool stageOutputMuted, bool testRequestActive) => !stageOutputMuted && !testRequestActive;

    /// <summary>The request in <paramref name="lines"/> (id=32 hex, expiresUtc=round-trip time in (now, now+5 min],
    /// purpose=native-gearbox-check, nothing else), as of <paramref name="now"/>.</summary>
    public static Request Parse(IReadOnlyList<string> lines, DateTimeOffset now)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in lines)
        {
            if (line.Length == 0) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0 || !fields.TryAdd(line[..eq], line[(eq + 1)..])) return new(false, null, "malformed or repeated field");
        }
        if (fields.Count != 3 || !fields.TryGetValue("id", out var id) || !fields.TryGetValue("expiresUtc", out var expiry) || !fields.TryGetValue("purpose", out var purpose))
            return new(false, null, "needs exactly id, expiresUtc and purpose");
        if (purpose != Purpose) return new(false, null, "unknown purpose");
        if (!Guid.TryParseExact(id, "N", out _)) return new(false, null, "id is not 32 hex digits");
        if (!DateTimeOffset.TryParseExact(expiry, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var until))
            return new(false, null, "expiresUtc is not a round-trip time");
        if (until <= now) return new(false, null, "expired");
        if (until > now + MaxLifetime) return new(false, null, "expires more than 5 minutes ahead");
        return new(true, id, "active until " + until.ToString("O", CultureInfo.InvariantCulture));
    }
}
