using System.Diagnostics;
using System.Text;
using Dbce.Wheel.Support;
using UnityEngine;

namespace WodenRallyEdge;

// Host UI and packaged metadata only. Shared source owns reading/redaction/drafts.
internal static partial class Panel
{
    private static readonly ReportProduct ReportProduct = new("Super Woden Rally Edge",Plugin.Version,"d-b-c-e/dbce-mods-woden-rally-edge");
    private static bool _reportOpen, _reportIncludeLog, _reportReviewed;
    private static string _reportTitle="", _reportDescription="", _reportStatus="Logs are optional. Review the report before opening GitHub.";
    private static string? _reportFile;
    private static PreparedReport? _reportPrepared;
    private static List<string> _reportPages=new();
    private static int _reportPage;

    private static void InvalidateReport()
    { _reportPrepared=null;_reportReviewed=false;_reportFile=null;_reportPages.Clear();_reportPage=0; }

    private static float ReportHelp(float y)
    {
        if(Button(250,y,250,"Submit GitHub Issue")){_reportOpen=!_reportOpen;_reportReviewed=false;}
        if(Button(512,y,194,"Mod on GitHub"))OpenReportUrl(ReportProduct.ModPage);
        if(Button(718,y,226,"Wheelkit on GitHub"))OpenReportUrl(ReportProduct.WheelkitPage);
        y+=50;
        if(!_reportOpen)return y;
        Label(250,y,694,"Describe the problem. Redaction is a best effort; check every preview page before sharing.",true,48);y+=58;
        Label(250,y,694,"Issue title");y+=32;
        ReportText(y,38,ref _reportTitle,200);y+=48;
        Label(250,y,694,"What happened, and what did you expect?");y+=32;
        ReportText(y,96,ref _reportDescription,10000);y+=106;
        Label(250,y+4,410,"Include BepInEx LogOutput.log:");
        if(Button(670,y,128,"Off",!_reportIncludeLog)&&_reportIncludeLog){_reportIncludeLog=false;InvalidateReport();}
        if(Button(810,y,134,"On",_reportIncludeLog)&&!_reportIncludeLog){_reportIncludeLog=true;InvalidateReport();}
        y+=48;
        Label(250,y,694,"Only the selected recent log excerpt is included (up to 24 KB). No settings, bindings, saves or recordings are collected.",true,48);y+=58;
        if(Button(250,y,250,"Review report"))PrepareReport();
        if(Button(512,y,194,"Cancel report")){_reportOpen=false;InvalidateReport();}
        y+=50;
        if(_reportPrepared!=null)
        {
            Label(250,y,694,"Redacted report preview · page "+(_reportPage+1)+" of "+_reportPages.Count);y+=36;
            Label(250,y,694,_reportPages[_reportPage],true,210);y+=220;
            if(Button(250,y,180,"Previous page",false,_reportPage>0))_reportPage--;
            if(Button(442,y,180,"Next page",false,_reportPage+1<_reportPages.Count))_reportPage++;
            y+=48;
            if(Button(250,y,694,_reportReviewed?"Reviewed: Yes — click to clear":"I reviewed the report — click to confirm",_reportReviewed))_reportReviewed=!_reportReviewed;
            y+=48;
            Label(250,y,694,"Opening GitHub sends the reviewed draft text. Attaching a file uploads it immediately; submit the issue on GitHub when ready.",true,58);y+=68;
            if(Button(250,y,340,"Open reviewed GitHub draft",false,_reportReviewed))OpenPreparedReport();
            if(_reportFile!=null&&Button(604,y,340,"Open report folder"))OpenReportFolder();
            y+=48;
        }
        Label(250,y,694,_reportStatus,true,80);y+=92;
        return y;
    }

    private static void ReportText(float y,float height,ref string value,int limit)
    {
        var rect=R(250,y,694,height);
        if(!Visible(rect))return;
        string changed=GUI.TextArea(rect,value,limit);
        if(changed!=value){value=changed;InvalidateReport();_reportStatus="Report changed. Review it again before sharing.";}
    }

    private static void PrepareReport()
    {
        InvalidateReport();
        try
        {
            string[] selected=_reportIncludeLog?new[]{Path.Combine(BepInEx.Paths.BepInExRootPath,"LogOutput.log")}:Array.Empty<string>();
            _reportPrepared=SupportReport.Prepare(ReportProduct,_reportTitle,_reportDescription,selected);
            _reportPages=ReportPreviewPages(_reportPrepared.Preview);
            _reportStatus="Review every page, then confirm. Nothing has been sent.";
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or System.Text.RegularExpressions.RegexMatchTimeoutException)
        { InvalidateReport();_reportStatus="Could not prepare the report. Check the title, description and selected text log, then review again."; }
    }

    private static List<string> ReportPreviewPages(string text)
    {
        // Presentation-only wrapping; export and browser use the original immutable snapshot.
        var pages=new List<string>();var page=new StringBuilder();int column=0,line=1;
        foreach(char c in text.Replace("\r\n","\n").Replace('\r','\n'))
        {
            if(c=='\n'||column>=40&&!char.IsLowSurrogate(c))
            {
                page.Append('\n');column=0;line++;
                if(line>9){pages.Add(page.ToString());page.Clear();line=1;}
                if(c=='\n')continue;
            }
            page.Append(c);column++;
        }
        if(page.Length>0||pages.Count==0)pages.Add(page.ToString());
        return pages;
    }

    private static void OpenPreparedReport()
    {
        if(_reportPrepared==null||!_reportReviewed)return;
        try
        {
            var submission=_reportPrepared.CreateBrowserSubmission(_reportReviewed);
            if(submission.NeedsAttachment&&_reportFile==null)
            {
                string folder=Path.Combine(BepInEx.Paths.BepInExRootPath,"WodenSupport","report-"+Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(folder);
                string path=Path.Combine(folder,"support-report.txt");
                _reportPrepared.WriteReviewFile(path);_reportFile=path;
            }
            if(OpenReportUrl(submission.Url))
                _reportStatus=submission.Instructions+(_reportFile==null?"":" Use Open report folder to find support-report.txt.");
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        { _reportStatus="Could not open the report. Check the title, report folder and browser, then try again."; }
    }

    private static bool OpenReportUrl(Uri url)
    {
        if(url.Scheme!="https"||url.Host!="github.com")return false;
        try{Process.Start(new ProcessStartInfo(url.AbsoluteUri){UseShellExecute=true});return true;}
        catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { Message=_reportStatus="Could not open GitHub. Check the default browser and try again.";return false; }
    }

    private static void OpenReportFolder()
    {
        if(_reportFile==null)return;
        try{Process.Start(new ProcessStartInfo(Path.GetDirectoryName(_reportFile)!){UseShellExecute=true});}
        catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { _reportStatus="Could not open the report folder. Use the local report path shown here."; }
    }
}
