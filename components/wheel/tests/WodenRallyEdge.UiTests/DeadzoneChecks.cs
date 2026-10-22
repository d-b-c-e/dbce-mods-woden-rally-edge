using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WodenRallyEdge;
using WodenRallyEdge.Core;

internal static class DeadzoneChecks
{
    internal static void Run(string directory, Action<bool,string> check)
    {
        Runtime.Settings = new(new ConfigFile(Path.Combine(directory,"deadzone.cfg"),false));
        Runtime.Settings.FfbEnabled = false; Runtime.Settings.UiPage = "Controls";
        Runtime.Local = null; Runtime.Focused = true; Keyboard.current = new();
        Runtime.Devices = new(); // Synthetic, deliberately disconnected. Editing needs no recapture/poll.
        string path = Path.Combine(directory,"deadzone-bindings.json");
        var input = new WheelInput(path); Runtime.Wheel = input;
        var guid = Guid.NewGuid();
        var brake = new AxisBinding(guid,5,new(0,65535));
        var steering = new AxisBinding(guid,0,new(123,65000,32123,.017)) { Inverted = true };
        input.Bindings.Brake = brake; input.Bindings.Steer = steering;
        input.Bindings.Throttle = new(guid,2,new(0,65535));
        input.Bindings.Handbrake = new(guid,7,new(65535,0,null,.04)) { Inverted = true };
        input.Bindings.Buttons["Gear up"] = new(guid,3);
        input.Bindings.Save(path);
        string before = File.ReadAllText(path);
        string Expected(AxisBinding value)
        { var copy = input.Bindings.Copy(); copy.Brake = value; return JsonSerializer.Serialize(copy); }
        int polls = Runtime.Devices.Polls;
        input.BeginDeadzone("Brake");
        check(input.EditingDeadzone && input.Capturing && input.Capture == null && input.CaptureAxis == "Brake",
            "existing binding enters a draft without an axis capture");
        check(Runtime.Devices.Polls == polls,"deadzone editing does not poll or capture devices");
        input.CaptureDeadzone = 3;
        var proposal = input.PreviewBinding!;
        check(proposal.DeviceGuid == brake.DeviceGuid && proposal.Axis == brake.Axis && proposal.Inverted == brake.Inverted &&
            (proposal.Calibration with { Deadzone = brake.Calibration.Deadzone }) == brake.Calibration,
            "preview changes only deadzone, preserving exact identity/endpoints/center/inversion");
        check(input.Bindings.Brake == brake && File.ReadAllText(path) == before,"draft leaves the live binding and saved bytes unchanged");
        foreach (int raw in new[] { 830,913,927,1966 }) check(proposal.Normalize(raw) == 0,"actual rest raw maps to exact zero: "+raw);
        check(proposal.Normalize(1967) > 0 && proposal.Normalize(65535) == 1,"preview keeps above-deadzone and full-travel input");
        input.Cancel();
        check(!input.Capturing && input.PreviewBinding == null && File.ReadAllText(path) == before,"Cancel discards the deadzone draft without writing");

        input.BeginDeadzone("Brake"); input.CaptureDeadzone = 3;
        var savedProposal = input.PreviewBinding!; string expected = Expected(savedProposal);
        input.FinishAxis();
        check(!input.Capturing && !input.SavePending && input.Bindings.Brake == savedProposal,"explicit Save commits the draft even with a disconnected device");
        check(JsonSerializer.Serialize(Bindings.Load(path)) == expected,"round trip changes only the selected calibration deadzone");
        check(File.ReadAllText(path+".bak") == before,"atomic save retains the exact prior binding file");
        check(Runtime.Devices.Polls == polls,"Save deadzone never recaptures endpoints");

        input.BeginDeadzone("Steer"); input.CaptureDeadzone = 5;
        var steerPreview = input.PreviewBinding!;
        check(steerPreview.Inverted && steerPreview.Calibration.Rest == 123 && steerPreview.Calibration.End == 65000 &&
            steerPreview.Calibration.Centre == 32123,"inverted centered steering calibration is preserved too");
        input.Cancel();
        foreach (string axis in new[] { "Throttle","Brake","Handbrake" })
        {
            input.BeginAxis(axis);
            check(input.CaptureDeadzone == 3,"new "+axis+" capture starts with visible 3% deadzone");
            input.Cancel();
        }
        input.BeginAxis("Steer"); check(input.CaptureDeadzone == 0,"new steering capture keeps its zero default"); input.Cancel();
        input.BeginAxis("Throttle",true);
        check(input.CaptureDeadzone == 0,"recapturing an existing zero-deadzone pedal does not migrate it"); input.Cancel();
        input.BeginAxis("Brake",true);
        check(Math.Abs(input.CaptureDeadzone-3)<.0001,"recapturing an existing pedal keeps its saved deadzone"); input.Cancel();
        string saved = File.ReadAllText(path);
        input.BeginDeadzone("Brake"); input.CaptureDeadzone = 8; Runtime.Clock.Advance(31); input.UpdateCapture();
        check(input.EditingDeadzone,"manual deadzone editing is not cut off at the capture's 30-second limit");
        Runtime.Clock.Advance(90); input.UpdateCapture();
        check(!input.Capturing && File.ReadAllText(path) == saved,"timeout discards an unapplied deadzone edit");

        string failedPath = Path.Combine(directory,"blocked-binding.json"); Directory.CreateDirectory(failedPath);
        var failed = new WheelInput(failedPath); failed.Bindings.Brake = brake;
        failed.BeginDeadzone("Brake"); failed.CaptureDeadzone = 3; failed.FinishAxis();
        check(failed.SavePending && failed.Capturing && failed.Bindings.Brake == brake,"failed Save retains the original live binding and draft");
        Directory.Delete(failedPath); // Only this empty fixture directory; no recursive/owner deletion.
        failed.CaptureDeadzone = 6; // Retry must retain the exact previously failed proposal.
        failed.RetrySave();
        check(!failed.Capturing && Bindings.Load(failedPath).Brake!.Calibration.Deadzone == .03,"explicit Retry saves the exact retained deadzone proposal");

        const BindingFlags hidden = BindingFlags.Static|BindingFlags.NonPublic;
        var dirty = typeof(Panel).GetField("_dirty",hidden)!;
        void Draw(Event? value = null) { GUI.Commands.Clear(); Event.current = value ?? new(); Panel.Draw(); }
        Screen.width=1280; Screen.height=720;
        Runtime.Devices=null;
        input.BeginDeadzone("Brake"); Panel.Toggle(); Draw();
        check(GUI.Commands.Any(c=>c.Text=="Save deadzone") && GUI.Commands.Any(c=>c.Text=="Cancel") &&
            GUI.Commands.Any(c=>c.Text.Contains("Saved device unavailable")),"missing DeviceHub still renders a usable saved-binding editor");
        var noHubSave=GUI.Commands.First(c=>c.Kind=="text" && c.Text=="Save deadzone");
        Draw(new(){type=EventType.MouseDown,button=0,mousePosition=new(noHubSave.Rect.x+10,noHubSave.Rect.y+10)}); Draw();
        check(!input.Capturing && Bindings.Load(path).Brake!.Calibration.Deadzone==.03,"explicit UI Save works without a DeviceHub");
        saved=File.ReadAllText(path); Panel.Close(false); Runtime.Devices=new();
        Runtime.Settings.FfbEnabled=true;
        int reconnects=Runtime.Force!.Reconnects, forceChanges=Runtime.Force.Changes;
        foreach (var size in new[] { (1280,720),(7680,1440) })
        {
            Screen.width=size.Item1; Screen.height=size.Item2;
            Panel.Toggle(); Draw();
            var button = GUI.Commands.First(c=>c.Kind=="text" && c.Text=="Deadzone");
            Draw(new(){type=EventType.MouseDown,button=0,mousePosition=new(button.Rect.x+10,button.Rect.y+10)}); Draw();
            check(input.EditingDeadzone && input.CaptureAxis=="Steer","visible existing-binding Deadzone button opens the matching editor");
            check(GUI.Commands.Any(c=>c.Text=="Save deadzone") && !GUI.Commands.Any(c=>c.Text=="Invert calibration"),
                "editor offers explicit Save and keeps inversion read-only");
            check(GUI.Commands.Any(c=>c.Text.Contains("without recapturing")),"editor explains exact zero and unchanged endpoints");
            foreach(string caption in new[] { "Save deadzone","Cancel" })
            {
                var text=GUI.Commands.First(c=>c.Kind=="text" && c.Text==caption);
                check(text.Rect.x>=0 && text.Rect.y>=0 && text.Rect.x+text.Rect.width<=Screen.width && text.Rect.y+text.Rect.height<=Screen.height,
                    caption+" remains inside the viewport");
            }
            dirty.SetValue(null,false);
            var plus=GUI.Commands.First(c=>c.Kind=="text" && c.Text=="+");
            float beforeStep=input.CaptureDeadzone;
            Runtime.Clock.Advance(119);
            Draw(new(){type=EventType.MouseDown,button=0,mousePosition=new(plus.Rect.x+8,plus.Rect.y+8)}); Draw();
            check(Math.Abs(input.CaptureDeadzone-beforeStep-.25f)<.0001,"the observed + control actually changes the provisional value");
            check(GUI.Commands.Any(c=>c.Kind=="text" && c.Text=="1.95"),"a quarter-percent click visibly changes 1.7 to 1.95 in the editor");
            Runtime.Clock.Advance(2); input.UpdateCapture();
            check(input.EditingDeadzone,"actual slider changes refresh the manual editor inactivity timeout");
            check(dirty.GetValue(null) is false,"editing a provisional deadzone does not dirty unrelated saved settings");
            var cancel=GUI.Commands.First(c=>c.Kind=="text" && c.Text=="Cancel");
            Draw(new(){type=EventType.MouseDown,button=0,mousePosition=new(cancel.Rect.x+10,cancel.Rect.y+10)}); Draw();
            check(!input.Capturing && input.Bindings.Steer==steering && File.ReadAllText(path)==saved,"UI Cancel preserves the existing binding and disk bytes");
            input.BeginAxis("Throttle"); Draw();
            check(GUI.Commands.Any(c=>c.Text=="Deadzone (%)") && GUI.Commands.Any(c=>c.Text=="3"),
                "new pedal capture visibly shows the 3% default");
            Keyboard.current![Key.Escape].wasPressedThisFrame=true; Panel.Update(); Keyboard.current.Clear();
            check(!input.Capturing && File.ReadAllText(path)==saved,"Escape discards a new capture without saving its default");
            input.BeginDeadzone("Brake"); input.CaptureDeadzone=6;
            Keyboard.current[Key.Escape].wasPressedThisFrame=true; Panel.Update(); Keyboard.current.Clear();
            check(!input.Capturing && File.ReadAllText(path)==saved,"Escape discards a deadzone-only draft without saving");
            Panel.Close(false);
        }
        input.BeginDeadzone("Steer"); input.CaptureDeadzone=1.95f; input.FinishAxis();
        Panel.Toggle(); Draw();
        check(GUI.Commands.Any(c=>c.Text.StartsWith("1.95% · Invert On",StringComparison.Ordinal)),
            "saved Controls summary preserves fractional deadzone digits too");
        Panel.Close(false);
        check(Runtime.Settings.FfbEnabled && Runtime.Force.Reconnects==reconnects && Runtime.Force.Changes==forceChanges,
            "editor retains the On preference without reconnecting or changing force settings");
        LegacyPersistence(directory, check);
    }
    private static void LegacyPersistence(string directory, Action<bool,string> check)
    {
        string path = Path.Combine(directory,"legacy-deadzone-bindings.json");
        var guid = Guid.NewGuid();
        var original = new Bindings { Brake = new(guid,5,new(0,65535)) };
        foreach (string action in new[] { "Gear up","Gear down","Handbrake","Camera","Rear view","Lights","Horn","Respawn","Pause","Settings panel","Confirm","Next song" })
            original.Buttons[action] = new(guid,3);
        original.CameraKeys["Camera up"]="Numpad8"; original.CameraKeys["Camera down"]="Numpad2";
        original.CameraKeys["Camera forward"]="Numpad9"; original.CameraKeys["Camera back"]="Numpad7";
        original.CameraKeys["Camera pitch down"]="Numpad3"; original.CameraKeys["Camera reset"]="Numpad0";
        var document = JsonSerializer.SerializeToNode(original)!.AsObject();
        foreach (var button in document["Buttons"]!.AsObject()) button.Value!.AsObject().Remove("HatNeighbours");
        document["Buttons"]!["Confirm"]!["HatNeighbours"]=false; // Preserve explicit false as well as absent defaults.
        document["UnrelatedExtension"]=JsonNode.Parse("{\"values\":[0,null,true],\"note\":\"preserve\"}");
        File.WriteAllText(path,document.ToJsonString()); string before = File.ReadAllText(path);
        var input = new WheelInput(path); Runtime.Wheel = input;
        check(input.Bindings.CameraKeys.OrderBy(p=>p.Key).SequenceEqual(original.CameraKeys.OrderBy(p=>p.Key)) && !input.Bindings.CameraKeysMigrated,
            "loading an explicitly saved earlier camera layout never migrates its keys");
        input.BeginDeadzone("Brake"); input.CaptureDeadzone=3; input.Cancel();
        check(File.ReadAllText(path)==before,"legacy-shape Cancel keeps exact bytes, including absent button fields");
        input.BeginDeadzone("Brake"); input.CaptureDeadzone=3; input.FinishAxis();
        var saved = JsonNode.Parse(File.ReadAllText(path))!;
        check(!input.Capturing && saved["Brake"]!["Calibration"]!["Deadzone"]!.GetValue<double>()==.03,
            "actual WheelInput Save writes numeric0.03 through the existing atomic writer");
        saved["Brake"]!["Calibration"]!["Deadzone"]=0;
        check(JsonNode.DeepEquals(saved,document),
            "legacy Save changes only the edited deadzone: all camera keys, optional button fields, extensions and other values remain exact");
        check(File.ReadAllText(path+".bak")==before,"deadzone-only save retains the exact legacy backup");
        var restarted = new WheelInput(path);
        check(restarted.Bindings.Brake!.Calibration.Deadzone==.03 && restarted.Bindings.CameraKeys.OrderBy(p=>p.Key).SequenceEqual(original.CameraKeys.OrderBy(p=>p.Key)),
            "cold WheelInput readback retains3% and the original saved camera keys");
    }
}
