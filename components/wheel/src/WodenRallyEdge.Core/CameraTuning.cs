using System.Numerics;

namespace WodenRallyEdge.Core;

public readonly record struct CameraPose(float Side, float Height, float Forward, float Pitch, float Fov)
{
    public static CameraPose Bonnet => new(0, .7367809f, .9380049f, 8, 70);
    public static CameraPose Bumper => new(0, .35f, 2.2f, 0, 70);
    public CameraPose Bounded()
    {
        static float Limit(float value, float min, float max, float fallback) => float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
        return new(Limit(Side, -2, 2, 0), Limit(Height, .1f, 3, Bonnet.Height), Limit(Forward, -2, 4, Bonnet.Forward), Limit(Pitch, -30, 30, 8), Limit(Fov, 30, 110, 70));
    }
    public static CameraPose FitBonnet(Vector3 min, Vector3 max)
    {
        var size = max - min;
        if (!float.IsFinite(size.X + size.Y + size.Z + min.X + min.Y + min.Z) || size.X < .2f || size.Y < .2f || size.Z < .5f)
            return Bonnet;
        // Just in front of the windscreen, below the roof, with room for the
        // bonnet in the lower picture. Use body-local bounds, never world AABB.
        // Owner's saved correction on the second vehicle: +15 cm up / +5 cm
        // forward. Retain the body-relative fit when changing vehicles.
        return new CameraPose((min.X + max.X) * .5f, min.Y + size.Y * .78f + .15f,
            (min.Z + max.Z) * .5f + size.Z * .18f + .05f, 8, 70).Bounded();
    }

    /// <summary>Default distance of the bumper view in front of the car body, metres.</summary>
    public const float BumperAheadDefault = .05f;

    /// <summary>
    /// Bumper view a saved distance in front of the body's front. One fixed forward offset put
    /// the view inside longer cars (owner, 2026-10-06: body front at 2.31 m, offset 2.2 m).
    /// Height, side, tilt and FOV stay the saved values; missing bounds keep the saved pose.
    /// </summary>
    public static CameraPose FitBumper(Vector3 min, Vector3 max, CameraPose saved, float ahead)
    {
        var size = max - min;
        if (!float.IsFinite(size.X + size.Y + size.Z + max.Z + ahead) || size.X < .2f || size.Y < .2f || size.Z < .5f)
            return saved.Bounded();
        return (saved with { Forward = max.Z + ahead }).Bounded();
    }

    /// <summary>How far an adjusted bumper pose sits in front of the body front.</summary>
    public static float BumperAhead(Vector3 max, CameraPose pose) => pose.Bounded().Forward - max.Z;
}

/// <summary>Per-press adjustment sizes, a player setting (toolkit default 0.02 m, 1°, 2°).</summary>
public readonly record struct CameraSteps(float Move, float Tilt, float Fov)
{
    public static CameraSteps Default => new(.02f, 1, 2);
    public CameraSteps Bounded()
    {
        static float Limit(float value, float min, float max, float fallback) => float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
        return new(Limit(Move, .005f, .25f, .02f), Limit(Tilt, .1f, 10, 1), Limit(Fov, .5f, 10, 2));
    }
}

public static class CameraTuning
{
    public static readonly string[] Actions = { "Camera", "Rear view", "Camera up", "Camera down", "Camera forward", "Camera back", "Camera left", "Camera right", "Camera pitch up", "Camera pitch down", "Camera wider", "Camera narrower", "Camera reset" };
    public static readonly string[] Labels = { "Change camera", "Look behind", "Move up", "Move down", "Move forward", "Move back", "Move left", "Move right", "Tilt up", "Tilt down", "Widen FOV", "Narrow FOV", "Reset view" };
    // Family layout (owner, 2026-10-04): 8/2 forward/back, 9/3 up/down, 4/6 left/right,
    // 7/1 tilt forward/back, +/- FOV, 5 reset. Tilt forward looks down (pitch down).
    private static readonly string[] Keys = { "None", "None", "Numpad9", "Numpad3", "Numpad8", "Numpad2", "Numpad4", "Numpad6", "Numpad1", "Numpad7", "NumpadPlus", "NumpadMinus", "Numpad5" };
    public static Dictionary<string, string> DefaultKeys() => Actions.Select((a, i) => (a, i)).ToDictionary(x => x.a, x => Keys[x.i]);

    // Earlier family defaults, adjustment keys only (up, down, forward, back, left, right,
    // pitch up, pitch down, wider, narrower, reset). A saved set equal to one of these
    // records no player choice and moves to the current layout.
    private static readonly string[][] PreviousLayouts =
    {
        new[] { "Numpad8", "Numpad2", "Numpad9", "Numpad7", "Numpad4", "Numpad6", "Numpad3", "Numpad1", "NumpadPlus", "NumpadMinus", "Numpad0" },
        new[] { "Numpad8", "Numpad2", "Numpad9", "Numpad7", "Numpad4", "Numpad6", "Numpad1", "Numpad3", "NumpadPlus", "NumpadMinus", "Numpad0" },
        new[] { "Numpad9", "Numpad3", "Numpad8", "Numpad2", "NumpadDivide", "NumpadMultiply", "Numpad4", "Numpad6", "NumpadPlus", "NumpadMinus", "Numpad5" },
        new[] { "Numpad9", "Numpad3", "Numpad8", "Numpad2", "NumpadDivide", "NumpadMultiply", "Numpad6", "Numpad4", "NumpadPlus", "NumpadMinus", "Numpad5" },
    };

    /// <summary>Moves an untouched earlier default set to the current layout; true when it did.</summary>
    public static bool MigratePreviousDefaults(Dictionary<string, string> keys)
    {
        var adjustments = Actions.Skip(2).ToArray();
        string Saved(string action) => keys.TryGetValue(action, out var key) ? key : "None";
        if (!PreviousLayouts.Any(layout => adjustments.Select((a, i) => string.Equals(Saved(a), layout[i], StringComparison.OrdinalIgnoreCase)).All(x => x))) return false;
        var current = DefaultKeys();
        foreach (var action in adjustments) keys[action] = current[action];
        return true;
    }
    public static void RestoreAdjustmentKeys(Bindings bindings)
    {
        var conflict = AdjustmentDefaultsConflict(bindings);
        if (conflict != null) throw new InvalidOperationException("A numpad default is assigned to " + conflict + ". Rebind it first.");
        foreach (var action in Actions.Skip(2)) { bindings.CameraKeys[action] = DefaultKeys()[action]; bindings.Buttons.Remove(action); }
    }
    public static string? AdjustmentDefaultsConflict(Bindings bindings)
    {
        var adjustments = Actions.Skip(2).ToHashSet();
        var keys = DefaultKeys().Where(k => adjustments.Contains(k.Key)).Select(k => k.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return bindings.CameraKeys.FirstOrDefault(k => !adjustments.Contains(k.Key) && keys.Contains(k.Value)).Key;
    }
    public static CameraPose Adjust(CameraPose pose, string action) => Adjust(pose, action, CameraSteps.Default);
    public static CameraPose Adjust(CameraPose pose, string action, CameraSteps steps)
    {
        steps = steps.Bounded(); float m = steps.Move, t = steps.Tilt, f = steps.Fov;
        return (action switch
        {
            "Camera up" => pose with { Height = pose.Height + m }, "Camera down" => pose with { Height = pose.Height - m },
            "Camera forward" => pose with { Forward = pose.Forward + m }, "Camera back" => pose with { Forward = pose.Forward - m },
            "Camera left" => pose with { Side = pose.Side - m }, "Camera right" => pose with { Side = pose.Side + m },
            "Camera pitch up" => pose with { Pitch = pose.Pitch - t }, "Camera pitch down" => pose with { Pitch = pose.Pitch + t },
            "Camera wider" => pose with { Fov = pose.Fov + f }, "Camera narrower" => pose with { Fov = pose.Fov - f }, _ => pose
        }).Bounded();
    }
}
