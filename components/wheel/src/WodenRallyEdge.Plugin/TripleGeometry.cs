using System;

namespace WodenRallyEdge;

/// <summary>Off-axis frustum of one panel, as tangents at a near plane of 1 (Unity camera space).</summary>
internal readonly record struct PanelFrustum(double YawDegrees, double Left, double Right, double Bottom, double Top);

/// <summary>
/// Planar three-panel rig: the eye sits on the center panel's normal; each side panel is
/// hinged at the center panel's edge (plus the bezel gap) and turned toward the viewer.
/// Kooima's generalized perspective projection, in Unity's left-handed camera space
/// (x right, y up, z forward). Pure math so it can be checked without the game.
/// </summary>
internal static class TripleGeometry
{
    /// <returns>Left, center, right.</returns>
    public static PanelFrustum[] Compute(double panelWidth, double panelHeight, double eyeDistance,
        double sideAngleDegrees, double bezelGap)
    {
        if (!(panelWidth > 0) || !(panelHeight > 0) || !(eyeDistance > 0) || bezelGap < 0 ||
            !(sideAngleDegrees >= 0 && sideAngleDegrees < 90))
            throw new ArgumentOutOfRangeException(nameof(panelWidth), "Invalid rig dimensions.");

        double w = panelWidth, h = panelHeight, d = eyeDistance;
        double a = sideAngleDegrees * Math.PI / 180;
        var up = new V3(0, h, 0);

        var center = Panel(new V3(-w / 2, -h / 2, d), new V3(w / 2, -h / 2, d), new V3(-w / 2, h / 2, d));

        var toRight = new V3(Math.Cos(a), 0, -Math.Sin(a));
        var rightInner = new V3(w / 2, -h / 2, d) + toRight * bezelGap;
        var right = Panel(rightInner, rightInner + toRight * w, rightInner + up);

        var toLeft = new V3(-Math.Cos(a), 0, -Math.Sin(a));
        var leftInner = new V3(-w / 2, -h / 2, d) + toLeft * bezelGap;
        var leftOuter = leftInner + toLeft * w;
        var left = Panel(leftOuter, leftInner, leftOuter + up);

        return new[] { left, center, right };
    }

    /// <summary>Eye distance at which the center panel spans the given vertical field of view.</summary>
    public static double EyeDistanceForVerticalFov(double panelHeight, double verticalFovDegrees) =>
        panelHeight / 2 / Math.Tan(verticalFovDegrees * Math.PI / 360);

    // pa lower-left, pb lower-right, pc upper-left, all relative to the eye.
    private static PanelFrustum Panel(V3 pa, V3 pb, V3 pc)
    {
        var vr = (pb - pa).Normalized();
        var vu = (pc - pa).Normalized();
        var forward = V3.Cross(vr, vu).Normalized(); // away from the eye in a left-handed basis
        double distance = V3.Dot(pa, forward);
        return new PanelFrustum(
            Math.Atan2(forward.X, forward.Z) * 180 / Math.PI,
            V3.Dot(vr, pa) / distance, V3.Dot(vr, pb) / distance,
            V3.Dot(vu, pa) / distance, V3.Dot(vu, pc) / distance);
    }

    private readonly record struct V3(double X, double Y, double Z)
    {
        public static V3 operator +(V3 a, V3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static V3 operator -(V3 a, V3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V3 operator *(V3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
        public static double Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static V3 Cross(V3 a, V3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public V3 Normalized() { double l = Math.Sqrt(Dot(this, this)); return new(X / l, Y / l, Z / l); }
    }
}
