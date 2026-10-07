namespace WodenRallyEdge.Core;

public static class ForceObservationSemantics
{
    public const string Model = "woden-force-signal@3";
    /// <summary>Force model version 4: GripSignal (toolkit AxleForceCurve on the rebuilt front lateral force).</summary>
    public const string GripModel = "woden-grip-signal@4";
    public const long TicksPerSecond = 10_000_000;

    public static int ModelReason(string reason) => reason switch
    {
        "estimated tyre signal" => 0,
        "low-speed fade" => 1,
        "inactive" or "paused" or "replay" or "respawning" or "unfocused" or "no-local-car" or "invalid-motion" => 10,
        "recorded-discontinuity" => 11,
        "motion unavailable" => 12,
        "reverse: signal not validated" => 13,
        "invalid tuning" => 14,
        "front contact unavailable" => 15,
        "invalid front contact" => 16,
        "front wheels airborne" => 17,
        "force sample gap" => 18,
        "grip model" => 2,
        "measuring front load" => 3,
        "friction curve unavailable" => 19,
        _ => 99
    };

    public static int Gate(string gate) => gate switch
    {
        "active" => 0,
        "diagnostic-force-disabled" => 1,
        "saved-off" => 2,
        "faulted" => 3,
        "settings-open" => 4,
        "unfocused" => 5,
        "stock-owner-unready" => 6,
        "camera-not-owned" => 7,
        "wheel-input-unavailable" => 8,
        "model-invalid" => 9,
        "wheel-not-connected" => 10,
        "write-failed" => 11,
        _ => 99
    };
}
