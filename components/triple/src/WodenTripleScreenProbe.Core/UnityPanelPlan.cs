using Dbce.TripleScreen;

namespace WodenTripleScreenProbe.Core;

/// <summary>
/// Engine boundary only: toolkit geometry looks down -Z, while a Unity Transform's forward
/// vector is +Z. Frustum extents remain toolkit outputs; Unity builds its depth matrix.
/// </summary>
public readonly record struct UnityPanelPlan(
    string Id, Vector3d LocalForward, Vector3d LocalUp,
    double Left, double Right, double Bottom, double Top, double Near, double Far)
{
    public static UnityPanelPlan From(OffAxisProjection projection) => new(
        projection.Surface.Id,
        ReflectZ(projection.CameraForward), ReflectZ(projection.CameraUp),
        projection.Left, projection.Right, projection.Bottom, projection.Top,
        projection.Near, projection.Far);

    private static Vector3d ReflectZ(Vector3d value) => new(value.X, value.Y, -value.Z);
}
