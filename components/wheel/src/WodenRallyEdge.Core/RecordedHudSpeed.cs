namespace WodenRallyEdge.Core;

/// <summary>
/// The game's own HUD speed (MainCar.TrueSpeed, recorded as game.trueSpeed) for each trajectory row, read from the sealed
/// source.jsonl for replay.
/// While playing, StagePlayback skips MainCar.FixedUpdate (BeforeCar returns false), so its TrueSpeed write is the only
/// one the game's HUD.FixedUpdate reads. Deriving the speed from the recorded rigidbody velocity (3.6 * |v|, the earlier
/// replay) reads about 22% high against the game's value: about 2.96 * m/s in the sealed Kenya reference, with live 70
/// against derived 105 where they differ most. That reference is hub artifacts/woden-hud-jitter-review-20261013.
/// The rules for a capture:
/// - Recorded before the channel existed, so no row carries it: only then does replay derive the value.
/// - Supplying the channel: every row, aligned with capture.trajectoryIndex, as a finite whole number in range.
///   Anything else is refused, never filled in.
/// </summary>
public sealed class RecordedHudSpeed
{
    public const string Channel = "game.trueSpeed";
    public const int Minimum = -1000, Maximum = 1000;   // km/h on the HUD; the game's int, reverse included
    readonly List<int> _speeds = new();
    int _rows, _supplied;

    /// <summary>One sample row's channels, in file order: its capture.trajectoryIndex must be the next row.</summary>
    public void Add(IReadOnlyDictionary<string, double> channels)
    {
        if (!channels.TryGetValue("capture.trajectoryIndex", out double index) || index != _rows)
            throw new InvalidDataException($"Recorded HUD speed row alignment differs at row {_rows}.");
        if (channels.TryGetValue(Channel, out double speed))
        {
            if (!double.IsFinite(speed) || speed != Math.Truncate(speed) || speed < Minimum || speed > Maximum)
                throw new InvalidDataException($"Recorded HUD speed is malformed at row {_rows}: {speed}.");
            _speeds.Add((int)speed); _supplied++;
        }
        _rows++;
    }

    /// <summary>The per-row table for a trajectory of <paramref name="frames"/> rows. It is Derived when no row supplied the
    /// channel; it throws for a partial supply or a row count that differs.</summary>
    public RecordedHudSpeedTable Finish(int frames)
    {
        if (_rows != frames) throw new InvalidDataException($"Recorded HUD speed rows ({_rows}) differ from the trajectory ({frames}).");
        if (_supplied == 0) return RecordedHudSpeedTable.Derived;
        if (_supplied != _rows) throw new InvalidDataException($"Recorded HUD speed is missing on {_rows - _supplied} of {_rows} rows.");
        return new RecordedHudSpeedTable(_speeds.ToArray());
    }
}

/// <summary>The HUD speed replay writes for each row: the recorded value, or the velocity-derived one only for a capture
/// without the channel (Derived).</summary>
public sealed class RecordedHudSpeedTable
{
    readonly int[]? _speeds;
    public static readonly RecordedHudSpeedTable Derived = new(null);
    internal RecordedHudSpeedTable(int[]? speeds) => _speeds = speeds;
    public bool Recorded => _speeds != null;
    public int Rows => _speeds?.Length ?? 0;

    /// <summary>Row <paramref name="row"/>'s HUD speed. A recorded table never falls back: a row outside it throws.</summary>
    public int Value(int row, int derived)
    {
        if (_speeds == null) return derived;
        if (row < 0 || row >= _speeds.Length) throw new InvalidDataException($"Replay row {row} is outside the recorded HUD speed ({_speeds.Length} rows).");
        return _speeds[row];
    }
}
