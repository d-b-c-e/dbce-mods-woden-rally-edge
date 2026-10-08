namespace WodenRallyEdge.Core;

/// <summary>
/// STD-022: triple screens as one three-way choice, Off / Surround / Separate monitors, in the same form as the other
/// mods (iRacing Arcade's TripleLayoutChoice). Surround and Separate both use the Auto views; only Separate opens the
/// borderless span over separate monitors, applied at the next game start. Off also ends the span then.
/// </summary>
public static class TripleLayoutChoice
{
    public static readonly string[] Labels = { "Off", "Surround", "Separate monitors" };
    public static int Current(bool off, bool span) => off ? 0 : span ? 2 : 1;
    public static (bool Off, bool Span) Apply(int choice) => (choice == 0, choice == 2);
}
