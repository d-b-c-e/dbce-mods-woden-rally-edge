using BepInEx.Configuration;
using WodenRallyEdge.Core;
using static WodenRallyEdge.Core.NativeGearboxPreference;

namespace WodenRallyEdge;

// [Input] NativeGearbox (NativeGearboxPreference; sana 94134/94537): which StatsData elements one look at the record
// sets, and the cfg key's default and normalization through the production Settings. The array shapes are the game's
// bool[4] with true = Automatic, as retained in the stage-context fixtures (kenya-ss1: all true; real-s1:
// arcadeAutoTransmission [false,true,true,true]).
internal static class NativeGearboxChecks
{
    static readonly bool[] AllAutomatic = { true, true, true, true }, AllManual = { false, false, false, false };
    static string Writes(Plan p) => string.Join(";", p.Writes.Select(w => $"{w.Field}[{w.Index}]{(w.Before ? 'A' : 'M')}>{(w.After ? 'A' : 'M')}"));

    internal static void Run(Action<bool, string> check)
    {
        // Keep is the default and every other word keeps the game's choice; no write in any case.
        check(Default == "Keep" && Options.SequenceEqual(new[] { "Keep", "Manual", "Automatic" }), "the choices are Keep, Manual and Automatic, Keep by default");
        foreach (var word in new string?[] { null, "", "Keep", "manual", "MANUAL", "automatic", "Shim", " Manual" })
        {
            var keep = Decide(word, 0, false, AllAutomatic, AllAutomatic);
            check(Parse(word) == Choice.Keep && Normalize(word) == "Keep" && keep.Outcome == Outcome.Keep && keep.Writes.Count == 0,
                $"'{word ?? "null"}' keeps the game's own choice and writes nothing");
        }

        // Manual and Automatic, player zero: Auto_Transmission[0] and the Arcade element, as the game's own buttons set them.
        var manual = Decide("Manual", 0, false, AllAutomatic, AllAutomatic);
        check(manual.Outcome == Outcome.Apply && Writes(manual) == "Auto_Transmission[0]A>M;Arcade_Auto_Transmission[0]A>M", "Manual sets both player-zero elements from Automatic");
        var automatic = Decide("Automatic", 0, false, AllManual, AllManual);
        check(automatic.Outcome == Outcome.Apply && Writes(automatic) == "Auto_Transmission[0]M>A;Arcade_Auto_Transmission[0]M>A", "Automatic sets both player-zero elements from Manual");
        check(Decide("Automatic", 0, false, AllAutomatic, AllAutomatic).Outcome == Outcome.Unchanged && Decide("Manual", 0, false, AllManual, AllManual).Writes.Count == 0,
            "a record that already matches is left unchanged, both ways");
        // The real-s1 capture's record: Arcade already Manual, the other modes Automatic. Only the differing element changes.
        var realS1 = Decide("Manual", 0, false, AllAutomatic, new[] { false, true, true, true });
        check(Writes(realS1) == "Auto_Transmission[0]A>M", "a mixed record changes only the element that differs");

        // Selected player slot only: another player's Auto element, never the Arcade entry or a neighbour.
        var player2 = Decide("Manual", 2, false, AllAutomatic, AllAutomatic);
        check(Writes(player2) == "Auto_Transmission[2]A>M", "player two sets Auto_Transmission[2] only");
        check(Decide("Manual", 2, false, AllAutomatic, null).Outcome == Outcome.Apply, "a player other than zero does not wait for the Arcade array");
        foreach (int player in new[] { -1, 4, 9 })
            check(Decide("Manual", player, false, AllAutomatic, AllAutomatic) is { Outcome: Outcome.Refused, Writes.Count: 0 }, $"player index {player} is refused");

        // Tooling and replay own the choice, whatever the record says.
        check(Decide("Manual", 0, true, AllAutomatic, AllAutomatic) is { Outcome: Outcome.Tooling, Writes.Count: 0 }, "stage tooling or replay is never touched");

        // Late-loaded record: waits while either needed array is missing, then applies once both are there.
        check(Decide("Manual", 0, false, null, AllAutomatic).Outcome == Outcome.Wait && Decide("Manual", 0, false, AllAutomatic, null).Outcome == Outcome.Wait &&
            Decide("Manual", 1, false, null, null).Outcome == Outcome.Wait, "a record whose arrays are not loaded yet waits, with no write");
        var looks = new[] { Decide("Manual", 0, false, null, null), Decide("Manual", 0, false, AllAutomatic, null), Decide("Manual", 0, false, AllAutomatic, AllAutomatic) };
        check(looks.Select(p => p.Outcome).SequenceEqual(new[] { Outcome.Wait, Outcome.Wait, Outcome.Apply }) && Writes(looks[2]) == Writes(manual),
            "successive looks at a loading record wait, then plan exactly the loaded record's writes");

        // Malformed arrays are refused, never resized or partly written.
        check(Decide("Manual", 0, false, Array.Empty<bool>(), AllAutomatic) is { Outcome: Outcome.Refused, Writes.Count: 0 }, "an empty Auto_Transmission is refused");
        check(Decide("Manual", 3, false, new[] { true, true, true }, AllAutomatic) is { Outcome: Outcome.Refused, Writes.Count: 0 } p3 && p3.Status.Contains("3 entries", StringComparison.Ordinal),
            "an Auto_Transmission with no entry for the player is refused and says so");
        check(Decide("Manual", 0, false, AllAutomatic, Array.Empty<bool>()) is { Outcome: Outcome.Refused, Writes.Count: 0 }, "an empty Arcade array is refused for player zero, with no write to the other array");
        check(Decide("Manual", 0, false, new[] { true, true, true, true, true, true }, AllAutomatic).Outcome == Outcome.Apply, "a longer array is accepted; only the selected element is planned");

        // Status after a failed write (sana 96081): never "not applied" once a write was attempted.
        var autoWrite = new Write(AutoTransmission, 0, true, false); var arcadeWrite = new Write(ArcadeAutoTransmission, 0, true, false);
        check(Describe(autoWrite) == "Auto_Transmission[0] Automatic -> Manual", "a write is described with its element and both values");
        check(FailedStatus(Array.Empty<Write>(), null, "no record") == "not applied: no record", "a failure before any write is not applied");
        check(FailedStatus(new[] { autoWrite }, arcadeWrite, "read back Automatic") ==
            "incomplete: set Auto_Transmission[0] Automatic -> Manual; Arcade_Auto_Transmission[0] read back Automatic",
            "a second element that reads back wrong reports the first as set and names the second's value");
        check(FailedStatus(Array.Empty<Write>(), autoWrite, "unknown after setter failed") == "incomplete: Auto_Transmission[0] unknown after setter failed",
            "a first setter that threw reports its element unknown, not unapplied");
        check(FailedStatus(new[] { autoWrite }, null, "status failed") == "incomplete: set Auto_Transmission[0] Automatic -> Manual; status failed",
            "a failure after the writes still lists them");

        // The cfg key through the production Settings: section/key, Keep by default, exact words kept, others normalized.
        string dir = Path.Combine(Path.GetTempPath(), "woden-gearbox-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
        try
        {
            var fresh = new Settings(new ConfigFile(Path.Combine(dir, "fresh.cfg"), false));
            check(fresh.NativeGearbox == "Keep", "a fresh config keeps the game's choice");
            foreach (var (written, read) in new[] { ("Manual", "Manual"), ("Automatic", "Automatic"), ("manual", "Keep"), ("Shim", "Keep") })
            {
                string path = Path.Combine(dir, written + ".cfg");
                File.WriteAllText(path, $"[{Section}]\n{Key} = {written}\n");
                var s = new Settings(new ConfigFile(path, false));
                s.Save();
                string saved = File.ReadAllText(path);
                check(s.NativeGearbox == read && saved.Contains($"{Key} = {read}", StringComparison.Ordinal) && saved.Contains($"[{Section}]", StringComparison.Ordinal),
                    $"[{Section}] {Key} = {written} reads and saves as {read}");
            }
        }
        finally { Directory.Delete(dir, true); }
    }
}
