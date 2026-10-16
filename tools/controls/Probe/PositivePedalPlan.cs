namespace Woden.ControlsTesting;

public sealed record PedalWindow(string Name, double Seconds, int? Throttle = null, int? Brake = null, int? Handbrake = null, int? TtlMilliseconds = null)
{
    public IEnumerable<string> Commands(string device)
    {
        foreach(var (axis,value) in new[]{(2,Throttle),(5,Brake),(7,Handbrake)})
            if(value is int raw)yield return $"inject raw axis {axis} dev={device} value={raw} range=0..65535 ms={TtlMilliseconds ?? (int)(Seconds*1000)}";
    }
}
public static class PositivePedalPlan
{
    public const int ObservationSeconds=22;
    public const string Device="{d71b8350-61b7-11f1-8001-444553540000}";
    // Release windows submit NOTHING. Object TTL expiry returns all uninjected objects to their physical values.
    public static readonly PedalWindow[] Windows={
        new("P0-physical",1.5), new("Z-zero-brake",2,Brake:0), new("B-zero-brake",2,65535,0),
        new("service-brake-after-B",2,0,65535,TtlMilliseconds:100), new("release-stop-B",.5),
        new("P1-physical",1.5), new("A-physical-brake",2,65535),
        new("service-brake-after-A",2,0,65535,TtlMilliseconds:100), new("release-stop-A",.5),
        new("handbrake",1.5,Brake:0,Handbrake:65535), new("final-release",1)
    };
    public static bool Delivered(PedalWindow window, IDictionary<string,double> c)
    {
        bool Near(string name,double value)=>c.TryGetValue(name,out double actual)&&double.IsFinite(actual)&&Math.Abs(actual-value)<.00001;
        bool Axis(string raw,string normalized,int? wanted)=>wanted is null || Near("wheelRaw."+raw,wanted.Value) && Near("wheelInput."+normalized,wanted.Value/65535.0);
        bool brakeMapped=c.TryGetValue("wheelRaw.brake",out double rawBrake)&&double.IsFinite(rawBrake)&&Near("wheelInput.brake",rawBrake/65535.0)&&Near("controls.brake",rawBrake/65535.0);
        return brakeMapped && Axis("throttle","throttle",window.Throttle) && Axis("brake","brake",window.Brake) && Axis("handbrake","handbrake",window.Handbrake) &&
            (window.Throttle is null || Near("controls.throttle",window.Throttle.Value/65535.0)) &&
            (window.Brake is null || Near("controls.brake",window.Brake.Value/65535.0)) &&
            c.TryGetValue("wheelInput.appliedTicks",out double ticks) && double.IsFinite(ticks) && ticks>0;
    }
    public static bool ComparableStart(IDictionary<string,double> c)
    {
        bool Value(string name,out double value)=>c.TryGetValue(name,out value)&&double.IsFinite(value);
        return Value("game.brakeTimer",out _) && Value("game.grounded",out double grounded)&&grounded==1 &&
            Value("game.handbrake",out double hb)&&hb==0 &&
            Value("motion.orientation.x",out double x)&&Value("motion.orientation.z",out double z)&&1-2*(x*x+z*z)>.5 &&
            Value("game.trueSpeed",out double speed)&&Math.Abs(speed)<=1 && Value("game.gear",out double gear)&&gear>=1 &&
            Value("game.automatic",out double automatic)&&automatic==1 && Value("game.shifting",out double shifting)&&shifting==0;
    }
    public static bool Eligible(string state, IDictionary<string,double> c)
    {
        bool Equal(string name,double value)=>c.TryGetValue(name,out double actual)&&double.IsFinite(actual)&&actual==value;
        return state=="driving" && Equal("game.status",1) && Equal("game.locked",0) && Equal("game.paused",0) &&
            Equal("game.replay",0) && Equal("game.respawning",0) && Equal("game.automatic",1);
    }
}
