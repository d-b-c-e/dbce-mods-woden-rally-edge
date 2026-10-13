using System.Text.Json;
using BepInEx;
using Woden.ControlsTesting;

static partial class Test
{
    static void HudStopOnlyChecks()
    {
        void Begin(string name)
        {
            Reset("hud-stop-"+name);
            Paths.GameRootPath=Dir;
            Set("Request",Get<ColdRequest>("Request") with{Schema=8,Mode="shim-hud-stop-only"});
            string source=Path.Combine(Dir,"BepInEx","WodenRecordings","request-"+new string('a',32),"source.jsonl");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            File.WriteAllText(source,"fixture original source\n");
            WodenRallyEdge.Runtime._recordingPath=source;
            WodenRallyEdge.Runtime._recordLaunch=new {Version=2,Id=Guid.ParseExact(new string('a',32),"N"),
                ExpiresUtc=Get<ColdRequest>("Request").ExpiresUtc,DisableForces=true,CaseId="woden-shim-hud-"+new string('a',32)};
            Call("VerifyRecordingRequest",WodenRallyEdge.Runtime._recordLaunch);
            Call("StartHudStopOnly");
        }
        void RecordingElapsed(){Call("AfterTick");Set("HudRecordingSince",Now-3);Call("AfterTick");}
        Begin("complete");
        Call("AfterTick");Check(Get<double>("HudRecordingSince")>0&&WodenRallyEdge.Runtime.Output.RecordingStatus=="Recording","menu source begins Recording without input");
        Set("HudRecordingSince",Now-3);Call("AfterTick");
        Check(Get<bool>("Armed")&&WodenRallyEdge.Runtime.Output.RecordingStatus=="Completed"&&Get<double>("HudStopped")>0,"probe-direct Stop retains armed post-Stop window");
        using(var stream=new FileStream(Path.Combine(Dir,"observations.tsv"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
        using(var reader=new StreamReader(stream))
            Check(reader.ReadToEnd().Contains("hud-recorder-stop"),"Stop row flushed for post-Stop capture trigger");
        Call("AfterTick");Check(Get<bool>("Armed"),"five-second dwell cannot complete early");
        Set("HudStopped",Now-6);Call("AfterTick");Stopped("HUD menu stop and dwell");
        Check(Get<string>("HudVerdict").StartsWith("passed:")&&Submitted==0,"menu recipe passed without native submission");
        using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir,"result.json"))))
            Check(doc.RootElement.GetProperty("reason").GetString()=="bounded menu recorder stop and dwell completed"&&doc.RootElement.GetProperty("carEvidence").ValueKind==JsonValueKind.Null,"distinct menu-only result without car evidence");
        foreach(string fault in new[]{"car","drops","stop","data-written","counter","source"})
        {
            Begin(fault);
            if(fault=="car")WodenRallyEdge.Runtime.Local=new();
            if(fault=="drops")WodenRallyEdge.Runtime.Output.RecordingDrops=1;
            if(fault=="stop")WodenRallyEdge.Runtime.Output.Complete=false;
            if(fault is "car" or "drops"){Call("AfterTick");Stopped("HUD "+fault);}
            else {
                RecordingElapsed();
                if(fault=="stop")Stopped("HUD failed Stop");
                else {
                    if(fault=="data-written")WodenRallyEdge.Runtime.Output.RecordingRequiresFinalization=false;
                    if(fault=="counter")WodenRallyEdge.Runtime.Output.RecordingCounters.DroppedSamples=1;
                    if(fault=="source")File.Delete(WodenRallyEdge.Runtime._recordingPath!);
                    Set("HudStopped",Now-6);Call("AfterTick");Stopped("HUD "+fault);
                }
            }
            Check(Submitted==0,"failed menu recipe never submits raw input");
        }
        Begin("wrong-case");
        WodenRallyEdge.Runtime._recordLaunch=new {Version=2,Id=Guid.ParseExact(new string('a',32),"N"),
            ExpiresUtc=Get<ColdRequest>("Request").ExpiresUtc,DisableForces=true,CaseId="woden-shim-car-"+new string('a',32)};
        try{Call("VerifyRecordingRequest",WodenRallyEdge.Runtime._recordLaunch);throw new Exception("Accepted car case for HUD mode");}
        catch(InvalidDataException){}
        Call("Close","done");
    }
}
