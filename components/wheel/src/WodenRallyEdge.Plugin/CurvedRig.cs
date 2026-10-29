using System;
using System.Linq;
using Dbce.TripleScreen;

namespace WodenRallyEdge;

/// <summary>
/// The three views through the shared toolkit rig (Toolkit/Triple, b4b7120): eye distance is to the middle of the
/// center screen's glass, a curved panel's chord sits one sagitta closer, hinges stay at the chord ends. Radius 0 gives
/// the same frusta as <see cref="TripleGeometry.Compute"/> (checked by the tests); a positive radius widens each view to
/// the planar source surface its curved panel samples (<see cref="Warps"/>), which TripleCurve redraws along the eye rays.
/// Pure math, no Unity.
/// </summary>
internal sealed class CurvedRig
{
    private CurvedRig(PanelFrustum[] views, CurvedPanelWarp[]? warps) { Views = views; Warps = warps; }

    /// <summary>Left, center, right, as TripleGeometry.Compute.</summary>
    internal PanelFrustum[] Views { get; }
    /// <summary>Per view, or null when flat.</summary>
    internal CurvedPanelWarp[]? Warps { get; }

    /// <exception cref="ArgumentOutOfRangeException">An unusable rig: radius under half the panel width, or the eye behind the curve.</exception>
    internal static CurvedRig Compute(double panelWidth, double panelHeight, double eyeDistance, double sideAngleDegrees,
        double bezelGap, double curveRadius)
    {
        var definition = new TripleRigDefinition(panelWidth, panelHeight, eyeDistance, sideAngleDegrees, sideAngleDegrees, 0d, bezelGap, curveRadius);
        var panels = TripleRigBuilder.Build(definition);
        var eye = new Vector3d(0d, 0d, 0d);
        var warps = curveRadius > 0d ? panels.Select(panel => CurvedPanelWarp.Create(panel, eye, curveRadius)).ToArray() : null;
        var views = new PanelFrustum[3];
        for (var index = 0; index < 3; index++)
        {
            // Near 1 makes the extents tangents. The toolkit is right-handed (center panel at -Z); Unity camera space is
            // left-handed (+Z forward), so the yaw reads the forward vector with Z flipped.
            var projection = ProjectionCalculator.Calculate(warps?[index].SourceSurface ?? panels[index], eye, 1d, 2d);
            var forward = projection.CameraForward;
            views[index] = new PanelFrustum(Math.Atan2(forward.X, -forward.Z) * 180 / Math.PI,
                projection.Left, projection.Right, projection.Bottom, projection.Top);
        }
        return new CurvedRig(views, warps);
    }
}
