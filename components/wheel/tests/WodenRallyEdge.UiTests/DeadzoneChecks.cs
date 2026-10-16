using System.Reflection;
using System.Text.Json;
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
        input.BeginAxis("Brake",true);
        check(Math.Abs(input.CaptureDeadzone-3)<.0001,"recapturing an existing pedal keeps its saved deadzone"); input.Cancel();
        string saved = File.ReadAllText(path);
        input.BeginDeadzone("Brake"); input.CaptureDeadzone = 8; Runtime.Clock.Advance(31); input.UpdateCapture();
        check(!input.Capturing && File.ReadAllText(path) == saved,"timeout discards an unapplied deadzone edit");

        string failedPath = Path.Combine(directory,"blocked-binding.json"); Directory.CreateDirectory(failedPath);
        var failed = new WheelInput(failedPath); failed.Bindings.Brake = brake;
        failed.BeginDeadzone("Brake"); failed.CaptureDeadzone = 3; failed.FinishAxis();
        check(failed.SavePending && failed.Capturing && failed.Bindings.Brake == brake,"failed Save retains the original live binding and draft");
        Directory.Delete(failedPath); // Only this empty fixture directory; no recursive/owner deletion.
        failed.RetrySave();
        check(!failed.Capturing && Bindings.Load(failedPath).Brake!.Normalize(927) == 0,"explicit Retry saves the retained deadzone proposal");

        const BindingFlags hidden = BindingFlags.Static|BindingFlags.NonPublic;
        var dirty = typeof(Panel).GetField("_dirty",hidden)!;
        void Draw(Event? value = null) { GUI.Commands.Clear(); Event.current = value ?? new(); Panel.Draw(); }
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
            Draw(new(){type=EventType.MouseDown,button=0,mousePosition=new(plus.Rect.x+8,plus.Rect.y+8)}); Draw();
            check(dirty.GetValue(null) is false,"editing a provisional deadzone does not dirty unrelated saved settings");
            var cancel=GUI.Commands.First(c=>c.Kind=="text" && c.Text=="Cancel");
            Draw(new(){type=EventType.MouseDown,button=0,mousePosition=new(cancel.Rect.x+10,cancel.Rect.y+10)}); Draw();
            check(!input.Capturing && input.Bindings.Steer==steering && File.ReadAllText(path)==saved,"UI Cancel preserves the existing binding and disk bytes");
            Panel.Close(false);
        }
        check(!Runtime.Settings.FfbEnabled,"deadzone editing does not enable forces");
    }
}
