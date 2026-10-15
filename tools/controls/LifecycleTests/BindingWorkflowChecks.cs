using System.Text.Json;
using Dbce.Wheel.Input;
using Woden.ControlsTesting;

static partial class Test
{
    static void BindingWorkflowChecks()
    {
        int start=Checks;string request=Guid.NewGuid().ToString("N");
        var commands=new BindingWorkflowCommand();var host=new WorkflowHost();
        string[] Words(string operation,string extra="")=>("bindings "+request+" "+Guid.NewGuid().ToString("N")+" "+operation+(extra.Length>0?" "+extra:"")).Split(' ');
        object Run(string[] words,bool ready=true,bool panel=true,bool focus=true,bool foreground=true,bool menu=true)=>
            commands.Run(words,request,ready,panel,focus,foreground,menu,()=>host);
        void Reject(Action work,string why){bool refused=false;try{work();}catch(InvalidOperationException){refused=true;}catch(ArgumentException){refused=true;}Check(refused,why);}
        host.Access=SharedBindingAccess.ReadOnly;
        var status=JsonSerializer.SerializeToElement(Run(Words("status"),panel:false,focus:false,foreground:false));
        Check(status.GetProperty("accepted").GetBoolean()&&status.GetProperty("access").GetString()=="ReadOnly"&&host.Calls==0,"read-only status can report actual background state without editing");
        Reject(()=>Run(Words("invert","steer on")),"read-only host refuses editing");host.Access=SharedBindingAccess.Editable;
        foreach(string gate in new[]{"cold","panel","focus","foreground","menu"})
        {
            int before=host.Calls;
            Reject(()=>Run(Words("invert","steer on"),gate!="cold",gate!="panel",gate!="focus",gate!="foreground",gate!="menu"),gate+" independently refuses edit");
            Check(host.Calls==before,gate+" refusal never reaches production host");
        }
        Reject(()=>Run(Words("status"),menu:false),"status is still menu-only");
        var duplicate=Words("invert","steer on");Run(duplicate);int calls=host.Calls;
        Reject(()=>Run(duplicate),"repeated step refuses");Check(host.Calls==calls,"replayed step has no host effect");
        Reject(()=>Run(Words("begin","modMenu "+Guid.NewGuid().ToString("D"))),"Begin refuses a device absent from accepted host observations");
        Run(Words("begin","modMenu "+host.Instance));Check(host.CapturingAction=="modMenu","explicit accepted device reaches Begin");
        Reject(()=>Run(Words("begin","modMenu "+host.Instance)),"second listening capture refuses");
        Run(Words("cancel"));Check(host.CapturingAction==null,"Cancel reaches existing host");
        host.FailSave=true;var failure=JsonSerializer.SerializeToElement(Run(Words("save")));
        Check(!failure.GetProperty("accepted").GetBoolean()&&host.HasDraft,"checked Save failure returns refusal and retains draft");
        host.FailSave=false;Run(Words("save"));Check(!host.HasDraft&&host.SavedPendingRestart,"explicit Save keeps next-start separate");
        Run(Words("invert","steer off"));Run(Words("discard"));Check(!host.HasDraft&&host.SavedPendingRestart,"Discard retains saved-next-start state");
        var unknown=Words("status");unknown[0]="raw";Reject(()=>Run(unknown),"unrecognized command family refuses");
        var bounded=new BindingWorkflowCommand();
        for(int i=0;i<64;i++)bounded.Run(Words("status"),request,true,true,true,true,true,()=>host);
        Reject(()=>bounded.Run(Words("status"),request,true,true,true,true,true,()=>host),"more than64 steps refuses");
        Console.WriteLine($"PASS {Checks-start} binding workflow command checks; fake host, no file/device/OS input.");
    }
    sealed class WorkflowHost:ISharedBindingHost
    {
        public string Instance=Guid.NewGuid().ToString("D");public int Calls;public bool FailSave;
        public SharedBindingAccess Access{get;set;}=SharedBindingAccess.Editable;
        public string AccessReason=>"fixture unavailable";public string ActiveSource=>"immutable startup";
        readonly SharedBindingRowView row=new(){Action="steer",IsAxis=true,Active=new(true,"startup")};
        public IReadOnlyList<SharedBindingRowView> Rows=>new[]{row};
        public IReadOnlyList<SharedBindingDeviceView> Devices=>new[]{new SharedBindingDeviceView{InstanceGuid=Instance,ProductGuid=Guid.NewGuid().ToString("D"),Name="fixture"}};
        public string? CapturingAction{get;private set;}public double CaptureSecondsLeft=>10;public string CaptureMessage=>"fixture";
        public bool HasDraft{get;private set;}public bool SavedPendingRestart{get;private set;}
        public void BeginCapture(string action)=>throw new Exception("Explicit instance required");
        public void BeginCapture(string action,string instance){Calls++;CapturingAction=action;}
        public void CancelCapture(){Calls++;CapturingAction=null;}
        public void Clear(string action)=>throw new Exception("Clear is not admitted");
        public void SetInverted(string action,bool inverted){Calls++;row.DraftInverted=inverted;HasDraft=true;}
        public bool Save(out string message){Calls++;message="fixture";if(FailSave)return false;HasDraft=false;SavedPendingRestart=true;return true;}
        public void Discard(){Calls++;HasDraft=false;}
    }
}
