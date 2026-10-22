using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WodenRallyEdge;
using Dbce.Wheel.Support;

internal static class HelpReportChecks
{
    internal static void Run(string directory,Action<bool,string> check)
    {
        Runtime.Settings=new(new ConfigFile(Path.Combine(directory,"help-report.cfg"),false));
        Runtime.Settings.FfbEnabled=false;Runtime.Settings.UiPage="Help";
        Runtime.Wheel=new(Path.Combine(directory,"help-report-bindings.json"));
        Runtime.Local=null;Runtime.Focused=true;Keyboard.current=new();
        Screen.width=1280;Screen.height=720;
        const BindingFlags hidden=BindingFlags.Static|BindingFlags.NonPublic;
        FieldInfo Field(string name)=>typeof(Panel).GetField(name,hidden)!;
        void Set(string name,object? value)=>Field(name).SetValue(null,value);
        object? Get(string name)=>Field(name).GetValue(null);
        void Prepare()=>typeof(Panel).GetMethod("PrepareReport",hidden)!.Invoke(null,null);
        void Draw(Event? input=null){GUI.Commands.Clear();Event.current=input??new();Panel.Draw();}
        Panel.Toggle();Draw();
        foreach(string caption in new[]{"Submit GitHub Issue","Mod on GitHub","Wheelkit on GitHub"})
            check(GUI.Commands.Any(c=>c.Text==caption),"Help offers "+caption);
        check(Get("_reportIncludeLog") is false,"report logs start off");
        Set("_reportTitle","Issue user@example.com");Set("_reportDescription","Reproduction steps");Prepare();
        var prepared=(PreparedReport)Get("_reportPrepared")!;
        check(prepared.Repository=="d-b-c-e/dbce-mods-woden-rally-edge"&&prepared.Body.Contains("No logs included"),"packaged issue route and no default collection");
        check(!prepared.Title.Contains("user@example.com"),"review includes redacted title");
        Set("_reportReviewed",true);Set("_reportOpen",true);Set("_dirty",false);
        GUI.TextReplacement="Changed title";Draw();
        check(Get("_reportPrepared")==null&&Get("_reportReviewed") is false,"editing title invalidates reviewed snapshot");
        check(Get("_dirty") is false,"temporary report edits do not dirty saved settings");
        Prepare();Set("_reportReviewed",true);Draw();
        var on=GUI.Commands.First(c=>c.Text=="On");
        Draw(new(){type=EventType.MouseDown,button=0,mousePosition=new(on.Rect.x+10,on.Rect.y+10)});
        check(Get("_reportIncludeLog") is true&&Get("_reportPrepared")==null&&Get("_reportReviewed") is false,
            "changing log selection clears stale preview and review");
        check(Get("_dirty") is false,"log choice does not save owner settings");
        Set("_reportIncludeLog",false);Prepare();Set("_reportReviewed",true);
        Set("_reportTitle","");Prepare();
        check(Get("_reportPrepared")==null&&Get("_reportReviewed") is false,"failed preparation cannot reuse an old report");
        var paginate=typeof(Panel).GetMethod("ReportPreviewPages",hidden)!;
        var pages=(List<string>)paginate.Invoke(null,new object[]{string.Concat(Enumerable.Repeat("完整预览😀 with line\n",100))})!;
        check(pages.Count>1&&pages.All(p=>p.Count(c=>c=='\n')<=9),"full preview is paged into bounded readable rows");
        // No link/browser/export action is invoked by this fixture.
        check(!Runtime.Settings.FfbEnabled,"report preparation does not enable forces");
        Panel.Close(false);
    }
}
