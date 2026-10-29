using Dbce.TripleScreen;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WodenRallyEdge;

/// <summary>
/// Curved panels ([Triple] CurveRadiusMm): the three views render full-rect into textures sized for their widened source
/// surfaces, and one presenter camera (depth source+5, under the HUD camera at +10) redraws each texture over its third
/// of the window along the eye rays (<see cref="CurvedPanelWarp.SourceUv"/>, 64x36 grid, the always-included copy
/// shader). Built once per rig key; any failure (missing engine binding, shader, render target, pass) latches flat output
/// with the reason until the key changes, so nothing is retried or allocated every frame. Same approach as art of rally.
/// </summary>
internal static class TripleCurve
{
    internal const int Columns = 64, Rows = 36;
    private const string ShaderName = "Hidden/BlitCopy";
    private const int Quads = 7; // GL.QUADS (the constant is stripped from the interop)
    private static string _key = "";       // output size and size bucket: only these rebuild targets and presenter
    private static string _gridKey = "";   // the rig key: a change recomputes the UV grids only (a speed-varying game FOV changes it every frame)
    private static readonly CurveEvidence Evidence = new();
    private static float _depth;
    private static readonly RenderTexture?[] Targets = new RenderTexture?[3];
    private static readonly Vector2[]?[] Source = new Vector2[]?[3];
    private static GameObject? _presenterGo;
    private static Camera? _presenter;
    private static Material? _material;
    private static bool _registered;
    // Set inside OnPostRender; applied (latched) by the next Configure, never from the draw itself.
    private static string? _drawFailure;

    /// <summary>Why the curve is off for the current key, or null.</summary>
    internal static string? Failure { get; private set; }
    /// <summary>Presenter frames drawn since the build: the evidence that curved output reached the window, not only the textures.</summary>
    internal static long Frames => Evidence.Drawn;
    internal static bool Active => Failure == null && _presenter != null && Targets[0] != null && Targets[1] != null && Targets[2] != null;
    /// <summary>All three panels drawn by the presenter within the last two source frames.</summary>
    internal static bool Presented => Active && Evidence.Presented;
    internal static string Note => Presented ? ", curved" : Active ? ", curved: waiting for the first presented frame" : Failure != null ? " (curve off: " + Failure + ")" : "";
    internal static RenderTexture? Target(int index) => Targets[index];

    internal static bool Owns(Texture? texture)
    {
        if (texture == null) return false;
        foreach (var target in Targets) if (target != null && target.Pointer == texture.Pointer) return true;
        return false;
    }

    /// <summary>True when curved output is ready for <paramref name="key"/>. Builds once per key; a failure keeps flat output until the key changes.</summary>
    internal static bool Configure(CurvedPanelWarp[] warps, string key, Camera source, int thirdPx, int heightPx)
    {
        // Target size: the panel's pixels scaled by the source widening, rounded up to an eighth so small eye changes
        // (a speed-varying game FOV) keep the same textures; the UV grids absorb the exact geometry.
        var (scaleX, scaleY) = SizeBucket(warps);
        string resourceKey = thirdPx + "x" + heightPx + "|" + scaleX + "|" + scaleY;
        if (resourceKey == _key)
        {
            if (_drawFailure != null && Failure == null) { Fail(_drawFailure); return false; }
            if (!Active) return false;
            if (key != _gridKey) { Grids(warps); _gridKey = key; }
            float depth = source.depth + 5;
            if (depth != _depth) { _depth = depth; TripleNative.SetDepth(_presenter!, depth); }
            if (Evidence.SourceFrame() is { } stalled) { Fail(stalled); return false; }
            return true;
        }
        Release();
        _key = resourceKey; _gridKey = ""; Failure = null; _drawFailure = null; Evidence.Reset();
        try
        {
            if (!TripleNative.TargetAvailable) throw new InvalidOperationException("engine binding Camera.set_targetTexture is missing");
            if (!TripleNative.PresenterAvailable) throw new InvalidOperationException("engine bindings GL.PushMatrix/LoadOrtho/PopMatrix are missing");
            var shader = Shader.Find(ShaderName);
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("the copy shader is unavailable");
            _material ??= new Material(shader);
            int width = Math.Clamp((int)Math.Round(thirdPx * scaleX), 1, thirdPx * 2), height = Math.Clamp((int)Math.Round(heightPx * scaleY), 1, heightPx * 2);
            for (var index = 0; index < 3; index++)
            {
                var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                target.name = "Woden.Triple.Curved" + index;
                Targets[index] = target; // owned before Create, so a failed Create is released with the rest
                if (!target.Create()) throw new InvalidOperationException("render target " + index + " could not be created");
            }
            Grids(warps);
            _gridKey = key;
            if (!_registered) { ClassInjector.RegisterTypeInIl2Cpp<TriplePresenter>(); _registered = true; }
            _presenterGo = new GameObject("Woden.Triple.Presenter");
            Object.DontDestroyOnLoad(_presenterGo);
            _presenter = _presenterGo.AddComponent(Il2CppType.Of<Camera>()).Cast<Camera>();
            _presenter.enabled = false;
            TripleNative.SetCullingMask(_presenter, 0);
            // The three panels cover every pixel of the window, so a depth-only clear is enough (no background colour setter needed).
            TripleNative.SetClearFlags(_presenter, CameraClearFlags.Depth);
            _depth = source.depth + 5;
            TripleNative.SetDepth(_presenter, _depth);
            _presenter.rect = new Rect(0f, 0f, 1f, 1f);
            _presenterGo.AddComponent(Il2CppType.Of<TriplePresenter>());
            _presenter.enabled = true;
            Evidence.SourceFrame();
            Runtime.Log.LogInfo($"Curved triples on: targets {Targets[0]!.width}x{Targets[0]!.height}, {Targets[1]!.width}x{Targets[1]!.height}, {Targets[2]!.width}x{Targets[2]!.height}; presenter depth {source.depth + 5}");
            return true;
        }
        catch (Exception ex)
        {
            Fail(ex.Message);
            return false;
        }
    }

    /// <summary>The largest source-to-panel scale over the three panels, rounded up to an eighth (at least 1).</summary>
    internal static (double X, double Y) SizeBucket(CurvedPanelWarp[] warps)
    {
        double x = 1d, y = 1d;
        foreach (var warp in warps)
        {
            x = Math.Max(x, warp.SourceSurface.Width / warp.Panel.Width);
            y = Math.Max(y, warp.SourceSurface.Height / warp.Panel.Height);
        }
        return (Math.Ceiling(x * 8d) / 8d, Math.Ceiling(y * 8d) / 8d);
    }

    private static void Grids(CurvedPanelWarp[] warps)
    {
        for (var index = 0; index < 3; index++)
        {
            var warp = warps[index];
            var uv = Source[index] is { } existing && existing.Length == (Columns + 1) * (Rows + 1) ? existing : new Vector2[(Columns + 1) * (Rows + 1)];
            for (var row = 0; row <= Rows; row++)
            for (var column = 0; column <= Columns; column++)
            {
                warp.SourceUv((double)column / Columns, (double)row / Rows, out var u, out var v);
                uv[(row * (Columns + 1)) + column] = new Vector2((float)u, (float)v);
            }
            Source[index] = uv;
        }
    }

    internal static void Fail(string reason)
    {
        Release();
        Failure = reason;
        Runtime.Log.LogWarning("Curved triples off, flat views kept: " + reason);
    }

    /// <summary>Drops targets and presenter; the key and failure stay so nothing is retried until the rig changes.</summary>
    internal static void Release()
    {
        // Destroy is deferred: disable the old presenter now so it cannot draw beside a presenter rebuilt this frame.
        try { if (_presenter != null) _presenter.enabled = false; } catch { }
        try { if (_presenterGo != null) Object.Destroy(_presenterGo); } catch { }
        _presenterGo = null; _presenter = null;
        for (var index = 0; index < 3; index++)
        {
            try { Targets[index]?.Release(); if (Targets[index] != null) Object.Destroy(Targets[index]); } catch { }
            Targets[index] = null; Source[index] = null;
        }
    }

    /// <summary>For Stop: everything released and the next start allowed to try again.</summary>
    internal static void Reset()
    {
        Release();
        _key = ""; _gridKey = ""; Failure = null; _drawFailure = null; Evidence.Reset();
    }

    /// <summary>From TriplePresenter.OnPostRender: the three panels over the window, left to right.</summary>
    internal static void Draw()
    {
        if (Failure != null || _drawFailure != null || _material == null) return;
        for (var index = 0; index < 3; index++)
            if (Targets[index] == null || Source[index] == null) { _drawFailure = "panel " + index + " has no texture"; return; }
        try
        {
            TripleNative.GlPushMatrix();
            try
            {
                TripleNative.GlLoadOrtho();
                for (var index = 0; index < 3; index++)
                {
                    var target = Targets[index]!; var uv = Source[index]!;
                    _material.mainTexture = target;
                    if (!_material.SetPass(0)) { _drawFailure = "the copy shader pass was refused"; return; }
                    GL.Begin(Quads);
                    try
                    {
                        float x0 = index / 3f, width = 1f / 3f;
                        for (var row = 0; row < Rows; row++)
                        for (var column = 0; column < Columns; column++)
                        {
                            Corner(uv, x0, width, column, row);
                            Corner(uv, x0, width, column + 1, row);
                            Corner(uv, x0, width, column + 1, row + 1);
                            Corner(uv, x0, width, column, row + 1);
                        }
                    }
                    finally { GL.End(); }
                }
                Evidence.Drew();
            }
            finally { TripleNative.GlPopMatrix(); }
        }
        catch (Exception ex) { _drawFailure = "presenter draw: " + ex.Message; }
    }

    private static void Corner(Vector2[] uv, float x0, float width, int column, int row)
    {
        var s = uv[(row * (Columns + 1)) + column];
        GL.TexCoord2(s.x, s.y);
        GL.Vertex3(x0 + (width * column / Columns), (float)row / Rows, 0f);
    }
}

/// <summary>Only the Unity message lives on the injected component (AGENTS: managed logic stays outside IL2CPP registration).</summary>
public sealed class TriplePresenter : MonoBehaviour
{
    public TriplePresenter(IntPtr pointer) : base(pointer) { }
    private void OnPostRender() => TripleCurve.Draw();
}
