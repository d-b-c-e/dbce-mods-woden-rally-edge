namespace WodenRallyEdge.Core;

/// <summary>
/// H-pattern shifting on Woden's manual gearbox (owner work stream, 2026-10-06; ROADMAP item 3). The game has gears
/// 1..N, no neutral and no reverse gear: it reverses by braking at a standstill (TransmissionSys.Max_Reverse_Speed).
/// So a held gate steps the game's own Shift up / Shift down actions, one press at a time and never during a gear
/// change, until the gear matches; out of gear cuts the drive (emulated neutral); R selects first gear and swaps the
/// pedals so the throttle reverses. Pure: the plugin supplies the gate, the car's gear state and the clock.
/// </summary>
public sealed class HPatternShifter
{
    /// <summary>Gate value for reverse; 0 is out of gear.</summary>
    public const int Reverse = -1;
    public const int MaxGates = 6;
    /// <summary>One synthetic shift press, then a release before the next, so the game sees separate presses.</summary>
    public const double PressSeconds = .06, ReleaseSeconds = .06;

    private bool _pressing;
    private int _direction;
    private double _until = double.NegativeInfinity;

    public string Status { get; private set; } = "Bind the shifter gates below";

    /// <summary>The gear a gate selects on a car with <paramref name="gears"/> forward gears (0 = none).</summary>
    public static int Target(int gate, int gears) => gate == Reverse ? 1 : gate >= 1 ? Math.Min(gate, Math.Max(1, gears)) : 0;

    /// <summary>The shift action to hold this frame.</summary>
    public (bool Up, bool Down) Step(int gate, int gear, int gears, bool automatic, bool changing, double now)
    {
        if (automatic) { Stop(); Status = "Set the game's transmission to manual to use the H-pattern"; return (false, false); }
        int target = Target(gate, gears);
        if (target == 0) { Stop(); Status = "Out of gear (drive cut)"; return (false, false); }
        if (_pressing)
        {
            if (now < _until) return (_direction > 0, _direction < 0);
            _pressing = false; _until = now + ReleaseSeconds;
            return (false, false);
        }
        if (now < _until || changing) return (false, false);
        if (gear == target) { Status = gate == Reverse ? "R: throttle reverses" : $"Gear {target}"; return (false, false); }
        _direction = target > gear ? 1 : -1; _pressing = true; _until = now + PressSeconds;
        Status = $"Shifting to {(gate == Reverse ? "R" : target.ToString())}";
        return (_direction > 0, _direction < 0);
    }

    /// <summary>
    /// Pedals for the gate: out of gear drops the throttle (no drive); R swaps them, because Woden reverses on the brake
    /// at a standstill and brakes a reversing car with the throttle.
    /// </summary>
    public static (float Throttle, float Brake) Pedals(int gate, float throttle, float brake) =>
        gate == Reverse ? (brake, throttle) : gate == 0 ? (0f, brake) : (throttle, brake);

    public void Stop() { _pressing = false; _direction = 0; _until = double.NegativeInfinity; }
}
