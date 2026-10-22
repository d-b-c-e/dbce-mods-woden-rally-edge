using BepInEx.Configuration;
using UnityEngine.InputSystem;
using WodenRallyEdge;
using WodenRallyEdge.Core;

internal static class PanelShutdownChecks
{
    internal static void Run(string dir, Action<bool, string> check)
    {
        var settings = Runtime.Settings;
        var wheel = Runtime.Wheel;
        var local = Runtime.Local;
        var keyboard = Keyboard.current;
        var cycle = MountedCamera.Cycle;
        bool focused = Runtime.Focused;
        try
        {
            check(!Panel.Open, "shutdown fixture starts with F6 closed");
            Runtime.Local = new();
            Keyboard.current = new();
            Runtime.Wheel = new(Path.Combine(dir, "shutdown-bindings.json"));
            foreach (bool unfocused in new[] { false, true })
            foreach (bool bumper in new[] { false, true })
            {
                string path = Path.Combine(dir, $"shutdown-{unfocused}-{bumper}.cfg");
                Runtime.Settings = new(new ConfigFile(path, false));
                Runtime.Settings.FfbEnabled = bumper;
                Runtime.Settings.FfbSmoothing = 61;
                Runtime.Settings.Save();
                var forceOptions = Runtime.Settings.ForceOptions;
                var force = Runtime.Force!;
                int reconnects = force.Reconnects, changes = force.Changes;
                string before = File.ReadAllText(path);
                var pose = Runtime.Settings.GetCameraPose(bumper);
                MountedCamera.Cycle = new();
                MountedCamera.Cycle.StockChanged(4, 0, !bumper, bumper);
                Runtime.Focused = true;
                Keyboard.current.Clear();
                CameraShortcuts.Update(); // Release arms the actual shortcut repeat gate.
                Keyboard.current[Key.NumpadPlus].isPressed = true;
                Keyboard.current[Key.Numpad9].isPressed = true;
                CameraShortcuts.Update();
                var edited = Runtime.Settings.GetCameraPose(bumper);
                check(edited.Fov == pose.Fov + Runtime.Settings.CameraFovStep &&
                    Math.Abs(edited.Height - pose.Height - Runtime.Settings.CameraMoveStep) < .0001f,
                    "closed-panel shortcuts change FOV and pose before shutdown");
                check(File.ReadAllText(path) == before, "shortcut changes remain pending before shutdown");
                Keyboard.current.Clear();
                if (unfocused)
                {
                    Runtime.Focused = false;
                    Runtime.Clock.Advance(1);
                    Panel.Update();
                    check(File.ReadAllText(path) == before, "unfocused update leaves the pending shortcut save for shutdown");
                }
                Panel.Close(false); // Runtime.FinishStop uses this same lifecycle entry point.
                var saved = new Settings(new ConfigFile(path, false));
                check(saved.GetCameraPose(bumper) == edited, "closed-panel shutdown persists the edited mount without refocus");
                check(!Panel.Open && !MenuOwnership.Closing, "shutdown save does not open F6 or request a neutral handoff");
                check(saved.FfbEnabled == bumper && saved.ForceOptions == forceOptions &&
                    force.Reconnects == reconnects && force.Changes == changes,
                    "shutdown persistence preserves saved force state and tune without force actions");
                // A clean repeated stop must not write unrelated in-memory changes.
                string after = File.ReadAllText(path);
                Runtime.Settings.CameraFov++;
                Panel.Close(false);
                check(File.ReadAllText(path) == after, "successful shutdown clears dirty state and clean close does not save");
            }
        }
        finally
        {
            Runtime.Settings = settings;
            Runtime.Wheel = wheel;
            Runtime.Local = local;
            Runtime.Focused = focused;
            Keyboard.current = keyboard;
            MountedCamera.Cycle = cycle;
        }
    }
}
