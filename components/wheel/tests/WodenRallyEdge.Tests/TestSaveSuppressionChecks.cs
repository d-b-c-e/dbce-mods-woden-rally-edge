using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// TestSaveSuppression (sana 98059): which suppress-saves.txt makes one runner launch skip the game's SaveOnEnable saves.
// Only an exact, fresh, short-lived request does; everything else leaves saving as in an ordinary player launch.
internal static class TestSaveSuppressionChecks
{
    internal static void Run(Action<bool, string> check)
    {
        // The central Save policy (sana 98439): an ordinary launch saves; a confirmed runner request or muted stage
        // playback skips it.
        check(TestSaveSuppression.SaveAllowed(stageOutputMuted: false, testRequestActive: false), "an ordinary launch saves as normal");
        check(!TestSaveSuppression.SaveAllowed(stageOutputMuted: false, testRequestActive: true), "a confirmed private check request skips the central Save");
        check(!TestSaveSuppression.SaveAllowed(stageOutputMuted: true, testRequestActive: false), "muted stage playback skips the central Save");
        check(!TestSaveSuppression.SaveAllowed(stageOutputMuted: true, testRequestActive: true), "both together still skip it");

        var now = new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
        string id = Guid.NewGuid().ToString("N");
        string[] Lines(string? expires = null, string? purpose = null, string? identity = null, params string[] extra) =>
            new[] { "id=" + (identity ?? id), "expiresUtc=" + (expires ?? now.AddMinutes(4).ToString("O")), "purpose=" + (purpose ?? TestSaveSuppression.Purpose) }.Concat(extra).ToArray();

        var good = TestSaveSuppression.Parse(Lines(), now);
        check(good.Active && good.Id == id, "an exact, fresh request for the native gearbox check suppresses saves for that launch");
        check(TestSaveSuppression.Parse(Lines().Append("").ToArray(), now).Active, "a trailing blank line is tolerated");
        check(TestSaveSuppression.Parse(Lines(expires: now.AddMinutes(5).ToString("O")), now).Active, "exactly five minutes ahead is still accepted");

        foreach (var (name, lines) in new (string, string[])[] {
            ("expired", Lines(expires: now.AddSeconds(-1).ToString("O"))),
            ("expiring now", Lines(expires: now.ToString("O"))),
            ("more than five minutes ahead", Lines(expires: now.AddMinutes(5).AddSeconds(1).ToString("O"))),
            ("not a round-trip time", Lines(expires: "2026-10-14 12:04")),
            ("another purpose", Lines(purpose: "stage-replay")),
            ("an id that is not 32 hex", Lines(identity: "not-a-guid")),
            ("an extra field", Lines(extra: "action=replay")),
            ("a repeated field", Lines(extra: "id=" + id)),
            ("a missing field", new[] { "id=" + id, "purpose=" + TestSaveSuppression.Purpose }),
            ("a line without =", Lines(extra: "suppress")),
            ("an empty file", Array.Empty<string>()) })
        {
            var request = TestSaveSuppression.Parse(lines, now);
            check(!request.Active && request.Id == null && request.Reason.Length > 0, $"{name} suppresses nothing and says why ({request.Reason})");
        }
    }
}
