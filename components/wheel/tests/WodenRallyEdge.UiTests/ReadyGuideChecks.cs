using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WodenRallyEdge;
using WodenRallyEdge.Core;

/// <summary>The guided setup asks what the wheel route asks (Bindings.DrivingAxisReady): a hand-edited bindings file
/// with a centre-less steering axis or a centred pedal says "bind or reconnect", never "Ready to drive".</summary>
internal static class ReadyGuideChecks
{
    internal static void Run(string directory, Action<bool,string> check)
    {
        check(!ShimInput.Owns, "ready-guide fixture runs on the Native route");
        var (settings, wheel, devices, local, focused, keyboard, width, height) =
            (Runtime.Settings, Runtime.Wheel, Runtime.Devices, Runtime.Local, Runtime.Focused, Keyboard.current, Screen.width, Screen.height);
        bool wasOpen = Panel.Open;
        try
        {
            Runtime.Settings = new(new ConfigFile(Path.Combine(directory, "ready-guide.cfg"), false));
            Runtime.Settings.FfbEnabled = false; Runtime.Settings.WheelEnabled = true; Runtime.Settings.UiPage = "Setup";
            Runtime.Local = null; Runtime.Focused = true; Keyboard.current = new();
            var guid = Guid.NewGuid();
            Runtime.Devices = new(); Runtime.Devices.Devices.Add(new() { Info = new() { InstanceGuid = guid } });
            var input = new WheelInput(Path.Combine(directory, "ready-guide-bindings.json")); Runtime.Wheel = input;
            var steer = new AxisBinding(guid, 0, new(0, 65535, 32767));
            var pedal = new AxisBinding(guid, 2, new(0, 65535));
            Screen.width = 1280; Screen.height = 720;
            if (!wasOpen) Panel.Toggle();
            string Line()
            {
                GUI.Commands.Clear(); Event.current = new(); Panel.Draw();
                return GUI.Commands.LastOrDefault(c => c.Kind == "text" && (c.Text.StartsWith("Next:") || c.Text.StartsWith("Ready to drive")))?.Text ?? "";
            }
            foreach (var (s, t, b, expect) in new (AxisBinding, AxisBinding, AxisBinding, string)[]
            {
                (steer with { Calibration = new(0, 65535) }, pedal, pedal with { Axis = 5 }, "Next: bind or reconnect Steering."),
                (steer, pedal with { Calibration = new(0, 65535, 32767) }, pedal with { Axis = 5 }, "Next: bind or reconnect Throttle."),
                (steer, pedal, pedal with { Axis = 5 }, "Ready to drive. Close settings and check the car responds."),
            })
            {
                input.Bindings.Steer = s; input.Bindings.Throttle = t; input.Bindings.Brake = b;
                string line = Line();
                check(line == expect, "guided setup shows \"" + expect + "\" (drew \"" + line + "\")");
                check(line.StartsWith("Ready", StringComparison.Ordinal) == input.Bindings.DrivingAxesReady,
                    "guided Ready matches the wheel route's DrivingAxesReady: " + expect);
            }
        }
        finally
        {
            if (!wasOpen && Panel.Open) Panel.Close(false);
            (Runtime.Settings, Runtime.Wheel, Runtime.Devices, Runtime.Local, Runtime.Focused, Keyboard.current, Screen.width, Screen.height) =
                (settings, wheel, devices, local, focused, keyboard, width, height);
            GUI.Commands.Clear();
        }
    }
}
