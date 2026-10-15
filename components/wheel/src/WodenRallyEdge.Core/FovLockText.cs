namespace WodenRallyEdge.Core;

/// <summary>
/// Player text for the Physical rig FOV lock (triples on, MatchGameFov off): the bonnet and bumper FOV keys and slider
/// do nothing, because the measured rig sets the projection. The texts name the actual angle and the one place to
/// change it, so the player is told rather than finding out by pressing a key. Nothing here changes a setting.
/// </summary>
public static class FovLockText
{
    public const string SwitchPath = "F6 › Cameras › Triple FOV › Adjustable";

    /// <summary>The center view's vertical angle, or a plain fallback before three views are active.</summary>
    public static string Angle(double? verticalDegrees) =>
        verticalDegrees is { } v && double.IsFinite(v) && v > 0
            ? FormattableString.Invariant($"{v:0.0}° vertical")
            : "the measured rig";

    /// <summary>Shown when a locked FOV key is pressed.</summary>
    public static string KeyMessage(double? verticalDegrees) =>
        $"FOV keys are off: Physical rig sets {Angle(verticalDegrees)} from your monitors. To zoom, choose {SwitchPath}.";

    /// <summary>Shown once per session on the first locked bonnet or bumper view.</summary>
    public static string ViewHint(bool bumper, double? verticalDegrees) =>
        $"{(bumper ? "Bumper" : "Bonnet")} FOV is set by your rig ({Angle(verticalDegrees)}, Physical). FOV keys are off; {SwitchPath} to zoom.";
}
