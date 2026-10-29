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
    private static string _key = "";
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
    /// <summary>Presenter frames drawn: the evidence that curved output reached the window, not only the textures.</summary>
    internal static long Frames { get; private set; }
    internal static bool Active => Failure == null && Targets[1] != null;
    internal static string Note => Active ? ", curved" : Failure != null ? " (curve off: " + Failure + ")" : "";
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
        if (key == _key)
        {
            if (_drawFailure != null && Failure == null) Fail(_drawFailure);
            return Active;
        }
        Release();
        _key = key; Failure = null; _drawFailure = null;
        try
        {
            if (!TripleNative.TargetAvailable) throw new InvalidOperationException("engine binding Camera.set_targetTexture is missing");
            if (!TripleNative.PresenterAvailable) throw new InvalidOperationException("engine bindings GL.PushMatrix/LoadOrtho/PopMatrix are missing");
            var shader = Shader.Find(ShaderName);
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("the copy shader is unavailable");
            _material ??= new Material(shader);
            for (var index = 0; index < 3; index++)
            {
                var warp = warps[index];
                // The source surface is the panel widened to cover the curve; keep its pixel density.
                int width = Math.Clamp((int)Math.Round(thirdPx * warp.SourceSurface.Width / warp.Panel.Width), 1, thirdPx * 2);
                int height = Math.Clamp((int)Math.Round(heightPx * warp.SourceSurface.Height / warp.Panel.Height), 1, heightPx * 2);
                var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                target.name = "Woden.Triple.Curved" + index;
                if (!target.Create()) throw new InvalidOperationException("render target " + index + " could not be created");
                Targets[index] = target;
                var uv = new Vector2[(Columns + 1) * (Rows + 1)];
                for (var row = 0; row <= Rows; row++)
                for (var column = 0; column <= Columns; column++)
                {
                    warp.SourceUv((double)column / Columns, (double)row / Rows, out var u, out var v);
                    uv[(row * (Columns + 1)) + column] = new Vector2((float)u, (float)v);
                }
                Source[index] = uv;
            }
            if (!_registered) { ClassInjector.RegisterTypeInIl2Cpp<TriplePresenter>(); _registered = true; }
            _presenterGo = new GameObject("Woden.Triple.Presenter");
            Object.DontDestroyOnLoad(_presenterGo);
            _presenter = _presenterGo.AddComponent(Il2CppType.Of<Camera>()).Cast<Camera>();
            _presenter.enabled = false;
            TripleNative.SetCullingMask(_presenter, 0);
            // The three panels cover every pixel of the window, so a depth-only clear is enough (no background colour setter needed).
            TripleNative.SetClearFlags(_presenter, CameraClearFlags.Depth);
            TripleNative.SetDepth(_presenter, source.depth + 5);
            _presenter.rect = new Rect(0f, 0f, 1f, 1f);
            _presenterGo.AddComponent(Il2CppType.Of<TriplePresenter>());
            _presenter.enabled = true;
            Runtime.Log.LogInfo($"Curved triples on: targets {Targets[0]!.width}x{Targets[0]!.height}, {Targets[1]!.width}x{Targets[1]!.height}, {Targets[2]!.width}x{Targets[2]!.height}; presenter depth {source.depth + 5}");
            return true;
        }
        catch (Exception ex)
        {
            Fail(ex.Message);
            return false;
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
        _key = ""; Failure = null; _drawFailure = null;
    }

    /// <summary>From TriplePresenter.OnPostRender: the three panels over the window, left to right.</summary>
    internal static void Draw()
    {
        if (Failure != null || _drawFailure != null || _material == null) return;
        try
        {
            TripleNative.GlPushMatrix();
            try
            {
                TripleNative.GlLoadOrtho();
                for (var index = 0; index < 3; index++)
                {
                    var target = Targets[index]; var uv = Source[index];
                    if (target == null || uv == null) continue;
                    _material.mainTexture = target;
                    if (!_material.SetPass(0)) { _drawFailure = "the copy shader pass was refused"; return; }
                    GL.Begin(Quads);
                    float x0 = index / 3f, width = 1f / 3f;
                    for (var row = 0; row < Rows; row++)
                    for (var column = 0; column < Columns; column++)
                    {
                        Corner(uv, x0, width, column, row);
                        Corner(uv, x0, width, column + 1, row);
                        Corner(uv, x0, width, column + 1, row + 1);
                        Corner(uv, x0, width, column, row + 1);
                    }
                    GL.End();
                }
                Frames++;
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
