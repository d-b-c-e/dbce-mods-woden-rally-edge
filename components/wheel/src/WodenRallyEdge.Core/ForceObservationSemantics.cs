namespace WodenRallyEdge.Core;

public static class ForceObservationSemantics
{
    public const string Model = "woden-force-signal@3";
    /// <summary>Force model version 4: GripSignal (toolkit AxleForceCurve on the rebuilt front lateral force).</summary>
    public const string GripModel = "woden-grip-signal@4";
    /// <summary>Force model version 5: the grip model with Strength scaling only the tyre force (STD-027).</summary>
    public const string GripModelV5 = "woden-grip-signal@5";
    public static string ModelId(int model) => model switch
    {
        3 => Model, GripSignal.CoupledDampingVersion => GripModel, GripSignal.ModelVersion => GripModelV5,
        _ => throw new IOException("Unsupported force model version " + model)
    };
    /// <summary>The force-config artifact version that declares a model: 1 classic, 2 grip v4, 3 grip v5.</summary>
    public static int ConfigVersion(int model) => model switch
    {
        3 => 1, GripSignal.CoupledDampingVersion => 2, GripSignal.ModelVersion => 3,
        _ => throw new IOException("Unsupported force model version " + model)
    };
    public static int ModelForConfigVersion(int version) => version switch
    {
        1 => 3, 2 => GripSignal.CoupledDampingVersion, 3 => GripSignal.ModelVersion,
        _ => throw new IOException("Unsupported force config version " + version)
    };
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
