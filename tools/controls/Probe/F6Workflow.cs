using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using BepInEx;
using UnityEngine;
using Dbce.Wheel.Input;

namespace Woden.ControlsTesting;

// Private schema-9 extension of the existing admission mailbox. Fixed mod-panel
// and production-host actions only: no game navigation, input, reader or focus change.
public sealed partial class ControlsProbe
{
    static readonly BindingWorkflowCommand F6Commands = new();
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);

    static (bool Accepted,string Reply) F6WorkflowCommand(string text)
    {
        string[] words=text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
        try
        {
            if(!Armed||!Protocol.F6Workflow(Request))throw new InvalidOperationException("F6 workflow requires the cold schema-9 request.");
            ObserveShim(); // Retains menu-only, no force-open and no network fences.
            using var process=Process.GetCurrentProcess();
            IntPtr window=process.MainWindowHandle;GetWindowThreadProcessId(window,out uint owner);
            bool foreground=window!=IntPtr.Zero&&owner==process.Id&&GetForegroundWindow()==window;
            bool focused=(bool)Runtime.GetProperty("Focused",All)!.GetValue(null)!;
            bool offStage=Runtime.GetField("Local",All)!.GetValue(null)==null;
            bool open=(bool)Static("Panel","Open")!;
            var host=Static("SharedBindingHost","Current") as ISharedBindingHost ?? throw new InvalidOperationException("Shared host contract is unavailable.");
            object reply;
            if(words.Length>0&&words[0]=="bindings")
            {
                string profile=Path.Combine(Paths.ConfigPath,"dbce-controls.ini");
                string before=Hash(profile);
                if(words.Length==4&&words[3]=="save"&&File.Exists(Path.Combine(Request!.OutputDirectory,"f6-saved-profile.json")))
                    throw new InvalidOperationException("Only one explicit Save is admitted per workflow.");
                reply=F6Commands.Run(words,Request!.Nonce,true,open,focused,foreground,offStage,()=>host);
                if(words.Length==4&&words[3]=="save"&&host.SavedPendingRestart&&!host.HasDraft)
                {
                    // Record the actual production checked Save, never write the INI here.
                    string witness=Path.Combine(Request.OutputDirectory,"f6-saved-profile.json");
                    var saved=new{schema=1,nonce=Request.Nonce,stepId=words[2],startupSha256=Request.ControlsSha256,
                        beforeSha256=before,savedSha256=Hash(profile),activeSource=host.ActiveSource};
                    using var file=new FileStream(witness,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
                    JsonSerializer.Serialize(file,saved,Protocol.Json);
                }
            }
            else
            {
                F6Commands.Claim(words,Request!.Nonce,true,"view");
                if(words.Length!=4||!offStage)throw new ArgumentException("View requires one fixed menu-only operation.");
                string operation=words[3];
                bool prompt=(bool)Field(Type("Panel"),"_sharedClosePrompt")!;
                if(operation is "controls" or "ffb" or "cameras")
                {
                    ShimVisual("panel"); // Stores original presentation before any change.
                    var settings=Field(Runtime,"Settings")!;
                    settings.GetType().GetField("UiPage",All)!.SetValue(settings,operation=="ffb"?"FFB":operation=="cameras"?"Cameras":"Controls");
                }
                else if(operation=="keep")
                {
                    if(!prompt)throw new InvalidOperationException("No draft-close dialog is open.");
                    Method(Type("Panel"),"MenuAction").Invoke(null,new object[]{"Back"});
                }
                else if(operation is "close" or "discard-close")
                {
                    if(operation=="discard-close")
                    {
                        if(!open||!focused||!foreground)throw new InvalidOperationException("Discard requires the actual foreground editor.");
                        host.Discard();
                    }
                    if(!host.HasDraft)RestoreShimPresentation(); // Before generic config Save on close.
                    Method(Type("Panel"),"Close").Invoke(null,new object[]{true});
                }
                else if(operation!="status")throw new ArgumentException("View accepts controls, ffb, cameras, close, keep, discard-close, status.");
                string? shot=null;
                if(operation!="status")
                {
                    shot="f6-panel-"+words[2]+".png";
                    string path=Path.Combine(Request.OutputDirectory,shot);
                    if(File.Exists(path))throw new InvalidOperationException("Step screenshot already exists.");
                    ScreenCapture.CaptureScreenshot(path,1);
                }
                reply=new{schema="woden.panel-workflow-step@1",requestId=Request.Nonce,stepId=words[2],operation,
                    admitted=true,accepted=true,focused,ownedForeground=foreground,offStage,
                    panelOpen=Static("Panel","Open"),page=Field(Field(Runtime,"Settings")!.GetType(),"UiPage",Field(Runtime,"Settings")),
                    closePrompt=Field(Type("Panel"),"_sharedClosePrompt"),closing=Static("MenuOwnership","Closing"),
                    host.HasDraft,host.SavedPendingRestart,screenshot=shot};
            }
            using var decoded=JsonDocument.Parse(JsonSerializer.Serialize(reply,Protocol.Json));
            var properties=decoded.RootElement.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.Clone());
            string? nativeProfile=null;
            if((bool)Static("ShimInput","Ready")!)
                nativeProfile=(string)Method(Type("ShimNative"),"ProfileInfo").Invoke(null,new[]{Field(Type("ShimInput"),"_handle")})!;
            properties["nativeProfileInfo"]=JsonSerializer.SerializeToElement(nativeProfile,Protocol.Json); // Cache getter, no reader call.
            string json=JsonSerializer.Serialize(properties,Protocol.Json);Row("f6-workflow",json);
            return (decoded.RootElement.GetProperty("accepted").GetBoolean(),json);
        }
        catch(Exception ex)
        {
            var cause=ex is TargetInvocationException{InnerException:{} inner}?inner:ex;
            string json=JsonSerializer.Serialize(new{schema="woden.workflow-refusal@1",requestId=words.Length>1?words[1]:null,
                stepId=words.Length>2?words[2]:null,admitted=false,accepted=false,message=cause.Message},Protocol.Json);
            Row("f6-workflow-refused",json);return(false,json);
        }
    }
}
