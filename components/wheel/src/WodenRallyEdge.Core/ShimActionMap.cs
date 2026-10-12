namespace WodenRallyEdge.Core;

// ABI 2 values from the pinned shared shim. Never renumber consumer statuses.
public enum ShimStatus : byte { Unbound, Invalid, Unsupported, DeviceMissing, ProductMismatch, ObjectMissing,
    RangeUnqualified, Opening, ReadPending, ReadFailed, Stale, Rearming, Live, TestNeutral }
public readonly record struct ShimAction(float Value, ShimStatus Status, bool Injected = false)
{
    public float Applied => Status == ShimStatus.Live && float.IsFinite(Value) ? Value : 0;
}
public static class ShimActionMap
{
    public const int Abi = 2, Count = 31;
    public static readonly string[] Names = { "steer", "throttle", "brake", "clutch", "handbrake", "shiftUp", "shiftDown", "neutral", "reverse",
        "confirm", "back", "start", "select", "navUp", "navDown", "navLeft", "navRight", "camera", "lookBack", "reset", "horn", "modMenu", "muteFfb",
        "gear1", "gear2", "gear3", "gear4", "gear5", "gear6", "gear7", "gear8" };
    public static readonly IReadOnlyDictionary<string,string> Buttons = new Dictionary<string,string> {
        ["Gear up"]="shiftUp", ["Gear down"]="shiftDown", ["Handbrake"]="handbrake", ["Camera"]="camera", ["Rear view"]="lookBack",
        ["Respawn"]="reset", ["Horn"]="horn", ["Pause"]="start", ["Settings panel"]="modMenu", ["Panic stop"]="muteFfb",
        ["Confirm"]="confirm", ["Back"]="back", ["Menu up"]="navUp", ["Menu down"]="navDown", ["Menu left"]="navLeft", ["Menu right"]="navRight" };
    public static readonly string[] UnsupportedNames = { "clutch", "neutral", "reverse", "select", "gear1", "gear2", "gear3", "gear4", "gear5", "gear6", "gear7", "gear8" };
    public static string Unsupported => string.Concat(UnsupportedNames.Select(a=>a+"=not delivered by Woden's wheel-only shim pilot\n"));
    public static int Index(string id) => Array.IndexOf(Names,id);
    public static float Axis(IReadOnlyList<ShimAction> actions,string id) => actions[Index(id)].Applied;
    // Only clean native producer values may join the shim. On the admitted wheel-only
    // route ReadInputs has no Gamepad source and independently overlays legacy keys.
    public static (float Steer,float Throttle,float Brake) Compose(float steer,float throttle,float brake,float nativeSteer,float nativeThrottle,float nativeBrake)
    {
        static float Safe(float value, float min) => float.IsFinite(value) ? Math.Clamp(value, min, 1) : 0;
        nativeSteer = Safe(nativeSteer, -1);
        return (nativeSteer != 0 ? nativeSteer : Safe(steer, -1), Math.Max(Safe(throttle, 0), Safe(nativeThrottle, 0)),
            Math.Max(Safe(brake, 0), Safe(nativeBrake, 0)));
    }
}

// Consumer context/release gate. Core still owns device availability and rearm.
public sealed class ShimButtonGate
{
    private readonly bool[] _armed = new bool[ShimActionMap.Count], _held = new bool[ShimActionMap.Count], _pressed = new bool[ShimActionMap.Count];
    private readonly HashSet<string> _consumed = new();
    private int _context = int.MinValue;
    public void Observe(IReadOnlyList<ShimAction> actions,int context,bool allowed)
    {
        bool changed=context!=_context; _context=context; _consumed.Clear();
        for(int i=0;i<_held.Length;i++) {
            bool down=actions[i].Applied>.5f;
            bool live=actions[i].Status==ShimStatus.Live;
            if(changed || !allowed || !live) _armed[i]=false;
            if(!down && allowed && live) _armed[i]=true;
            bool held=allowed&&_armed[i]&&down;
            _pressed[i]=held&&!_held[i]; _held[i]=held;
        }
    }
    public bool Read(string label,bool edge)
    {
        if(!ShimActionMap.Buttons.TryGetValue(label,out var action)) return false;
        int index=ShimActionMap.Index(action);
        return edge ? _pressed[index]&&_consumed.Add(label) : _held[index];
    }
}
