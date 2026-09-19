using System.Reflection;
using System.Text.Json;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WodenRallyEdge;
using WodenRallyEdge.Core;

var outputDir=Path.GetFullPath("artifacts/ux-ui-fixture");Directory.CreateDirectory(outputDir);
var dir=Path.Combine(outputDir,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
int checks=0;void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
void Draw(Event? e=null){GUI.Commands.Clear();Event.current=e??new();Panel.Draw();Check(Runtime.Log.Errors.Count==0,string.Join("\n",Runtime.Log.Errors));}
DrawCommand? Find(string label,int index=0)=>GUI.Commands.Where(c=>c.Kind=="text"&&c.Text==label).Skip(index).FirstOrDefault();
void Click(string label,int index=0)
{
    Draw();var found=Find(label,index);
    for(int i=0;found==null&&i<25;i++) {var down=Find("Down");if(down==null)break;Draw(new(){type=EventType.MouseDown,mousePosition=new(down.Rect.x+20,down.Rect.y+12)});Draw();found=Find(label,index);}
    Check(found!=null,"Visible action: "+label);Draw(new(){type=EventType.MouseDown,mousePosition=new(found!.Rect.x+10,found.Rect.y+12)});Draw();
}
void BindAction(string label)
{
    Draw();for(int i=0;Find(label)==null&&i<20;i++)Click("Down");var row=Find(label)!;Check(row!=null,"Binding row "+label);
    // Production row's Bind control is 44 logical units below its action label.
    float scale=Screen.width>=3000?1.5f:1;Draw(new(){type=EventType.MouseDown,mousePosition=new(row!.Rect.x+25,row.Rect.y+54*scale)});Draw();
}
void ArmCapture()=>typeof(WheelInput).GetField("_captureAfter",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Runtime.Wheel,0d);
void KeyPress(Key key,bool modifier=false)
{
    Keyboard.current!.Clear();Keyboard.current[key].isPressed=true;Keyboard.current[key].wasPressedThisFrame=true;
    if(modifier)Keyboard.current[Key.LeftShift].isPressed=true;
    Runtime.Wheel!.UpdateCapture();Keyboard.current.Clear();
}
void Snapshot(string name){Draw();File.WriteAllText(Path.Combine(outputDir,name+".json"),JsonSerializer.Serialize(new{width=Screen.width,height=Screen.height,commands=GUI.Commands},new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));}

Runtime.Settings=new(new ConfigFile(Path.Combine(dir,"settings.cfg"),false));Runtime.Wheel=new(Path.Combine(dir,"bindings.json"));
var id=Guid.NewGuid();var device=new DeviceHub.Device{Info=new(){InstanceGuid=id,Name="MOZA R12"}};device.Axes[0]=32768;Runtime.Devices!.Devices.Add(device);
Runtime.Wheel.Bindings.Steer=new(id,0,new(0,65535,32768));Runtime.Wheel.Bindings.Throttle=new(id,2,new(0,65535));Runtime.Wheel.Bindings.Brake=new(id,5,new(0,65535));
Runtime.Wheel.Bindings.Buttons["Handbrake"]=new(id,18);Runtime.Wheel.Save();Runtime.Settings.WheelEnabled=true;
Runtime.Settings.FfbEnabled=false;Runtime.Settings.FfbSmoothing=61;Runtime.Settings.CameraAutoFit=false;Runtime.Settings.CameraHeight=1.23f;
Panel.Toggle();Snapshot("setup-720");Check(Find("Centre")!=null,"Steering uses Centre");Check(Find("Optional controls and buttons")!=null,"Simple setup next step fits 720p");
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
Click("FFB");Snapshot("ffb-720");Check(Find("Custom FFB tuning active. Review in Advanced")!=null,"Simple custom tune summary");
Click("Use steering wheel  v");Click("MOZA R12");Check(!Runtime.Settings.FfbFollowSteering&&Runtime.Settings.FfbGuid==id.ToString(),"direct dropdown saves explicit device");
Click("MOZA R12  v");Click("Use steering wheel");Check(Runtime.Settings.FfbFollowSteering&&!Runtime.Settings.FfbEnabled,"follow selection preserves Off");
Click("Cameras");Snapshot("cameras-720");Click("+ Adjustment bindings");BindAction("Move up");ArmCapture();KeyPress(Key.U);
Check(Runtime.Wheel.Bindings.CameraKeys["Camera up"]=="U","actual camera capture binds non-numpad key");
Click("Cameras");BindAction("Move down");ArmCapture();KeyPress(Key.U);Check(Runtime.Wheel.CaptureButton=="Camera down"&&Runtime.Wheel.Status.Contains("Already assigned"),"conflict rejects without silently removing other action");
KeyPress(Key.J,true);Check(Runtime.Wheel.CaptureButton!=null&&Runtime.Wheel.Status.Contains("Chords"),"modifier chord rejected");
Click("Cancel");Check(Runtime.Wheel.Bindings.CameraKeys["Camera down"]=="Numpad2","cancel retained previous shortcut");
Click("Cameras");BindAction("Move down");typeof(WheelInput).GetField("_captureDeadline",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Runtime.Wheel,-1d);Runtime.Wheel.UpdateCapture();Check(!Runtime.Wheel.Capturing&&Runtime.Wheel.Status.Contains("timed out"),"timeout preserves prior shortcut");
Click("Cameras");Click("Restore numpad defaults");Check(Runtime.Wheel.Bindings.CameraKeys["Camera pitch down"]=="Numpad1"&&Runtime.Settings.CameraHeight==1.23f,"default restore uses family tilt pair and preserves pose");
Runtime.Wheel.TryCommit(b=>{b.CameraKeys["Camera up"]="U";b.CameraKeys["Camera"]="Numpad8";});
var resetBefore=File.ReadAllText(Path.Combine(dir,"bindings.json"));var resetKeys=Runtime.Wheel.Bindings.CameraKeys.ToDictionary();
Click("Restore numpad defaults");Check(Panel.Message.Contains("Camera")&&Runtime.Wheel.Bindings.CameraKeys.SequenceEqual(resetKeys)&&File.ReadAllText(Path.Combine(dir,"bindings.json"))==resetBefore,"batch defaults reject camera-cycle collision before any saved/effective change");
Click("Cameras");BindAction("Move up");ArmCapture();KeyPress(Key.Numpad8);Check(Runtime.Wheel.Capturing&&Runtime.Wheel.Status.Contains("Already assigned to Camera"),"individual default key rejects same camera-cycle collision");Click("Cancel");
Runtime.Wheel.TryCommit(b=>{b.CameraKeys["Camera"]="None";b.CameraKeys["Handbrake"]="Numpad8";});Click("Cameras");Click("Restore numpad defaults");Check(Panel.Message.Contains("Handbrake")&&Runtime.Wheel.Bindings.CameraKeys["Camera up"]=="U","batch preflight covers saved game assignments too");
Runtime.Wheel.TryCommit(b=>b.CameraKeys.Remove("Handbrake"));Click("Restore numpad defaults");
Click("Telemetry");Click("Connection settings (Advanced)");
// Scroll to actual text fields, inject edited text through the GUI fixture.
for(int i=0;Find("Forza UDP port (0 = off)")==null&&i<10;i++)Click("Down");GUI.TextReplacement="9123";Draw();Draw();Click("Simple");Check(Runtime.Settings.UiView=="Advanced"&&Runtime.Settings.ForzaPort==8000,"unapplied connection edit locks presentation without runtime mutation");Click("Cancel");Click("Simple");Check(Runtime.Settings.ForzaPort==8000,"connection cancel retains working destination");
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
Keyboard.current![Key.Numpad8].isPressed=true;CameraShortcuts.Update();Panel.Close(false);CameraShortcuts.Update();
Check(Runtime.Settings.GetCameraPose(false)==previousPose,"actual shortcut source waits for release on panel close");
Keyboard.current.Clear();CameraShortcuts.Update();Keyboard.current[Key.Numpad8].isPressed=true;CameraShortcuts.Update();
Check(Math.Abs(Runtime.Settings.CameraHeight-previousPose.Height-.05f)<.0001f&&Runtime.Settings.GetCameraPose(true)==previousBumper,"actual shortcut moves only active mount");
Draw();Check(GUI.Commands.Any(c=>c.Text.StartsWith("Bonnet:")),"live camera adjustment reports active mount and value");
Runtime.Focused=false;CameraShortcuts.Update();Runtime.Focused=true;var afterTap=Runtime.Settings.GetCameraPose(false);CameraShortcuts.Update();Check(Runtime.Settings.GetCameraPose(false)==afterTap,"focus return cannot repeat a held shortcut");Keyboard.current.Clear();CameraShortcuts.Update();
Panel.Toggle();
string cfgPath=Path.Combine(dir,"settings.cfg");File.Move(cfgPath,cfgPath+".previous");Directory.CreateDirectory(cfgPath);
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
Directory.Delete(bindingPath);File.Move(bindingPath+".previous",bindingPath);Click("Retry save");Check(Runtime.Wheel.Bindings.CameraKeys["Camera up"]=="Numpad8"&&!Runtime.Wheel.Capturing,"failed defaults retry without changing the proposal");
Runtime.Wheel.BeginButton("Confirm");ArmCapture();var previousButton=Runtime.Wheel.Bindings.Buttons.GetValueOrDefault("Confirm");Runtime.Devices!.Pressed.Add(new(id,72));
File.Move(bindingPath,bindingPath+".previous");Directory.CreateDirectory(bindingPath);Runtime.Wheel.UpdateCapture();Check(Runtime.Wheel.CaptureButton=="Confirm"&&Runtime.Wheel.Bindings.Buttons.GetValueOrDefault("Confirm")==previousButton,"failed wheel button save preserves assignment and draft");
Directory.Delete(bindingPath);File.Move(bindingPath+".previous",bindingPath);Runtime.Devices.Pressed.Clear();Click("Retry save");Check(Runtime.Wheel.Bindings.Buttons["Confirm"].Button==72,"wheel button retry succeeds after releasing the captured button");
// Actual menu routing source only sends native UI events in menu/paused context.
Runtime.Local=null;var ui=new UnityEngine.EventSystems.EventSystem{firstSelectedGameObject=new()};UnityEngine.EventSystems.EventSystem.current=ui;
Panel.Close(false);Runtime.Devices.Pressed.Clear();MenuNavigation.Update();Runtime.Devices.Pressed.Add(new(id,72));MenuNavigation.Update();
Check(UnityEngine.EventSystems.ExecuteEvents.Delivered.Last()=="ISubmitHandler"&&ui.currentSelectedGameObject!=null,"bound Confirm dispatches to existing selected native menu");
int delivered=UnityEngine.EventSystems.ExecuteEvents.Delivered.Count;Runtime.Devices.Pressed.Clear();MenuNavigation.Update();Runtime.Local=new();Runtime.Devices.Pressed.Add(new(id,72));MenuNavigation.Update();Check(UnityEngine.EventSystems.ExecuteEvents.Delivered.Count==delivered,"native menu events excluded during driving");
Runtime.Devices.Pressed.Clear();Runtime.Local=null;Panel.Toggle();Click("Setup");Panel.MenuAction("Menu down");Panel.MenuAction("Confirm");Draw();Check(Runtime.Settings.UiView=="Advanced","wheel focus and Confirm activate panel header");Panel.MenuAction("Back");Check(!Panel.Open,"wheel Back closes panel");Panel.Toggle();
Panel.Close(false);Check(!Pause.Paused,"fixture pause restored");
var saved=new Settings(new ConfigFile(Path.Combine(dir,"settings.cfg"),false));Check(saved.UiView=="Advanced"&&!saved.FfbEnabled&&saved.FfbSmoothing==61,"reopen/restart retains explicit view and tune");
Console.WriteLine($"UI fixture: {checks} assertions passed. Actual Panel/WheelInput/Settings source, simulated Unity events/devices; no game or force output. Artifacts: {outputDir}");
