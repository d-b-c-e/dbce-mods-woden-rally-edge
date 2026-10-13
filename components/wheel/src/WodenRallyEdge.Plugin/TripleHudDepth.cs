namespace WodenRallyEdge;

/// <summary>Center HUD camera depth, selected for ordinary triple-screen use.</summary>
internal readonly record struct TripleHudDepth(float Plane, float Far)
{
    internal const float CameraY = -10000f, FieldOfView = 60f, Near = .1f;
    internal static TripleHudDepth Production => new(100f, 110f);
}
