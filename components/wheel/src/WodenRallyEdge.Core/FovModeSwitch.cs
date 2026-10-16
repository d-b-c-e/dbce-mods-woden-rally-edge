namespace WodenRallyEdge.Core;

/// <summary>
/// Physical rig → Adjustable FOV, chosen by the player in F6: the mounted lens on screen starts at the vertical angle
/// the player is looking at, so the switch keeps the view and the FOV keys then zoom from there. Exceptions: a shown
/// angle outside the 30–110° lens bounds starts at the nearest bound, and when no start applies (chase view, a fitted
/// bonnet, no measured angle) the saved lens is used unchanged, so the view can still change on the switch.
/// </summary>
/// <remarks>
/// Owner UAT 2026-10-15: bonnet "too narrow", +/- "no visible change". In Physical mode the measured rig sets the
/// projection, so the saved lens is unused; before the lock (641914e) FOV keys still saved it, and the owner's bonnet
/// lens had reached the 30° floor without any visible effect. Adopting that stale value on the switch made the bonnet
/// narrower than the view the player chose to adjust.
/// </remarks>
public static class FovModeSwitch
{
    public const float MinFov = 30, MaxFov = 110;

    /// <summary>The lens value to start Adjustable at, or null to leave the saved lens alone: no mounted view on
    /// screen, a fitted lens that is not the saved one, or no measured center angle yet.</summary>
    public static float? AdjustableStart(double? centerVerticalDegrees, bool mountedViewOnScreen, bool savedLensInUse) =>
        mountedViewOnScreen && savedLensInUse && centerVerticalDegrees is { } v && double.IsFinite(v) && v > 0
            ? (float)Math.Round(Math.Clamp(v, MinFov, MaxFov), 1)
            : null;
}
