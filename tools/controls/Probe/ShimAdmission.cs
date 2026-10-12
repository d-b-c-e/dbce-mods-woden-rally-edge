using System.Text.Json;
using System.Reflection;
using BepInEx;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Woden.ControlsTesting;

// Observer-only extension of the existing bounded, force-fenced controls addon.
// It does not open/tick the shim itself, consume buttons or select a game menu.
public sealed partial class ControlsProbe
{
    static bool ShimIdentityWritten;
    static string? ShimDevices;
    static string? ShimOriginalPage;
    static MethodInfo PanelToggle => Type("Panel").GetMethod("Toggle",All,null,System.Type.EmptyTypes,null) ??
        throw new MissingMethodException("Panel.Toggle()");
    static object? Value(object target,string name) => target.GetType().GetProperty(name,All)?.GetValue(target) ??
        target.GetType().GetField(name,All)?.GetValue(target) ?? throw new MissingMemberException(target.GetType().FullName,name);
    static object? Static(string type,string name) => Type(type).GetProperty(name,All)?.GetValue(null) ?? Type(type).GetField(name,All)?.GetValue(null);
    static void VerifyShimAdmission()
    {
        Match(Path.Combine(Paths.ConfigPath,"dbce-controls.ini"),Request!.ControlsSha256!);
        Match(Path.Combine(Path.GetDirectoryName(Mod.Location)!,"dbce_shim.dll"),Request.ShimSha256!);
        var settings=Field(Runtime,"Settings")!;
        if((string)Field(settings.GetType(),"InputBackend",settings)! != "Shim") throw new InvalidDataException("Shim backend was not requested.");
        foreach(var name in new[]{"Owns","Ready","Status","SteeringGuid","TransmissionStatus"})
            if(Type("ShimInput").GetProperty(name,All) is null) throw new MissingMemberException("Shim observer seam: "+name);
        foreach(var name in new[]{"Stop","Action"}) Method(Type("ShimInput"),name);
        _=PanelToggle;
        HookContract.VerifyShim(Mod);
        ShimIdentityWritten=false;
        ShimDevices=null;
        ShimOriginalPage=null;
    }
    static void ObserveShim()
    {
        if(Runtime.GetField("Local",All)!.GetValue(null) is not null) throw new InvalidOperationException("Menu-only test reached a player car.");
        var force=Field(Runtime,"Force")!;
        foreach(var name in new[]{"Attempts","Opens","Failures"})
            if(Convert.ToInt64(Value(force,name))!=0) throw new InvalidOperationException("Force counter is nonzero: "+name);
        if((bool)Value(force,"Connected")! || Convert.ToSingle(Value(force,"Sent"))!=0) throw new InvalidOperationException("Force connection/value is nonzero.");
        var output=Field(Runtime,"Output")!;
        if(Convert.ToInt64(Value(output,"ForzaPackets"))!=0) throw new InvalidOperationException("Network output counter is nonzero.");
        var producer=Type("ShimInput").GetField("_producer",All)!.GetValue(null) as GamePadSystem;
        var pads=new List<object>();
        if(producer?.Game_Pads is {} nativePads)
            foreach(var p in nativePads) pads.Add(p==null ? new { missing=true } : (object)new {
                controller=p.Controller!=null,ls=new[]{p.LS.x,p.LS.y},rs=new[]{p.RS.x,p.RS.y},p.LT,p.RT,
                p.A,p.B,p.X,p.Y,p.Back,p.Start,p.LB,p.RB,p.L3,p.R3,p.Dpad_Up,p.Dpad_Down,p.Dpad_Left,p.Dpad_Right,p.AnyKey });
        bool owns=(bool)Static("ShimInput","Owns")!,ready=(bool)Static("ShimInput","Ready")!;
        var actions=new Dictionary<string,object?>();
        foreach(var id in new[]{"steer","throttle","brake","handbrake","shiftUp","shiftDown","confirm","back","start","navLeft","navRight","modMenu"})
        {
            var action=Method(Type("ShimInput"),"Action").Invoke(null,new object[]{id})!;
            actions[id]=new{value=Value(action,"Value"),status=Value(action,"Status")?.ToString(),injected=Value(action,"Injected")};
        }
        Row("shim-state",JsonSerializer.Serialize(new { unityGamepads=Gamepad.all.Count, producer=producer!=null,pads,owns,ready,
            status=Static("ShimInput","Status"),steeringGuid=Static("ShimInput","SteeringGuid"),transmission=Static("ShimInput","TransmissionStatus"),
            tickFrame=Type("ShimInput").GetField("_frame",All)!.GetValue(null),sequence=Static("ShimInput","Sequence"),run=Static("ShimInput","Run"),actions,forceOpens=Value(force,"Opens"),forceAttempts=Value(force,"Attempts"),
            forceFailures=Value(force,"Failures"),forceConnected=Value(force,"Connected"),forzaPackets=Value(output,"ForzaPackets") }));
        if(ready && !ShimIdentityWritten)
        {
            var handle=Field(Type("ShimInput"),"_handle")!;
            foreach(var name in new[]{"ProfileInfo","StartReport","DeviceReports"})
                File.WriteAllText(Path.Combine(Request!.OutputDirectory,"shim-"+name+".txt"),(string)Method(Type("ShimNative"),name).Invoke(null,new[]{handle})!);
            ShimIdentityWritten=true;
        }
        if(ready)
        {
            var handle=Field(Type("ShimInput"),"_handle")!;
            string report=(string)Method(Type("ShimNative"),"DeviceReports").Invoke(null,new[]{handle})!;
            if(report!=ShimDevices)
            {
                // Existing host snapshot only: no new enumeration or reader.
                File.AppendAllText(Path.Combine(Request!.OutputDirectory,"shim-device-history.jsonl"),JsonSerializer.Serialize(new{
                    utc=DateTimeOffset.UtcNow,frame=Time.frameCount,sequence=Static("ShimInput","Sequence"),report})+"\n");
                File.WriteAllText(Path.Combine(Request.OutputDirectory,"shim-DeviceReports-latest.txt"),report);ShimDevices=report;
            }
        }
    }
    static void ShimVisual(string operation)
    {
        ObserveShim(); // Recheck menu-only and zero output before visual actions.
        var panel=Type("Panel");
        bool open=(bool)panel.GetProperty("Open",All)!.GetValue(null)!;
        if(operation=="panel")
        {
            // Temporary in-memory presentation only; do not save/rewrite config.
            var settings=Field(Runtime,"Settings")!;
            ShimOriginalPage ??= (string)Field(settings.GetType(),"UiPage",settings)!;
            settings.GetType().GetField("UiPage",All)!.SetValue(settings,"Controls");
            if(!open) PanelToggle.Invoke(null,null);
            Row("visual","Opened F6 Controls using the native panel method; no keyboard input.");
        }
        else
        {
            if(!open) throw new InvalidOperationException("Open the panel and inspect status before the screenshot.");
            string path=Path.Combine(Request!.OutputDirectory,"shim-panel.png");
            if(File.Exists(path)) throw new InvalidOperationException("Screenshot already exists.");
            ScreenCapture.CaptureScreenshot(path,1);
            Row("visual","Requested shim-panel.png; file/pixels must be verified separately.");
        }
    }
    static void RestoreShimPresentation()
    {
        if(ShimOriginalPage is null) return;
        var settings=Field(Runtime,"Settings")!;
        settings.GetType().GetField("UiPage",All)!.SetValue(settings,ShimOriginalPage);
        ShimOriginalPage=null;
    }
}
