using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Woden.ControlsTesting;

// Fixed diagnostic route only. Batches enter shared Core; all changes to native
// menus still come from the production wheel consumers. No scene/selection setter.
public sealed partial class ControlsProbe
{
    const string Garage="Menus/MenuTree/World_Map_Screen/Map_Buttons/Garage_button";
    const string Workshop="Menus/MenuTree/World_Map_Screen/Map_Buttons/WorkShop_button";
    enum TrialStep { DailyReady, DailyRelease, TitleReady, TitleRelease, MapReady, RightRelease, LeftReady, LeftRelease, Ending, Passed }
    static TrialStep Trial;
    static int TrialBatch;
    static double TrialChanged, TrialNeutral;
    static bool TrialSawInjection;
    static string? TrialSession, TrialBatchFile;
    static readonly Dictionary<int,(BaseInputModule module,bool enabled)> TrialModules=new();
    static string TrialVerdict="unclassified";

    static void StartMenuTrial()
    {
        if(Native?.Armed!=true) throw new InvalidOperationException("Native no-force fence is not armed.");
        TrialSession=Path.Combine(Request!.OutputDirectory,"shim-session.txt");
        TrialBatchFile=Path.Combine(Request.OutputDirectory,"shim-batch.txt");
        using(var file=new StreamWriter(new FileStream(TrialSession,FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(false)))
            file.Write("nonce="+Request.Nonce+"\nexpires="+Request.ExpiresUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)+"\n");
        Method(Type("ShimInput"),"ConfigureMenuTrial").Invoke(null,new object[]{Request.Nonce,TrialSession,TrialBatchFile,Request.ExpiresUtc,true});
        Method(Type("ShimInput"),"MenuTrialScope").Invoke(null,new object[]{1});
        Trial=TrialStep.DailyReady;TrialBatch=0;TrialChanged=Clock.Elapsed.TotalSeconds;TrialNeutral=0;TrialSawInjection=false;
        ObserveUntil=Deadline; // Retain actual production button queries through the entire bounded trial.
        TrialVerdict="pending";TrialModules.Clear();
        Row("menu-trial","Core latched; fixed daily Confirm, title Start, Garage right/left; no later confirm/start");
    }
    static object TrialAction(string id) => Method(Type("ShimInput"),"Action").Invoke(null,new object[]{id})!;
    static bool TrialKnownNeutral(string id) => (bool)Method(Type("ShimInput"),"KnownNeutral").Invoke(null,new object[]{id})!;
    static bool TrialReleased(string id,double now)
    {
        var a=TrialAction(id);
        if((bool)Value(a,"Injected")! && Value(a,"Status")!.ToString()=="Live" && Convert.ToSingle(Value(a,"Value"))==1)
        { TrialSawInjection=true;TrialNeutral=0;return false; }
        if(!TrialSawInjection || !TrialKnownNeutral(id)) { TrialNeutral=0;return false; }
        if(TrialNeutral==0)TrialNeutral=now;
        return now-TrialNeutral>=.2;
    }
    static bool NeutralFor(string id,double now)
    {
        if(!TrialKnownNeutral(id)) { TrialNeutral=0;return false; }
        if(TrialNeutral==0) TrialNeutral=now;
        return now-TrialNeutral>=.3;
    }
    static void SendTrial(string command,TrialStep next,double now)
    {
        ObserveShim(); // Recheck actual force/network counters before every batch.
        if(++TrialBatch>5) throw new InvalidOperationException("Trial batch limit.");
        TrialBatchWriter.Replace(TrialBatchFile!,"nonce="+Request!.Nonce+"\nbatch="+TrialBatch+"\n"+command+"\n");
        Row("menu-trial-batch",TrialBatch+": "+command);
        Trial=next;TrialChanged=now;TrialNeutral=0;TrialSawInjection=false;
    }
    static string Selected() => EventSystem.current?.currentSelectedGameObject is {} g?ObjectPath(g.transform):"none";
    static void Scope(int scope,TrialStep next,double now)
    { Method(Type("ShimInput"),"MenuTrialScope").Invoke(null,new object[]{scope});Trial=next;TrialChanged=now;TrialNeutral=0;TrialSawInjection=false; }
    static void SuppressTrialModules()
    {
        var events=EventSystem.current;if(events==null)return;
        if(!events.enabled || !events.isFocused) throw new InvalidOperationException("Native EventSystem is not enabled/focused.");
        foreach(var module in events.GetComponents<BaseInputModule>())
        {
            if(module==null)continue;
            int id=module.GetInstanceID();
            if(!TrialModules.ContainsKey(id))
            {
                if(TrialModules.Count>=8)throw new InvalidOperationException("Unexpected menu input-module churn.");
                TrialModules.Add(id,(module,module.enabled));
                Row("menu-trial-module","Suppress native module "+module.GetType().FullName+" instance="+id);
            }
            module.enabled=false;
            if(module.enabled)throw new InvalidOperationException("Native menu module did not suppress.");
        }
    }
    static void VerifyMap(string expected,bool outward)
    {
        if(Selected()!=expected)throw new InvalidOperationException("Unexpected native map selection: "+Selected()+"; expected "+expected);
        var current=EventSystem.current!.currentSelectedGameObject ?? throw new InvalidOperationException("Native selection disappeared.");
        var selectable=current.GetComponent<Selectable>();
        if(selectable==null || !selectable.isActiveAndEnabled || !selectable.IsInteractable())throw new InvalidOperationException("Selection is not interactable.");
        var neighbor=outward?selectable.FindSelectableOnRight():selectable.FindSelectableOnLeft();
        if(neighbor==null || !neighbor.isActiveAndEnabled || !neighbor.IsInteractable() ||
            ObjectPath(neighbor.transform)!=(outward?Workshop:Garage))throw new InvalidOperationException("Exact native navigation neighbor differs.");
        var back=outward?neighbor.FindSelectableOnLeft():neighbor.FindSelectableOnRight();
        if(back==null || back.GetInstanceID()!=selectable.GetInstanceID())throw new InvalidOperationException("Native navigation pair is not reciprocal.");
    }
    static void TickMenuTrial()
    {
        if(!Protocol.MenuTrial(Request) || !Armed || Trial==TrialStep.Passed)return;
        double now=Clock.Elapsed.TotalSeconds;
        if(now-TrialChanged>35)throw new InvalidOperationException("Menu trial timed out in "+Trial);
        if(!(bool)Static("ShimInput","Ready")!)
        {
            if((bool)Static("ShimInput","Owns")! || ((string)Static("ShimInput","Status")!).Contains("refused"))
                throw new InvalidOperationException("Shared host unavailable: "+Static("ShimInput","Status"));
            return;
        }
        if(!(bool)Method(Type("ShimInput"),"TrialNativeMenuAllowed").Invoke(null,null)!)throw new InvalidOperationException("Actual focus or physical idle fence lost.");
        if(Native?.Armed!=true || Runtime.GetField("Local",All)!.GetValue(null)!=null)throw new InvalidOperationException("Menu/no-force fence lost.");
        SuppressTrialModules();
        Changed("menu-trial-selection","selected",Selected());
        byte run=Convert.ToByte(Static("ShimInput","Run"));
        switch(Trial)
        {
            case TrialStep.DailyReady:
                var daily=UnityEngine.Object.FindObjectOfType<DailyMessage>();
                if(daily==null || !daily.field_Private_Boolean_0 || daily.MyControls==null || MenuCameraScript.Exiting)return;
                if(run==2 && NeutralFor("confirm",now))SendTrial("inject action confirm 1 ms=150",TrialStep.DailyRelease,now);
                break;
            case TrialStep.DailyRelease:
                if(TrialReleased("confirm",now))Scope(2,TrialStep.TitleReady,now);
                break;
            case TrialStep.TitleReady:
                var title=UnityEngine.Object.FindObjectOfType<TitleScreenScript>();
                if(title==null || title.Starting || title.field_Private_Boolean_0 || title.FrameCount>=title.FramesToDemo ||
                    title.MyControls==null || title.MyControls.ButtonB || title.SceneToLoad!="MapScreen" || MenuCameraScript.Exiting)return;
                if(run is 2 or 3 && NeutralFor("start",now))SendTrial("inject action start 1 ms=150",TrialStep.TitleRelease,now);
                break;
            case TrialStep.TitleRelease:
                if(TrialReleased("start",now))Scope(3,TrialStep.MapReady,now);
                break;
            case TrialStep.MapReady:
                if(Selected()=="none")return;
                VerifyMap(Garage,true);
                if(run is 2 or 3 && NeutralFor("navRight",now))SendTrial("inject action navRight 1 ms=150",TrialStep.RightRelease,now);
                break;
            case TrialStep.RightRelease:
                if(!TrialReleased("navRight",now))return;
                VerifyMap(Workshop,false);Trial=TrialStep.LeftReady;TrialChanged=now;TrialNeutral=0;break;
            case TrialStep.LeftReady:
                VerifyMap(Workshop,false);
                if(run is 2 or 3 && NeutralFor("navLeft",now))SendTrial("inject action navLeft 1 ms=150",TrialStep.LeftRelease,now);
                break;
            case TrialStep.LeftRelease:
                if(!TrialReleased("navLeft",now))return;
                VerifyMap(Garage,true);Scope(4,TrialStep.Ending,now);SendTrial("end",TrialStep.Ending,now);break;
            case TrialStep.Ending:
                if(run!=4)return;
                if(TrialNeutral==0)TrialNeutral=now;
                if(now-TrialNeutral<.5)return;
                Trial=TrialStep.Passed;TrialVerdict="passed: native Garage -> Workshop -> Garage, Core injected then neutral";
                Row("menu-trial-verdict",TrialVerdict);Close("bounded Core menu trial completed");break;
        }
    }
    static void RestoreTrialModules()
    {
        if(!Protocol.MenuTrial(Request))return;
        if(Trial!=TrialStep.Passed)TrialVerdict="incomplete: "+Trial;
        foreach(var item in TrialModules.Values)
            if(item.module!=null){item.module.enabled=item.enabled;if(item.module.enabled!=item.enabled)throw new InvalidOperationException("Native input-module restoration differs.");}
        TrialModules.Clear();
    }
}
