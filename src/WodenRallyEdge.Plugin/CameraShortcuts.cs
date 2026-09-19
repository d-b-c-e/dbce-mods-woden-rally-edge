using UnityEngine.InputSystem;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

internal static class CameraShortcuts
{
    internal static readonly Key[] BindableKeys = Enum.GetValues<Key>().Where(k => k is not (Key.None or Key.F6 or Key.F8 or Key.Escape)).Distinct().ToArray();
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
            if (!Runtime.Wheel.Button(action) || !allowed) continue;
            if (action == "Camera reset") { cfg.ResetCamera(bumper); Panel.SettingsChanged(); return; }
            pose = CameraTuning.Adjust(pose, action); changed = true;
        }
        if (!changed) return;
        cfg.SetCameraPose(bumper, pose); if (!bumper) cfg.CameraAutoFit = false;
        Panel.SettingsChanged();
    }
}
