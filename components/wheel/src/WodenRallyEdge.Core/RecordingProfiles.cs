using System.Globalization;
using Dbce.Wheel.Recording;

namespace WodenRallyEdge.Core;

/// <summary>
/// Recorder limits. <see cref="Ordinary"/> is the ordinary capture: the outer source.jsonl and the ordinary full
/// session's nested stage copy, which records the same rows for as long. <see cref="Stage"/> is the supervised stage tool.
/// Both share one channel bound above the whole schema (<see cref="TelemetrySample.Add"/> refuses any other channel),
/// so a row is never trimmed to fit.
/// </summary>
/// <remarks>
/// The size cap holds the full 20 minutes at the measured producer rate. That rate is an observation of this game
/// build, not a universal bound:
/// - Physics rows: MainCar.FixedUpdate postfix. sample.simulationSeconds advanced 0.0167 s on every driving gap of two
///   real captures (638/638 and 1398/1398), so 60 Hz. Rows were 8.9 to 9.1 KB on average (at most 9,357 B), with
///   262 to 269 of the 333 schema channels.
/// - Idle rows: Plugin.Update, at most one per 0.1 s when unfocused, paused or without a local car. Both kinds can
///   run while the window is unfocused, so the budget adds them.
/// - Markers are written only on a change and are not in the row budget.
/// The former 64 MiB lasted about 2 to 2.5 minutes of such rows. The cap now budgets the largest possible row (every
/// schema channel, each at the longest finite double text) at <see cref="ProducerHz"/> for the whole duration, plus
/// the header and the recorder's footer reserve: 16,395 B a row and 1.28 GiB for 20 minutes (RecordingCapacityChecks
/// measures it with the real recorder). The cap is 1.75 GiB, DRIVE's figure, still below 2 GiB. Files grow only as
/// written: 20 minutes of measured rows is about 0.65 GB. Above about 95 rows a second of the largest row the cap would
/// stop first, with the fileSizeLimit stop reason. The stage profile is unchanged.
/// </remarks>
public static class RecordingProfiles
{
    public const int MaxChannels = 512;
    /// <summary>Measured fixed physics step of 1/60 s; one row per step.</summary>
    public const int PhysicsHz = 60;
    /// <summary>The idle path writes at most one row per 0.1 s.</summary>
    public const int IdleHz = 10;
    /// <summary>Rows per second the ordinary cap budgets.</summary>
    public const int ProducerHz = PhysicsHz + IdleHz;

    public static RecordingOptions Ordinary() => new() { QueueCapacity = 512, MaxDurationSeconds = 1200, MaxFileBytes = 1792L * 1024 * 1024, MaxChannelsPerSample = MaxChannels };

    public static RecordingOptions Stage() => new() { QueueCapacity = 512, MaxDurationSeconds = 1801, MaxFileBytes = 128L * 1024 * 1024, MaxChannelsPerSample = MaxChannels };

    /// <summary>The ordinary limits as the panel states them, read from <see cref="Ordinary"/>.</summary>
    public static string OrdinaryLimit
    {
        get { var o = Ordinary(); return (o.MaxDurationSeconds / 60).ToString("0", CultureInfo.InvariantCulture) + " minutes / " + Size(o.MaxFileBytes); }
    }

    public static string Size(long bytes) => bytes >= 1L << 30
        ? (bytes / (double)(1L << 30)).ToString("0.##", CultureInfo.InvariantCulture) + " GiB"
        : (bytes / (double)(1L << 20)).ToString("0.0", CultureInfo.InvariantCulture) + " MiB";
}
