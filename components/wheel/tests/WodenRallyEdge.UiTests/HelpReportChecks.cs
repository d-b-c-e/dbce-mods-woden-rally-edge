using System.Reflection;
using System.Diagnostics;
using System.ComponentModel;
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
        object? Call(string name,params object[] args)=>typeof(Panel).GetMethod(name,hidden)!.Invoke(null,args);
        void Draw(Event? input=null){GUI.Commands.Clear();Event.current=input??new();Panel.Draw();}
        void Click(string caption)
        {
            Set("_focus",-1);
            for(int scroll=0;scroll<2400;scroll+=120)
            {
                Set("_scroll",(float)scroll);Draw();
                if(GUI.Commands.Any(c=>c.Text==caption))break;
            }
            var command=GUI.Commands.First(c=>c.Text==caption);
            Draw(new(){type=EventType.MouseDown,button=0,mousePosition=new(command.Rect.x+3,command.Rect.y+3)});
        }
        var root=Path.Combine(directory,"selected-report-root");Directory.CreateDirectory(root);
        var launches=new List<ProcessStartInfo>();
        var oldRoot=Get("_reportRoot");var oldShell=Get("_reportShell");
        Set("_reportRoot",(Func<string>)(()=>root));
        Set("_reportShell",(Action<ProcessStartInfo>)(info=>launches.Add(info)));
        try
        {
        Panel.Toggle();Draw();
        foreach(string caption in new[]{"Submit GitHub Issue","Mod on GitHub","Wheelkit on GitHub"})
            check(GUI.Commands.Any(c=>c.Text==caption),"Help offers "+caption);
        check(Get("_reportIncludeLog") is false,"report logs start off");
        check(launches.Count==0,"drawing Help never dispatches a shell action");
        Click("Mod on GitHub");Click("Wheelkit on GitHub");
        check(launches.Select(p=>p.FileName).SequenceEqual(new[]{"https://github.com/d-b-c-e/dbce-mods-woden-rally-edge","https://github.com/d-b-c-e/dbce-wheelkit"})&&launches.All(p=>p.UseShellExecute),
            "deliberate underlined links dispatch their exact GitHub pages through the shell");
        Click("Submit GitHub Issue");
        check(Get("_reportOpen") is true&&launches.Count==2,"submit action opens the local form before any draft dispatch");
        Set("_reportTitle","Issue user@example.com");Set("_reportDescription","Reproduction steps");Prepare();
        var prepared=(PreparedReport)Get("_reportPrepared")!;
        check(prepared.Repository=="d-b-c-e/dbce-mods-woden-rally-edge"&&prepared.Body.Contains("No logs included"),"packaged issue route and no default collection");
        check(!prepared.Title.Contains("user@example.com"),"review includes redacted title");
        check(prepared.Body.Contains("- Mod: "+typeof(Plugin).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion),"descriptor uses the loaded informational version");
        Call("OpenPreparedReport");
        check(launches.Count==2&&Get("_reportFile")==null,"unreviewed report refuses shell and export");
        Click("Open reviewed GitHub draft");
        check(launches.Count==2,"unreviewed draft button cannot dispatch");
        Set("_reportReviewed",true);Click("Open reviewed GitHub draft");
        check(launches.Count==3&&launches[^1].FileName==prepared.CreateBrowserSubmission(true).Url.AbsoluteUri&&Get("_reportFile")==null,
            "reviewed short draft dispatches the immutable sanitized issue URL without export");
        check(new Uri(launches[^1].FileName).AbsolutePath=="/d-b-c-e/dbce-mods-woden-rally-edge/issues/new","draft reaches the intended GitHub issue destination");
        Set("_reportReviewed",true);Set("_reportOpen",true);Set("_dirty",false);
        Set("_focus",-1);Set("_scroll",0f);
        GUI.TextReplacement="Changed title";Draw();
        check(Get("_reportPrepared")==null&&Get("_reportReviewed") is false,"editing title invalidates reviewed snapshot");
        check(Get("_dirty") is false,"temporary report edits do not dirty saved settings");
        Prepare();Set("_reportReviewed",true);Draw();
        Click("On");
        check(Get("_reportIncludeLog") is true&&Get("_reportPrepared")==null&&Get("_reportReviewed") is false,
            "changing log selection clears stale preview and review");
        check(Get("_dirty") is false,"log choice does not save owner settings");
        Set("_reportIncludeLog",false);Prepare();Set("_reportReviewed",true);
        // Exercise the description field separately from the title editor.
        GUI.TextReplacement="Changed description";Set("_focus",-1);Set("_scroll",320f);Draw();
        check(Get("_reportDescription") as string=="Changed description"&&Get("_reportPrepared")==null&&Get("_reportReviewed") is false,"editing description invalidates review");
        Set("_reportTitle","Long report user@example.com");Set("_reportDescription",new string('x',6000));
        string log=Path.Combine(root,"LogOutput.log");File.WriteAllText(log,"reviewed log marker\npassword=fixture-secret\nuser@example.com");
        Set("_reportIncludeLog",true);Prepare();Set("_reportReviewed",true);
        var longReport=(PreparedReport)Get("_reportPrepared")!;
        check(longReport.SelectedLogCount==1&&longReport.Body.Contains("reviewed log marker")&&!longReport.Preview.Contains("fixture-secret")&&!longReport.Preview.Contains("user@example.com"),
            "only fixture-selected log enters the sanitized long review");
        File.WriteAllText(log,"post-review changed log");
        Call("OpenPreparedReport");string exported=(string)Get("_reportFile")!;
        check(Path.GetFullPath(exported).StartsWith(root+Path.DirectorySeparatorChar)&&File.ReadAllText(exported)==longReport.Preview,
            "long handoff exports exactly the reviewed snapshot without rereading changed logs");
        check(launches[^1].FileName==longReport.CreateBrowserSubmission(true).Url.AbsoluteUri&&((string)Get("_reportStatus")!).Contains("attach that one file"),
            "long issue draft keeps useful setup and explicit attachment instructions");
        var bytes=File.ReadAllBytes(exported);Call("OpenPreparedReport");
        check((string)Get("_reportFile")! ==exported&&File.ReadAllBytes(exported).SequenceEqual(bytes)&&Directory.GetFiles(root,"support-report.txt",SearchOption.AllDirectories).Length==1,
            "retry retains one immutable reviewed attachment");
        Call("OpenReportFolder");
        check(launches[^1].FileName==Path.GetDirectoryName(exported)&&launches[^1].UseShellExecute,"folder handoff dispatches the exported report directory");
        int beforeFailure=launches.Count;
        Set("_reportShell",(Action<ProcessStartInfo>)(_=>throw new Win32Exception("fixture shell unavailable")));
        Call("OpenPreparedReport");
        check(((string)Get("_reportStatus")!).Contains("default browser")&&Get("_reportFile") as string==exported&&File.ReadAllBytes(exported).SequenceEqual(bytes),
            "browser failure leaves actionable retry status and intact attachment path");
        Call("OpenReportFolder");
        check(((string)Get("_reportStatus")!).Contains("local report path"),"folder failure points to manual recovery path");
        Set("_reportShell",(Action<ProcessStartInfo>)(_=>throw new InvalidOperationException("fixture shell unavailable")));
        Call("OpenPreparedReport");
        check(((string)Get("_reportStatus")!).Contains("default browser"),"invalid shell state is recoverable");
        Set("_reportShell",(Action<ProcessStartInfo>)(info=>launches.Add(info)));
        check(Call("OpenReportUrl",new Uri("https://example.com/")) is false&&Call("OpenReportUrl",new Uri("http://github.com/")) is false&&launches.Count==beforeFailure,
            "host refuses non-GitHub and insecure shell URLs");
        string blocked=Path.Combine(directory,"blocked-report-root");Directory.CreateDirectory(blocked);File.WriteAllText(Path.Combine(blocked,"WodenSupport"),"fixture export obstacle");
        Set("_reportRoot",(Func<string>)(()=>blocked));Set("_reportIncludeLog",false);Prepare();Set("_reportReviewed",true);Call("OpenPreparedReport");
        check(Get("_reportFile")==null&&launches.Count==beforeFailure&&((string)Get("_reportStatus")!).Contains("report folder"),
            "export failure keeps review, gives recovery instructions and never dispatches an incomplete draft");
        Set("_reportRoot",(Func<string>)(()=>root));Call("OpenPreparedReport");
        check(Get("_reportFile") is string&&launches.Count==beforeFailure+1,"export retry succeeds from the retained reviewed snapshot");
        Click("On");
        check(Get("_reportFile")==null&&Get("_reportPrepared")==null&&Get("_reportReviewed") is false&&File.ReadAllBytes(exported).SequenceEqual(bytes),
            "log change invalidates attachment handoff without deleting the prior export");
        Set("_reportIncludeLog",false);Prepare();Set("_reportReviewed",true);
        Set("_reportTitle","");Prepare();
        check(Get("_reportPrepared")==null&&Get("_reportReviewed") is false,"failed preparation cannot reuse an old report");
        var paginate=typeof(Panel).GetMethod("ReportPreviewPages",hidden)!;
        var pages=(List<string>)paginate.Invoke(null,new object[]{string.Concat(Enumerable.Repeat("完整预览😀 with line\n",100))})!;
        check(pages.Count>1&&pages.All(p=>p.Count(c=>c=='\n')<=9),"full preview is paged into bounded readable rows");
        check(Get("_dirty") is false&&!File.Exists(Path.Combine(directory,"help-report.cfg")),"all handoffs and failures leave settings unsaved");
        check(!Runtime.Settings.FfbEnabled,"report preparation does not enable forces");
        Panel.Close(false);
        }
        finally
        {
            Set("_reportRoot",oldRoot);Set("_reportShell",oldShell);
            Call("InvalidateReport");Set("_reportOpen",false);
        }
    }
}
