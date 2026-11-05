using UnityEngine.InputSystem;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

internal static class CameraShortcuts
{
    internal static readonly Key[] BindableKeys = Enum.GetValues<Key>().Where(k => k is not (Key.None or Key.F6 or Key.F8 or Key.Escape)).Distinct().ToArray();
    // Reserve only while the set shortcut applies. Saved maps are retained; single-screen keys and wheel buttons keep their routes.
    internal static Key? SetKey => TripleView.FramingEnabled && Enum.TryParse<Key>(TripleView.FovKeyName, true, out var key) && key != Key.None ? key : null;
    private static readonly HashSet<Key> ReservedHeld = new();
    private static readonly Dictionary<string, ShortcutRepeat> Repeats = CameraTuning.Actions.Skip(2).ToDictionary(a => a, _ => new ShortcutRepeat());
    // Lock texts carry a menu path, so they stay longer than the 3 s adjustment readout.
    private const double LockMessageSeconds = 6;
    private static bool _fovHintShown;   // once per session
    internal static bool ModifierHeld => Keyboard.current is { } kb && new[] { Key.LeftShift, Key.RightShift, Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftMeta, Key.RightMeta }.Any(k => kb[k].isPressed);
    internal static bool KeyPressed(string action, bool edge)
    {
        var kb = Keyboard.current;
        if (kb == null || Runtime.Wheel == null || !Runtime.Wheel.Bindings.CameraKeys.TryGetValue(action, out string? name) ||
            !Enum.TryParse<Key>(name, true, out var key) || !BindableKeys.Contains(key)) return false;
        bool held = kb[key].isPressed;
        if (!held) ReservedHeld.Remove(key);
        if (SetKey == key) { if (held) ReservedHeld.Add(key); return false; }
        // Leaving triples or changing its key cannot turn an observed blocked hold into a fresh adjustment.
        if (ReservedHeld.Contains(key)) return false;
        return edge ? kb[key].wasPressedThisFrame : kb[key].isPressed;
    }
    internal static void Update()
    {
        if (Runtime.Wheel == null) return;
        bool allowed = Runtime.Focused && !Panel.Open && Runtime.Local != null && Runtime.CameraAvailable(Runtime.Local) && MountedCamera.PlayerOwned &&
            (TripleView.FramingEnabled || MountedCamera.Cycle.View != MountedView.Stock);
        if (TripleView.FramingEnabled)
        {
            foreach (string action in CameraTuning.Actions.Skip(2))
            {
                // Only this action's held binding controls its repeat/release;
                // an H-pattern gate held in gear cannot block camera keys.
                bool held = Runtime.Wheel.Button(action, false);
                if (!Repeats[action].Tick(held, allowed && TripleView.CameraEditAllowed, Runtime.Clock.Elapsed.TotalSeconds, action != "Camera reset")) continue;
                if (action == "Camera reset") TripleView.ResetCurrent();
                else if (action == "Camera wider") TripleView.AdjustFov(Runtime.Settings.CameraSteps.Fov);
                else if (action == "Camera narrower") TripleView.AdjustFov(-Runtime.Settings.CameraSteps.Fov);
                else TripleView.AdjustPosition(action);
            }
            return;
        }
        bool bumper = MountedCamera.Cycle.View == MountedView.Bumper;
        // The first locked bonnet or bumper view of the session says why the FOV keys do nothing, before they are tried.
        // It only shows text; the saved mode and poses are untouched.
        if (allowed && TripleView.MountedFovLocked && !_fovHintShown)
        {
            _fovHintShown = true;
            Panel.ShowCameraMessage(TripleView.FovLockHint(bumper), LockMessageSeconds);
        }
        var cfg = Runtime.Settings;
        var pose = allowed ? MountedCamera.Pose(bumper) : default; bool changed = false;
        foreach (string action in CameraTuning.Actions.Skip(2))
        {
            bool held = Runtime.Wheel.Button(action, false);
            if (!Repeats[action].Tick(held, allowed, Runtime.Clock.Elapsed.TotalSeconds, action != "Camera reset")) continue;
            if ((action is "Camera wider" or "Camera narrower") && TripleView.MountedFovLocked)
            {
                Panel.ShowCameraMessage(TripleView.FovLockMessage, LockMessageSeconds);
                continue; // Do not save an unused lens value or turn off the saved bonnet fit.
            }
            if (action == "Camera reset") { cfg.ResetCamera(bumper); Panel.ShowCameraMessage((bumper ? "Bumper" : "Bonnet") + ": default view restored"); Panel.SettingsChanged(); return; }
            pose = CameraTuning.Adjust(pose, action, cfg.CameraSteps); changed = true;
            Panel.ShowCameraMessage((bumper ? "Bumper" : "Bonnet") + ": " + CameraTuning.Labels[Array.IndexOf(CameraTuning.Actions, action)] + $" · height {pose.Height:F2} m · forward {pose.Forward:F2} m · tilt {pose.Pitch:F0}° · FOV {pose.Fov:F0}°");
        }
        if (!changed) return;
        MountedCamera.SavePose(bumper, pose);
        Panel.SettingsChanged();
    }
}
