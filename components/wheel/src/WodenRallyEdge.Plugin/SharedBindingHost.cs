using Dbce.Wheel.Input;
using Dbce.Wheel.Shim;
using Dbce.Wheel.Ui;
using WodenRallyEdge.Core;
using UnityEngine;

namespace WodenRallyEdge;

// Woden policy and profile presentation only. The common flow owns capture;
// accepted snapshots arrive from the ordinary shim tick, never another reader.
internal sealed class SharedBindingHost : ISharedBindingHost, ISharedBindingSaveStatus
{
    internal static SharedBindingHost Current { get; private set; } = Create(null,null);
    internal static bool MenuEditable => Runtime.Focused && Panel.Open && Runtime.Settings.UiPage=="Controls" &&
        Runtime.Local == null && ShimInput.Ready && Runtime.Settings.InputBackend == "Shim";
    private static SharedBindingHost Create(string? path,byte[]? bytes) => new(path,bytes,
        ()=>Runtime.Clock.Elapsed.TotalSeconds,()=>MenuEditable);
    internal static void Configure(string path,byte[] bytes) { Current.ReaderStopped();Current=Create(path,(byte[])bytes.Clone()); }
    private readonly string? _path;
    private readonly ControlProfileDraft? _draft;
    private readonly Func<bool> _canEdit;
    private readonly SharedBindingCaptureFlow _flow;
    private readonly BindingSaveStatus _saveStatus = new();
    public string SaveFailure => _saveStatus.SaveFailure;
    private string? _error;
    private bool _readerUnavailable;
    internal ShimSnapshotFeed SnapshotFeed { get; } = new();
    private readonly SharedBindingFrameGuard _frameGuard = new();
    private string _readerReason="Shared capture needs the accepted ordinary reader snapshot extension. Active bindings are kept; use standalone Controls Setup if unavailable.";

    internal SharedBindingHost(string? path,byte[]? active,Func<double> clock,Func<bool> canEdit)
    {
        _path=path;_canEdit=canEdit;
        _flow=new(clock,AcceptDraft);
        if(active==null)return;
        try { _draft=new ControlProfileDraft(active); }
        catch(Exception ex) when(ex is IOException or InvalidDataException or ArgumentException){_error=ex.Message;}
    }
    private string? AcceptDraft(string action,ControlBinding binding)
    {
        if(_draft==null||!_canEdit())return "Woden's main-menu binding editor is no longer eligible; the prior draft was kept.";
        string reason=Reason(action);if(reason.Length!=0)return reason;
        try{_draft.Set(action,binding.ToString());return null;}
        catch(ArgumentException ex){return ex.Message;}
    }
    internal bool WantsSnapshot => _draft!=null&&!_readerUnavailable&&_canEdit();
    internal bool BlocksDispatch => _frameGuard.Owns(Time.frameCount,CapturingAction!=null);
    internal void NoteCaptureFrame(){if(CapturingAction!=null)_frameGuard.Note(Time.frameCount);}
    internal void Observe(AcceptedBindingObservation observation) {NoteCaptureFrame();_flow.Observe(observation,_canEdit());}
    internal void Remove(Guid instance,string reason) {NoteCaptureFrame();_flow.Remove(instance,reason);}
    internal void ReaderStopped(){NoteCaptureFrame();_flow.ReaderStopped();}
    internal void ReaderUnavailable(string reason){_readerUnavailable=true;_readerReason=reason;ReaderStopped();}
    internal void Update(){NoteCaptureFrame();_flow.Refresh(_canEdit());}
    private bool Editable => _draft!=null&&!_readerUnavailable&&_canEdit()&&_flow.HasFreshObservation;
    public SharedBindingAccess Access {get{Update();return _draft==null?(_error==null?SharedBindingAccess.Missing:SharedBindingAccess.ReadOnly):Editable?SharedBindingAccess.Editable:SharedBindingAccess.ReadOnly;}}
    public string AccessReason => _draft==null?_error??"Native standalone bindings are active or no shared startup profile is available. Shared profiles use the standalone Controls Setup download; Wheelkit is optional.":
        !_canEdit()?"Shared binding edits require focused settings in the main menu; active bindings stay unchanged.":
        !_flow.HasFreshObservation?_readerReason:"Shared binding edits are a draft. Save bindings, then restart to use them.";
    public string ActiveSource => _draft==null?"No editable shared startup profile":_draft.Active.Name+" · "+_draft.Active.Revision;
    public string? CapturingAction => _flow.CapturingAction;
    public double CaptureSecondsLeft => _flow.SecondsLeft;
    public string CaptureMessage => _flow.Message;
    public bool HasDraft => _draft?.HasDraft==true;
    public bool SavedPendingRestart => _draft?.SavedPendingRestart==true;
    public IReadOnlyList<SharedBindingDeviceView> Devices => _flow.Observations.Select(d=>new SharedBindingDeviceView{
        InstanceGuid=d.Instance.ToString("D"),ProductGuid=d.Product.ToString("D"),Name=d.Name,
        IsDefault=_draft?.Binding("steer")?.Device==d.Instance}).ToArray();
    public IReadOnlyList<SharedBindingRowView> Rows => ShimActionMap.Names.Select(Row).ToArray();
    private SharedBindingRowView Row(string action)
    {
        ControlBinding? active=null,saved=null;
        _draft?.Active.Bindings.TryGetValue(action,out active);_draft?.Saved.Bindings.TryGetValue(action,out saved);
        var proposed=_draft?.Binding(action);
        bool changed=_draft?.Changes.ContainsKey(action)==true,pending=saved?.ToString()!=active?.ToString();
        return new(){Action=action,Label=Label(action),Active=Value(active),Draft=changed?Value(proposed):null,
            NextStart=pending?Value(saved):null,Device=(proposed??saved??active)?.Name??"No device bound",
            IsAxis=proposed?.Kind==ControlKind.Axis,ActiveInverted=active?.Inverted==true,
            DraftInverted=changed?proposed?.Inverted==true:null,NextStartInverted=pending?saved?.Inverted==true:null,
            State=_error!=null?SharedBindingRowState.Error:CapturingAction==action?SharedBindingRowState.Capturing:
                changed?SharedBindingRowState.Changed:proposed==null?SharedBindingRowState.Unbound:SharedBindingRowState.Bound,
            Note=Reason(action)};
    }
    private static SharedBindingValue Value(ControlBinding? b) => new(b!=null,b==null?"Unbound":
        (b.Kind==ControlKind.Axis?$"Axis {b.Index+1}":b.Kind==ControlKind.Button?$"Button {b.Index+1}":$"Hat {b.Index+1} / {b.Angle/100.0:0.##}°")+
        (b.Inverted?" · inverted":"")+" · "+b.Name);
    private static string Label(string id) => id switch {
        "steer"=>"Steering","throttle"=>"Accelerator","brake"=>"Brake","clutch"=>"Clutch","handbrake"=>"Handbrake",
        "shiftUp"=>"Shift up","shiftDown"=>"Shift down","modMenu"=>"Settings","muteFfb"=>"Stop FFB",
        "camera"=>"Change camera","lookBack"=>"Look behind","reset"=>"Reset car","start"=>"Start / pause",
        "navUp"=>"Navigate up","navDown"=>"Navigate down","navLeft"=>"Navigate left","navRight"=>"Navigate right",
        _=>char.ToUpperInvariant(id[0])+id[1..]};
    private string Reason(string id)
    {
        if(ShimActionMap.UnsupportedNames.Contains(id))return "Not delivered by Woden's current shared input adapter; its saved value is kept.";
        if(_draft==null)return _error??"";
        if((id is "shiftUp" or "shiftDown") && _draft.Saved.Transmission!="Sequential")return "This profile does not select Sequential. Use standalone setup to change transmission.";
        if(ShimActionMap.IsGate(id)&&_draft.Saved.Transmission!="H-pattern")return "This profile does not select H-pattern. Use standalone setup to change transmission.";
        return "";
    }
    public void BeginCapture(string action) => BeginCapture(action,(_draft?.Binding(action)?.Device??_draft?.Binding("steer")?.Device??Guid.Empty).ToString("D"));
    public void BeginCapture(string action,string instanceGuid)
    {
        Update();string reason=Reason(action);
        if(!Editable||!ShimActionMap.Names.Contains(action)||reason.Length!=0){_flow.Message=reason.Length!=0?reason:AccessReason;return;}
        if(Guid.TryParse(instanceGuid,out var instance))_flow.Begin(action,instance,_canEdit());
    }
    public void CancelCapture(){NoteCaptureFrame();_flow.Cancel();}
    public void Clear(string action){Update();if(Editable&&CapturingAction==null&&ShimActionMap.Names.Contains(action)&&Reason(action).Length==0)_draft!.Set(action,null);}
    public void SetInverted(string action,bool inverted)
    {
        Update();var binding=_draft?.Binding(action);
        if(Editable&&CapturingAction==null&&Reason(action).Length==0&&binding?.Kind==ControlKind.Axis)_draft!.Set(action,binding.WithInverted(inverted).ToString());
    }
    public bool Save(out string message)
    {
        Update();if(!Editable||CapturingAction!=null||_path==null){message="Save requires the focused main-menu editor and a qualified reader. The draft was kept.";return _saveStatus.Record(false,message);}
        bool saved=_draft!.Save(_path,out message);_flow.Message=message;return _saveStatus.Record(saved,message);
    }
    public void Discard(){CancelCapture();_draft?.Discard();_saveStatus.Discard();_flow.Message="Unsaved binding edits discarded. Active and saved bindings are unchanged.";}
}
