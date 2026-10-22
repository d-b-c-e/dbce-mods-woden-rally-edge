namespace Woden.ControlsTesting;

public sealed record PedalWindow(string Name, double Seconds, int? Throttle = null, int? Brake = null, int? Handbrake = null, int? TtlMilliseconds = null, int? Steer = null)
{
    public IEnumerable<string> Commands(string device)
    {
        foreach(var (axis,value) in new[]{(0,Steer),(2,Throttle),(5,Brake),(7,Handbrake)})
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
    public const string CandidateRecipe="candidate-rest-throttle-release@1";
    public const int CandidateObservationSeconds=10;
    public static readonly PedalWindow[] CandidateWindows={
        new("candidate-physical",1), new("candidate-throttle",4,65535), new("final-release",1)
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
    public static bool CandidateDelivered(PedalWindow window,IDictionary<string,double> c)
    {
        bool Near(string name,double wanted)=>c.TryGetValue(name,out double v)&&double.IsFinite(v)&&Math.Abs(v-wanted)<.00001;
        if(!c.TryGetValue("wheelRaw.brake",out double raw)||!double.IsFinite(raw)||raw is <0 or >65535||raw!=Math.Truncate(raw))return false;
        double mapped=new WodenRallyEdge.Core.AxisCalibration(0,65535,Deadzone:.03).Normalize((int)raw);
        return Near("wheelInput.brake",mapped)&&Near("controls.brake",mapped)&&Near("game.brake",mapped)&&
            (window.Throttle is null || mapped==0&&Near("wheelRaw.throttle",65535)&&Near("wheelInput.throttle",1)&&Near("controls.throttle",1)&&Near("game.throttle",1))&&
            c.TryGetValue("wheelInput.appliedTicks",out double ticks)&&double.IsFinite(ticks)&&ticks>0;
    }
    public static bool CandidateReady(IDictionary<string,double> c)
    {
        bool Value(string name,double wanted)=>c.TryGetValue(name,out double v)&&double.IsFinite(v)&&v==wanted;
        return new[]{"wheelInput.throttle","controls.throttle","game.throttle","wheelInput.brake","controls.brake","game.brake","wheelInput.handbrake","game.handbrake"}.All(k=>Value(k,0))&&
            Value("game.grounded",1)&&c.TryGetValue("game.trueSpeed",out double speed)&&double.IsFinite(speed)&&Math.Abs(speed)<=1&&
            c.TryGetValue("motion.velocity.local.z",out double forward)&&double.IsFinite(forward)&&Math.Abs(forward)<=1;
    }
    public static bool CandidateForwardResponse(IDictionary<string,double> c)
    {
        bool Value(string name,double wanted)=>c.TryGetValue(name,out double v)&&double.IsFinite(v)&&v==wanted;
        return Value("game.throttle",1)&&Value("game.brake",0)&&Value("game.handbrake",0)&&
            new[]{"fl","fr","rl","rr"}.All(w=>Value("wheel."+w+".brakeTorque",0))&&
            c.TryGetValue("motion.velocity.local.z",out double forward)&&double.IsFinite(forward)&&forward>1;
    }
    // Schema 12 over the same 04EB tuple. Steering raws are the pinned 04EB Steer calibration (Rest6184/Centre33484/
    // End59740, not inverted) at production-normalized -0.25/+0.25; left first because the schema-11 straight run
    // drifted right before its first contact at ~45 m. The brake window reuses the 100 ms pulses that stop at native
    // trueSpeed<=1: a held native brake reverses a stopped car (R9 final-release).
    public const string DriveRecipe="drive-throttle-steer-brake-release@1";
    public static readonly WodenRallyEdge.Core.AxisCalibration DriveSteerCalibration=new(6184,59740,Centre:33484);
    public const int DriveSteerLeft=26659, DriveSteerRight=40048;
    public static readonly PedalWindow[] DriveWindows={
        new("drive-physical",1), new("drive-throttle",1.25,65535),
        new("drive-steer-left",1,Steer:DriveSteerLeft), new("drive-steer-right",1,Steer:DriveSteerRight),
        new("drive-brake",2,0,65535,TtlMilliseconds:100), new("final-release",1.5)
    };
    public static readonly string[] DriveChannels={"wheelRaw.steer","wheelInput.steer","motion.angularVelocity.local.y","drive.simulationSeconds","drive.crashCount"};
    // Live gate: the mod's raw/normalized/applied path only. The game's own steer/brake values may be smoothed; their
    // exact equality is a DriveClassifier delivery verdict, not a reason to abort the recipe.
    public static bool DriveDelivered(PedalWindow window,IDictionary<string,double> c)
    {
        if(window.Throttle==65535)return CandidateDelivered(window,c);
        bool Near(string name,double wanted)=>c.TryGetValue(name,out double v)&&double.IsFinite(v)&&Math.Abs(v-wanted)<.00001;
        bool ticks=c.TryGetValue("wheelInput.appliedTicks",out double t)&&double.IsFinite(t)&&t>0;
        if(window.Steer is int steer)return ticks&&Near("wheelRaw.steer",steer)&&Near("wheelInput.steer",DriveSteerCalibration.Normalize(steer));
        if(window.Brake==65535)return ticks&&Near("wheelRaw.brake",65535)&&Near("wheelInput.brake",1)&&Near("wheelRaw.throttle",0)&&Near("wheelInput.throttle",0);
        return false;
    }
    // Instantaneous native effect counters only; motion verdicts belong to DriveClassifier.
    public static bool DriveEffect(PedalWindow window,IDictionary<string,double> c)
    {
        bool Value(string name,out double v)=>c.TryGetValue(name,out v)&&double.IsFinite(v);
        if(window.Throttle==65535)return CandidateForwardResponse(c);
        if(window.Steer is int steer)
        {
            int sign=Math.Sign(DriveSteerCalibration.Normalize(steer));
            return Value("wheel.fl.steerAngle",out double fl)&&Value("wheel.fr.steerAngle",out double fr)&&Math.Sign(fl)==sign&&Math.Sign(fr)==sign;
        }
        return window.Brake==65535&&Value("game.brake",out double brake)&&brake==1&&Value("motion.velocity.local.z",out double forward)&&forward>1;
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
