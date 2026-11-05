using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BepInEx.Configuration;
using UnityEngine.InputSystem;
using WodenRallyEdge;
using WodenRallyEdge.Core;

internal static class CameraSetKeyChecks
{
    internal static void Run(string directory, Action<bool,string> check)
    {
        string configPath=Path.Combine(directory,"camera-keys.cfg"),bindingPath=Path.Combine(directory,"camera-keys.json");
        File.WriteAllText(configPath,"# owner formatting\r\n[Triple]\r\nBonnetRealistic = false\r\nBonnetGameOffsetDegrees = 4\r\nOwnerKeep = unchanged\r\n");
        var config=new ConfigFile(configPath,false){SaveOnConfigSet=false};
        Runtime.Settings=new(config);Runtime.Settings.FfbEnabled=false;
        Runtime.Local=new();Runtime.Focused=true;Runtime.Devices=new();Keyboard.current=new();
        MountedCamera.PlayerOwned=true;
        var guid=Guid.NewGuid();Runtime.Devices.Devices.Add(new DeviceHub.Device{Info=new(){InstanceGuid=guid}});
        var input=new WheelInput(bindingPath);
        input.Bindings.CameraKeys["Camera up"]="F7";input.Bindings.CameraKeys["Camera down"]="J";
        input.Bindings.Buttons["Camera up"]=new(guid,4);input.Bindings.Save(bindingPath);
        var json=JsonNode.Parse(File.ReadAllText(bindingPath))!.AsObject();json["OwnerKeep"]="preserve this unknown field";
        File.WriteAllText(bindingPath,json.ToJsonString(new JsonSerializerOptions{WriteIndented=true}).Replace("\n","\r\n"),new UTF8Encoding(true));
        input=new(bindingPath);Runtime.Wheel=input;
        var bindingBefore=File.ReadAllBytes(bindingPath);
        var store=new CameraFramingStore(config);var camera=new CameraSlot(MountedView.Bonnet);
        TripleView.FramingEnabled=true;TripleView.FovKeyName="F7";TripleView.CurrentCamera=camera;
        int writes=0;
        TripleView.CameraAction=action=>{
            writes++;bool realistic=store.Realistic(camera);
            if(action=="Camera reset")store.Reset(camera,7,realistic);
            else if(action is "Camera wider" or "Camera narrower")store.SetFov(camera,realistic,70,70+store.FovOffset(camera,realistic)+(action=="Camera wider"?2:-2));
            else store.SetPosition(camera,7,realistic,store.Position(camera,7,realistic).Adjust(action,Runtime.Settings.CameraSteps));
        };
        void Neutral(){Keyboard.current!.Clear();Runtime.Devices!.Pressed.Clear();CameraShortcuts.Update();}
        void Press(Key key,bool shift=false){Keyboard.current![key].isPressed=true;Keyboard.current[key].wasPressedThisFrame=true;if(shift)Keyboard.current[Key.LeftShift].isPressed=true;}
        foreach(var choice in new[]{("F7",Key.F7,"Camera up"),("J",Key.J,"Camera down"),("j",Key.J,"Camera down"),(((int)Key.J).ToString(),Key.J,"Camera down")})
        foreach(bool shift in new[]{true,false})
        {
            TripleView.FovKeyName=choice.Item1;Neutral();int calls=writes;var before=File.ReadAllBytes(configPath);
            if(shift)store.Press(camera,true,null,true); // Actual developer/store handler, before the actual shortcut consumer.
            Press(choice.Item2,shift);CameraShortcuts.Update();
            if(choice.Item1=="F7"&&shift)File.WriteAllText(Path.Combine(directory,"dispatch-witness.json"),JsonSerializer.Serialize(new{writesBefore=calls,writesAfter=writes,configUnchanged=File.ReadAllBytes(configPath).SequenceEqual(before),bindingsUnchanged=File.ReadAllBytes(bindingPath).SequenceEqual(bindingBefore)}));
            check(writes==calls&&File.ReadAllBytes(configPath).SequenceEqual(before),"REGRESSION session-only comparison key cannot write offsets: "+choice.Item1+" shift="+shift);
            check(!input.Button(choice.Item3,false),"active set key is absent from keyboard edge/held dispatch");
            check(File.ReadAllBytes(bindingPath).SequenceEqual(bindingBefore),"reservation never rewrites stored bindings");
        }
        check(input.Bindings.CameraKeys["Camera up"]=="F7"&&input.Bindings.CameraKeys["Camera down"]=="J","saved conflicting mappings remain intact");
        check(store.FovOffset(camera,false)==4,"blocked keys preserve the current Custom FOV offset");
        TripleView.FovKeyName="J";Neutral();Press(Key.F7);check(input.Button("Camera up",false),"changing the set key releases the old keyboard key");
        CameraShortcuts.Update();check(writes==1&&store.Position(camera,7,false).Height!=0,"an ordinary saved camera key still reaches the actual position store");
        foreach(string disabled in new[]{"None","not-a-key"}){TripleView.FovKeyName=disabled;Neutral();Press(Key.J);check(input.Button("Camera down",false),"disabled/invalid set key reserves nothing");}
        TripleView.FovKeyName="F7";Neutral();Press(Key.F7);
        check(!input.Button("Camera up",false),"active set-key hold is blocked before mode changes");
        TripleView.FramingEnabled=false;
        check(!input.Button("Camera up",false),"leaving triples cannot replay an observed reserved hold");
        Neutral();Press(Key.F7);
        check(input.Button("Camera up",false),"single-screen keyboard behavior remains available");
        TripleView.FramingEnabled=true;Neutral();Press(Key.F7);Runtime.Devices.Pressed.Add(new(guid,4));
        check(input.Button("Camera up",false),"a saved wheel binding is not suppressed by keyboard reservation");
        int wheelBefore=writes;CameraShortcuts.Update();check(writes==wheelBefore+1,"fake wheel input still reaches the actual position store");
        check(File.ReadAllBytes(bindingPath).SequenceEqual(bindingBefore),"ordinary dispatch keeps original binding bytes/BOM/unknown fields");
        Neutral();store.Press(camera,false,null,true);check(store.Realistic(camera),"actual ordinary set-switch handler remains independent");
        store.SetPosition(camera,8,true,new(.5f,0,0,0));store.SetPosition(camera,7,true,new(.2f,.3f,.4f,5));store.SetFov(camera,true,34,39);
        Neutral();Press(Key.Numpad5);CameraShortcuts.Update();
        check(input.Bindings.CameraKeys["Camera reset"]=="Numpad5"&&store.FovOffset(camera,true)==0&&store.Position(camera,7,true)==default,"numpad5 resets current actual store namespace");
        check(store.Position(camera,8,true).Side==.5f&&store.FovOffset(camera,false)==4&&store.Realistic(camera),"reset preserves other car/set and starting choice");
        // Production capture requires the settings panel's fresh stock-input observation.
        // Opening the actual source-linked panel establishes that ownership; a closed panel
        // deliberately makes MenuOwnership.AfterStock report unknown input.
        Panel.Toggle();check(Panel.Open,"capture fixture opens the actual settings panel");
        var stock=new GamePadSystem();stock.Game_Pads.Add(new());
        void Stock(){MenuOwnership.BeforeStock(stock);foreach(var pad in stock.Game_Pads)MenuOwnership.AfterRead(pad);MenuOwnership.AfterStock(stock);}
        void Arm(string action){Neutral();input.BeginButton(action);Stock();MenuOwnership.CaptureReady();Runtime.Clock.Advance(.11);Stock();check(MenuOwnership.CaptureReady(),"capture has a fresh observed neutral interval: "+action);typeof(WheelInput).GetField("_captureAfter",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(input,0d);}
        foreach(var choice in new[]{("F7",Key.F7),("J",Key.J)})
        foreach(bool shift in new[]{false,true})
        {
            TripleView.FovKeyName=choice.Item1;Arm("Camera forward");var before=File.ReadAllBytes(bindingPath);string previous=input.Bindings.CameraKeys["Camera forward"];
            Press(choice.Item2,shift);input.UpdateCapture();
            check(input.CaptureButton=="Camera forward"&&input.Bindings.CameraKeys["Camera forward"]==previous&&!input.SavePending,"reserved key refuses capture without publishing a proposal");
            check(input.Status.Contains(choice.Item1)&&input.Status.Contains("camera sets"),"capture explains the active reserved key");
            check(File.ReadAllBytes(bindingPath).SequenceEqual(before),"refused key capture preserves original bytes");input.Cancel();
        }
        TripleView.FovKeyName="F7";Arm("Camera forward");var lockedBefore=File.ReadAllBytes(bindingPath);
        using(var locked=new FileStream(bindingPath,FileMode.Open,FileAccess.Read,FileShare.None)){Press(Key.F7);input.UpdateCapture();check(!input.SavePending&&input.Status.Contains("camera sets"),"reserved capture never attempts the locked binding writer");}
        check(File.ReadAllBytes(bindingPath).SequenceEqual(lockedBefore),"locked refusal leaves exact old binding file");input.Cancel();
        Arm("Camera forward");Press(Key.U);input.UpdateCapture();
        check(input.CaptureButton==null&&input.Bindings.CameraKeys["Camera forward"]=="U","ordinary keyboard capture still commits through the real writer");
        Arm("Camera down");Runtime.Devices.Pressed.Add(new(guid,5));input.UpdateCapture();
        check(input.CaptureButton==null&&input.Bindings.Buttons["Camera down"]==new ButtonBinding(guid,5)&&input.Bindings.CameraKeys["Camera down"]=="None","ordinary wheel capture remains available");
        input=new(Path.Combine(directory,"single-screen-bindings.json"));Runtime.Wheel=input;TripleView.FramingEnabled=false;
        Arm("Camera up");Press(Key.F7);input.UpdateCapture();
        check(input.CaptureButton==null&&input.Bindings.CameraKeys["Camera up"]=="F7","single-screen capture does not reserve the triples set key");
        check(File.ReadAllText(configPath).Contains("OwnerKeep = unchanged"),"real framing writes preserve unrelated settings");
    }
}
