namespace WodenRallyEdge.Core;

/// <summary>
/// Player text for the Physical rig FOV lock (triples on, MatchGameFov off): the bonnet and bumper FOV keys and slider
/// do nothing, because the [Triple] rig settings set the projection. The texts name the actual angle and the one place to
/// change it, so the player is told rather than finding out by pressing a key. Nothing here changes a setting.
/// </summary>
public static class FovLockText
{
    public const string SwitchPath = "F6 › Cameras › Triple FOV › Adjustable";
    public const string ConfigFile = "BepInEx/config/dbce.wodenrallyedgewheel.cfg";

    /// <summary>The center view's vertical angle, or a plain fallback before three views are active.</summary>
    public static string Angle(double? verticalDegrees) =>
        verticalDegrees is { } v && double.IsFinite(v) && v > 0
            ? FormattableString.Invariant($"{v:0.0}° vertical")
            : "the rig settings";

    /// <summary>Shown when a locked FOV key is pressed.</summary>
    public static string KeyMessage(double? verticalDegrees) =>
        $"FOV keys are off: Physical rig sets {Angle(verticalDegrees)} from the rig settings shown in F6 › Cameras. To zoom, choose {SwitchPath}.";

    /// <summary>Shown once per session on the first locked bonnet or bumper view.</summary>
    public static string ViewHint(bool bumper, double? verticalDegrees) =>
        $"{(bumper ? "Bumper" : "Bonnet")} FOV is set by the rig settings ({Angle(verticalDegrees)}, Physical). FOV keys are off; {SwitchPath} to zoom.";

    /// <summary>The [Triple] geometry Physical mode uses. F6 has no editor for it, so untouched values are named as the
    /// shipped defaults rather than presented as the player's measurements (owner UAT 2026-10-15: bonnet too narrow).</summary>
    public static string RigSettings(double panelWidthMm, double panelHeightMm, double eyeDistanceMm, double sideAngleDegrees, double bezelMm, bool shippedDefaults) =>
        FormattableString.Invariant($"{panelWidthMm:0.#} × {panelHeightMm:0.#} mm screens, eyes {eyeDistanceMm:0.#} mm from the center screen, sides turned {sideAngleDegrees:0.#}°, bezel {bezelMm:0.#} mm") +
        (shippedDefaults
            ? $". These are the shipped defaults, not your measurements: set [Triple] PanelWidthMm, PanelHeightMm, EyeDistanceMm, SideAngle and BezelMm in {ConfigFile}, or choose Adjustable FOV to zoom."
            : $" (your [Triple] values in {ConfigFile}).");
}
