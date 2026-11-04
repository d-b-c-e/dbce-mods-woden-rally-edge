namespace WodenRallyEdge.Core;

/// <summary>Only views the Woden adapter actually supplies. Stock preset indices stay separate; no cockpit is invented.</summary>
public readonly record struct CameraSlot(MountedView Mount, int StockPreset = -1, bool Isometric = false)
{
    public string Key => Mount != MountedView.Stock ? Mount.ToString() :
        (Isometric ? "Isometric" : "Chase") + (StockPreset < 0 ? "" : StockPreset.ToString(System.Globalization.CultureInfo.InvariantCulture));
    public string Name => Mount != MountedView.Stock ? Mount + " Camera" :
        (Isometric ? "Isometric" : "Chase") + (StockPreset < 0 ? "" : " " + (StockPreset + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
    public bool DefaultRealistic => Mount == MountedView.Bumper;
    /// <summary>Late native preset arrays keep restoration pending; no invalid native index is emitted.</summary>
    public int? RestorePreset(int currentPreset, int availableCount)
    {
        int desired = Mount == MountedView.Stock && StockPreset >= 0 ? StockPreset : currentPreset;
        return desired >= 0 && desired < availableCount ? desired : null;
    }
    public static bool TryParse(string value, out CameraSlot camera)
    {
        camera = default;
        if (value is "Bonnet" or "Bumper") { camera = new(value == "Bonnet" ? MountedView.Bonnet : MountedView.Bumper); return true; }
        string prefix = value.StartsWith("Isometric", StringComparison.Ordinal) ? "Isometric" : "Chase";
        if (!value.StartsWith(prefix, StringComparison.Ordinal)) return false;
        string index = value[prefix.Length..];
        int preset = -1;
        if (index.Length > 0 && (!int.TryParse(index, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out preset) || preset < 0)) return false;
        camera = new(MountedView.Stock, preset, prefix == "Isometric");
        return true;
    }
}

/// <summary>Player offsets on the adapter's calculated/native pose. Finite values have no arbitrary position limits.</summary>
public readonly record struct CameraOffset(float Side, float Height, float Forward, float Pitch)
{
    public bool Finite => float.IsFinite(Side) && float.IsFinite(Height) && float.IsFinite(Forward) && float.IsFinite(Pitch);
    public static CameraOffset Between(CameraPose actual, CameraPose start) =>
        new(actual.Side - start.Side, actual.Height - start.Height, actual.Forward - start.Forward, actual.Pitch - start.Pitch);
    public CameraPose On(CameraPose start) => start with
    { Side = start.Side + Side, Height = start.Height + Height, Forward = start.Forward + Forward, Pitch = start.Pitch + Pitch };
    public CameraOffset Adjust(string action, CameraSteps steps)
    {
        steps = steps.Bounded();
        return action switch
        {
            "Camera up" => this with { Height = Height + steps.Move }, "Camera down" => this with { Height = Height - steps.Move },
            "Camera forward" => this with { Forward = Forward + steps.Move }, "Camera back" => this with { Forward = Forward - steps.Move },
            "Camera left" => this with { Side = Side - steps.Move }, "Camera right" => this with { Side = Side + steps.Move },
            "Camera pitch up" => this with { Pitch = Pitch - steps.Tilt }, "Camera pitch down" => this with { Pitch = Pitch + steps.Tilt }, _ => this
        };
    }
}

/// <summary>Truckful's adopted TripleLens ownership rule: remove our last output before the native smoothing input.</summary>
public sealed class CameraLens
{
    private float _native, _applied;
    private bool _owned;
    public void Applied(float native, float applied) { _native = native; _applied = applied; _owned = true; }
    public float Peek(float current) => _owned && Math.Abs(current - _applied) < .0001f ? _native : current;
    public float Remove(float current) { float native = Peek(current); _owned = false; return native; }
    public static float Fov(double start, double offset)
    {
        double value = start + offset;
        if (!double.IsFinite(value) || value <= 1 || value >= 179) throw new ArgumentOutOfRangeException(nameof(offset), "FOV must remain between 1 and 179 degrees.");
        return (float)value;
    }
}
