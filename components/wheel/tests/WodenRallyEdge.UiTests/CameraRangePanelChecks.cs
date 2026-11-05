using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WodenRallyEdge;
using WodenRallyEdge.Core;

internal static class CameraRangePanelChecks
{
    internal static void Run(string directory,Action<bool,string> check)
    {
        string[] rows={"Current camera FOV (degrees)","Side offset (m)","Height offset (m)","Forward offset (m)","Tilt offset (degrees)"};
        string[] fields={"Fov","Side","Height","Forward","Pitch"};
        foreach(int width in new[]{1280,860,540,360})foreach(bool lower in new[]{false,true})
        {
            Screen.width=width;Screen.height=720;Keyboard.current=new();Runtime.Local=new();Runtime.Focused=true;
            string path=Path.Combine(directory,$"camera-panel-{width}-{lower}.cfg");
            var config=new ConfigFile(path,false);Runtime.Settings=new(config);
            Runtime.Settings.FfbEnabled=false;Runtime.Settings.UiView="Advanced";Runtime.Settings.UiPage="Cameras";
            TripleView.Bind(config);SpanWindow.Bind(config);TripleView.Mode=TripleMode.On;
            TripleView.FramingEnabled=true;TripleView.PanelFovRequest=lower?-300:500;TripleView.PanelFovEffective=lower?1:179;
            Runtime.Wheel=new(Path.Combine(directory,$"camera-panel-{width}-{lower}-bindings.json"));
            Runtime.Settings.Save();var before=File.ReadAllBytes(path);
            var force=Runtime.Force!;int changes=force.Changes,reconnects=force.Reconnects;
            Panel.Toggle();var reached=new HashSet<string>();bool angles=false;
            void Draw(Event? input=null)
            {
                typeof(Panel).GetField("_focus",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,-1);
                GUI.Commands.Clear();Event.current=input??new();Panel.Draw();
                check(Runtime.Log.Errors.Count==0,"actual panel draws without logged errors");
            }
            Draw();
            for(int step=0;step<30&&reached.Count<rows.Length;step++)
            {
                angles|=GUI.Commands.Any(c=>c.Kind=="text"&&c.Text.StartsWith("Requested FOV ")&&c.Text.Contains("Effective lens "));
                float scale=Math.Min((width-32f)/Panel.Width,Runtime.Settings.UiScale/100);
                for(int i=0;i<rows.Length;i++)
                {
                    var row=GUI.Commands.FirstOrDefault(c=>c.Kind=="text"&&c.Text==rows[i]);
                    if(row==null||reached.Contains(rows[i]))continue;
                    var command=GUI.Commands.FirstOrDefault(c=>c.Kind=="text"&&c.Text=="Default"&&(Math.Abs(c.Rect.y-row.Rect.y-5*scale)<1||Math.Abs(c.Rect.y-row.Rect.y-37*scale)<1));
                    if(command==null)continue;
                    check(command.Rect.x-8*scale>=0&&command.Rect.x+command.Rect.width+8*scale<=width&&command.Rect.y>=0&&command.Rect.y+command.Rect.height<=720,"complete Default bounds inside actual viewport");
                    check(Math.Abs(command.Rect.width-80*scale)<1,"actual numeric row reserves96px Default button including text padding");
                    int count=TripleView.DefaultCommands;
                    Draw(new(){type=EventType.MouseDown,button=0,mousePosition=new(command.Rect.x+4*scale,command.Rect.y+4*scale)});
                    check(TripleView.DefaultCommands==count+1&&TripleView.LastDefault==fields[i],"actual full-panel Default dispatch reaches selected field only");
                    reached.Add(rows[i]);Draw();
                }
                Draw(new(){type=EventType.KeyDown,keyCode=KeyCode.PageDown});Draw();
            }
            check(reached.Count==5,"all five current camera controls reachable by actual page scrolling width="+width+" reached="+string.Join(",",reached));
            check(angles,"requested and effective lens readout reachable");
            check(File.ReadAllBytes(path).SequenceEqual(before),"actual draw and field commands never call whole Settings save");
            check(force.Changes==changes&&force.Reconnects==reconnects,"camera panel does not reconnect or change force");
            Panel.Close(false);MenuOwnership.CancelClose();TripleView.FramingEnabled=false;
        }
    }
}
