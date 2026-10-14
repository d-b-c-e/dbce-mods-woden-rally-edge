namespace WodenRallyEdge.Core;

/// <summary>
/// The rig profile's gearbox as Woden's own Manual/Automatic choice: [Input] NativeGearbox (sana 94134/94537).
/// The game keeps that choice in its save record, StatsData (static Progress.StatsData_), as two bool[4] arrays where
/// true is Automatic:
/// - Arcade_Auto_Transmission[0], set by ArcadeCarSelect.SetTransmission (the Arcade AT/MT buttons);
/// - Auto_Transmission[player], toggled by TransmissionSelect.SwichTrans (the other modes).
/// Neither button saves; the game writes the record with its next own save, and the car takes DriveAssists.Automatic
/// from it at the stage (StageStartup's MapScreen write is the same route).
/// The rules:
/// - Keep (the default) never touches the record.
/// - Manual or Automatic sets exactly the selected player's elements, once per launch, while the first main menu shows a
///   loaded record, as the game's own buttons do. The car-select menu can still change it afterwards.
/// - Stage tooling and replay own the choice; they are never touched.
/// - A record whose arrays are not loaded yet waits. One too short for the player is refused, never resized.
/// </summary>
public static class NativeGearboxPreference
{
    public const string Section = "Input", Key = "NativeGearbox", Default = "Keep";
    public const string AutoTransmission = "Auto_Transmission", ArcadeAutoTransmission = "Arcade_Auto_Transmission";
    public static readonly string[] Options = { "Keep", "Manual", "Automatic" };
    public const string Help = "Keep (default), Manual or Automatic: the game's own transmission choice, set once at the next game start " +
        "on the first main menu. Keep leaves the game's choice alone. The car-select menu can still change it; the game saves it as usual.";

    public enum Choice { Keep, Manual, Automatic }
    public enum Outcome { Keep, Tooling, Wait, Refused, Unchanged, Apply }
    public readonly record struct Write(string Field, int Index, bool Before, bool After);
    public sealed record Plan(Outcome Outcome, IReadOnlyList<Write> Writes, string Status);

    public static string Describe(Write w) => $"{w.Field}[{w.Index}] {Word(w.Before)} -> {Word(w.After)}";
    public static string Word(bool automatic) => automatic ? "Automatic" : "Manual";

    /// <summary>
    /// The status after a write fails (sana 96081): the elements already set, then the one that failed. A setter that
    /// threw leaves its element unknown, so the caller says so in <paramref name="failure"/>. Only a failure before any
    /// write was attempted is "not applied".
    /// </summary>
    public static string FailedStatus(IReadOnlyList<Write> completed, Write? attempted, string failure)
    {
        if (completed.Count == 0 && attempted == null) return "not applied: " + failure;
        var parts = new List<string>();
        if (completed.Count > 0) parts.Add("set " + string.Join(", ", completed.Select(Describe)));
        parts.Add(attempted is { } a ? $"{a.Field}[{a.Index}] {failure}" : failure);
        return "incomplete: " + string.Join("; ", parts);
    }

    /// <summary>Only the exact option words select a gearbox; anything else, including another case, keeps the game's.</summary>
    public static Choice Parse(string? value) => value switch { "Manual" => Choice.Manual, "Automatic" => Choice.Automatic, _ => Choice.Keep };
    public static string Normalize(string? value) => Parse(value).ToString();

    /// <summary>
    /// The writes for one look at the record. A null array is not loaded yet; the caller looks again later. Only a
    /// player-zero launch writes the Arcade element, the single entry its menu uses.
    /// </summary>
    public static Plan Decide(string? setting, int player, bool tooling, bool[]? autoTransmission, bool[]? arcadeAutoTransmission)
    {
        var choice = Parse(setting);
        if (choice == Choice.Keep) return new(Outcome.Keep, Array.Empty<Write>(), "Keep: the game's own transmission choice");
        if (tooling) return new(Outcome.Tooling, Array.Empty<Write>(), "not applied: stage tooling or replay owns the transmission");
        if (player is < 0 or > 3) return new(Outcome.Refused, Array.Empty<Write>(), "not applied: player index " + player + " is outside 0..3");
        if (autoTransmission == null || player == 0 && arcadeAutoTransmission == null)
            return new(Outcome.Wait, Array.Empty<Write>(), "waiting for the game's saved transmission choice");
        if (autoTransmission.Length <= player)
            return new(Outcome.Refused, Array.Empty<Write>(), $"not applied: {AutoTransmission} has {autoTransmission.Length} entries, none for player {player}");
        if (player == 0 && arcadeAutoTransmission!.Length < 1)
            return new(Outcome.Refused, Array.Empty<Write>(), $"not applied: {ArcadeAutoTransmission} is empty");
        bool automatic = choice == Choice.Automatic;
        var targets = new List<Write> { new(AutoTransmission, player, autoTransmission[player], automatic) };
        if (player == 0) targets.Add(new(ArcadeAutoTransmission, 0, arcadeAutoTransmission![0], automatic));
        var writes = targets.Where(t => t.Before != t.After).ToArray();
        string word = automatic ? "Automatic" : "Manual";
        return writes.Length == 0
            ? new(Outcome.Unchanged, writes, word + ": the game's choice already matched")
            : new(Outcome.Apply, writes, word + ": set " + string.Join(", ", writes.Select(w => $"{w.Field}[{w.Index}]")) + " for this start");
    }
}
