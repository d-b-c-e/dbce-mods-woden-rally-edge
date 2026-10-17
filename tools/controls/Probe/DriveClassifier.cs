using WodenRallyEdge.Core;

namespace Woden.ControlsTesting;

public sealed record DriveRow(int Window, long Sequence, Dictionary<string,double> Channels);
public sealed record DriveVerdict(string Verdict, string Reason, IReadOnlyDictionary<string,double> Values);
public sealed record DrivePhase(string Window, DriveVerdict Delivery, DriveVerdict Response);
public sealed record DriveReport(string Recipe, string Outcome, IReadOnlyList<DrivePhase> Phases, string Scope);

// Schema-12 phase verdicts over the probe's own eligible, contiguous rows. Input delivery (raw -> mod -> Controls ->
// native car) and vehicle response (forward speed, yaw rate, deceleration, no drive after release) are separate.
// Missing rows, motion, contact witness or this run's own baseline is INCONCLUSIVE, never a pass. The only fixed
// bounds are existing ones: five delivery rows and the near-rest bound of 1 (m/s, native trueSpeed).
public static class DriveClassifier
{
    public const string Pass="PASS", Fail="FAIL", Inconclusive="INCONCLUSIVE";
    const int Rows=5;
    const double Rest=1;
    static readonly AxisCalibration Pedal=new(0,65535), Brake=new(0,65535,Deadzone:.03);

    public static DriveReport Classify(IReadOnlyList<DriveRow> rows,bool complete,bool released)
    {
        var w=PositivePedalPlan.DriveWindows;
        DriveRow[] In(int i)=>rows.Where(r=>r.Window==i).OrderBy(r=>r.Sequence).ToArray();
        bool Finished(int i)=>i<w.Length-1?rows.Any(r=>r.Window>i):complete;
        // The production crash counter runs after each sample, so a contact on a phase's last row shows on the next row.
        bool? Contact(int i)
        {
            var span=In(i).Concat(In(i+1).Take(1)).Select(r=>V(r,"drive.crashCount")).ToArray();
            if(span.Length==0||span.Any(c=>c is null))return null;
            return span.Max()>span[0];
        }
        var phases=new List<DrivePhase>();

        var throttle=In(1);
        int delivered=throttle.Count(r=>PositivePedalPlan.CandidateDelivered(w[1],r.Channels));
        var noise=throttle.Where(r=>V(r,"motion.velocity.local.z")>Rest).Select(r=>V(r,"motion.angularVelocity.local.y")).ToArray();
        double? straight=noise.Length>=Rows&&noise.All(v=>v is not null)?noise.Max(v=>Math.Abs(v!.Value)):null;
        phases.Add(new(w[1].Name,
            throttle.Length<Rows?Inc("fewer than 5 rows",throttle.Length):delivered>=Rows?Ok("raw/normalized/Controls/native throttle 1, brake released",throttle.Length,("delivered",delivered)):
                Bad("throttle path not delivered",throttle.Length,("delivered",delivered)),
            Motion(1,throttle,(first,last)=>last>Rest&&last>first?Ok("forward speed rose",throttle.Length,("vzFirst",first),("vzLast",last)):
                Bad("no forward motion under throttle",throttle.Length,("vzFirst",first),("vzLast",last)))));

        foreach(int i in new[]{2,3})
        {
            var steer=In(i);int raw=w[i].Steer!.Value;double mapped=PositivePedalPlan.DriveSteerCalibration.Normalize(raw);
            int mod=steer.Count(r=>PositivePedalPlan.DriveDelivered(w[i],r.Channels));
            int native=steer.Count(r=>PositivePedalPlan.DriveDelivered(w[i],r.Channels)&&Near(r,"controls.steer",mapped)&&Near(r,"game.steer",mapped));
            double? gameFirst=steer.Length>0?V(steer[0],"game.steer"):null, gameLast=steer.Length>0?V(steer[^1],"game.steer"):null;
            var delivery=steer.Length<Rows?Inc("fewer than 5 rows",steer.Length):
                mod<Rows?Bad("raw/normalized steer not delivered",steer.Length,("modRows",mod)):
                native>=Rows?Ok("raw/normalized/Controls/native steer equal mapped",steer.Length,("modRows",mod),("nativeRows",native),("mapped",mapped)):
                gameFirst is double a&&gameLast is double b&&Math.Sign(b)==Math.Sign(mapped)&&Math.Abs(b-mapped)<Math.Abs(a-mapped)
                    ?Inc("native steer moved toward but never equaled mapped",steer.Length,("modRows",mod),("nativeRows",native),("mapped",mapped),("gameSteerLast",b))
                    :Bad("native steer did not follow the delivered value",steer.Length,("modRows",mod),("nativeRows",native),("mapped",mapped));
            phases.Add(new(w[i].Name,delivery,Yaw(i,steer,mapped)));
        }

        var brake=In(4);
        int brakeMod=brake.Count(r=>PositivePedalPlan.DriveDelivered(w[4],r.Channels));
        int brakeNative=brake.Count(r=>PositivePedalPlan.DriveDelivered(w[4],r.Channels)&&Near(r,"controls.brake",1)&&Near(r,"game.brake",1));
        bool movingAtBrake=brake.Length>0&&V(brake[0],"game.trueSpeed")>Rest;
        phases.Add(new(w[4].Name,
            brake.Length<Rows?Inc("fewer than 5 rows",brake.Length):!movingAtBrake?Inc("car not moving; no brake pulse is submitted",brake.Length):
                brakeNative>=Rows?Ok("raw/normalized/Controls/native brake 1 with throttle 0",brake.Length,("nativeRows",brakeNative)):
                brakeMod<Rows?Bad("raw/normalized brake not delivered",brake.Length,("modRows",brakeMod)):
                Inc("native brake never equaled 1",brake.Length,("modRows",brakeMod),("nativeRows",brakeNative)),
            Decel(brake)));

        var release=In(5);
        phases.Add(new(w[5].Name,
            released?Ok("injection expired and raw axes returned to their physical baseline",release.Length):
                complete?Bad("raw axes did not return to the physical baseline",release.Length):Inc("release phase not completed",release.Length),
            Released(release)));

        var verdicts=phases.SelectMany(p=>new[]{p.Delivery.Verdict,p.Response.Verdict}).ToArray();
        return new(PositivePedalPlan.DriveRecipe,verdicts.Contains(Fail)?Fail:verdicts.All(v=>v==Pass)?Pass:Inconclusive,phases,
            "muted automated raw injection on the private 04EB tuple; vehicle response only, not physical wheel/pedal, force or mounted-camera acceptance");

        DriveVerdict Motion(int i,DriveRow[] phase,Func<double,double,DriveVerdict> judge)
        {
            if(Unusable(i,phase) is DriveVerdict refused)return refused;
            if(V(phase[0],"motion.velocity.local.z") is not double first||V(phase[^1],"motion.velocity.local.z") is not double last)
                return Inc("forward velocity unavailable",phase.Length);
            return judge(first,last);
        }
        // Yaw rate in the later half of the phase (after the game's own steering lag) against the largest yaw rate seen
        // while driving straight under throttle in this run (Unity: positive = right). Beyond it in the commanded
        // direction passes, beyond it the other way fails, and anything within it is insufficient data.
        DriveVerdict Yaw(int i,DriveRow[] phase,double mapped)
        {
            if(Unusable(i,phase) is DriveVerdict refused)return refused;
            if(phase.Length<2*Rows)return Inc("fewer than 10 rows",phase.Length);
            if(phase.Any(r=>!(V(r,"motion.velocity.local.z")>Rest)))return Inc("not moving forward through the whole phase",phase.Length);
            if(straight is not double floor)return Inc("no straight-line yaw baseline from the throttle phase",phase.Length);
            var later=phase.Skip(phase.Length/2).Select(r=>V(r,"motion.angularVelocity.local.y")).ToArray();
            if(later.Any(v=>v is null))return Inc("yaw rate unavailable",phase.Length);
            double rate=later.Average(v=>v!.Value);
            var values=new List<(string,double)>{("yawRate",rate),("straightBaseline",floor),("mapped",mapped),("vzMin",phase.Min(r=>V(r,"motion.velocity.local.z")!.Value))};
            if(Turn(phase[0],phase[^1]) is double turn)values.Add(("headingChangeDeg",turn));
            double toward=Math.Sign(mapped)*rate;
            return toward>floor?Ok("yaw rate followed the commanded direction beyond the straight-line baseline",phase.Length,values.ToArray()):
                toward< -floor?Bad("yaw rate opposite to the command beyond the straight-line baseline",phase.Length,values.ToArray()):
                Inc("yaw rate within the straight-line baseline",phase.Length,values.ToArray());
        }
        // Average deceleration while braking must exceed the coasting deceleration of the two steer phases.
        DriveVerdict Decel(DriveRow[] phase)
        {
            if(Unusable(4,phase) is DriveVerdict refused)return refused;
            if(!(V(phase[0],"motion.velocity.local.z")>Rest))return Inc("not moving forward at brake start",phase.Length);
            var coast=In(2).Concat(In(3)).ToArray();
            if(!Finished(3)||Contact(2)!=false||Contact(3)!=false||coast.Length<Rows||coast.Any(r=>!(V(r,"motion.velocity.local.z")>Rest)))
                return Inc("no clean coasting baseline from the steer phases",phase.Length);
            if(Rate(coast) is not double coasting||Rate(phase) is not double braking)return Inc("velocity or simulation time unavailable",phase.Length);
            double first=V(phase[0],"motion.velocity.local.z")!.Value, last=V(phase[^1],"motion.velocity.local.z")!.Value;
            return braking>coasting&&last<first?Ok("deceleration exceeded coasting",phase.Length,("decel",braking),("coastDecel",coasting),("vzFirst",first),("vzLast",last)):
                Bad("deceleration did not exceed coasting",phase.Length,("decel",braking),("coastDecel",coasting),("vzFirst",first),("vzLast",last));
        }
        DriveVerdict Released(DriveRow[] phase)
        {
            if(Unusable(5,phase) is DriveVerdict refused)return refused;
            var last=phase[^1];
            if(V(last,"wheelRaw.throttle") is not double throttleRaw||V(last,"wheelRaw.brake") is not double brakeRaw||
                V(phase[0],"motion.velocity.local.z") is not double first||V(last,"motion.velocity.local.z") is not double end)
                return Inc("release channels unavailable",phase.Length);
            double throttleMapped=Pedal.Normalize((int)throttleRaw), brakeMapped=Brake.Normalize((int)brakeRaw);
            if(!Near(last,"game.throttle",throttleMapped)||!Near(last,"game.brake",brakeMapped))
                return Bad("native throttle/brake still differ from the physical mapping after release",phase.Length,("throttleMapped",throttleMapped),("brakeMapped",brakeMapped));
            return end<=Math.Max(first,Rest)&&end>=-Rest?Ok("no drive or reverse after release",phase.Length,("vzFirst",first),("vzLast",end)):
                Bad("car accelerated or reversed after release",phase.Length,("vzFirst",first),("vzLast",end));
        }
        DriveVerdict? Unusable(int i,DriveRow[] phase)
        {
            if(phase.Length<Rows)return Inc("fewer than 5 rows",phase.Length);
            if(!Finished(i))return Inc("phase not completed",phase.Length);
            return Contact(i) switch{null=>Inc("contact witness unavailable",phase.Length),true=>Inc("contact during phase",phase.Length),_=>null};
        }
    }

    static double? V(DriveRow r,string name)=>r.Channels.TryGetValue(name,out double v)&&double.IsFinite(v)?v:null;
    static bool Near(DriveRow r,string name,double wanted)=>V(r,name) is double v&&Math.Abs(v-wanted)<.00001;
    static double? Rate(DriveRow[] phase)=>V(phase[0],"motion.velocity.local.z") is double v0&&V(phase[^1],"motion.velocity.local.z") is double v1&&
        V(phase[0],"drive.simulationSeconds") is double t0&&V(phase[^1],"drive.simulationSeconds") is double t1&&t1>t0?(v0-v1)/(t1-t0):null;
    static double? Heading(DriveRow r)=>V(r,"motion.orientation.x") is double x&&V(r,"motion.orientation.y") is double y&&
        V(r,"motion.orientation.z") is double z&&V(r,"motion.orientation.w") is double q?Math.Atan2(2*(q*y+x*z),1-2*(x*x+y*y))*180/Math.PI:null;
    static double? Turn(DriveRow a,DriveRow b)=>Heading(a) is double h0&&Heading(b) is double h1?Math.IEEERemainder(h1-h0,360):null;
    static DriveVerdict Ok(string reason,int rows,params (string,double)[] values)=>Make(Pass,reason,rows,values);
    static DriveVerdict Bad(string reason,int rows,params (string,double)[] values)=>Make(Fail,reason,rows,values);
    static DriveVerdict Inc(string reason,int rows,params (string,double)[] values)=>Make(Inconclusive,reason,rows,values);
    static DriveVerdict Make(string verdict,string reason,int rows,(string,double)[] values)
    {
        var map=new SortedDictionary<string,double>(StringComparer.Ordinal){["rows"]=rows};
        foreach(var (name,value) in values)if(double.IsFinite(value))map[name]=value;
        return new(verdict,reason,map);
    }
}
