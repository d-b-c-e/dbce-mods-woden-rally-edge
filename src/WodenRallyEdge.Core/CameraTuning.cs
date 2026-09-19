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
}

public static class CameraTuning
{
    public static readonly string[] Actions = { "Camera", "Rear view", "Camera up", "Camera down", "Camera forward", "Camera back", "Camera left", "Camera right", "Camera pitch up", "Camera pitch down", "Camera wider", "Camera narrower", "Camera reset" };
    public static readonly string[] Labels = { "Change camera", "Look behind", "Move up", "Move down", "Move forward", "Move back", "Move left", "Move right", "Pitch up", "Pitch down", "Widen FOV", "Narrow FOV", "Reset view" };
    private static readonly string[] Keys = { "None", "None", "Numpad8", "Numpad2", "Numpad9", "Numpad7", "Numpad4", "Numpad6", "Numpad1", "Numpad3", "NumpadPlus", "NumpadMinus", "Numpad0" };
    public static Dictionary<string, string> DefaultKeys() => Actions.Select((a, i) => (a, i)).ToDictionary(x => x.a, x => Keys[x.i]);
    public static CameraPose Adjust(CameraPose pose, string action) => (action switch
    {
        "Camera up" => pose with { Height = pose.Height + .05f }, "Camera down" => pose with { Height = pose.Height - .05f },
        "Camera forward" => pose with { Forward = pose.Forward + .05f }, "Camera back" => pose with { Forward = pose.Forward - .05f },
        "Camera left" => pose with { Side = pose.Side - .05f }, "Camera right" => pose with { Side = pose.Side + .05f },
        "Camera pitch up" => pose with { Pitch = pose.Pitch - 1 }, "Camera pitch down" => pose with { Pitch = pose.Pitch + 1 },
        "Camera wider" => pose with { Fov = pose.Fov + 2 }, "Camera narrower" => pose with { Fov = pose.Fov - 2 }, _ => pose
    }).Bounded();
}
