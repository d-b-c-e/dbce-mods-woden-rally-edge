namespace WodenRallyEdge;

/// <summary>Private depth-only HUD experiment; normal builds retain the accepted geometry.</summary>
internal readonly record struct TripleHudDepth(float Plane, float Far)
{
    internal const float CameraY = -10000f, FieldOfView = 60f, Near = .1f;
#if WODEN_HUD_DEPTH_DIAGNOSTIC
    internal const bool DiagnosticBuild = true;
#else
    internal const bool DiagnosticBuild = false;
#endif
    internal static TripleHudDepth Select(bool diagnosticBuild, bool forceMuted, bool outputMuted) =>
        diagnosticBuild && forceMuted && outputMuted ? new(100f, 110f) : new(1f, 10f);
    internal static TripleHudDepth ForRun(bool forceMuted, bool outputMuted) =>
        Select(DiagnosticBuild, forceMuted, outputMuted);
}
