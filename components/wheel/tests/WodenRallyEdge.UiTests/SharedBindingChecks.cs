using System.Text;
using Dbce.Wheel.Input;
using Dbce.Wheel.Shim;
using UnityEngine;
using WodenRallyEdge;

internal static class SharedBindingChecks
{
    internal static void SaveStatusChecks(string directory,Action<bool,string> check)
    {
        Guid instance=Guid.NewGuid(),product=Guid.NewGuid();double now=0;bool menu=true;
        string path=Path.Combine(directory,"binding-save-status.ini");
        var steer=ControlBinding.FromCapture("steer",ControlKind.Axis,0,1,32768,instance,product,"Fixture wheel",0,65535);
        string original="; owner note\r\n[Controls]\r\nSchema = 1\r\nProfile = Fixture\r\nRevision = original\r\nTransmission = Sequential\r\nsteer = "+steer+"\r\n[Other]\r\nKeep = exact\r\n";
        File.WriteAllText(path,original,new UTF8Encoding(false));
        var host=new SharedBindingHost(path,File.ReadAllBytes(path),()=>now,()=>menu);
        host.Observe(new(instance,product,"Fixture wheel","fixture-status",1,1,now,
            new(new[]{32768},new[]{false},Array.Empty<int>()),new[]{0},new[]{65535}));
        host.SetInverted("steer",true);
        File.AppendAllText(path,"; external change\r\n");string external=File.ReadAllText(path);
        check(!host.Save(out var failure)&&failure.Length>0&&host.SaveFailure==failure&&host.HasDraft&&File.ReadAllText(path)==external,
            "actual checked Save refusal retains independent warning/draft and external bytes");
        host.BeginCapture("modMenu",instance.ToString("D"));
        check(host.CapturingAction=="modMenu"&&host.CaptureMessage!=failure&&host.SaveFailure==failure,
            "later capture changes transient text without clearing failed Save");
        string footer=Panel.BindingFooterStatus(host,"Unrelated settings message");
        check(footer.StartsWith(Dbce.Wheel.Ui.BindingPresentation.Footer(true,false,failure))&&footer.Contains("Unrelated settings message"),
            "production footer preserves binding failure independently of unrelated panel status");
        host.CancelCapture();host.Discard();
        check(host.SaveFailure.Length==0&&!host.HasDraft&&Panel.BindingFooterStatus(host,"General status")=="General status",
            "explicit Discard resolves binding warning without retaining stale global panel failure");
        check(!host.Rows.Single(r=>r.Action=="steer").ActiveInverted,
            "failed Save/capture/Discard leave active startup binding unchanged");
        File.WriteAllText(path,original,new UTF8Encoding(false));host.SetInverted("steer",true);menu=false;
        check(!host.Save(out _)&&host.SaveFailure.Length>0&&host.HasDraft,
            "Save access refusal is retained independently and keeps its draft");
        menu=true;
        check(host.Save(out _)&&host.SaveFailure.Length==0&&host.SavedPendingRestart&&!host.HasDraft,
            "successful checked Save resolves earlier warning and stages next start");
        check(!host.Rows.Single(r=>r.Action=="steer").ActiveInverted&&File.ReadAllText(path).Contains("Keep = exact"),
            "successful Save preserves active startup binding and unrelated fields");
        var replacement=new SharedBindingHost(path,File.ReadAllBytes(path),()=>now,()=>menu);
        check(replacement.SaveFailure.Length==0,"new binding host cannot inherit an old warning");
    }

    internal static void Run(string directory,Action<bool,string> check)
    {
        SnapshotChecks(directory,check);
        Guid instance=Guid.NewGuid(),product=Guid.NewGuid();double now=0;bool menu=true;
        string path=Path.Combine(directory,"shared-controls.ini");
        var steer=ControlBinding.FromCapture("steer",ControlKind.Axis,0,1,32768,instance,product,"Fixture wheel",0,65535);
        string original="; fixture comments\r\n[Controls]\r\nSchema = 1\r\nProfile = Fixture\r\nRevision = original\r\nTransmission = Sequential\r\nsteer = "+steer+"\r\n[Other]\r\nKeep = exact\r\n";
        File.WriteAllText(path,original,new UTF8Encoding(false));
        var host=new SharedBindingHost(path,File.ReadAllBytes(path),()=>now,()=>menu);
        check(host.Access==SharedBindingAccess.ReadOnly&&!host.HasDraft,"profile without accepted reader stays read-only");
        AcceptedBindingObservation Observation(ulong sequence,bool pressed=false,ulong generation=1)
        {
            var axes=new int[8];axes[0]=32768;var buttons=new bool[128];buttons[3]=pressed;
            return new(instance,product,"Fixture wheel","fixture-ordinary-handle",generation,sequence,now,
                new(axes,buttons,new[]{-1,-1,-1,-1}),new int[8],Enumerable.Repeat(65535,8).ToArray());
        }
        host.Observe(Observation(1));
        check(host.Access==SharedBindingAccess.Editable&&host.Devices.Count==1,"accepted ordinary-reader observation permits main-menu edits");
        var active=host.Rows.Single(r=>r.Action=="steer").Active;
        host.SetInverted("steer",true);
        check(host.HasDraft&&host.Rows.Single(r=>r.Action=="steer").Active.Text==active.Text&&File.ReadAllText(path)==original,
            "inversion stays a draft without changing active input or disk");
        check(host.Save(out _)&&host.SavedPendingRestart&&!host.HasDraft,"explicit checked Save creates next-start state");
        var saved=host.Rows.Single(r=>r.Action=="steer");
        check(!saved.ActiveInverted&&saved.NextStartInverted==true&&File.ReadAllText(path).Contains("[Other]\r\nKeep = exact\r\n"),
            "Save preserves active startup value and unknown-section bytes");
        host.BeginCapture("modMenu",instance.ToString("D"));
        now=.05;host.Observe(Observation(2));now=.1;host.Observe(Observation(3,true));
        check(host.CapturingAction==null&&host.HasDraft&&host.Rows.Single(r=>r.Action=="modMenu").Draft?.Bound==true,
            "new physical edge reaches only the common-flow draft callback");
        string beforeExternal=File.ReadAllText(path);File.AppendAllText(path,"; external same-EOL edit\r\n");string external=File.ReadAllText(path);
        check(!host.Save(out _)&&host.HasDraft&&File.ReadAllText(path)==external&&external!=beforeExternal,
            "external same-EOL content mutation refuses checked Save and retains draft");
        host.Discard();check(!host.HasDraft&&host.SavedPendingRestart,"Discard keeps saved-next-start distinction");
        host.BeginCapture("clutch",instance.ToString("D"));
        check(host.CapturingAction==null&&host.CaptureMessage.Contains("Not delivered"),"unsupported Woden action is explained rather than rebound");
        host.BeginCapture("modMenu",instance.ToString("D"));menu=false;host.Update();
        check(host.CapturingAction==null&&!host.HasDraft,"leaving editable menu cancels through common flow");
        menu=true;now=.15;host.ReaderStopped();host.Observe(Observation(3));
        check(host.Access==SharedBindingAccess.ReadOnly,"same-handle cached sequence cannot regain eligibility after reader stop");
        now=.2;host.Observe(Observation(4));host.BeginCapture("modMenu",instance.ToString("D"));
        now=.3;host.Observe(Observation(1,false,2));
        check(host.CapturingAction==null&&!host.HasDraft,"changed reader generation cancels capture without a draft edit");
        check(host.Rows.Single(r=>r.Action=="steer").Active.Text==active.Text,"all editor operations leave active startup steering unchanged");
    }

    private static void SnapshotChecks(string directory,Action<bool,string> check)
    {
        Guid instance=Guid.NewGuid(),product=Guid.NewGuid();
        string path=Path.Combine(directory,"snapshot-controls.ini");
        byte[] active=Encoding.UTF8.GetBytes("[Controls]\nSchema = 1\nProfile = Fixture\nRevision = original\nTransmission = Sequential\nsteer = "+
            ControlBinding.FromCapture("steer",ControlKind.Axis,0,1,32768,instance,product,"Fixture wheel",0,65535)+"\n");
        File.WriteAllBytes(path,active);
        string identity=System.Text.Json.JsonSerializer.Serialize(new[]{new{index=0,epoch=1,instance=instance.ToString("B"),product=product.ToString("B"),name="Fixture wheel",interfacePath="fixture-path"}});
        ShimSnapshot Snapshot(ulong frame,bool pressed=false,bool stale=false,bool missing=false)
        {
            byte[] h=ShimSnapshot.NewHeader(),d=new byte[192],r=new byte[missing?0:224];
            void U(byte[] b,int at,uint value)=>BitConverter.GetBytes(value).CopyTo(b,at);
            void L(byte[] b,int at,long value)=>BitConverter.GetBytes(value).CopyTo(b,at);
            U(h,4,1);BitConverter.GetBytes(frame).CopyTo(h,16);L(h,24,1_000_000);L(h,32,1_000_000);L(h,40,100_000);U(h,48,1);U(h,52,missing?0u:1u);
            U(d,0,192);U(d,4,1);U(d,12,1);U(d,16,3);U(d,20,1);U(d,24,1);d[32]=8;U(d,80,65535);
            Encoding.ASCII.GetBytes(instance.ToString("B")).CopyTo(d,112);Encoding.ASCII.GetBytes(product.ToString("B")).CopyTo(d,152);
            if(!missing){U(r,0,224);U(r,4,1);U(r,12,1);BitConverter.GetBytes(frame).CopyTo(r,16);L(r,24,999_000);L(r,32,999_500);r[42]=1;r[43]=(byte)(stale?1:0);U(r,48,32768);r[83]=(byte)(pressed?128:0);}
            return ShimSnapshot.Decode(h,d,r);
        }
        foreach(string outcome in new[]{"refused","wrong frame","unreadable","timeout","stale","aged","identity miss","complete"})
        {
            double now=0;
            var host=new SharedBindingHost(path,active,()=>now,()=>true);
            void Feed(ulong seq,ShimSnapshot snapshot,string? names=null,double delivery=0)=>
                ShimBindingSnapshots.ObserveFrame(host,seq,()=>(0,snapshot),()=>names??identity,now-delivery,()=>now);
            Time.frameCount++;Feed(1,Snapshot(1));
            check(host.Access==SharedBindingAccess.Editable,"snapshot warmup before Bind uses braced identity and ordinary accepted frame");
            host.BeginCapture("modMenu",instance.ToString("D"));now=.05;Feed(2,Snapshot(2));
            check(host.CapturingAction=="modMenu","consumer capture awaits a new physical edge");
            Time.frameCount++;now=.1;
            switch(outcome)
            {
                case "refused":ShimBindingSnapshots.ObserveFrame(host,3,()=>(ShimSnapshotAbi.Refused,null),()=>identity,now,()=>now);break;
                case "wrong frame":Feed(3,Snapshot(4));break;
                case "unreadable":ShimBindingSnapshots.ObserveFrame(host,3,()=>throw new InvalidDataException("fixture"),()=>identity,now,()=>now);break;
                case "timeout":now=11;Feed(3,Snapshot(3));break;
                case "stale":Feed(3,Snapshot(3,stale:true));break;
                case "aged":Feed(3,Snapshot(3),delivery:.101);break;
                case "identity miss":Feed(3,Snapshot(3),"[]");break;
                default:Feed(3,Snapshot(3,pressed:true));break;
            }
            check(host.CapturingAction==null&&host.BlocksDispatch,outcome+" owns wheel actions in its ending production frame");
            check(host.HasDraft==(outcome=="complete"),outcome+" only an accepted edge can change the draft");
            Time.frameCount++;check(!host.BlocksDispatch,outcome+" releases dispatch on the next frame");
            if(outcome is "refused" or "wrong frame" or "unreadable")check(!host.WantsSnapshot,"snapshot transport refusal disables capture until a new reader lifetime");
            if(outcome is "stale" or "aged" or "identity miss")check(host.Access==SharedBindingAccess.ReadOnly,"known invalid observation cannot bridge prior fresh eligibility");
        }
        double ageClock=0;
        var absent=new SharedBindingHost(path,active,()=>ageClock,()=>true);
        ShimBindingSnapshots.Admit(absent,Snapshot(1),identity,0,0);
        ageClock=.05;ShimBindingSnapshots.Admit(absent,Snapshot(2,missing:true),identity,.05,.05);
        check(absent.Access==SharedBindingAccess.Editable,"missing read can retain prior fresh eligibility");
        ageClock=.3;absent.Update();check(absent.Access==SharedBindingAccess.ReadOnly,"missing read expires from its original source time");
    }
}
