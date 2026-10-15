using System.Reflection;
using System.Text.Json;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WodenRallyEdge;
using WodenRallyEdge.Core;
using Dbce.Wheel.Ui.Unity;

var outputDir=Path.GetFullPath("artifacts/ux-ui-fixture");Directory.CreateDirectory(outputDir);
var dir=Path.Combine(outputDir,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
int checks=0;void Check(bool ok,string message)
{
    checks++;if(ok)return;
    string failure=Path.Combine(dir,"failure-ui.json");
    File.WriteAllText(failure,JsonSerializer.Serialize(new{message,width=Screen.width,height=Screen.height,commands=GUI.Commands},new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
    throw new Exception(message+"; trace: "+failure);
}
if(args.SequenceEqual(new[]{"--binding-save-status-only"}))
{
    SharedBindingChecks.SaveStatusChecks(dir,Check);
    Console.WriteLine($"PASS {checks} focused source-linked Woden binding Save status checks; no game/device/native module.");
    return;
}
if(args.SequenceEqual(new[]{"--close-release-only"}))
{
    CloseReleaseChecks.Run(dir,Check);
    Console.WriteLine($"PASS {checks} focused source-linked Woden close/release checks; no game/device/native module.");
    return;
}
if(args.SequenceEqual(new[]{"--help-report-only"}))
{
    HelpReportChecks.Run(dir,Check);
    Console.WriteLine($"PASS {checks} focused source-linked Woden Help report checks; no browser/export/game/device/native module.");
    return;
}
if(args.Length>0)throw new ArgumentException("Unknown fixture mode; refusing the full baseline.");
Screen.width=7680;Screen.height=1440;GUI.Commands.Clear();Event.current.type=EventType.Repaint;
var hudColor=GUI.color;var hudEnabled=GUI.enabled;var hudMatrix=GUI.matrix;
new StrippedSessionStatusHud(UiNative.Style).Draw(RecordingHudLine.Describe("Recording",65,new(),null,true,true)!,true,112);
Check(GUI.Commands.Any(c=>c.Kind=="text"&&c.Text.Contains("REC  01:05")&&c.Text.Contains("F6 > Telemetry > Stop recording")&&c.FontSize>0&&c.Wrap),
    "source-linked shared recording renderer shows the actual Woden stop route");
Check(GUI.Commands.Count(c=>c.Kind=="rect")==2&&GUI.Commands.Where(c=>c.Kind=="rect").All(c=>c.Rect.x>2560&&c.Rect.x+c.Rect.width<5120&&c.Rect.y>100),
    "source-linked recording renderer stays below stage banner on center display");
Check(GUI.color.Equals(hudColor)&&GUI.enabled==hudEnabled&&GUI.matrix.Equals(hudMatrix),
    "source-linked recording renderer restores IMGUI state");
Screen.width=1280;Screen.height=720;GUI.Commands.Clear();
StageLifecycleChecks.Run(dir, Check);
StageReplayChecks.Run(Check);
ShimChecks.Run(dir, Check);
SharedBindingChecks.Run(dir, Check);
void Draw(Event? e=null)
{
    bool repaint=e==null||e.type==EventType.Repaint;string viewBefore=Runtime.Settings.UiView;
    GUI.Commands.Clear();Event.current=e??new();Panel.Draw();Check(Runtime.Log.Errors.Count==0,string.Join("\n",Runtime.Log.Errors));
    if(!Panel.Open)return;
    float scale=FixtureScale();
    // A body action can change the view after this event already drew its sidebar.
    // The complete current tab set must be present on repaint; mutation events
    // still check every actually drawn tab's full bounds.
    var tabs=repaint&&viewBefore==Runtime.Settings.UiView?SettingsPresentation.Pages(Runtime.Settings.UiView):SettingsPresentation.CorePages
        .Concat(GUI.Commands.Any(c=>c.Kind=="text"&&c.Text=="Driving")?new[]{"Driving"}:Array.Empty<string>());
    foreach(string command in new[]{"Simple","Advanced","Close","Stop FFB"}.Concat(tabs))
    {
        var label=GUI.Commands.FirstOrDefault(c=>c.Kind=="text"&&c.Text==command);
        Check(label!=null&&label.Rect.x-8*scale>=0&&label.Rect.y-5*scale>=0&&label.Rect.x+label.Rect.width+8*scale<=Screen.width&&label.Rect.y-5*scale+Dbce.Wheel.Ui.F6Metrics.RowHeight*scale<=Screen.height,
            command+" complete button remains inside actual viewport after header/body/footer reservation");
    }
}
DrawCommand? Find(string label,int index=0)=>GUI.Commands.Where(c=>c.Kind=="text"&&c.Text==label).Skip(index).FirstOrDefault();
float FixtureScale()=>Math.Min((Screen.width-32f)/980,(Screen.width>=3000?1.5f:1)*Runtime.Settings.UiScale/100);
void Reach(Func<bool> visible,string description)
{
    Draw();
    for(int i=0;!visible()&&i<25;i++)
    {
        var down=Find("Down");if(down==null)break;
        Draw(new(){type=EventType.MouseDown,mousePosition=new(down.Rect.x+20,down.Rect.y+12)});Draw();
    }
    Check(visible(),"Reachable: "+description);
}
void Click(string label,int index=0)
{
    Reach(()=>Find(label,index)!=null,"action "+label);var found=Find(label,index)!;
    Draw(new(){type=EventType.MouseDown,mousePosition=new(found.Rect.x+10,found.Rect.y+12)});Draw();
}
void BindAction(string label)
{
    // A visible action label does not imply its lower Bind control fits the viewport.
    // Scroll until the actual control is drawn, then click that control.
    float scale=FixtureScale();
    DrawCommand? Control(){var row=Find(label);return row==null?null:GUI.Commands.FirstOrDefault(c=>c.Kind=="text"&&c.Text=="Bind"&&Math.Abs(c.Rect.x-row.Rect.x-8*scale)<1&&Math.Abs(c.Rect.y-row.Rect.y-49*scale)<1);}
    Reach(()=>Control()!=null,"binding control for "+label);var bind=Control();
    Draw(new(){type=EventType.MouseDown,mousePosition=new(bind!.Rect.x+10,bind.Rect.y+12)});Draw();
}
var stock=new GamePadSystem();
void StockFrame(){MenuOwnership.BeforeStock(stock);foreach(var pad in stock.Game_Pads)MenuOwnership.AfterRead(pad);MenuOwnership.AfterStock(stock);}
void ReleasedClose(){Keyboard.current?.Clear();Runtime.Devices!.Pressed.Clear();StockFrame();MenuOwnership.Tick();Runtime.Clock.Advance(.11);StockFrame();MenuOwnership.Tick();}
void ArmCapture(){StockFrame();MenuOwnership.CaptureReady();Runtime.Clock.Advance(.11);StockFrame();MenuOwnership.CaptureReady();typeof(WheelInput).GetField("_captureAfter",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Runtime.Wheel,0d);}
void KeyPress(Key key,bool modifier=false)
{
    Keyboard.current!.Clear();Keyboard.current[key].isPressed=true;Keyboard.current[key].wasPressedThisFrame=true;
    if(modifier)Keyboard.current[Key.LeftShift].isPressed=true;
    Runtime.Wheel!.UpdateCapture();Keyboard.current.Clear();
}
void Snapshot(string name){Draw();File.WriteAllText(Path.Combine(outputDir,name+".json"),JsonSerializer.Serialize(new{width=Screen.width,height=Screen.height,commands=GUI.Commands},new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));}

var settingsFile=new ConfigFile(Path.Combine(dir,"settings.cfg"),false);
Runtime.Settings=new(settingsFile);TripleView.Bind(settingsFile);SpanWindow.Bind(settingsFile);Runtime.Wheel=new(Path.Combine(dir,"bindings.json"));
var id=Guid.NewGuid();var device=new DeviceHub.Device{Info=new(){InstanceGuid=id,Name="MOZA R12"}};device.Axes[0]=32768;Runtime.Devices!.Devices.Add(device);
Runtime.Wheel.Bindings.Steer=new(id,0,new(0,65535,32768));Runtime.Wheel.Bindings.Throttle=new(id,2,new(0,65535));Runtime.Wheel.Bindings.Brake=new(id,5,new(0,65535));
Runtime.Wheel.Bindings.Buttons["Handbrake"]=new(id,18);Runtime.Wheel.Save();Runtime.Settings.WheelEnabled=true;
Runtime.Settings.FfbEnabled=false;Runtime.Settings.FfbSmoothing=61;Runtime.Settings.CameraAutoFit=false;Runtime.Settings.CameraHeight=1.23f;
// Exercise the actual no-car hotkey path after the live injected F6 smoke failed.
// This proves the menu gate, not Windows injection or the real InputSystem edge.
Keyboard.current![Key.F6].wasPressedThisFrame=true;Runtime.Focused=false;Panel.Update();Check(!Panel.Open,"unfocused F6 does not open settings");
Runtime.Focused=true;Panel.Update();Check(Panel.Open&&Runtime.Local==null,"focused F6 opens settings without a local car");
Keyboard.current.Clear();Panel.Update();Check(Panel.Open,"released F6 leaves settings open");
Time.frameCount++;Keyboard.current[Key.F6].wasPressedThisFrame=true;Panel.Update();Check(Panel.Open&&MenuOwnership.Closing,"fresh F6 requests close without handing input back immediately");ReleasedClose();Check(!Panel.Open,"F6 closes after verified release without a local car");
Panel.Toggle();Snapshot("setup-720");Check(Find("Center")!=null,"Steering uses Center");Check(Find("Optional controls and buttons")!=null,"Simple setup next step fits 720p");
Check(!GUI.Commands.Any(c=>c.Text.Contains("Countdown")),"Driving assist absent from Simple");
var before=File.ReadAllText(Path.Combine(dir,"bindings.json"));var opts=Runtime.Settings.ForceOptions;
Click("Advanced");Check(Runtime.Settings.UiView=="Advanced","Advanced action saves explicit view");Click("Driving");Click("Simple");
Check(Runtime.Settings.UiPage=="Setup"&&Runtime.Settings.UiView=="Simple","Advanced-only page falls back to Setup");
Check(Runtime.Settings.ForceOptions==opts&&!Runtime.Settings.FfbEnabled&&Runtime.Force!.Reconnects==0&&Runtime.Force.Changes==0,"view switches leave force/tune/device untouched");
Check(File.ReadAllText(Path.Combine(dir,"bindings.json"))==before&&Runtime.Settings.CameraHeight==1.23f,"view preserves bindings and pose");
Click("Controls");Snapshot("controls-720");Check(GUI.Commands.Count(c=>c.Text=="Bind")==4,"Four direct axis Bind controls fit at 720p");
Click("Bind",3);Check(Runtime.Wheel.CaptureAxis=="Handbrake","Handbrake Bind starts provisional capture directly");
Click("Advanced");Check(Runtime.Settings.UiView=="Simple","capture locks view");device.Axes[7]=54000;Runtime.Wheel.UpdateCapture();Snapshot("handbrake-calibration-720");Check(Find("Save calibration")!=null&&Find("Cancel")!=null,"calibration Save and Cancel fit alongside preview at 720p");
Click("Cancel");Check(Runtime.Wheel.Bindings.Handbrake==null&&Runtime.Wheel.Bindings.Buttons["Handbrake"].Button==18,"cancel preserves old button and unbound axis");
device.Axes[7]=0;Click("Controls");Click("Bind",3);device.Axes[7]=60000;Runtime.Wheel.UpdateCapture();device.Axes[7]=30000;Runtime.Wheel.UpdateCapture();
Check(Math.Abs(Runtime.Wheel.PreviewBinding!.Normalize(30000)-.5)<.001,"provisional half-pull preview");Click("Save calibration");
Check(Runtime.Wheel.Bindings.Handbrake?.Axis==7&&Runtime.Wheel.Bindings.Buttons["Handbrake"].Button==18,"save retains additive axis and button");
Click("FFB");Snapshot("ffb-720");
Reach(()=>Find("Custom FFB tuning active. Review in Advanced")!=null,"Simple custom-tune summary");
Check(Find("Custom FFB tuning active. Review in Advanced")!=null,"Simple custom tune summary remains reachable with shared stacked row spacing");
Click("FFB"); // Return to the device controls at the top before the next scenario.
Click("Use steering wheel  v");Click("MOZA R12");Check(!Runtime.Settings.FfbFollowSteering&&Runtime.Settings.FfbGuid==id.ToString(),"direct dropdown saves explicit device");
Click("MOZA R12  v");Click("Use steering wheel");Check(Runtime.Settings.FfbFollowSteering&&!Runtime.Settings.FfbEnabled,"follow selection preserves Off");
Click("Cameras");
DrawCommand? FpsOn()
{
    var label=Find("Show frame rate on screen:");if(label==null)return null;
    return GUI.Commands.FirstOrDefault(c=>c.Kind=="text"&&c.Text=="On"&&Math.Abs(c.Rect.y-label.Rect.y-37*FixtureScale())<1);
}
Reach(()=>Find("Show frame rate on screen:")!=null&&FpsOn()!=null,"frame-rate label and its actual On control");
Check(Find("Show frame rate on screen:")!=null,"F6 Cameras shows the frame-rate toggle (STD-023)");
var fpsOn=FpsOn()!;Draw(new(){type=EventType.MouseDown,mousePosition=new(fpsOn.Rect.x+10,fpsOn.Rect.y+12)});Draw();
Check(Runtime.Settings.ShowFrameRate,"frame-rate toggle turns the on-screen counter on");
Reach(()=>GUI.Commands.Any(c=>c.Kind=="text"&&c.Text.StartsWith("Frame rate, last 10 s: 52 fps")),"10-second frame-rate summary");
Check(GUI.Commands.Any(c=>c.Kind=="text"&&c.Text.StartsWith("Frame rate, last 10 s: 52 fps")),"F6 Cameras shows the reachable 10 s frame-rate summary (STD-024)");
Runtime.Settings.ShowFrameRate=false;
Click("Cameras");Snapshot("cameras-720");
// Real panel clicks must schedule the production Settings save without closing
// the panel or relying on another control's dirty state. The camera fixture uses
// real BepInEx entries, with the same deferred-save behavior as runtime.
Runtime.Clock.Advance(.8);Panel.Update();
foreach(var choice in new[]{("Off",TripleMode.Off,false),("Surround",TripleMode.Auto,false),("Separate monitors",TripleMode.Auto,true)})
{
    string prior=File.ReadAllText(settingsFile.ConfigFilePath);
    Click(choice.Item1);
    Check(TripleView.Mode==choice.Item2&&SpanWindow.Enabled==choice.Item3,"selector applies "+choice.Item1);
    Check(File.ReadAllText(settingsFile.ConfigFilePath)==prior,"selector defers disk write until debounce");
    Check(GUI.Commands.Any(c=>c.Text.EndsWith("· Saving…")),"selector displays pending save");
    Runtime.Clock.Advance(.8);Panel.Update();Draw();
    var reopened=new ConfigFile(settingsFile.ConfigFilePath,false){SaveOnConfigSet=false};
    Check(reopened.Bind("Triple","Mode",TripleMode.On).Value==choice.Item2&&reopened.Bind("Triple","SpanSeparateMonitors",!choice.Item3).Value==choice.Item3,"selector persists both keys without closing panel");
    Check(Panel.Open&&GUI.Commands.Any(c=>c.Text.EndsWith("· Saved")),"selector saved while panel remains open");
}
Click("+ Adjustment bindings");BindAction("Move up");ArmCapture();KeyPress(Key.U);
Check(Runtime.Wheel.Bindings.CameraKeys["Camera up"]=="U","actual camera capture binds non-numpad key");
Click("Cameras");BindAction("Move down");ArmCapture();KeyPress(Key.U);Check(Runtime.Wheel.CaptureButton=="Camera down"&&Runtime.Wheel.Status.Contains("Already assigned"),"conflict rejects without silently removing other action");
KeyPress(Key.J,true);Check(Runtime.Wheel.CaptureButton!=null&&Runtime.Wheel.Status.Contains("Chords"),"modifier chord rejected");
Click("Cancel");Check(Runtime.Wheel.Bindings.CameraKeys["Camera down"]=="Numpad3","cancel retained previous shortcut");
Click("Cameras");BindAction("Move down");typeof(WheelInput).GetField("_captureDeadline",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Runtime.Wheel,-1d);Runtime.Wheel.UpdateCapture();Check(!Runtime.Wheel.Capturing&&Runtime.Wheel.Status.Contains("timed out"),"timeout preserves prior shortcut");
Click("Cameras");Click("Restore numpad defaults");Check(Runtime.Wheel.Bindings.CameraKeys["Camera pitch down"]=="Numpad7"&&Runtime.Settings.CameraHeight==1.23f,"default restore uses family tilt pair and preserves pose");
Runtime.Wheel.TryCommit(b=>{b.CameraKeys["Camera up"]="U";b.CameraKeys["Camera"]="Numpad8";});
var resetBefore=File.ReadAllText(Path.Combine(dir,"bindings.json"));var resetKeys=Runtime.Wheel.Bindings.CameraKeys.ToDictionary();
Click("Restore numpad defaults");Check(Panel.Message.Contains("Camera")&&Runtime.Wheel.Bindings.CameraKeys.SequenceEqual(resetKeys)&&File.ReadAllText(Path.Combine(dir,"bindings.json"))==resetBefore,"batch defaults reject camera-cycle collision before any saved/effective change");
Click("Cameras");BindAction("Move up");ArmCapture();KeyPress(Key.Numpad8);Check(Runtime.Wheel.Capturing&&Runtime.Wheel.Status.Contains("Already assigned to Camera"),"individual default key rejects same camera-cycle collision");Click("Cancel");
Runtime.Wheel.TryCommit(b=>{b.CameraKeys["Camera"]="None";b.CameraKeys["Handbrake"]="Numpad8";});Click("Cameras");Click("Restore numpad defaults");Check(Panel.Message.Contains("Handbrake")&&Runtime.Wheel.Bindings.CameraKeys["Camera up"]=="U","batch preflight covers saved game assignments too");
Runtime.Wheel.TryCommit(b=>b.CameraKeys.Remove("Handbrake"));Click("Restore numpad defaults");
Click("Telemetry");Click("Connection settings (Advanced)");
// Scroll to actual text fields, inject edited text through the GUI fixture.
DrawCommand? ForzaField()
{
    var label=Find("Forza UDP port (0 = off)");if(label==null)return null;
    float scale=FixtureScale();return GUI.Commands.FirstOrDefault(c=>c.Kind=="text"&&Math.Abs(c.Rect.x-label.Rect.x-420*scale)<1&&Math.Abs(c.Rect.y-label.Rect.y+4*scale)<1&&Math.Abs(c.Rect.width-220*scale)<1);
}
Reach(()=>ForzaField()!=null,"Forza label and actual editable field");GUI.TextReplacement="9123";Draw();Draw();Click("Simple");Check(Runtime.Settings.UiView=="Advanced"&&Runtime.Settings.ForzaPort==8000,"unapplied connection edit locks presentation without runtime mutation");Click("Cancel");Click("Simple");Check(Runtime.Settings.ForzaPort==8000,"connection cancel retains working destination");
Click("Help");Snapshot("help-720");
Click("Setup");Draw(new(){type=EventType.KeyDown,keyCode=KeyCode.Tab});Draw(new(){type=EventType.KeyDown,keyCode=KeyCode.Return});Check(Runtime.Settings.UiView=="Advanced","header supports Tab/Enter activation");
Click("Simple");Screen.width=3840;Screen.height=2160;Snapshot("setup-4k");
foreach(var resolution in new[]{(1280,720),(3840,2160)})
foreach(var view in new[]{"Simple","Advanced"})
{
    Screen.width=resolution.Item1;Screen.height=resolution.Item2;Click(view);foreach(var page in SettingsPresentation.Pages(view)){Click(page);Draw();Check(Find("Close")!=null&&Find("Stop FFB")!=null&&Find("Simple")!=null&&Find("Advanced")!=null,"persistent controls: "+view+"/"+page);}
}
Click("Setup");Runtime.Settings.UiScale=150;Screen.width=1280;Screen.height=720;Snapshot("setup-720-scale150");Check(Find("Close")!=null&&Find("Stop FFB")!=null,"large scale retains escape routes");Runtime.Settings.UiScale=100;
Runtime.Local=new();MountedCamera.Cycle.StockChanged(4,0,true,true);
var previousPose=Runtime.Settings.GetCameraPose(false);var previousBumper=Runtime.Settings.GetCameraPose(true);
Keyboard.current![Key.Numpad9].isPressed=true;CameraShortcuts.Update();Panel.Close(false);CameraShortcuts.Update();
Check(Runtime.Settings.GetCameraPose(false)==previousPose,"actual shortcut source waits for release on panel close");
Keyboard.current.Clear();CameraShortcuts.Update();Keyboard.current[Key.Numpad9].isPressed=true;CameraShortcuts.Update();
Check(Math.Abs(Runtime.Settings.CameraHeight-previousPose.Height-Runtime.Settings.CameraMoveStep)<.0001f&&Runtime.Settings.GetCameraPose(true)==previousBumper,"actual shortcut moves only active mount");
Draw();Check(GUI.Commands.Any(c=>c.Text.StartsWith("Bonnet:")),"live camera adjustment reports active mount and value");
Runtime.Focused=false;CameraShortcuts.Update();Runtime.Focused=true;var afterTap=Runtime.Settings.GetCameraPose(false);CameraShortcuts.Update();Check(Runtime.Settings.GetCameraPose(false)==afterTap,"focus return cannot repeat a held shortcut");Keyboard.current.Clear();CameraShortcuts.Update();
// Actual shortcut and panel sources; only native renderer state is mocked here.
TripleView.Active=true;TripleView.MatchGameFov=false;
Runtime.Settings.CameraAutoFit=true;
var physicalBonnet=Runtime.Settings.GetCameraPose(false);var physicalBumper=Runtime.Settings.GetCameraPose(true);
var physicalMode=TripleView.Mode;var cameraKeys=Runtime.Wheel.Bindings.CameraKeys.ToDictionary();
CameraShortcuts.Update();
Check(!TripleView.MatchGameFov&&TripleView.Mode==physicalMode&&Runtime.Settings.CameraAutoFit&&Runtime.Settings.GetCameraPose(false)==physicalBonnet&&Runtime.Settings.GetCameraPose(true)==physicalBumper,
    "no-input physical view preserves mode, fit and both saved poses");
Check(Panel.Message==TripleView.FovLockHint(false)&&Panel.Message.Contains("33.6° vertical")&&Panel.Message.Contains(WodenRallyEdge.Core.FovLockText.SwitchPath),
    "first locked bonnet view explains the FOV lock before a key is tried");
Panel.Message="";CameraShortcuts.Update();
Check(Panel.Message==""&&!TripleView.MatchGameFov,"the FOV lock hint shows once per session and changes no setting");
foreach(var key in new[]{Key.NumpadPlus,Key.NumpadMinus})
{
    Keyboard.current[key].isPressed=true;CameraShortcuts.Update();
    Check(Runtime.Settings.CameraAutoFit&&Runtime.Settings.GetCameraPose(false)==physicalBonnet&&Runtime.Settings.GetCameraPose(true)==physicalBumper,
        "physical FOV shortcut cannot save an unused lens or disable fit");
    Draw();Check(GUI.Commands.Any(c=>c.Text==TripleView.FovLockMessage),"physical shortcut explains the inactive control");
    Keyboard.current.Clear();CameraShortcuts.Update();
}
Check(TripleView.FovLockMessage.Contains("33.6° vertical")&&TripleView.FovLockMessage.Contains(WodenRallyEdge.Core.FovLockText.SwitchPath),
    "locked key message names the actual angle and the one switch");
TripleView.MatchGameFov=true;
Keyboard.current[Key.NumpadPlus].isPressed=true;CameraShortcuts.Update();
Check(Math.Abs(Runtime.Settings.CameraFov-physicalBonnet.Fov-Runtime.Settings.CameraFovStep)<.0001f&&Runtime.Settings.GetCameraPose(true)==physicalBumper,
    "adjustable bonnet shortcut changes only its saved lens");
var adjustedBonnet=Runtime.Settings.GetCameraPose(false);
Keyboard.current.Clear();CameraShortcuts.Update();MountedCamera.Cycle.Advance(true,true);
Keyboard.current[Key.NumpadPlus].isPressed=true;CameraShortcuts.Update();
Check(Math.Abs(Runtime.Settings.GetCameraPose(true).Fov-physicalBumper.Fov-Runtime.Settings.CameraFovStep)<.0001f&&Runtime.Settings.GetCameraPose(false)==adjustedBonnet,
    "adjustable bumper shortcut changes only its saved lens");
Keyboard.current.Clear();CameraShortcuts.Update();MountedCamera.Cycle.Handoff();MountedCamera.Cycle.StockChanged(4,0,true,true);
var adjustedBumper=Runtime.Settings.GetCameraPose(true);
Panel.Toggle();Click("Cameras");Draw();
Check(TripleView.MatchGameFov&&TripleView.Mode==physicalMode&&Runtime.Settings.GetCameraPose(false)==adjustedBonnet&&Runtime.Settings.GetCameraPose(true)==adjustedBumper,
    "drawing Cameras preserves explicit mode and poses");
Click("Physical rig");
typeof(Panel).GetMethod("Save",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);
string cfgPath=Path.Combine(dir,"settings.cfg");
Check(!new ConfigFile(cfgPath,false).Bind("Triple","MatchGameFov",true).Value,"Physical rig persists through the existing key");
Click("Adjustable FOV");typeof(Panel).GetMethod("Save",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);
Check(new ConfigFile(cfgPath,false).Bind("Triple","MatchGameFov",false).Value&&TripleView.Mode==physicalMode&&Runtime.Settings.GetCameraPose(false)==adjustedBonnet&&Runtime.Settings.GetCameraPose(true)==adjustedBumper&&Runtime.Wheel.Bindings.CameraKeys.SequenceEqual(cameraKeys),
    "explicit FOV choice persists without changing poses, triple layout or saved keys");
Click("Physical rig");Draw();
Check(Find("Use Adjustable FOV")==null,"Simple view keeps the one Triple FOV selector, no duplicate switch");
Click("Advanced");Reach(()=>Find("Use Adjustable FOV")!=null,"locked Advanced FOV row offers one explicit switch");
Check(!TripleView.MatchGameFov,"the Advanced switch is offered, not applied");
Click("Use Adjustable FOV");Draw();
Check(TripleView.MatchGameFov&&Find("Use Adjustable FOV")==null&&TripleView.Mode==physicalMode&&Runtime.Settings.GetCameraPose(false)==adjustedBonnet&&Runtime.Settings.GetCameraPose(true)==adjustedBumper,
    "Advanced switch selects Adjustable FOV without changing poses or triple layout");
Click("Simple");
TripleView.Active=false;TripleView.MatchGameFov=false;
Runtime.Settings.SetCameraPose(false,physicalBonnet);Runtime.Settings.SetCameraPose(true,physicalBumper);Runtime.Settings.CameraAutoFit=false;
Click("Setup");
File.Move(cfgPath,cfgPath+".previous");Directory.CreateDirectory(cfgPath);
Panel.SettingsChanged();typeof(Panel).GetMethod("Save",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);Draw();Check(GUI.Commands.Any(c=>c.Text.Contains("Save failed")),"write failure stays visible in Simple/Advanced footer");Directory.Delete(cfgPath);File.Move(cfgPath+".previous",cfgPath);
File.WriteAllText(Path.Combine(dir,"broken-bindings.json"),"broken");var broken=new WheelInput(Path.Combine(dir,"broken-bindings.json"));Check(broken.SaveError?.Contains("could not load")==true,"binding load failure remains visible instead of disappearing into a readiness bar");
// Binding persistence failures must preserve both live mapping and provisional edit.
string bindingPath=Path.Combine(dir,"bindings.json");var originalHandbrake=Runtime.Wheel!.Bindings.Handbrake;
Runtime.Wheel.BeginAxis("Handbrake");device.Axes[7]=0;Runtime.Wheel.UpdateCapture();
Runtime.Devices!.Devices.Remove(device);Runtime.Wheel.FinishAxis();Check(Runtime.Wheel.Capturing&&Runtime.Wheel.Bindings.Handbrake==originalHandbrake&&Runtime.Wheel.Status.Contains("disconnected"),"lost capture device cannot replace saved binding at commit");Runtime.Devices.Devices.Add(device);
File.Move(bindingPath,bindingPath+".previous");Directory.CreateDirectory(bindingPath);
Runtime.Wheel.FinishAxis();Check(Runtime.Wheel.Capturing&&Runtime.Wheel.Bindings.Handbrake==originalHandbrake&&Runtime.Wheel.SaveError!=null,"failed axis save preserves live binding and draft");
Directory.Delete(bindingPath);File.Move(bindingPath+".previous",bindingPath);Runtime.Wheel.FinishAxis();Check(!Runtime.Wheel.Capturing&&Runtime.Wheel.SaveError==null,"axis save retries the same draft after recovery");
Click("Cameras");Runtime.Wheel.BeginButton("Camera up");ArmCapture();string previousKey=Runtime.Wheel.Bindings.CameraKeys["Camera up"];
File.Move(bindingPath,bindingPath+".previous");Directory.CreateDirectory(bindingPath);KeyPress(Key.J);
Check(Runtime.Wheel.CaptureButton=="Camera up"&&Runtime.Wheel.Bindings.CameraKeys["Camera up"]==previousKey,"failed camera save retains old key and capture");
Directory.Delete(bindingPath);File.Move(bindingPath+".previous",bindingPath);KeyPress(Key.U);Check(Runtime.Wheel.SavePending,"later input cannot overwrite the failed proposal");Click("Retry save");Check(Runtime.Wheel.CaptureButton==null&&Runtime.Wheel.Bindings.CameraKeys["Camera up"]=="J","Retry saves the exact failed key proposal without recapture");
File.Move(bindingPath,bindingPath+".previous");Directory.CreateDirectory(bindingPath);
Check(!Runtime.Wheel.TryCommit(proposed=>proposed.SetAxis("Handbrake",null))&&Runtime.Wheel.Bindings.Handbrake!=null,"failed Clear cannot erase effective axis");
Click("Cancel");Check(!Runtime.Wheel.SavePending&&Runtime.Wheel.Bindings.Handbrake!=null,"failed Clear offers Cancel and preserves axis");
var customKeys=Runtime.Wheel.Bindings.CameraKeys.ToDictionary();Check(!Runtime.Wheel.TryCommit(CameraTuning.RestoreAdjustmentKeys)&&Runtime.Wheel.Bindings.CameraKeys.SequenceEqual(customKeys),"failed defaults do not alter effective keys");
Directory.Delete(bindingPath);File.Move(bindingPath+".previous",bindingPath);Click("Retry save");Check(Runtime.Wheel.Bindings.CameraKeys["Camera up"]=="Numpad9"&&!Runtime.Wheel.Capturing,"failed defaults retry without changing the proposal");
Runtime.Wheel.BeginButton("Confirm");ArmCapture();var previousButton=Runtime.Wheel.Bindings.Buttons.GetValueOrDefault("Confirm");Runtime.Devices!.Pressed.Add(new(id,72));
File.Move(bindingPath,bindingPath+".previous");Directory.CreateDirectory(bindingPath);Runtime.Wheel.UpdateCapture();Check(Runtime.Wheel.CaptureButton=="Confirm"&&Runtime.Wheel.Bindings.Buttons.GetValueOrDefault("Confirm")==previousButton,"failed wheel button save preserves assignment and draft");
Directory.Delete(bindingPath);File.Move(bindingPath+".previous",bindingPath);Runtime.Devices.Pressed.Clear();Click("Retry save");Check(Runtime.Wheel.Bindings.Buttons["Confirm"].Button==72,"wheel button retry succeeds after releasing the captured button");
// Actual menu routing source only sends native UI events in menu/paused context.
Runtime.Local=null;var ui=new UnityEngine.EventSystems.EventSystem{firstSelectedGameObject=new()};UnityEngine.EventSystems.EventSystem.current=ui;
Panel.Close(false);Runtime.Devices.Pressed.Clear();MenuNavigation.Update();Runtime.Devices.Pressed.Add(new(id,72));MenuNavigation.Update();
Check(UnityEngine.EventSystems.ExecuteEvents.Delivered.Last()=="ISubmitHandler"&&ui.currentSelectedGameObject!=null,"bound Confirm dispatches to existing selected native menu");
int delivered=UnityEngine.EventSystems.ExecuteEvents.Delivered.Count;Runtime.Devices.Pressed.Clear();MenuNavigation.Update();Runtime.Local=new();Runtime.Devices.Pressed.Add(new(id,72));MenuNavigation.Update();Check(UnityEngine.EventSystems.ExecuteEvents.Delivered.Count==delivered,"native menu events excluded during driving");
Runtime.Devices.Pressed.Clear();Runtime.Local=null;Panel.Toggle();Click("Setup");Panel.MenuAction("Menu down");Panel.MenuAction("Confirm");Draw();Check(Runtime.Settings.UiView=="Advanced","wheel focus and Confirm activate panel header");Panel.MenuAction("Back");Check(MenuOwnership.Closing,"wheel Back requests release before close");ReleasedClose();Check(!Panel.Open,"wheel Back closes panel after release");Panel.Toggle();
Panel.Close(false);Check(!Pause.Paused,"fixture pause restored");
// Real ownership/aggregation source: retained stock references, remapped actions,
// held keyboard/Settings/wheel/pad/pointer, and failed/stale reads all gate handoff.
var pad=new GamePadSystem.Game_Pad{PadActions=new[]{new GamePadSystem.Actions("custom menu action",0,false,51,KeyCode.Space)},InputFloats=new float[60]};stock.Game_Pads.Add(pad);
var retainedActions=pad.PadActions;var retainedFloats=pad.InputFloats;
ui.enabled=true;Panel.Toggle();Check(!ui.enabled,"opening owns stock Unity pointer/navigation dispatch");
pad.Dpad_Up=true;pad.InputFloats[51]=1;pad.PadActions[0]=pad.PadActions[0] with {value=1,Pressed=true};StockFrame();
Check(retainedActions[0].value==0&&!retainedActions[0].Pressed&&retainedFloats[51]==0&&!pad.Dpad_Up,"existing stock action/raw references are neutralized in place");
Check(retainedActions[0].AssignedFloat==51&&retainedActions[0].KeyBoard_Key==KeyCode.Space&&retainedActions[0].Available,"suppression preserves actual configured bindings");
Check(MenuOwnership.Aggregate(Runtime.Clock.Elapsed.TotalSeconds).Held,"raw configured stock input is observed before mask");
Panel.Close(true);Runtime.Clock.Advance(.2);pad.Back=true;StockFrame();MenuOwnership.Tick();Check(Panel.Open&&!ui.enabled,"held stock Back cannot pass through close");
Keyboard.current![Key.A].isPressed=true;Runtime.Clock.Advance(.2);StockFrame();MenuOwnership.Tick();Check(Panel.Open,"arbitrary held keyboard key delays close");Keyboard.current.Clear();
Runtime.Wheel.Bindings.Buttons["Settings panel"]=new(id,22);Runtime.Devices.Pressed.Add(new(id,22));Runtime.Clock.Advance(.2);StockFrame();MenuOwnership.Tick();Check(Panel.Open,"held configured Settings wheel control delays close");Runtime.Devices.Pressed.Clear();
Runtime.Devices.Pressed.Add(new(id,72));Runtime.Clock.Advance(.2);StockFrame();MenuOwnership.Tick();Check(Panel.Open,"held configured wheel Confirm delays close");Runtime.Devices.Pressed.Clear();
Mouse.current=new();Mouse.current.leftButton.isPressed=true;Runtime.Clock.Advance(.2);StockFrame();MenuOwnership.Tick();Check(Panel.Open,"pointer remains owned through held close click");Mouse.current=null;
var keyboard=Keyboard.current;Keyboard.current=null;Input.FailRead=true;Runtime.Clock.Advance(.2);StockFrame();MenuOwnership.Tick();Check(Panel.Open,"missing keyboard and failed fallback are unknown rather than neutral");Input.FailRead=false;Keyboard.current=keyboard;
Runtime.Clock.Advance(.2);MenuOwnership.Tick();Check(Panel.Open,"stale stock input cannot release ownership");
MenuOwnership.BeforeStock(stock);MenuOwnership.AfterStock(stock);MenuOwnership.Tick();Check(Panel.Open&&!MenuOwnership.Aggregate(Runtime.Clock.Elapsed.TotalSeconds).Known,"skipped native read is not fresh neutral input");
StockFrame();MenuOwnership.Tick();Runtime.Clock.Advance(.05);StockFrame();MenuOwnership.Tick();Check(Panel.Open,"neutral input must persist for release interval");Runtime.Clock.Advance(.06);StockFrame();MenuOwnership.Tick();Check(!Panel.Open&&ui.enabled,"fresh neutral interval returns stock dispatch once");
// Capture uses the same aggregation and release interval, then accepts a new edge.
Panel.Toggle();Runtime.Wheel.BeginButton("Menu left");Runtime.Devices.Pressed.Add(new(id,22));Runtime.Wheel.UpdateCapture();Runtime.Clock.Advance(.4);StockFrame();Runtime.Wheel.UpdateCapture();Check(Runtime.Wheel.CaptureButton=="Menu left"&&!MenuOwnership.CaptureReady(),"held Settings cannot become captured menu binding");
Runtime.Devices.Pressed.Clear();StockFrame();MenuOwnership.CaptureReady();Runtime.Clock.Advance(.11);StockFrame();Check(MenuOwnership.CaptureReady(),"capture arms only after shared neutral interval");Runtime.Devices.Pressed.Add(new(id,74));Runtime.Wheel.UpdateCapture();Check(Runtime.Wheel.Bindings.Buttons["Menu left"].Button==74&&!Runtime.Wheel.Capturing,"new edge binds after release");Runtime.Devices.Pressed.Clear();Panel.Close(false);
Panel.Toggle();Runtime.Wheel.BeginButton("Menu right");ArmCapture();Runtime.Focused=false;Panel.Update();Runtime.Focused=true;Runtime.Devices.Pressed.Add(new(id,22));StockFrame();Runtime.Wheel.UpdateCapture();Check(Runtime.Wheel.CaptureButton=="Menu right"&&!MenuOwnership.CaptureReady(),"focus loss requires a new capture release before accepting held Settings");Runtime.Devices.Pressed.Clear();Runtime.Wheel.Cancel();Panel.Close(false);
foreach(bool skipped in new[]{false,true})
{
    Panel.Toggle();Runtime.Wheel.BeginButton("Menu right");ArmCapture();var previous=Runtime.Wheel.Bindings.Buttons.GetValueOrDefault("Menu right");
    if(skipped){MenuOwnership.BeforeStock(stock);MenuOwnership.AfterStock(stock);}else Runtime.Clock.Advance(.2);
    Runtime.Devices.Pressed.Add(new(id,75));Runtime.Wheel.UpdateCapture();Check(Runtime.Wheel.CaptureButton=="Menu right"&&Runtime.Wheel.Bindings.Buttons.GetValueOrDefault("Menu right")==previous,"armed capture rejects new edge after "+(skipped?"skipped":"stale")+" stock read");
    Runtime.Devices.Pressed.Clear();StockFrame();Runtime.Wheel.UpdateCapture();Check(Runtime.Wheel.Capturing&&!MenuOwnership.CaptureReady(),"recovery restarts neutral interval instead of reusing readiness");Runtime.Clock.Advance(.11);StockFrame();Check(MenuOwnership.CaptureReady(),"fresh neutral interval rearms capture after failed observation");Runtime.Devices.Pressed.Add(new(id,75));Runtime.Wheel.UpdateCapture();Check(!Runtime.Wheel.Capturing&&Runtime.Wheel.Bindings.Buttons["Menu right"].Button==75,"new healthy edge binds after observation recovery");Runtime.Devices.Pressed.Clear();Panel.Close(false);
}
// A failed panic read used to silently skip settings handling in the same try.
Time.frameCount++;Keyboard.current!.FailF8=true;Keyboard.current[Key.F6].wasPressedThisFrame=true;Panel.Update();Check(Panel.Open&&Runtime.Log.Warnings.Any(w=>w.Contains("fixture F8 read failed")),"F8 read failure is logged and cannot skip F6");Keyboard.current.FailF8=false;Keyboard.current.Clear();Panel.Close(false);
Time.frameCount++;Keyboard.current=null;Runtime.Devices.Pressed.Add(new(id,22));Panel.Update();Check(Panel.Open&&Panel.HotkeyStatus.StartsWith("Settings input observed"),"bound Settings remains usable without Keyboard.current");Runtime.Devices.Pressed.Clear();Panel.Close(false);
Time.frameCount++;Draw(new(){type=EventType.KeyDown,keyCode=KeyCode.F6});Check(Panel.Open&&Panel.HotkeyStatus.EndsWith("IMGUI F6"),"closed panel accepts existing IMGUI keyboard route without Keyboard.current");StockFrame();Input.Held=true;Check(MenuOwnership.Aggregate(Runtime.Clock.Elapsed.TotalSeconds) is (true,true),"legacy keyboard held state participates when InputSystem keyboard is absent");Input.Held=false;Check(MenuOwnership.Aggregate(Runtime.Clock.Elapsed.TotalSeconds) is (true,false),"legacy keyboard can establish known neutral release");
Keyboard.current=keyboard;Keyboard.current![Key.F6].wasPressedThisFrame=true;Panel.Update();Check(Panel.Open&&!MenuOwnership.Closing,"InputSystem and IMGUI F6 in same frame only toggle once");Keyboard.current.Clear();Time.frameCount++;Draw(new(){type=EventType.KeyDown,keyCode=KeyCode.F6});Check(!MenuOwnership.Closing,"held IMGUI key repeat does not toggle twice");Draw(new(){type=EventType.KeyUp,keyCode=KeyCode.F6});Time.frameCount++;Draw(new(){type=EventType.KeyDown,keyCode=KeyCode.F6});Check(MenuOwnership.Closing,"released and repressed IMGUI F6 requests close");Draw(new(){type=EventType.KeyUp,keyCode=KeyCode.F6});ReleasedClose();Check(!Panel.Open,"IMGUI close obeys shared release barrier");
var staleCar=new MainCar{MyControls=new()};typeof(WheelInput).GetField("_last",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Runtime.Wheel,new AppliedInput(staleCar.GetInstanceID(),Runtime.Clock.Elapsed.TotalSeconds,.7f,.5f,0,.8f));Runtime.Wheel.HandbrakeCar=staleCar;Runtime.Wheel.HandbrakeAmount=.8f;device.Ok=false;
Check(Runtime.Wheel.Apply(new Controls{field_Private_MainCar_0=staleCar})==null&&Runtime.Wheel.LastFor(staleCar)==null&&Runtime.Wheel.HandbrakeCar==null&&Runtime.Wheel.HandbrakeAmount==0,"shared primary device failure clears previous effective handbrake/input sample");device.Ok=true;
StartupMenuChecks.Run(Check,StockFrame,id);
CustomMenuChecks.Run(Check,id);
var saved=new Settings(new ConfigFile(Path.Combine(dir,"settings.cfg"),false));Check(saved.UiView=="Advanced"&&!saved.FfbEnabled&&saved.FfbSmoothing==61,"reopen/restart retains explicit view and tune");
Runtime.Devices.Pressed.Clear();Keyboard.current ??= new();Keyboard.current.Clear();
Panel.Close(false);Panel.Toggle();
var shared = new ButtonBinding(id, 91);
Runtime.Wheel.TryCommit(b=>b.Buttons["Confirm"]=shared);
foreach(var action in new[]{"Back","Camera"}) {
    Runtime.Devices.Pressed.Clear();Runtime.Wheel.BeginButton(action);ArmCapture();
    Runtime.Devices.Pressed.Add(shared);Runtime.Wheel.UpdateCapture();
    Check(!Runtime.Wheel.Capturing&&Runtime.Wheel.Bindings.Buttons[action]==shared&&Runtime.Wheel.Bindings.Buttons["Confirm"]==shared,"duplicate "+action+" capture retains Confirm without clearing it");
}
Runtime.Devices.Pressed.Clear();Panel.Close(false);
var persisted=Bindings.Load(bindingPath);
Check(persisted.Buttons["Confirm"]==shared&&persisted.Buttons["Back"]==shared&&persisted.Buttons["Camera"]==shared,"same and different-context button assignments survive restart");
Runtime.Local=new MainCar();Runtime.Devices.Pressed.Add(shared);
long beforeMenu=MenuNavigation.Delivered;MenuNavigation.Update();
Check(MenuNavigation.Delivered==beforeMenu,"Confirm/Back cannot dispatch to native menu during racing");
Check(Runtime.Wheel.Button("Camera",false),"same physical assignment remains available to gameplay reader");
Runtime.Devices.Pressed.Clear();Runtime.Local=null;
Panel.Toggle(); // Capture is a settings-panel workflow; producer observation is scoped to it.
foreach(var neutralPad in stock.Game_Pads)MenuOwnership.Mask(neutralPad);
Input.Held=false;Input.Keys.Clear();Input.Down.Clear();Keyboard.current.Clear();
foreach(int hat in new[]{128,130,132,134,129,131,133,135,159}) {
    Runtime.Wheel.BeginButton("Menu up");ArmCapture();Check(MenuOwnership.CaptureReady(),"fresh neutral fixture arms HAT capture");Runtime.Devices.Pressed.Add(new(id,hat));Runtime.Wheel.UpdateCapture();
    Check(!Runtime.Wheel.Capturing&&Runtime.Wheel.Bindings.Buttons["Menu up"].Button==hat,"POV cardinal/diagonal index captures as one binding: "+hat+" status="+Runtime.Wheel.Status+" active="+Runtime.Wheel.CaptureButton);
    Runtime.Devices.Pressed.Clear();
}
Check(Bindings.Load(bindingPath).Buttons["Menu up"].Button==159,"HAT binding persists without physical-button alias");
if(!Panel.Open)Panel.Toggle();Click("Controls");Click("+ Shifter bindings");Reach(()=>Find("Gear R")!=null&&Find("Gear 6")!=null,"H-pattern reverse and sixth-gear labels");Check(Find("Gear R")!=null&&Find("Gear 6")!=null,"F6 Controls lists the H-pattern gates");Reach(()=>GUI.Commands.Any(c=>c.Kind=="text"&&c.Text.StartsWith("H-pattern: hold a gate")),"H-pattern help below gate rows");Check(GUI.Commands.Any(c=>c.Kind=="text"&&c.Text.StartsWith("H-pattern: hold a gate")),"shifter help explains gates, out of gear and R");
ShimChecks.PanelCheck(dir,Check);
// Close-intent integration uses the actual panel and an injected accepted reader,
// while preserving the existing native JSON and active startup profile.
Panel.Close(false);Runtime.Local=null;Runtime.Devices.Pressed.Clear();Keyboard.current!.Clear();
string sharedPath=Path.Combine(dir,"panel-shared-controls.ini");
var sharedInstance=Guid.NewGuid();var sharedProduct=Guid.NewGuid();
byte[] sharedStartup=System.Text.Encoding.UTF8.GetBytes("[Controls]\nSchema = 1\nProfile = Panel fixture\nRevision = initial\nTransmission = Sequential\nsteer = "+
    Dbce.Wheel.Input.ControlBinding.FromCapture("steer",Dbce.Wheel.Input.ControlKind.Axis,0,1,32768,sharedInstance,sharedProduct,"Fixture wheel",0,65535)+"\n");
File.WriteAllBytes(sharedPath,sharedStartup);
var panelHost=new SharedBindingHost(sharedPath,sharedStartup,()=>Runtime.Clock.Elapsed.TotalSeconds,()=>true);
panelHost.Observe(new(sharedInstance,sharedProduct,"Fixture wheel","fixture-path",1,1,Runtime.Clock.Elapsed.TotalSeconds,
    new(new[]{32768,0,0,0,0,0,0,0},new bool[128],new[]{-1,-1,-1,-1}),new int[8],Enumerable.Repeat(65535,8).ToArray()));
var currentHostProperty=typeof(SharedBindingHost).GetProperty("Current",BindingFlags.Static|BindingFlags.NonPublic)!;
var retainedHost=SharedBindingHost.Current;currentHostProperty.SetValue(null,panelHost);
try
{
    foreach(var resolution in new[]{(1280,720),(7680,1440)})
    {
        Screen.width=resolution.Item1;Screen.height=resolution.Item2;Runtime.Settings.UiScale=100;
        Panel.Close(false);Panel.Toggle();Click("Controls");
        // The sidebar Controls click does not scroll the body: check its initial viewport.
        Check(Find("Save bindings")!=null&&Find("Discard changes")!=null&&Find("Capture device")!=null,
            "shared binding toolbar and device selector fit initially at "+resolution);
        var first=panelHost.Rows[0];
        Check(Find(first.Label)!=null&&Find("Active: "+first.Active.Text)!=null,
            "first shared binding label and active value fit initially at "+resolution);
        Check(GUI.Commands.Any(c=>c.Kind=="text"&&c.Text.Contains("Set Up Controls.bat")&&c.Text.Contains("devices already opened")),
            "standalone setup entry and initial device limitation remain visible at "+resolution);
        Check(GUI.Commands.Any(c=>c.Kind=="text"&&c.Text==panelHost.AccessReason)&&GUI.Commands.Any(c=>c.Kind=="text"&&c.Text==panelHost.CaptureMessage),
            "draft/restart and capture guidance remain visible at "+resolution);
        Snapshot("shared-controls-initial-"+resolution.Item1+"x"+resolution.Item2);
    }
    Screen.width=1280;Screen.height=720;Panel.Close(false);Panel.Toggle();Click("Controls");
    panelHost.BeginCapture("steer",sharedInstance.ToString("D"));Draw();
    Check(panelHost.CapturingAction=="steer"&&Find("Cancel capture")!=null&&GUI.Commands.Any(c=>c.Kind=="text"&&c.Text.StartsWith("Binding steer")),
        "capture instructions and Cancel retain their initial viewport space");
    panelHost.CancelCapture();Draw();
    panelHost.SetInverted("steer",true);Click("FFB");
    Check(panelHost.HasDraft&&GUI.Commands.Any(c=>c.Kind=="text"&&c.Text.StartsWith("Bindings unsaved")),"shared draft warning remains in fixed footer after switching to FFB");
    Click("Close");Draw();
    Check(Panel.Open&&panelHost.HasDraft&&!MenuOwnership.Closing&&Find("Discard and close")!=null&&Find("Keep editing")!=null,
        "closing from FFB with a shared draft shows global discard or keep-editing before ownership release");
    Click("Keep editing");Check(Panel.Open&&panelHost.HasDraft&&File.ReadAllBytes(sharedPath).SequenceEqual(sharedStartup),"keep editing preserves the draft and original profile bytes");
    Click("Cameras");Click("Close");
    Check(Find("Discard and close")!=null&&Find("Keep editing")!=null,"closing from Cameras shows the same global draft warning");
    Click("Discard and close");
    Check(!panelHost.HasDraft&&MenuOwnership.Closing&&File.ReadAllBytes(sharedPath).SequenceEqual(sharedStartup),"explicit discard requests ordinary release without saving the profile");
    ReleasedClose();Check(!Panel.Open,"discard-and-close retains the verified input release barrier");
}
finally{Panel.Close(false);currentHostProperty.SetValue(null,retainedHost);}
Console.WriteLine($"UI fixture: {checks} assertions passed. Actual Panel/WheelInput/Settings source, simulated Unity events/devices; no game or force output. Artifacts: {outputDir}");
