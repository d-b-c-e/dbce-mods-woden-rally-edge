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
        new("P0-physical",.25), new("Z-zero-brake",.5,Brake:0), new("B-zero-brake",4,65535,0),
        new("service-brake-after-B",2,0,65535,TtlMilliseconds:100), new("release-stop-B",.5),
        new("P1-physical",.25), new("A-physical-brake",4,65535),
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
    public static bool ShouldBrakePulse(IDictionary<string,double> c) =>
        c.TryGetValue("game.trueSpeed",out double speed)&&double.IsFinite(speed)&&speed>1; // Native branch is SIGNED, including all negative speeds.
    public static bool CausalDataAvailable(IDictionary<string,double> c)
    {
        bool Value(string name,out double value)=>c.TryGetValue(name,out value)&&double.IsFinite(value);
        return Value("game.brakeTimer",out _) && Value("game.maxReverseSpeed",out _) && Value("game.trueSpeed",out _) &&
            Value("game.grounded",out double grounded)&&grounded==1 && Value("game.handbrake",out double hb)&&hb==0 &&
            Value("motion.orientation.x",out double x)&&Value("motion.orientation.z",out double z)&&1-2*(x*x+z*z)>.5 &&
            new[]{"fl","fr","rl","rr"}.All(w=>Value("wheel."+w+".motorTorque",out _));
    }
    public static bool ComparableStart(IDictionary<string,double> c)
    {
        bool Value(string name,out double value)=>c.TryGetValue(name,out value)&&double.IsFinite(value);
        return CausalDataAvailable(c) && Value("game.trueSpeed",out double speed)&&Math.Abs(speed)<=1 &&
            Value("game.gear",out double gear)&&gear>=1 && Value("game.automatic",out double automatic)&&automatic==1 &&
            Value("game.shifting",out double shifting)&&shifting==0 &&
            (c["game.brakeTimer"]<=30 || new[]{"fl","fr","rl","rr"}.Sum(w=>c["wheel."+w+".motorTorque"])>=0);
    }
    public static bool NativeEffect(PedalWindow window,IDictionary<string,double> c)
    {
        bool Value(string name,out double value)=>c.TryGetValue(name,out value)&&double.IsFinite(value);
        bool Torque(string[] wheels,string kind,out double sum){sum=0;foreach(string w in wheels){if(!Value("wheel."+w+"."+kind,out double v))return false;sum+=v;}return true;}
        if(window.Handbrake==65535)return Value("game.handbrake",out double hb)&&hb==1&&Torque(new[]{"rl","rr"},"brakeTorque",out double rear)&&rear>0;
        if(window.Throttle==65535)return Value("game.throttle",out double throttle)&&throttle>.9&&Torque(new[]{"fl","fr","rl","rr"},"motorTorque",out double drive)&&drive>0;
        if(window.Brake==65535)return Torque(new[]{"fl","fr","rl","rr"},"brakeTorque",out double brake)&&brake>0;
        if(window.Brake==0)return Value("game.brake",out double zero)&&Math.Abs(zero)<.00001&&Torque(new[]{"fl","fr","rl","rr"},"motorTorque",out double motor)&&motor>=0;
        return CausalDataAvailable(c)&&c["game.trueSpeed"]<=1&&c["game.brakeTimer"]>30&&Torque(new[]{"fl","fr","rl","rr"},"motorTorque",out double reverse)&&reverse<0;
    }
    public static bool Eligible(string state, IDictionary<string,double> c)
    {
        bool Equal(string name,double value)=>c.TryGetValue(name,out double actual)&&double.IsFinite(actual)&&actual==value;
        return state=="driving" && Equal("game.status",1) && Equal("game.locked",0) && Equal("game.paused",0) &&
            Equal("game.replay",0) && Equal("game.respawning",0) && Equal("game.automatic",1);
    }
}
