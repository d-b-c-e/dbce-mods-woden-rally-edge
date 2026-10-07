using UnityEngine.InputSystem;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

internal static class CameraShortcuts
{
    internal static readonly Key[] BindableKeys = Enum.GetValues<Key>().Where(k => k is not (Key.None or Key.F6 or Key.F8 or Key.Escape)).Distinct().ToArray();
    private static readonly Dictionary<string, ShortcutRepeat> Repeats = CameraTuning.Actions.Skip(2).ToDictionary(a => a, _ => new ShortcutRepeat());
    internal static bool ModifierHeld => Keyboard.current is { } kb && new[] { Key.LeftShift, Key.RightShift, Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftMeta, Key.RightMeta }.Any(k => kb[k].isPressed);
    internal static bool KeyPressed(string action, bool edge)
    {
        var kb = Keyboard.current;
        if (kb == null || Runtime.Wheel == null || !Runtime.Wheel.Bindings.CameraKeys.TryGetValue(action, out string? name) ||
            !Enum.TryParse<Key>(name, true, out var key) || !BindableKeys.Contains(key)) return false;
        return edge ? kb[key].wasPressedThisFrame : kb[key].isPressed;
    }
    internal static void Update()
    {
        if (Runtime.Wheel == null) return;
        bool allowed = Runtime.Focused && !Panel.Open && Runtime.Local != null && Runtime.CameraAvailable(Runtime.Local) && MountedCamera.PlayerOwned && MountedCamera.Cycle.View != MountedView.Stock;
        bool bumper = MountedCamera.Cycle.View == MountedView.Bumper;
        var cfg = Runtime.Settings;
        var pose = allowed ? MountedCamera.Pose(bumper) : default; bool changed = false;
        foreach (string action in CameraTuning.Actions.Skip(2))
        {
            bool held = Runtime.Wheel.Button(action, false);
            if (!Repeats[action].Tick(held, allowed, Runtime.Clock.Elapsed.TotalSeconds, action != "Camera reset")) continue;
            if (action == "Camera reset") { cfg.ResetCamera(bumper); Panel.ShowCameraMessage((bumper ? "Bumper" : "Bonnet") + ": default view restored"); Panel.SettingsChanged(); return; }
            pose = CameraTuning.Adjust(pose, action, cfg.CameraSteps); changed = true;
            Panel.ShowCameraMessage((bumper ? "Bumper" : "Bonnet") + ": " + CameraTuning.Labels[Array.IndexOf(CameraTuning.Actions, action)] + $" · height {pose.Height:F2} m · forward {pose.Forward:F2} m · tilt {pose.Pitch:F0}° · FOV {pose.Fov:F0}°");
        }
        if (!changed) return;
        MountedCamera.SavePose(bumper, pose);
        Panel.SettingsChanged();
    }
}
