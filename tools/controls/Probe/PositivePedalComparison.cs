using System.Globalization;
using System.Text.Json;

namespace Woden.ControlsTesting;

// Explicit schema-10 comparison / schema-11 candidate-forward only. Same resident injector and GameSampler;
// no device open/poll, binding Apply, second reader, body/gear write or output arm.
public sealed partial class ControlsProbe
{
    static PedalWindow[] PositiveWindows => Protocol.CandidateForward(Request)?PositivePedalPlan.CandidateWindows:PositivePedalPlan.Windows;
    static string PositiveRecipe => Protocol.CandidateForward(Request)?PositivePedalPlan.CandidateRecipe:"physical-zero-B-physical-A@1";
    static bool PositiveReady, PositiveComplete, PositiveInjected, PositiveSawRead, PositivePhysicalReturned, PositiveSealedRace, PositivePzDataAvailable, PositivePulseSkipped;
    static int PositiveCar, PositiveContiguous, PositiveWindow=-1, PositiveReleaseRows, PositiveDeliveryRows;
    static long PositiveSequence=-1;
    static double PositiveSampleAt, PositiveWindowAt, PositiveAppliedTicks, PositiveNextPulse;
    static readonly Dictionary<string,double> PositiveLast=new(StringComparer.Ordinal), PositiveBaseline=new(StringComparer.Ordinal);
    static readonly Dictionary<string,bool> PositiveWindowStarts=new(StringComparer.Ordinal);
    static readonly Dictionary<string,int> PositiveEffects=new(StringComparer.Ordinal);
    static string PositiveVerdict="unrun";
    static void ReadPositiveNativeFields(object car,Dictionary<string,double> channels,List<string> missing)
    {
        if(!Protocol.NativePositive(Request)||Protocol.CandidateForward(Request))return;
        foreach(var (name,property) in new[]{("game.brakeTimer","BrakeTimer"),("game.maxReverseSpeed","Max_Reverse_Speed")})
            try{double v=Convert.ToDouble(Value(car,property));if(!double.IsFinite(v))throw new InvalidDataException();channels[name]=v;}
            catch{missing.Add(name);}
    }
    static void StartPositivePedals(object? launch)
    {
        if(launch is null || Request is null || (Guid)Value(launch,"Id")! != Guid.ParseExact(Request.Nonce,"N") ||
            (string?)Value(launch,"CaseId")!="woden-native-observation-"+Request.Nonce ||
            !(bool)Value(launch,"DisableForces")! || (string?)Value(launch,"InputOrigin")!="automated" ||
            !string.Equals((string?)Value(launch,"NativeObservationSource"),Request.StageReference,StringComparison.OrdinalIgnoreCase) ||
            Convert.ToInt32(Value(launch,"NativeObservationSeconds"))!=(Protocol.CandidateForward(Request)?10:15) ||
            (DateTimeOffset)Value(launch,"ExpiresUtc")!<Request.ExpiresUtc ||
            (DateTimeOffset)Value(launch,"ExpiresUtc")!>DateTimeOffset.UtcNow.AddMinutes(5))
            throw new InvalidDataException("Positive probe requires its exact existing Native observation launch/source/mute.");
        Match(Path.Combine(BepInEx.Paths.GameRootPath,"GameAssembly.dll"),"F422894D8D2B0DF4EDB7E5259E5E60CB8C4F8DEA2E85EBDFC09DD6766349250C");
        var transport=Type("NativeObservation");
        var seconds=transport.GetField("_seconds",All) ?? throw new MissingFieldException("Native observation duration seam changed");
        var first=transport.GetField("_firstAt",All) ?? throw new MissingFieldException("Native observation initial reader seam changed");
        if(Convert.ToDouble(seconds.GetValue(null))!=(Protocol.CandidateForward(Request)?10:15) || !double.IsNaN(Convert.ToDouble(first.GetValue(null))))
            throw new InvalidDataException("Native test requires its unstarted transport; no active interval extension.");
        // Configure ONCE before first reader admission: candidate keeps10,
        // legacy comparison owns22. Never touch the 180s deadline or 8s exit.
        seconds.SetValue(null,(double)Request.PositiveSeconds);
        Patch(Method(transport,"Observe"),nameof(PositiveReaderGate));
        PositiveReady=PositiveComplete=PositiveInjected=PositiveSawRead=PositivePhysicalReturned=PositiveSealedRace=false;PositivePzDataAvailable=!Protocol.CandidateForward(Request);
        PositiveCar=PositiveContiguous=PositiveReleaseRows=PositiveDeliveryRows=0;PositiveWindow=-1;PositiveSequence=-1;
        PositiveLast.Clear();PositiveBaseline.Clear();PositiveWindowStarts.Clear();PositiveEffects.Clear();PositiveVerdict="waiting-for-10-contiguous-RACE-rows";
        ObserveUntil=Deadline;
        Row("positive-mode",JsonSerializer.Serialize(new{schema=Request.Schema,underlyingPassiveSeconds=Protocol.CandidateForward(Request)?10:15,explicitPositiveSeconds=Request.PositiveSeconds,
            startupSeconds=180,normalExitSeconds=8,bindings=Request.BindingsSha256,recipe=PositiveRecipe,causalVerdict="requires actual comparable states/delivery"}));
    }
    static bool PositiveReaderGate(object __0)
    {
        if(!Protocol.NativePositive(Request))return true;
        try { PositiveSealedRace=(bool)Method(Type("StageStartup"),"ValidateRace").Invoke(null,new[]{__0})!;return PositiveReady && PositiveSealedRace; }
        catch(Exception ex){PositiveSealedRace=false;RequestStop("Positive sealed race validation failed: "+ex.Message);return false;}
    }
    static void ObservePositiveSample(object car,long sequence,string state,IDictionary<string,double> values)
    {
        if(!Protocol.NativePositive(Request))return;
        bool eligible=PositiveSealedRace && PositivePedalPlan.Eligible(state,values) && (bool)Runtime.GetProperty("Focused",All)!.GetValue(null)!;
        int id=Convert.ToInt32(car);double now=Clock.Elapsed.TotalSeconds;
        bool contiguous=PositiveSequence<0 || id==PositiveCar && sequence==PositiveSequence+1 && now-PositiveSampleAt<=.1;
        if(!eligible || !contiguous){
            if(PositiveReady && !PositiveComplete)throw new InvalidDataException("Positive comparison lost contiguous/focused RACE eligibility; do not retry.");
            PositiveContiguous=0;if(!eligible)return;
        }
        PositiveCar=id;PositiveSequence=sequence;PositiveSampleAt=now;PositiveLast.Clear();
        foreach(var p in values)if((Array.IndexOf(NativeChannels,p.Key)>=0 || p.Key is "game.brakeTimer" or "game.maxReverseSpeed") && double.IsFinite(p.Value))PositiveLast[p.Key]=p.Value;
        if(PositiveWindow>=0 && PositiveWindow<PositiveWindows.Length && PositiveLast.TryGetValue("wheelInput.appliedTicks",out double ticks)){
            if(ticks>PositiveAppliedTicks && (Protocol.CandidateForward(Request)?PositivePedalPlan.CandidateDelivered(PositiveWindows[PositiveWindow],PositiveLast):PositivePedalPlan.Delivered(PositiveWindows[PositiveWindow],PositiveLast)))PositiveDeliveryRows++;PositiveAppliedTicks=ticks;
        }
        if(!PositiveReady && ++PositiveContiguous>=10){PositiveReady=true;PositiveVerdict="collecting";PositiveWindowAt=now;}
        if(PositiveWindow>=0 && PositiveWindow<PositiveWindows.Length){
            var window=PositiveWindows[PositiveWindow];
            if(!Protocol.CandidateForward(Request) && (PositiveWindow is 0 or 1) && !PositivePedalPlan.CausalDataAvailable(PositiveLast))PositivePzDataAvailable=false;
            if(Protocol.CandidateForward(Request)?window.Throttle==65535&&PositivePedalPlan.CandidateForwardResponse(PositiveLast):PositivePedalPlan.NativeEffect(window,PositiveLast))PositiveEffects[window.Name]=PositiveEffects.TryGetValue(window.Name,out int n)?n+1:1;
        }
        if(PositiveWindow==0)foreach(var p in PositiveLast)PositiveBaseline[p.Key]=p.Value;
        if(PositiveWindow==PositiveWindows.Length-1 && PositiveSawRead && !PositiveInjected){
            PositiveReleaseRows++;
            bool Same(string name,double tolerance)=>PositiveBaseline.TryGetValue(name,out double before)&&PositiveLast.TryGetValue(name,out double after)&&Math.Abs(before-after)<=tolerance;
            PositivePhysicalReturned=PositiveReleaseRows>=10 && Same("wheelRaw.throttle",1024) && Same("wheelRaw.brake",1024) && Same("wheelRaw.handbrake",1024);
        }
    }
    static void TickPositivePedals(double now)
    {
        if(!Protocol.NativePositive(Request) || !PositiveReady || PositiveComplete)return;
        if(now-PositiveSampleAt>.1 || !(bool)Runtime.GetProperty("Focused",All)!.GetValue(null)!)
            throw new InvalidDataException("Positive comparison current RACE/focus witness stale; no raw submission.");
        // Existing08 already proves passive reverse. End this tiny P0 baseline
        // as soon as three actual reverse-effect rows exist; spend time on positive delivery.
        if(!Protocol.CandidateForward(Request) && PositiveWindow==0 && PositiveEffects.TryGetValue("P0-physical",out int reverseRows) && reverseRows>=3)
            PositiveWindowAt=now-PositiveWindows[0].Seconds;
        if(PositiveWindow>=0 && now-PositiveWindowAt<PositiveWindows[PositiveWindow].Seconds){
            var current=PositiveWindows[PositiveWindow];
            if(current.TtlMilliseconds==100){
                if(PositivePulseSkipped || !PositivePedalPlan.ShouldBrakePulse(PositiveLast)){
                    Row("positive-brake-release-native-reverse-boundary",JsonSerializer.Serialize(new{index=PositiveWindow,rawState=PositiveLast,expiryMilliseconds=100}));
                    PositiveWindowAt=now-current.Seconds;
                }else if(now>=PositiveNextPulse){SubmitPositiveWindow(current);PositiveNextPulse=now+.075;return;}
                else return;
            }else return;
        }
        if(PositiveWindow>=0 && PositiveWindows[PositiveWindow].Commands(PositivePedalPlan.Device).Any() && PositiveWindows[PositiveWindow].TtlMilliseconds is null && PositiveDeliveryRows<5)
            throw new InvalidDataException("Positive window lacks raw/normalized/applied delivery: "+PositiveWindows[PositiveWindow].Name);
        if(PositiveWindow>=0)Row("positive-window-end",JsonSerializer.Serialize(new{index=PositiveWindow,deliveryRows=PositiveDeliveryRows,nativeEffectRows=PositiveEffects.TryGetValue(PositiveWindows[PositiveWindow].Name,out int effects)?effects:0,end=PositiveLast}));
        PositiveDeliveryRows=0;
        if(++PositiveWindow>=PositiveWindows.Length){
            PositiveComplete=true;PositivePhysicalReturned=PositivePhysicalReturned && Native!.RefreshStatus() && Native.Status.Contains("no sample running",StringComparison.Ordinal);PositiveVerdict=PositivePhysicalReturned?"collection-complete-analysis-required":"unavailable-final-physical-return";
            Row("positive-complete",JsonSerializer.Serialize(new{PositiveVerdict,PositivePhysicalReturned,PositiveReleaseRows,PositivePzDataAvailable,nativeStatus=Native!.Status,nativeEffects=PositiveEffects,windowStarts=PositiveWindowStarts,physicalReleaseKnown=false}));Trace!.Flush();return;
        }
        var window=PositiveWindows[PositiveWindow];PositiveWindowAt=now;
        if(Protocol.CandidateForward(Request)&&window.Throttle==65535&&!PositivePedalPlan.CandidateReady(PositiveLast))
            throw new InvalidDataException("Candidate throttle requires current neutral pedals, grounded near-rest car and exact-zero brake; no submission.");
        PositiveAppliedTicks=PositiveLast.TryGetValue("wheelInput.appliedTicks",out double ticks)?ticks:0;
        PositiveWindowStarts[window.Name]=!Protocol.CandidateForward(Request)&&PositivePedalPlan.ComparableStart(PositiveLast);
        PositivePulseSkipped=window.TtlMilliseconds==100 && !PositivePedalPlan.ShouldBrakePulse(PositiveLast);
        if(!PositivePulseSkipped)SubmitPositiveWindow(window);
        PositiveNextPulse=now+.075;
        Row("positive-window",JsonSerializer.Serialize(new{index=PositiveWindow,window=window.Name,seconds=window.Seconds,
            commands=RawCommands,car=PositiveCar,sequence=PositiveSequence,start=PositiveLast,PositivePulseSkipped,
            releaseMeans="no commands; per-object TTL expiry, not manufactured physical rest",comparableStart=PositiveWindowStarts[window.Name],causalVerdict=Protocol.CandidateForward(Request)?"inspect actual motion trend and brake release; motorTorque is not propulsion":"unclassified; service-brake pulse is NOT a guaranteed stop/reset"}));Trace!.Flush();
    }
    static void SubmitPositiveWindow(PedalWindow window)
    {
        // Raw acceptance is only a request. Later raw-read/native-car rows must
        // prove every object, mapping, applied ticks and actual game response.
        foreach(string command in window.Commands(PositivePedalPlan.Device)){
            if(++RawCommands>256)throw new InvalidDataException("Positive command limit");
            bool accepted=Native!.SubmitRaw(command,out string reason);
            Row(accepted?"positive-request-accepted":"positive-request-refused",JsonSerializer.Serialize(new{sequence=RawCommands,window=window.Name,command,reason}));
            if(!accepted || !Native.Armed)throw new InvalidDataException("Positive raw command failed/uncertain; no retry: "+reason);
        }
    }
    static void ObservePositiveRead(object? guid,bool injected)
    {
        if(!Protocol.NativePositive(Request) || !Guid.TryParse(guid?.ToString(),out var id) || id!=Guid.Parse(PositivePedalPlan.Device))return;
        PositiveSawRead=true;PositiveInjected=injected;
    }
}
